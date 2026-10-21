using System.Data;
using System.Net;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustedInfrastructure;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class TrustedRuntimeIntegrationTests
{
    private static WebApplicationBuilder Builder(string environment, Dictionary<string, string?> values)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment, ContentRootPath = Path.GetTempPath() });
        builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(values); return builder;
    }
    private static TrustedSecretBindings Bindings() => new()
    {
        SqlConnectionName = "DefaultConnection", RedisConfigurationKeys = ["Scaling:Cache:Connection"],
        RedisBackplaneConfigurationKeys = ["Scaling:SignalR:Connection"],
        DataProtectionCertificatePasswordConfigurationKeys = ["Security:DataProtection:CertificatePassword"]
    };
    [Theory]
    [InlineData("Production", "Disabled", true)]
    [InlineData("Staging", "Disabled", false)]
    [InlineData("Production", "LegacyLocal", true)]
    [InlineData("Production", "Synthetic", false)]
    [InlineData("Staging", "Synthetic", false)]
    public async Task UnsafeProductionModesFailBeforeAnyDatabaseUse(string environment, string mode, bool require)
    {
        var b = Builder(environment, new() { ["TrustedVault:Mode"] = mode, ["TrustedVault:RequireInProduction"] = require.ToString() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => b.AddTrustedInfrastructureAsync("farm", Bindings()));
    }
    [Fact] public async Task ExplicitLegacyMigrationModeIsNeverCalledVaultProtected()
    {
        var b = Builder("Production", new() { ["TrustedVault:Mode"] = "LegacyLocal", ["TrustedVault:RequireInProduction"] = "false" });
        await b.AddTrustedInfrastructureAsync("farm", Bindings()); using var sp = b.Services.BuildServiceProvider();
        Assert.False(sp.GetRequiredService<TrustedRuntimeStatus>().VaultProtected); Assert.True(sp.GetRequiredService<ITrustedRuntimeGate>().IsAvailable);
    }
    private static Dictionary<string, string?> Synthetic() => new()
    {
        ["TrustedVault:Mode"] = "Synthetic",
        ["TrustedVault:Synthetic:SqlConnectionString"] = "Server=sql.invalid;Database=FarmSynthetic;Integrated Security=true;Encrypt=true;TrustServerCertificate=false",
        ["TrustedVault:Synthetic:ExpiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"),
        ["Scaling:Cache:Connection"] = "insecure-fallback-must-clear", ["Scaling:SignalR:Connection"] = "other-fallback-must-clear",
        ["Security:DataProtection:CertificatePassword"] = "fallback-not-used"
    };
    [Fact] public async Task TypedVaultBindingsOverrideSqlAndClearAbsentSecretFallbacks()
    {
        var b = Builder("Development", Synthetic()); await b.AddTrustedInfrastructureAsync("farm", Bindings());
        Assert.Contains("FarmSynthetic", b.Configuration.GetConnectionString("DefaultConnection"));
        Assert.Null(b.Configuration["Scaling:Cache:Connection"]); Assert.Null(b.Configuration["Scaling:SignalR:Connection"]);
        Assert.Null(b.Configuration["Security:DataProtection:CertificatePassword"]);
    }
    [Fact] public async Task CacheAndBackplaneRemainIndependentBindings()
    {
        var values = Synthetic(); values["TrustedVault:Synthetic:RedisConnectionString"] = "cache.invalid:6380,ssl=true";
        values["TrustedVault:Synthetic:RedisBackplaneConnectionString"] = "backplane.invalid:6380,ssl=true";
        var b = Builder("Development", values); await b.AddTrustedInfrastructureAsync("farm", Bindings());
        Assert.StartsWith("cache.invalid", b.Configuration["Scaling:Cache:Connection"]);
        Assert.StartsWith("backplane.invalid", b.Configuration["Scaling:SignalR:Connection"]);
    }
    [Fact] public async Task ExpiredConfigurationCannotInitializeSyntheticRuntime()
    {
        var values = Synthetic(); values["TrustedVault:Synthetic:ExpiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");
        await Assert.ThrowsAsync<TrustedVaultUnavailableException>(() => Builder("Development", values).AddTrustedInfrastructureAsync("farm", Bindings()));
    }
    private sealed class Probe { public int Id { get; set; } public string Value { get; set; } = ""; }
    private sealed class ProbeContext(DbContextOptions<ProbeContext> options) : DbContext(options)
    { public DbSet<Probe> Probes => Set<Probe>(); }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RevocationBlocksReadRawWriteAndSaveChanges(bool synchronous)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var gate = new TestRuntimeGate(); await using var db = new ProbeContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlite(connection)
            .AddInterceptors(new TrustedDatabaseCommandGate(gate), new TrustedDatabaseTransactionGate(gate)).Options);
        await db.Database.EnsureCreatedAsync(); db.Probes.Add(new() { Value = "safe" }); await db.SaveChangesAsync(); gate.IsAvailable = false;
        db.Probes.Add(new() { Value = "must-not-write" });
        if (synchronous)
        {
            Assert.Throws<TrustedVaultUnavailableException>(() => db.Probes.AsNoTracking().ToList());
            Assert.Throws<TrustedVaultUnavailableException>(() => db.Database.ExecuteSqlRaw("UPDATE Probes SET Value='bad'"));
            Assert.IsType<TrustedVaultUnavailableException>(Assert.Throws<DbUpdateException>(() => db.SaveChanges()).InnerException);
        }
        else
        {
            await Assert.ThrowsAsync<TrustedVaultUnavailableException>(() => db.Probes.AsNoTracking().ToListAsync());
            await Assert.ThrowsAsync<TrustedVaultUnavailableException>(() => db.Probes.ExecuteUpdateAsync(s => s.SetProperty(p => p.Value, "bad")));
            Assert.IsType<TrustedVaultUnavailableException>((await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync())).InnerException);
        }
        gate.IsAvailable = true; Assert.Equal("safe", await db.Probes.AsNoTracking().Select(x => x.Value).SingleAsync());
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RevocationImmediatelyBeforeCommitRollsBackWrittenTransaction(bool synchronous)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var gate = new TestRuntimeGate(); await using var db = new ProbeContext(new DbContextOptionsBuilder<ProbeContext>().UseSqlite(connection)
            .AddInterceptors(new TrustedDatabaseCommandGate(gate), new TrustedDatabaseTransactionGate(gate)).Options);
        await db.Database.EnsureCreatedAsync();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            db.Probes.Add(new() { Value = "uncommitted" }); await db.SaveChangesAsync(); gate.IsAvailable = false;
            if (synchronous) Assert.Throws<TrustedVaultUnavailableException>(() => tx.Commit());
            else await Assert.ThrowsAsync<TrustedVaultUnavailableException>(() => tx.CommitAsync());
            await tx.RollbackAsync();
        }
        gate.IsAvailable = true; Assert.Empty(await db.Probes.AsNoTracking().ToListAsync());
    }
    private static IConfiguration ProxyConfig(string ip = "127.0.0.1", string origin = "https://farm.example.invalid", string hosts = "farm.example.invalid") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["TrustedProxy:Enabled"] = "true", ["TrustedProxy:KnownProxyIp"] = ip, ["TrustedProxy:PublicOrigin"] = origin, ["AllowedHosts"] = hosts }).Build();
    [Theory]
    [InlineData("0.0.0.0", "https://farm.example.invalid", "farm.example.invalid")]
    [InlineData("::", "https://farm.example.invalid", "farm.example.invalid")]
    [InlineData("10.0.0.0/8", "https://farm.example.invalid", "farm.example.invalid")]
    [InlineData("127.0.0.1", "http://farm.example.invalid", "farm.example.invalid")]
    [InlineData("127.0.0.1", "https://farm.example.invalid/base", "farm.example.invalid")]
    [InlineData("127.0.0.1", "https://farm.example.invalid", "*")]
    [InlineData("127.0.0.1", "https://farm.example.invalid", "farm.example.invalid;other.invalid")]
    public void GatewayConfigurationRejectsBroadOrAmbiguousTrust(string ip, string origin, string hosts) => Assert.Throws<InvalidOperationException>(() => TrustedProxyOptions.Read(ProxyConfig(ip, origin, hosts), false));
    [Fact] public void GatewayProfileAcceptsOnlyExactPeerAndFixedHeaders()
    {
        var options = TrustedProxyOptions.Read(ProxyConfig(), false)!; var c = new DefaultHttpContext();
        c.Connection.RemoteIpAddress = IPAddress.Loopback; c.Request.Host = new("farm.example.invalid");
        c.Request.Headers["X-Forwarded-Host"] = "farm.example.invalid"; c.Request.Headers["X-Forwarded-Proto"] = "https";
        Assert.True(options.Accepts(c)); Assert.Single(options.Headers().KnownProxies); Assert.Empty(options.Headers().KnownIPNetworks); Assert.Equal(1, options.Headers().ForwardLimit);
        c.Request.Headers["X-Forwarded-Host"] = "attacker.invalid"; Assert.False(options.Accepts(c));
        c.Request.Headers["X-Forwarded-Host"] = "farm.example.invalid"; c.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10"); Assert.False(options.Accepts(c));
        c.Connection.RemoteIpAddress = IPAddress.Loopback; c.Request.Headers["X-Forwarded-Proto"] = "http,https"; Assert.False(options.Accepts(c));
    }
}
