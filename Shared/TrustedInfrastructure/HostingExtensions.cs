using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TrustedInfrastructure;

public static class HostingExtensions
{
    public static async Task<WebApplicationBuilder> AddTrustedInfrastructureAsync(this WebApplicationBuilder builder,
        string project, TrustedSecretBindings bindings, CancellationToken ct = default)
    {
        if (!OpenBaoClient.ProjectIds.Contains(project)) throw new ArgumentException("Unknown project.", nameof(project));
        if (new[] { bindings.SqlConnectionName }.Concat(bindings.AdditionalSqlConnectionNames).Any(name => string.IsNullOrWhiteSpace(name) || name.Contains(':')))
            throw new ArgumentException("A single connection string name is required.", nameof(bindings));
        var options = builder.Configuration.GetSection(TrustedVaultOptions.SectionName).Get<TrustedVaultOptions>() ?? new();
        OpenBaoClient.ValidateBounds(options);
        var development = builder.Environment.IsDevelopment();
        var mode = options.Mode;
        if (mode is "Disabled" or "LegacyLocal")
        {
            if (!development && options.RequireInProduction)
                throw new InvalidOperationException("TrustedVault secure production mode requires OpenBao. LegacyLocal is an explicit unprotected migration option and requires RequireInProduction=false.");
            if (!development && mode != "LegacyLocal")
                throw new InvalidOperationException("Disabled vault mode is Development-only. Choose OpenBao or explicitly acknowledge LegacyLocal.");
            builder.Services.AddSingleton<ITrustedRuntimeGate>(new RuntimeGate(null));
            builder.Services.AddSingleton(new TrustedRuntimeStatus(project, mode, false, null));
            return builder;
        }
        if (bindings.DatabaseProvider != TrustedDatabaseProvider.SqlServer)
            throw new InvalidOperationException("This vault-protected profile currently validates SQL Server TLS only. PostgreSql and Oracle remain available only through explicit LegacyLocal until their secure provider profiles are implemented and verified.");
        RuntimeSecrets secrets;
        int version;
        OpenBaoClient? client = null;
        if (mode == "Synthetic")
        {
            if (!development) throw new InvalidOperationException("Synthetic vault fixtures are Development-only.");
            secrets = options.Synthetic ?? throw new InvalidOperationException("Synthetic fixture data is required.");
            OpenBaoClient.ValidateSecrets(secrets, DateTimeOffset.UtcNow, options.MaximumSecretLifetimeMinutes);
            version = 1;
        }
        else if (mode == "OpenBao")
        {
            OpenBaoClient.ValidateOptions(options, development);
            OpenBaoClient.ValidateCredentialPaths(options, builder.Environment.ContentRootPath, builder.Environment.WebRootPath ?? "");
            client = new OpenBaoClient(options, development);
            try { (secrets, version) = await client.ReadRuntimeAsync(project, ct); }
            catch { client.Dispose(); throw; }
        }
        else throw new InvalidOperationException("Unknown TrustedVault mode. Supported: OpenBao, Synthetic (Development), LegacyLocal (explicit opt-out).");

        if (bindings.SecondarySqlConnectionName is not null && (string.IsNullOrWhiteSpace(bindings.SecondarySqlConnectionName) || bindings.SecondarySqlConnectionName.Contains(':')))
            throw new InvalidOperationException("A registered secondary SQL connection requires a valid connection name.");
        var configuration = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (secret, keys) in bindings.AsMap())
        {
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key) || !key.Contains(':') || key.StartsWith("TrustedVault:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Authentication:", StringComparison.OrdinalIgnoreCase) || key.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Only explicit service configuration targets may receive typed runtime secrets.");
                if (!configuration.TryAdd(key, secrets.Get(secret))) throw new InvalidOperationException("Duplicate secret configuration target.");
            }
        }
        // Last provider wins. Values live only in backend process memory, never saved to appsettings or returned by an endpoint.
        builder.Configuration.AddInMemoryCollection(configuration);
        var gate = new RuntimeGate(secrets.ExpiresAtUtc);
        builder.Services.AddSingleton<ITrustedRuntimeGate>(gate);
        builder.Services.AddSingleton(new TrustedRuntimeStatus(project, mode, mode == "OpenBao", secrets.ExpiresAtUtc));
        if (client is not null)
        {
            builder.Services.AddSingleton(client);
            builder.Services.AddSingleton<IHostedService>(sp => new VaultWatchdog(client, gate, project, version, options.RecheckSeconds,
                sp.GetRequiredService<IHostApplicationLifetime>(), sp.GetRequiredService<ILogger<VaultWatchdog>>()));
        }
        else builder.Services.AddSingleton<IHostedService>(sp => new ExpiryWatchdog(gate, sp.GetRequiredService<IHostApplicationLifetime>()));
        return builder;
    }

    /// <summary>Place before authentication/endpoints. Also inject ITrustedRuntimeGate into every background job.</summary>
    public static IApplicationBuilder UseTrustedInfrastructure(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var gate = context.RequestServices.GetRequiredService<ITrustedRuntimeGate>();
        if (!gate.IsAvailable)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new { error = "Trusted service temporarily unavailable." });
            return;
        }
        await next(context);
    });
}

public sealed record TrustedRuntimeStatus(string Project, string Mode, bool VaultProtected, DateTimeOffset? ExpiresAtUtc);

internal sealed class RuntimeGate(DateTimeOffset? expiresAt) : ITrustedRuntimeGate
{
    private int _failed;
    public bool IsAvailable => Volatile.Read(ref _failed) == 0 && (expiresAt is null || DateTimeOffset.UtcNow < expiresAt);
    public void ThrowIfUnavailable() { if (!IsAvailable) throw new TrustedVaultUnavailableException(); }
    public void Fail() => Interlocked.Exchange(ref _failed, 1);
}

internal sealed class VaultWatchdog(OpenBaoClient client, RuntimeGate gate, string project, int initialVersion, int interval,
    IHostApplicationLifetime lifetime, ILogger<VaultWatchdog> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var elapsed = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                gate.ThrowIfUnavailable();
                if (++elapsed < interval) continue;
                elapsed = 0;
                var (_, version) = await client.ReadRuntimeAsync(project, stoppingToken);
                if (version != initialVersion)
                {
                    // EF/SMTP/Redis clients captured startup configuration. Never pretend a memory override rotates existing pools.
                    gate.Fail();
                    logger.LogWarning("Trusted runtime version changed; requests and gated jobs are stopped. Supervisor restart is required.");
                    lifetime.StopApplication();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch
        {
            gate.Fail();
            logger.LogError("Trusted runtime validation failed; requests and gated jobs are stopped. Check OpenBao from trusted operator access.");
            lifetime.StopApplication();
        }
    }
}

internal sealed class ExpiryWatchdog(RuntimeGate gate, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                if (!gate.IsAvailable) { lifetime.StopApplication(); return; }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
