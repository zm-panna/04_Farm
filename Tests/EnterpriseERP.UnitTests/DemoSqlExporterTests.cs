using System.Globalization;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace EnterpriseERP.UnitTests;

[Collection("Demo seed environment")]
public sealed class DemoSqlExporterTests
{
    [Fact]
    public void SqlLiteralsAreUnicodeEscapedAndCultureIndependent()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal("N'খামার O''Brien; --'", DemoSqlExporter.SqlLiteral("খামার O'Brien; --"));
            Assert.Equal("1234.567", DemoSqlExporter.SqlLiteral(1234.56700m));
            Assert.Equal("0x00ABFF", DemoSqlExporter.SqlLiteral(new byte[] { 0, 171, 255 }));
            Assert.Equal("NULL", DemoSqlExporter.SqlLiteral(null));
            Assert.Equal("1", DemoSqlExporter.SqlLiteral(true));
            Assert.Equal("CONVERT(date, N'2026-10-19', 23)", DemoSqlExporter.SqlLiteral(new DateOnly(2026, 10, 19)));
            Assert.Equal("CONVERT(datetimeoffset, N'2026-10-19T01:02:03.0000000+06:00', 127)",
                DemoSqlExporter.SqlLiteral(new DateTimeOffset(2026, 10, 19, 1, 2, 3, TimeSpan.FromHours(6))));
            Assert.Throws<InvalidOperationException>(() => DemoSqlExporter.SqlLiteral(double.NaN));
            Assert.Throws<InvalidOperationException>(() => DemoSqlExporter.SqlLiteral(new object()));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task FreshFixtureExportsActualHashesAndForeignKeyOrderedGuardedSqlWithoutSecrets()
    {
        await using var fixture = await Fixture.CreateAsync();
        await DemoBusinessSeeder.RunAsync(fixture.Services);
        var file = Path.Combine(fixture.Directory, "demo-data.sql");
        await DemoSqlExporter.RunAsync(fixture.Services, file);
        var sql = await File.ReadAllTextAsync(file);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains("BEGIN TRANSACTION", sql);
        Assert.Contains("IF XACT_STATE() <> 0 ROLLBACK TRANSACTION", sql);
        Assert.Contains("CHARINDEX(N'demo', LOWER(DB_NAME()))", sql);
        Assert.Contains("CHARINDEX(N'prod', LOWER(DB_NAME()))", sql);
        Assert.Contains("WITH (UPDLOCK, HOLDLOCK)", sql);
        Assert.Contains("SET IDENTITY_INSERT [dbo].[Companies] ON", sql);
        Assert.DoesNotContain("SET IDENTITY_INSERT [dbo].[AspNetUsers]", sql);
        Assert.DoesNotContain("SET IDENTITY_INSERT [dbo].[AspNetUserRoles]", sql);
        Assert.DoesNotContain("[RowVersion]", sql);
        Assert.DoesNotContain(fixture.Password, sql);
        Assert.DoesNotContain("INSERT INTO [dbo].[AspNetUserTokens]", sql);
        foreach (var user in await db.Users.ToListAsync())
        {
            Assert.Contains(DemoSqlExporter.SqlLiteral(user.PasswordHash), sql);
            Assert.Contains(user.Email!, sql);
        }
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var inserted = sql.IndexOf("INSERT INTO [dbo].[" + entity.GetTableName() + "]", StringComparison.Ordinal);
            if (inserted < 0) continue;
            foreach (var fk in entity.GetForeignKeys())
            {
                var principal = sql.IndexOf("INSERT INTO [dbo].[" + fk.PrincipalEntityType.GetTableName() + "]", StringComparison.Ordinal);
                Assert.True(principal >= 0 && principal < inserted,
                    $"Principal {fk.PrincipalEntityType.GetTableName()} must precede {entity.GetTableName()}.");
            }
        }
        Assert.Equal(6, await db.Audits.CountAsync());
        Assert.Equal(await DemoSqlExporter.ComputeFixtureFingerprintAsync(db),
            (await db.Audits.SingleAsync(a => a.Action == DemoSqlExporter.FingerprintAction)).Reference);
        var second = Path.Combine(fixture.Directory, "demo-data-second.sql");
        await DemoSqlExporter.RunAsync(fixture.Services, second);
        Assert.Equal(sql, await File.ReadAllTextAsync(second));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSqlExporter.RunAsync(fixture.Services, file));
        Assert.Equal(sql, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task EnterpriseFixtureHasFiveMeaningfulRowsPerModuleAndExportsWithoutCredentials()
    {
        await using var fixture = await Fixture.CreateAsync();
        await DemoBusinessSeeder.RunAsync(fixture.Services, includeEnterprise: true);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(10, await db.Users.CountAsync());
        Assert.Equal(5, await db.CompanyBusinessPolicies.CountAsync()); Assert.Equal(5, await db.TaxPolicies.CountAsync());
        Assert.Equal(5, await db.HRPayrollStaff.CountAsync()); Assert.Equal(5, await db.HRPayrollCompensations.CountAsync()); Assert.Equal(5, await db.HRPayrollAttendance.CountAsync());
        Assert.Equal(5, await db.HRPayrollLeaves.CountAsync()); Assert.Equal(5, await db.HRPayrollPolicies.CountAsync());
        Assert.Equal(5, await db.HRPayrollRuns.CountAsync()); Assert.Equal(5, await db.HRPayrollLines.CountAsync());
        Assert.Equal(5, await db.CRMLeads.CountAsync()); Assert.Equal(5, await db.CRMConsentEvents.CountAsync());
        Assert.Equal(5, await db.CRMOpportunities.CountAsync()); Assert.Equal(5, await db.CRMActivities.CountAsync());
        Assert.Equal(5, await db.CRMQuotes.CountAsync()); Assert.Equal(5, await db.CRMQuoteLines.CountAsync()); Assert.Equal(5, await db.CRMSalesReadyRecords.CountAsync());
        Assert.Equal(5, await db.FixedAssets.CountAsync()); Assert.Equal(5, await db.FixedAssetCustodies.CountAsync());
        Assert.Equal(5, await db.FixedAssetDepreciationRuns.CountAsync()); Assert.Equal(5, await db.FixedAssetDepreciationLines.CountAsync()); Assert.Equal(5, await db.FixedAssetDisposals.CountAsync());
        Assert.Equal(5, await db.EcommerceCustomers.CountAsync()); Assert.Equal(5, await db.EcommerceProducts.CountAsync());
        Assert.Equal(5, await db.EcommerceCarts.CountAsync()); Assert.Equal(5, await db.EcommerceCartLines.CountAsync());
        Assert.Equal(5, await db.EcommerceOrders.CountAsync()); Assert.Equal(5, await db.EcommerceOrderLines.CountAsync());
        Assert.Equal(5, await db.EcommerceStockReservations.CountAsync()); Assert.Equal(20, await db.EcommerceOrderEvents.CountAsync());
        Assert.Equal(5, await db.EcommerceFulfillments.CountAsync()); Assert.Equal(5, await db.EcommerceReturns.CountAsync());
        Assert.All(await db.CompanyBusinessPolicies.ToListAsync(), p => Assert.Contains("FICTIONAL", p.ReviewBasis));
        Assert.All(await db.TaxPolicies.ToListAsync(), p => Assert.Contains("FICTIONAL", p.Reason));
        Assert.All(await db.EcommerceOrders.ToListAsync(), p => Assert.Equal("PendingUnverified", p.PaymentStatus));
        var vouchers = await db.Vouchers.Include(x => x.Lines).ToListAsync();
        Assert.All(vouchers, v => Assert.Equal(v.Lines.Sum(l => l.Debit), v.Lines.Sum(l => l.Credit)));
        foreach (var stock in await db.StockItems.ToListAsync())
        {
            Assert.Equal(stock.QuantityOnHand, (await db.StockMovements.Where(x => x.StockItemId == stock.Id).ToListAsync()).Sum(x => x.QuantityDelta));
            Assert.Equal(stock.ReservedQuantity, (await db.EcommerceStockReservations.Where(x => x.StockItemId == stock.Id && x.Status == "Reserved").ToListAsync()).Sum(x => x.Quantity));
        }
        var fingerprint = await DemoSqlExporter.ComputeFixtureFingerprintAsync(db);
        var file = Path.Combine(fixture.Directory, "enterprise-data.sql");
        await DemoSqlExporter.RunAsync(fixture.Services, file);
        var sql = await File.ReadAllTextAsync(file);
        Assert.Contains("FICTIONAL", sql); Assert.Contains("EcommerceOrders", sql); Assert.DoesNotContain(fixture.Password, sql);
        await DemoBusinessSeeder.RunAsync(fixture.Services, includeEnterprise: true);
        db.ChangeTracker.Clear(); Assert.Equal(fingerprint, await DemoSqlExporter.ComputeFixtureFingerprintAsync(db));
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoBusinessSeeder.RunAsync(fixture.Services));
    }

    [Theory]
    [InlineData("changed-business-row")]
    [InlineData("extra-company")]
    [InlineData("mfa-token")]
    [InlineData("platform-role")]
    [InlineData("missing-fingerprint")]
    public async Task ModifiedOrNonFixtureDataIsNeverExported(string change)
    {
        await using var fixture = await Fixture.CreateAsync();
        await DemoBusinessSeeder.RunAsync(fixture.Services);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            switch (change)
            {
                case "changed-business-row": (await db.SupportTickets.FirstAsync()).Subject = "Private customer case"; break;
                case "extra-company": db.Companies.Add(new Company { Name = "Actual customer" }); break;
                case "mfa-token":
                    db.UserTokens.Add(new IdentityUserToken<string> { UserId = (await db.Users.FirstAsync()).Id,
                        LoginProvider = "[AspNetUserStore]", Name = "AuthenticatorKey", Value = "TEST-MATERIAL-NOT-FOR-EXPORT" }); break;
                case "platform-role": db.Roles.Add(new IdentityRole("PlatformAdmin")); break;
                case "missing-fingerprint": db.Audits.Remove(await db.Audits.SingleAsync(a => a.Action == DemoSqlExporter.FingerprintAction)); break;
            }
            await db.SaveChangesAsync();
        }
        var file = Path.Combine(fixture.Directory, "rejected.sql");
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSqlExporter.RunAsync(fixture.Services, file));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task ExportRequiresExplicitOptInAndDevelopment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var file = Path.Combine(fixture.Directory, "rejected.sql");
        Environment.SetEnvironmentVariable("FARM_ALLOW_DEMO_EXPORT", null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSqlExporter.RunAsync(fixture.Services, file));
        Environment.SetEnvironmentVariable("FARM_ALLOW_DEMO_EXPORT", "YES");
        fixture.Services.GetRequiredService<IWebHostEnvironment>().EnvironmentName = "Production";
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSqlExporter.RunAsync(fixture.Services, file));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task OutputInsidePublicWebRootIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = Path.Combine(fixture.Directory, "wwwroot");
        System.IO.Directory.CreateDirectory(root);
        fixture.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath = root;
        var file = Path.Combine(root, "sensitive-demo.sql");
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoSqlExporter.RunAsync(fixture.Services, file));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void OutputMustBeANewAbsoluteLocalSqlFile()
    {
        Assert.Throws<InvalidOperationException>(() => DemoSqlExporter.ValidateOutputPath("demo.sql"));
        Assert.Throws<InvalidOperationException>(() => DemoSqlExporter.ValidateOutputPath("//server/share/demo.sql"));
        Assert.Throws<InvalidOperationException>(() => DemoSqlExporter.ValidateOutputPath(Path.Combine(Path.GetTempPath(), "farm-demo.db")));
        var path = Path.Combine(Path.GetTempPath(), "farm-demo-export-" + Guid.NewGuid().ToString("N") + ".sql");
        File.WriteAllText(path, "preserve");
        try
        {
            Assert.Throws<InvalidOperationException>(() => DemoSqlExporter.ValidateOutputPath(path));
            Assert.Equal("preserve", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    private sealed class Fixture(ServiceProvider services, string directory, string password,
        Dictionary<string, string?> previous) : IAsyncDisposable
    {
        public ServiceProvider Services { get; } = services;
        public string Directory { get; } = directory;
        public string Password { get; } = password;
        public static async Task<Fixture> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), "farm-demo-export-tests-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var password = "Generated-" + Guid.NewGuid().ToString("N") + "aA9!";
            var names = new[] { "FARM_ALLOW_DEMO_SEED", "FARM_DEMO_PASSWORD", "FARM_ALLOW_DEMO_EXPORT" };
            var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
            Environment.SetEnvironmentVariable(names[0], "YES");
            Environment.SetEnvironmentVariable(names[1], password);
            Environment.SetEnvironmentVariable(names[2], "YES");
            var services = new ServiceCollection(); services.AddLogging();
            services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
            services.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=" + Path.Combine(directory, "farm-demo.db") + ";Pooling=False"));
            services.AddIdentity<AppUser, IdentityRole>(options => { options.Password.RequiredLength = 12; options.User.RequireUniqueEmail = true; })
                .AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            return new Fixture(provider, directory, password, previous);
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            foreach (var item in previous) Environment.SetEnvironmentVariable(item.Key, item.Value);
            System.IO.Directory.Delete(Directory, true);
        }
    }
    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "EnterpriseERP.Web";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
