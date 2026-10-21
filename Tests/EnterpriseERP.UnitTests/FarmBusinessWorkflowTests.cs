using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class FarmBusinessWorkflowTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static FarmBusinessService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static async Task SeedAnimals(SecurityTestScope f)
    {
        f.Db.Animals.AddRange(
            new Animal { Id = 1, CompanyId = 1, BranchId = 11, Tag = "A-001", Breed = "Cattle", PurchaseCost = 1000 },
            new Animal { Id = 2, CompanyId = 1, BranchId = 12, Tag = "A-OTHER", PurchaseCost = 2000 },
            new Animal { Id = 3, CompanyId = 2, BranchId = 21, Tag = "B-001", PurchaseCost = 3000 },
            new Animal { Id = 4, CompanyId = 1, BranchId = 11, Tag = "A-SOLD", Sold = true, PurchaseCost = 1000 });
        await f.Db.SaveChangesAsync();
    }
    private static FatteningPlanInput Plan(int animal = 1, string reference = "PLAN-1") => new()
    {
        AnimalId = animal, Reference = reference, StartDate = Today.AddDays(-30), TargetDate = Today.AddDays(60),
        InitialWeightKg = 100, TargetWeightKg = 160, ExpectedSaleAmount = 2500
    };
    private static ExpenseInput Expense(int? animal = 1, string reference = "EXP-1") => new()
    {
        AnimalId = animal, Reference = reference, ExpenseDate = Today, Category = "Veterinary", Amount = 100.125m, Description = "Vet visit"
    };
    private static BreedingInput Breeding(int animal = 1, string reference = "BREED-1") => new()
    {
        AnimalId = animal, Reference = reference, ServiceDate = Today.AddDays(-40), ExpectedCalvingDate = Today.AddDays(243), Method = "Natural", SireOrSemen = "SIRE-22"
    };
    private static BreedingOutcomeInput Outcome(long id, string status, string reference) => new()
    {
        CycleId = id, Outcome = status, Reference = reference, EventDate = Today, Notes = "Recorded by farm operator"
    };

    [Fact] public async Task ExpensePostsBalancedCashVoucherAndAuditAtomically()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        var result = await Service(f).RecordExpense(Expense(reference: " EXP-1 "));
        f.Db.ChangeTracker.Clear();
        var row = await f.Db.FarmExpenses.Include(x => x.Voucher).ThenInclude(x => x.Lines).SingleAsync();
        Assert.True(row.VoucherId > 0); Assert.Equal(result.Id, row.Id); Assert.Equal("EXP-1", row.Reference);
        Assert.Equal("EXP:EXP-1", row.Voucher.Reference); Assert.Equal(1, row.Voucher.CompanyId); Assert.Equal(11, row.Voucher.BranchId);
        Assert.Equal(100.125m, row.Voucher.Lines.Sum(x => x.Debit)); Assert.Equal(100.125m, row.Voucher.Lines.Sum(x => x.Credit));
        Assert.Contains(row.Voucher.Lines, x => x.AccountCode == "6200" && x.Debit == row.Amount);
        Assert.Contains(row.Voucher.Lines, x => x.AccountCode == "1000" && x.Credit == row.Amount);
        Assert.Equal(Today, DateOnly.FromDateTime(row.Voucher.Date.UtcDateTime));
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "Expenses.Post").ToListAsync());
    }
    [Fact] public async Task ExpenseRequiresAccountingPermissionInAdditionToExpenseCreate()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("Expenses.Create");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).RecordExpense(Expense()));
        Assert.Empty(await f.Db.FarmExpenses.ToListAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    [Theory] [InlineData(2)] [InlineData(3)]
    public async Task ExpenseRejectsAnimalOutsideActiveBranch(int animal)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).RecordExpense(Expense(animal)));
        Assert.Empty(await f.Db.FarmExpenses.ToListAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    [Fact] public async Task SharedExpenseHasNoAnimalAndDuplicateNormalizedReferenceCannotDoublePost()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        await Service(f).RecordExpense(Expense(null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).RecordExpense(Expense(null, " EXP-1 ")));
        Assert.Null((await f.Db.FarmExpenses.SingleAsync()).AnimalId); Assert.Single(await f.Db.Vouchers.ToListAsync());
    }
    [Fact] public async Task StorageFailureRollsBackExpenseVoucherLinesAndAudit()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectExpenseAudit BEFORE INSERT ON Audits WHEN NEW.Action = 'Expenses.Post' BEGIN SELECT RAISE(ABORT, 'simulated audit failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => Service(f).RecordExpense(Expense(null)));
        Assert.Empty(await f.Db.FarmExpenses.ToListAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
        Assert.Empty(await f.Db.VoucherLines.ToListAsync()); Assert.Empty(await f.Db.Audits.ToListAsync());
    }
    [Theory] [InlineData("-1")] [InlineData("0")] [InlineData("0.0001")] [InlineData("1000000000001")]
    public async Task InvalidExpenseAmountWritesNothing(string value)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var input = Expense(null); input.Amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).RecordExpense(input));
        Assert.Empty(await f.Db.FarmExpenses.ToListAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    [Fact] public async Task UnknownExpenseCategoryCannotSelectArbitraryAccount()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var input = Expense(null); input.Category = "1000";
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).RecordExpense(input));
        Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    [Fact] public async Task ExpenseBrowseDoesNotLeakOtherCompanyOrBranch()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); await f.GrantAsync("*", "operator-b", 2);
        await Service(f).RecordExpense(Expense(null, "A-EXP"));
        f.ActAs("operator-b", 2, 21); await Service(f).RecordExpense(Expense(null, "B-EXP")); f.ActAs();
        var page = await Service(f).Expenses(); Assert.Single(page.Records); Assert.Equal("A-EXP", page.Records[0].Reference);
    }
    [Fact] public async Task FatteningCreatesStartingWeightAndKeepsLatestWeightOnBackdate()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        var service = Service(f); await service.StartPlan(Plan());
        await service.RecordWeight(new() { AnimalId = 1, Reference = "LATEST", WeighedOn = Today, WeightKg = 130 });
        await service.RecordWeight(new() { AnimalId = 1, Reference = "BACKDATED", WeighedOn = Today.AddDays(-10), WeightKg = 120 });
        Assert.Equal(130, (await f.Db.Animals.SingleAsync(x => x.Id == 1)).CurrentWeightKg);
        Assert.Equal(3, await f.Db.AnimalWeightRecords.CountAsync());
        var result = Assert.Single((await service.Fattening()).Results);
        Assert.Equal(30, result.WeightGainKg); Assert.Equal(1m, result.DailyGainKg); Assert.Equal(130, result.CurrentWeightKg);
    }
    [Fact] public async Task PlanAndWeightReferencesAreNormalizedAndDuplicateProtected()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        var service = Service(f); await service.StartPlan(Plan(reference: " PLAN-1 "));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartPlan(Plan(reference: "SECOND")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordWeight(new() { AnimalId = 1, Reference = "DUPDAY", WeighedOn = Today.AddDays(-30), WeightKg = 101 }));
        Assert.Single(await f.Db.FatteningPlans.ToListAsync()); Assert.Single(await f.Db.AnimalWeightRecords.ToListAsync());
        Assert.Equal(100, (await f.Db.Animals.AsNoTracking().SingleAsync(x => x.Id == 1)).CurrentWeightKg);
    }
    [Theory] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task FatteningRejectsForeignBranchForeignCompanyAndSoldAnimal(int animal)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).StartPlan(Plan(animal)));
        Assert.Empty(await f.Db.FatteningPlans.ToListAsync()); Assert.Empty(await f.Db.AnimalWeightRecords.ToListAsync());
    }
    [Fact] public async Task FatteningIncludesOnlyAnimalSpecificCostsAndRealSale()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        var service = Service(f); await service.StartPlan(Plan()); await service.RecordExpense(Expense()); await service.RecordExpense(Expense(null, "SHARED"));
        f.Db.StockItems.Add(new() { Id = 999, CompanyId = 1, BranchId = 11, SKU = "FEED-TEST", Name = "Feed", UnitCost = 2.5m });
        f.Db.AnimalFeedRecords.Add(new() { CompanyId = 1, BranchId = 11, AnimalId = 1, StockItemId = 999, Reference = "FEED", Quantity = 10, UnitCost = 2.5m, CostAmount = 25m, FeedDate = Today });
        f.Db.AnimalSales.Add(new() { CompanyId = 1, BranchId = 11, AnimalId = 1, Amount = 2000, Reference = "SALE" });
        (await f.Db.Animals.SingleAsync(x => x.Id == 1)).Sold = true; await f.Db.SaveChangesAsync();
        var result = Assert.Single((await service.Fattening()).Results);
        Assert.Equal(1125.125m, result.RecordedCost); Assert.Equal(874.875m, result.RealizedMargin); Assert.Equal(25m, result.FeedCost);
    }
    [Fact] public async Task BreedingLifecyclePreservesHistoryAndPreventsTwoOpenCycles()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        var service = Service(f); var cycle = await service.StartBreeding(Breeding());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartBreeding(Breeding(reference: "SECOND")));
        await service.RecordOutcome(Outcome(cycle.Id, "Pregnant", "CHECK-1"));
        Assert.False((await f.Db.BreedingCycles.SingleAsync()).IsClosed);
        await service.RecordOutcome(Outcome(cycle.Id, "Calved", "CALF-1"));
        var saved = await f.Db.BreedingCycles.Include(x => x.Events).SingleAsync();
        Assert.True(saved.IsClosed); Assert.Equal("Calved", saved.Status); Assert.Equal(2, saved.Events.Count);
        Assert.Equal(4, await f.Db.Animals.CountAsync()); // Never invents a calf record or valuation.
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordOutcome(Outcome(cycle.Id, "Aborted", "INVALID")));
        await service.StartBreeding(new() { AnimalId = 1, Reference = "NEXT", ServiceDate = Today, ExpectedCalvingDate = Today.AddDays(283), Method = "Natural", SireOrSemen = "SIRE-22" });
        Assert.Equal(2, await f.Db.BreedingCycles.CountAsync());
    }
    [Fact] public async Task BreedingRejectsBackdatedEventsAndInvalidTransitionsWithoutChangingState()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        var service = Service(f); var cycle = await service.StartBreeding(Breeding());
        var input = Outcome(cycle.Id, "Pregnant", "CHECK"); input.EventDate = Today.AddDays(-41);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordOutcome(input));
        input.EventDate = Today; await service.RecordOutcome(input);
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordOutcome(Outcome(cycle.Id, "NotPregnant", "BAD")));
        Assert.Equal("Pregnant", (await f.Db.BreedingCycles.AsNoTracking().SingleAsync()).Status);
        Assert.Single(await f.Db.BreedingCycleEvents.ToListAsync());
    }
    [Theory] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task BreedingCannotStartForUnavailableAnimal(int animal)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).StartBreeding(Breeding(animal)));
        Assert.Empty(await f.Db.BreedingCycles.ToListAsync());
    }
    [Fact] public async Task BreedingOutcomeCannotCrossTenantBoundary()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await SeedAnimals(f); await f.GrantAsync("*"); await f.GrantAsync("*", "operator-b", 2);
        f.ActAs("operator-b", 2, 21); var cycle = await Service(f).StartBreeding(Breeding(3)); f.ActAs();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).RecordOutcome(Outcome(cycle.Id, "Calved", "CROSS")));
        Assert.Empty((await Service(f).Breeding()).Cycles); Assert.Empty(await f.Db.BreedingCycleEvents.ToListAsync());
    }
    [Theory] [InlineData("Bred", "Pregnant", false)] [InlineData("Bred", "NotPregnant", true)] [InlineData("Pregnant", "Calved", true)] [InlineData("Pregnant", "Aborted", true)]
    public void AllowedBreedingTransitionsHaveExpectedClosure(string status, string outcome, bool closed) => Assert.Equal(closed, FarmBusinessRules.ClosesCycle(status, outcome));
    [Theory] [InlineData("Calved", "Pregnant")] [InlineData("Pregnant", "Pregnant")] [InlineData("Aborted", "Calved")] [InlineData("Bred", "Unknown")]
    public void InvalidBreedingTransitionsAreRejected(string status, string outcome) => Assert.Throws<ArgumentException>(() => FarmBusinessRules.ClosesCycle(status, outcome));
    [Fact] public void OperationalControllersRequireLoginAndEveryPostRequiresAntiforgery()
    {
        foreach (var type in new[] { typeof(FatteningController), typeof(ExpensesController), typeof(BreedingController) })
        {
            Assert.NotEmpty(type.GetCustomAttributes(typeof(AuthorizeAttribute), true));
            foreach (var method in type.GetMethods().Where(x => x.GetCustomAttributes(typeof(HttpPostAttribute), true).Length > 0))
                Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
        }
    }
}
