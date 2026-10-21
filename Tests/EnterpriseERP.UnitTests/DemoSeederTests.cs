using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace EnterpriseERP.UnitTests;

[CollectionDefinition("Demo seed environment", DisableParallelization = true)]
public sealed class DemoSeedEnvironmentCollection { }

[Collection("Demo seed environment")]
public sealed class DemoSeederTests
{
    [Fact]
    public async Task TenTenantEnterpriseFixturePopulatesEveryMappedTableAndRerunsWithoutChanges()
    {
        using var environment = new SeedEnvironment();
        await using var fixture = await Fixture.CreateAsync();
        await DemoBusinessSeeder.RunAsync(fixture.Services, includeEnterprise: true, tenantCount: 10);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.OpenConnectionAsync();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM \"" + entity.GetTableName() + "\"";
            Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync()) >= 10, entity.GetTableName());
        }
        Assert.Equal(20, await db.Users.CountAsync());
        Assert.False(await db.AutomationRules.AnyAsync(x => x.Active));
        Assert.False(await db.ScheduledJobs.AnyAsync(x => x.Status == "Pending"));
        var before = await DemoSqlExporter.ComputeFixtureFingerprintAsync(db);
        await DemoBusinessSeeder.RunAsync(fixture.Services, includeEnterprise: true, tenantCount: 10);
        db.ChangeTracker.Clear();
        Assert.Equal(before, await DemoSqlExporter.ComputeFixtureFingerprintAsync(db));
    }

    [Theory]
    [InlineData("Production", "Microsoft.EntityFrameworkCore.SqlServer", "FarmDemo", "YES")]
    [InlineData("Development", "Microsoft.EntityFrameworkCore.SqlServer", "FarmCustomer", "YES")]
    [InlineData("Development", "Microsoft.EntityFrameworkCore.SqlServer", "FarmDemoProduction", "YES")]
    [InlineData("Development", "Microsoft.EntityFrameworkCore.SqlServer", "FarmDemo", null)]
    [InlineData("Staging", "Microsoft.EntityFrameworkCore.Sqlite", "farm-demo.db", "YES")]
    [InlineData("Development", "Microsoft.EntityFrameworkCore.InMemory", "FarmDemo", "YES")]
    public void UnsafeTargetsAreRejected(string environment, string provider, string target, string? allowed) =>
        Assert.Throws<InvalidOperationException>(() => DemoBusinessSeeder.ValidateTarget(environment, provider, target, allowed));

    [Fact]
    public void ExplicitIsolatedSqlServerAndDevelopmentSqliteTargetsAreAccepted()
    {
        DemoBusinessSeeder.ValidateTarget("Development", "Microsoft.EntityFrameworkCore.SqlServer", "FarmDemo", "YES");
        DemoBusinessSeeder.ValidateTarget("Staging", "Microsoft.EntityFrameworkCore.SqlServer", "Farm_Demo_Review", "YES");
        DemoBusinessSeeder.ValidateTarget("Development", "Microsoft.EntityFrameworkCore.Sqlite", "/tmp/farm-demo.db", "YES");
    }

    [Fact]
    public async Task CompleteSeedBalancesLedgersAndSecondRunPreservesAccountsAndRows()
    {
        using var environment = new SeedEnvironment();
        await using var fixture = await Fixture.CreateAsync();
        await DemoBusinessSeeder.RunAsync(fixture.Services);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(5, await db.Companies.CountAsync());
        Assert.Equal(5, await db.Farms.CountAsync());
        Assert.Equal(10, await db.Branches.CountAsync());
        Assert.Equal(5, await db.Users.CountAsync());
        Assert.Equal(5, await db.Roles.CountAsync());
        Assert.False(await db.Roles.AnyAsync(x => x.Name == "PlatformAdmin"));
        Assert.Equal(5, await db.UserCompanyAccess.CountAsync());
        Assert.Equal(10, await db.UserBranchAccess.CountAsync());
        Assert.Equal(15, await db.Animals.CountAsync());
        Assert.Equal(5, await db.Animals.CountAsync(x => x.Sold));
        Assert.Equal(5, await db.MilkYields.CountAsync());
        Assert.Equal(10, await db.StockItems.CountAsync());
        Assert.Equal(45, await db.StockMovements.CountAsync());
        Assert.Equal(10, await db.InventoryCostBalances.CountAsync());
        Assert.Equal(5, await db.OpeningBalances.CountAsync());
        Assert.Equal(5, await db.OpeningStockValues.CountAsync());
        Assert.Equal(5, await db.OpeningAnimalValues.CountAsync());
        Assert.Equal(10, await db.StockTransfers.CountAsync());
        Assert.Equal(5, await db.PurchaseReceipts.CountAsync());
        Assert.Equal(10, await db.TradingParties.CountAsync());
        Assert.Equal(5, await db.PurchaseOrders.CountAsync());
        Assert.Equal(5, await db.PurchaseOrderReceipts.CountAsync());
        Assert.Equal(5, await db.StockReorderPolicies.CountAsync());
        Assert.Empty(await db.AccountingPeriodCloses.ToListAsync());
        Assert.Equal(5, await db.ProductSales.CountAsync());
        Assert.Equal(5, await db.AnimalSales.CountAsync());
        Assert.Equal(70, await db.Vouchers.CountAsync());
        Assert.Equal(170, await db.VoucherLines.CountAsync());
        Assert.Equal(25, await db.FinanceDocuments.CountAsync());
        Assert.Equal(5, await db.FinancePayments.CountAsync());
        Assert.Equal(5, await db.FinancePaymentAllocations.CountAsync());
        Assert.Equal(5, await db.BankStatementLines.CountAsync());
        Assert.Equal(5, await db.StockDocumentReturns.CountAsync());
        Assert.Equal(5, await db.FatteningPlans.CountAsync());
        Assert.Equal(5, await db.AnimalWeightRecords.CountAsync());
        Assert.Equal(5, await db.FarmExpenses.CountAsync());
        Assert.Equal(5, await db.BreedingCycles.CountAsync());
        Assert.Equal(5, await db.BreedingCycleEvents.CountAsync());
        Assert.Equal(5, await db.AnimalFeedRecords.CountAsync());
        Assert.Equal(5, await db.AnimalHealthRecords.CountAsync());
        Assert.Equal(5, await db.SupportTickets.CountAsync());
        Assert.Equal(5, await db.SupportTicketActivities.CountAsync());
        Assert.Equal(5, await db.SubscriptionInvoices.CountAsync());
        Assert.Equal(5, await db.SubscriptionPayments.CountAsync());
        Assert.Equal(5, await db.CompanySubscriptionPolicies.CountAsync());
        Assert.Equal(5, await db.AutomationRules.CountAsync());
        Assert.False(await db.AutomationRules.AnyAsync(x => x.Active));
        Assert.Empty(await db.ScheduledJobs.ToListAsync());
        Assert.Empty(await db.AutomationApprovals.ToListAsync());
        Assert.Empty(await db.AutomationExecutions.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Equal(6, await db.Audits.CountAsync());
        var vouchers = await db.Vouchers.Include(x => x.Lines).ToListAsync();
        Assert.All(vouchers, v =>
        {
            Assert.True(v.Posted);
            Assert.True(v.Lines.Sum(l => l.Debit) > 0);
            Assert.Equal(v.Lines.Sum(l => l.Debit), v.Lines.Sum(l => l.Credit));
        });
        var openingStockIds = (await db.OpeningStockValues.Select(x => x.StockItemId).ToListAsync()).ToHashSet();
        foreach (var stock in await db.StockItems.ToListAsync())
        {
            var movements = await db.StockMovements.Where(x => x.StockItemId == stock.Id).ToListAsync();
            Assert.Equal(openingStockIds.Contains(stock.Id) ? 10m : 96m, stock.QuantityOnHand);
            Assert.Equal("KG", stock.UnitOfMeasure);
            Assert.Equal(stock.QuantityOnHand, movements.Sum(x => x.QuantityDelta));
            var value = await db.InventoryCostBalances.SingleAsync(x => x.StockItemId == stock.Id);
            Assert.Equal(stock.QuantityOnHand * stock.UnitCost, value.Value);
            var inventoryLedger = vouchers.Where(v => v.CompanyId == stock.CompanyId && v.BranchId == stock.BranchId).SelectMany(v => v.Lines).Where(l => l.AccountCode == "1300");
            Assert.Equal(value.Value, inventoryLedger.Sum(l => l.Debit - l.Credit));
        }
        foreach (var opening in await db.OpeningBalances.Include(x => x.Stocks).Include(x => x.Animals).ToListAsync())
        {
            Assert.Single(opening.Stocks); Assert.Single(opening.Animals);
            var openingVoucher = vouchers.Single(x => x.Id == opening.VoucherId);
            Assert.Equal(opening.OpeningDate, DateOnly.FromDateTime(openingVoucher.Date.UtcDateTime));
            Assert.Equal(openingVoucher.Date, vouchers.Where(x => x.CompanyId == opening.CompanyId && x.BranchId == opening.BranchId).Min(x => x.Date));
            Assert.Equal(opening.Stocks.Sum(x => x.Value), openingVoucher.Lines.Where(x => x.AccountCode == "1300").Sum(x => x.Debit));
            Assert.Equal(opening.Animals.Sum(x => x.Value), openingVoucher.Lines.Where(x => x.AccountCode == "1400").Sum(x => x.Debit));
            var animalValue = opening.Animals.Single();
            Assert.Equal(animalValue.Value, (await db.Animals.SingleAsync(x => x.Id == animalValue.AnimalId)).PurchaseCost);
            Assert.False(await db.FinanceDocuments.AnyAsync(x => x.CompanyId == opening.CompanyId && x.Kind == "AnimalPurchase" && x.SourceId == animalValue.AnimalId));
        }
        var movementById = (await db.StockMovements.ToListAsync()).ToDictionary(x => x.Id);
        foreach (var transfer in await db.StockTransfers.ToListAsync())
        {
            Assert.Equal("KG", transfer.UnitOfMeasure);
            Assert.Equal(transfer.SourceQuantityBefore - transfer.Quantity, transfer.SourceQuantityAfter);
            Assert.Equal(transfer.DestinationQuantityBefore + transfer.Quantity, transfer.DestinationQuantityAfter);
            Assert.Equal(transfer.SourceValueBefore - transfer.CostAmount, transfer.SourceValueAfter);
            Assert.Equal(transfer.DestinationValueBefore + transfer.CostAmount, transfer.DestinationValueAfter);
            Assert.Equal(-transfer.Quantity, movementById[transfer.SourceMovementId].QuantityDelta);
            Assert.Equal(transfer.Quantity, movementById[transfer.DestinationMovementId].QuantityDelta);
            Assert.Equal(StockTransferService.OutReference(transfer.Reference), movementById[transfer.SourceMovementId].Reference);
            Assert.Equal(StockTransferService.InReference(transfer.Reference), movementById[transfer.DestinationMovementId].Reference);
            var outVoucher = vouchers.Single(x => x.Id == transfer.SourceVoucherId);
            var inVoucher = vouchers.Single(x => x.Id == transfer.DestinationVoucherId);
            Assert.Equal(transfer.CostAmount, outVoucher.Lines.Single(x => x.AccountCode == "1300").Credit);
            Assert.Equal(transfer.CostAmount, inVoucher.Lines.Single(x => x.AccountCode == "1300").Debit);
        }
        foreach (var branch in await db.Branches.ToListAsync())
            Assert.Equal(0m, vouchers.Where(x => x.BranchId == branch.Id).SelectMany(x => x.Lines).Where(x => x.AccountCode == "1290").Sum(x => x.Debit - x.Credit));
        foreach (var line in await db.BankStatementLines.ToListAsync())
            Assert.Equal(line.Amount, vouchers.Single(x => x.Id == line.MatchedVoucherId).Lines.Where(x => x.AccountCode == "1000").Sum(x => x.Debit - x.Credit));
        foreach (var order in await db.PurchaseOrders.ToListAsync())
        {
            Assert.Equal("Received", order.Status);
            var link = await db.PurchaseOrderReceipts.SingleAsync(x => x.PurchaseOrderId == order.Id);
            var receipt = await db.PurchaseReceipts.SingleAsync(x => x.Id == link.PurchaseReceiptId);
            Assert.Equal(order.Quantity, order.ReceivedQuantity);
            Assert.Equal(order.ReceivedQuantity, link.Quantity);
            Assert.Equal(order.ReceivedAmount, link.Amount);
            Assert.Equal(receipt.Quantity, link.Quantity);
            Assert.Equal(receipt.Quantity * receipt.UnitPrice, link.Amount);
        }
        var allocations = await db.FinancePaymentAllocations.ToListAsync();
        foreach (var document in await db.FinanceDocuments.Where(x => x.Kind == "Sale").ToListAsync())
            Assert.Equal(document.Amount, allocations.Where(x => x.FinanceDocumentId == document.Id).Sum(x => x.SignedAmount));
        foreach (var animal in await db.Animals.ToListAsync())
            Assert.Equal(animal.Sold, await db.AnimalSales.AnyAsync(x => x.AnimalId == animal.Id));
        foreach (var weight in await db.AnimalWeightRecords.ToListAsync())
            Assert.Equal(weight.WeightKg, (await db.Animals.SingleAsync(x => x.Id == weight.AnimalId)).CurrentWeightKg);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var passwords = (await db.Users.ToListAsync()).ToDictionary(x => x.Id, x => x.PasswordHash);
        foreach (var user in await db.Users.ToListAsync())
        {
            Assert.True(await users.CheckPasswordAsync(user, environment.Password));
            Assert.NotEqual(environment.Password, user.PasswordHash);
            Assert.True(user.LockoutEnabled);
        }
        var markerDates = (await db.Audits.OrderBy(x => x.Id).ToListAsync()).Select(x => x.CreatedUtc).ToArray();
        // A rerun never changes an existing password, even when an operator supplies a different secret.
        Environment.SetEnvironmentVariable("FARM_DEMO_PASSWORD", SeedEnvironment.NewPassword());
        await DemoBusinessSeeder.RunAsync(fixture.Services);
        db.ChangeTracker.Clear();
        Assert.Equal(70, await db.Vouchers.CountAsync());
        Assert.Equal(6, await db.Audits.CountAsync());
        Assert.Equal(markerDates, (await db.Audits.OrderBy(x => x.Id).ToListAsync()).Select(x => x.CreatedUtc));
        Assert.All(await db.Users.ToListAsync(), user => Assert.Equal(passwords[user.Id], user.PasswordHash));
    }

    [Fact]
    public async Task IdentityFailureRollsBackBusinessUsersRolesAndGrants()
    {
        using var environment = new SeedEnvironment();
        await using var fixture = await Fixture.CreateAsync(rejectThirdUser: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoBusinessSeeder.RunAsync(fixture.Services));
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Companies.ToListAsync());
        Assert.Empty(await db.Farms.ToListAsync());
        Assert.Empty(await db.Branches.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
        Assert.Empty(await db.UserPermissions.ToListAsync());
        Assert.Empty(await db.UserCompanyAccess.ToListAsync());
        Assert.Empty(await db.Vouchers.ToListAsync());
        Assert.Empty(await db.Audits.ToListAsync());
    }

    [Fact]
    public async Task ExistingCustomerDataIsNeverOverwrittenOrAdopted()
    {
        using var environment = new SeedEnvironment();
        await using var fixture = await Fixture.CreateAsync();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Companies.Add(new Company { Name = "Existing customer" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoBusinessSeeder.RunAsync(fixture.Services));
        Assert.Equal("Existing customer", (await db.Companies.SingleAsync()).Name);
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task OlderDemoVersionIsNotSilentlyAdoptedAsTheNewFixture()
    {
        using var environment = new SeedEnvironment();
        await using var fixture = await Fixture.CreateAsync();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = new Company { Name = "Previous demo", Slug = "demo-farm-1" };
        db.Companies.Add(company); await db.SaveChangesAsync();
        db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = "DEMO-CLI", Action = "DemoSeedCompletedV2", Reference = "FARM-DEMO-V2" });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoBusinessSeeder.RunAsync(fixture.Services));
        Assert.Equal("Previous demo", (await db.Companies.SingleAsync()).Name);
        Assert.Empty(await db.OpeningBalances.ToListAsync());
        Assert.Empty(await db.StockTransfers.ToListAsync());
        Assert.Equal("FARM-DEMO-V2", (await db.Audits.SingleAsync()).Reference);
    }

    [Fact]
    public async Task TrustedBootstrapCreatesPlatformAdminAndCannotBeRepeated()
    {
        await using var fixture = await Fixture.CreateAsync();
        var names = new[] { "FARM_BOOTSTRAP_EMAIL", "FARM_BOOTSTRAP_PASSWORD", "FARM_BOOTSTRAP_COMPANY", "FARM_BOOTSTRAP_BRANCH" };
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        var password = SeedEnvironment.NewPassword();
        try
        {
            Environment.SetEnvironmentVariable(names[0], "initial-admin@example.invalid");
            Environment.SetEnvironmentVariable(names[1], password);
            Environment.SetEnvironmentVariable(names[2], "Bootstrap Test Farm");
            Environment.SetEnvironmentVariable(names[3], "Bootstrap Test Branch");
            await BootstrapAdmin.RunAsync(fixture.Services);
            using var scope = fixture.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await db.Users.SingleAsync();
            Assert.True(await users.IsInRoleAsync(user, "PlatformAdmin"));
            Assert.True(await users.CheckPasswordAsync(user, password));
            Assert.True(user.LockoutEnabled);
            var company = await db.Companies.SingleAsync();
            var branch = await db.Branches.SingleAsync();
            Assert.Equal(company.Id, branch.CompanyId);
            Assert.True(await db.UserCompanyAccess.AnyAsync(x => x.CompanyId == company.Id && x.UserId == user.Id));
            Assert.True(await db.UserBranchAccess.AnyAsync(x => x.CompanyId == company.Id && x.BranchId == branch.Id && x.UserId == user.Id));
            Assert.True(await db.UserPermissions.AnyAsync(x => x.CompanyId == company.Id && x.UserId == user.Id && x.Permission == "*"));
            Assert.Equal("FirstAdministratorProvisioned", (await db.Audits.SingleAsync()).Action);
            await Assert.ThrowsAsync<InvalidOperationException>(() => BootstrapAdmin.RunAsync(fixture.Services));
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal(1, await db.Companies.CountAsync());
            Assert.Equal(1, await db.Roles.CountAsync());
        }
        finally
        {
            foreach (var name in names) Environment.SetEnvironmentVariable(name, previous[name]);
        }
    }

    private sealed class RejectThirdDemoUser : IPasswordValidator<AppUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password) =>
            Task.FromResult(user.Email == "farmdemo3@example.invalid"
                ? IdentityResult.Failed(new IdentityError { Code = "FixtureFailure", Description = "Injected Identity validation failure." })
                : IdentityResult.Success);
    }

    private sealed class SeedEnvironment : IDisposable
    {
        private readonly string? previousAllowed = Environment.GetEnvironmentVariable("FARM_ALLOW_DEMO_SEED");
        private readonly string? previousPassword = Environment.GetEnvironmentVariable("FARM_DEMO_PASSWORD");
        public string Password { get; } = NewPassword();
        public static string NewPassword() => "Fixture-" + Guid.NewGuid().ToString("N") + "aA9!";
        public SeedEnvironment()
        {
            Environment.SetEnvironmentVariable("FARM_ALLOW_DEMO_SEED", "YES");
            Environment.SetEnvironmentVariable("FARM_DEMO_PASSWORD", Password);
        }
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("FARM_ALLOW_DEMO_SEED", previousAllowed);
            Environment.SetEnvironmentVariable("FARM_DEMO_PASSWORD", previousPassword);
        }
    }

    private sealed class Fixture(ServiceProvider services, string directory) : IAsyncDisposable
    {
        public ServiceProvider Services { get; } = services;
        public static async Task<Fixture> CreateAsync(bool rejectThirdUser = false)
        {
            var directory = Path.Combine(Path.GetTempPath(), "farm-demo-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
            serviceCollection.AddDbContext<AppDbContext>(options => options.UseSqlite("Data Source=" + Path.Combine(directory, "farm-demo.db") + ";Pooling=False"));
            serviceCollection.AddIdentity<AppUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.User.RequireUniqueEmail = true;
            }).AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
            if (rejectThirdUser) serviceCollection.AddSingleton<IPasswordValidator<AppUser>, RejectThirdDemoUser>();
            var services = serviceCollection.BuildServiceProvider();
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            return new Fixture(services, directory);
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            Directory.Delete(directory, true);
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
