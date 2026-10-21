using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class CompanyLocalBusinessDateTests
{
    private static readonly DateOnly LocalToday = new(2026, 10, 19);
    private static readonly DateTimeOffset DhakaMidnightWindow = new(2026, 10, 18, 18, 30, 0, TimeSpan.Zero);
    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public int Reads { get; private set; }
        public override DateTimeOffset GetUtcNow() { Reads++; return instant; }
    }
    private static FixedClock Clock() => new(DhakaMidnightWindow);
    private static FarmService Farm(SecurityTestScope f, TimeProvider clock) => new(f.Db, f.Tenant, f.Permissions, clock);
    private static FinanceService Finance(SecurityTestScope f, TimeProvider clock) => new(f.Db, f.Tenant, f.Permissions, clock);
    private static IntegratedOperations Operations(SecurityTestScope f, TimeProvider clock) => new(f.Db, f.Tenant, f.Permissions, clock);
    private static FarmBusinessService Business(SecurityTestScope f, TimeProvider clock) => new(f.Db, f.Tenant, f.Permissions, clock);
    private static FarmHealthFeedService Care(SecurityTestScope f, TimeProvider clock) => new(f.Db, f.Tenant, f.Permissions, clock);
    private static async Task Policy(SecurityTestScope f, string zone = "Asia/Dhaka", DateOnly? from = null, DateOnly? to = null, int company = 1, bool locked = true)
    {
        f.Db.CompanyBusinessPolicies.Add(new CompanyBusinessPolicy
        {
            CompanyId = company, Version = 1, CountryCode = "BD", CurrencyCode = "BDT", CurrencyMinorUnits = 2,
            TimeZoneId = zone, EffectiveFrom = from ?? new DateOnly(2000, 1, 1), EffectiveTo = to,
            Status = "Approved", CreatedBy = "test-proposer", ApprovedBy = "test-reviewer",
            ReviewBasis = "Synthetic timezone regression fixture; no jurisdictional rules implied."
        });
        if (locked) f.Db.CompanyCurrencyLocks.Add(new CompanyCurrencyLock { CompanyId = company, CurrencyCode = "BDT", CurrencyMinorUnits = 2 });
        await f.Db.SaveChangesAsync();
    }
    private static async Task<Animal> Animal(SecurityTestScope f)
    {
        var animal = new Animal { CompanyId = 1, BranchId = 11, Tag = "DATE-COW", Breed = "Cattle" };
        f.Db.Animals.Add(animal); await f.Db.SaveChangesAsync(); return animal;
    }

    [Theory]
    [InlineData("2026-10-18T17:59:59+00:00", 18)]
    [InlineData("2026-10-18T18:00:00+00:00", 19)]
    [InlineData("2026-10-18T23:59:59+00:00", 19)]
    [InlineData("2026-10-19T00:00:00+00:00", 19)]
    public async Task DhakaDayChangesAtReviewedLocalMidnight(string instant, int day)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f);
        var clock = new FixedClock(DateTimeOffset.Parse(instant));
        var business = await CompanyBusinessDate.Current(f.Db, 1, clock: clock);
        Assert.Equal(new DateOnly(2026, 10, day), business.Date);
        Assert.Equal(CompanyBusinessDate.AccountingDate(business.Date), business.PostingDate);
        Assert.Equal(1, clock.Reads);
    }

    [Fact]
    public async Task PolicyEffectiveOnLocalDayAppliesBeforeUtcMidnight()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f, from: LocalToday);
        Assert.Equal(LocalToday, (await CompanyBusinessDate.Current(f.Db, 1, clock: Clock())).Date);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CompanyBusinessDate.Current(f.Db, 1,
            clock: new FixedClock(DhakaMidnightWindow.AddHours(-1))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConfiguredPolicyFailureNeverSilentlyFallsBackToUtc(bool expired)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await Policy(f, to: expired ? LocalToday.AddDays(-1) : null, locked: expired);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CompanyBusinessDate.Current(f.Db, 1, clock: Clock()));
    }

    [Fact]
    public async Task OnlyUnconfiguredCompanyRetainsLegacyUtcDayAndPostingInstant()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f, company: 2);
        var business = await CompanyBusinessDate.Current(f.Db, 1, clock: Clock());
        Assert.Null(business.Policy); Assert.Equal(LocalToday.AddDays(-1), business.Date);
        Assert.Equal(DhakaMidnightWindow, business.PostingDate);
        await f.GrantAsync("*"); var animal = await Animal(f);
        await Assert.ThrowsAsync<ArgumentException>(() => Farm(f, Clock()).RecordMilk(animal.Id, LocalToday, "Morning", 1, default));
        var voucher = await Farm(f, Clock()).PostVoucher("LEGACY", [new() { AccountCode = "1000", Debit = 1 }, new() { AccountCode = "3000", Credit = 1 }], default);
        Assert.Equal(DhakaMidnightWindow, voucher.Date);
    }

    [Fact]
    public async Task MilkAndBankAcceptCompanyTodayAndRejectTomorrow()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var animal = await Animal(f); var farm = Farm(f, Clock()); var finance = Finance(f, Clock());
        Assert.Equal(LocalToday, (await farm.RecordMilk(animal.Id, LocalToday, "Morning", 2, default)).Date);
        Assert.Equal(LocalToday, (await finance.RecordBankLine("LOCAL-BANK", 20, LocalToday, default)).ValueDate);
        await Assert.ThrowsAsync<ArgumentException>(() => farm.RecordMilk(animal.Id, LocalToday.AddDays(1), "Evening", 2, default));
        await Assert.ThrowsAsync<ArgumentException>(() => finance.RecordBankLine("FUTURE-BANK", 20, LocalToday.AddDays(1), default));
        Assert.Single(await f.Db.MilkYields.ToListAsync()); Assert.Single(await f.Db.BankStatementLines.ToListAsync());
    }

    [Fact]
    public async Task NegativeOffsetCompanyDoesNotAcceptUtcTomorrowAsToday()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f, zone: "America/New_York"); await f.GrantAsync("*");
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 19, 1, 0, 0, TimeSpan.Zero));
        var animal = await Animal(f); var farm = Farm(f, clock);
        Assert.Equal(LocalToday.AddDays(-1), (await CompanyBusinessDate.Current(f.Db, 1, clock: clock)).Date);
        await farm.RecordMilk(animal.Id, LocalToday.AddDays(-1), "Morning", 1, default);
        await Assert.ThrowsAsync<ArgumentException>(() => farm.RecordMilk(animal.Id, LocalToday, "Evening", 1, default));
    }

    [Fact]
    public async Task LocalPreviousDayCanCloseAndCurrentDayCannotClose()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var service = new AccountingPeriodService(f.Db, f.Tenant, f.Permissions, Clock()); var before = DateTimeOffset.UtcNow;
        await service.Close(LocalToday.AddDays(-1), "Reviewed local-day close", default);
        var close = await f.Db.AccountingPeriodCloses.SingleAsync();
        Assert.Equal(LocalToday.AddDays(-1), close.ClosedThrough);
        Assert.InRange(close.ClosedUtc, before, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ArgumentException>(() => service.Close(LocalToday, "Still open", default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.Close(LocalToday.AddDays(1), "Future", default));
        var controller = new AccountingPeriodController(f.Db, f.Tenant, f.Permissions, service, Clock());
        await controller.Index(default); Assert.Equal(LocalToday, Assert.IsType<DateOnly>(controller.ViewData["BusinessToday"]));
    }

    [Fact]
    public async Task OperationalPostingsUseOneCompanyDayAndKeepAuditInstantsAndHistory()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var historicalDate = new DateTimeOffset(2026, 10, 17, 8, 15, 0, TimeSpan.Zero);
        var historical = BusinessInput.Voucher(1, 11, "HISTORICAL", ("1000", 1m, 0m), ("3000", 0m, 1m));
        historical.Date = historicalDate; f.Db.Vouchers.Add(historical); await f.Db.SaveChangesAsync();
        await new AccountingPeriodService(f.Db, f.Tenant, f.Permissions, Clock()).Close(LocalToday.AddDays(-1), "Reviewed", default);
        var before = DateTimeOffset.UtcNow; var farm = Farm(f, Clock()); var operations = Operations(f, Clock());
        var stock = await farm.AddStock("LOCAL-STOCK", "Local stock", 10, default, "KG");
        var purchase = await operations.Receive(stock.Id, 10, 10, "LOCAL-PUR", default);
        var sale = await operations.Sell(stock.Id, 2, 15, "LOCAL-SAL", default);
        var saleDocument = await f.Db.FinanceDocuments.SingleAsync(x => x.Kind == "Sale" && x.SourceId == sale.Id);
        var purchaseDocument = await f.Db.FinanceDocuments.SingleAsync(x => x.Kind == "Purchase" && x.SourceId == purchase.Id);
        await operations.Return(saleDocument.Id, 1, "LOCAL-SAL-RET", default);
        await operations.Return(purchaseDocument.Id, 1, "LOCAL-PUR-RET", default);
        await Finance(f, Clock()).RecordDocumentPayment(saleDocument.Id, "LOCAL-PAY", 15, default);
        await farm.MoveStock(stock.Id, 1, "LOCAL-ADJ", default);
        var animal = await farm.AddAnimal("LOCAL-ANIMAL", "Cattle", 100, default);
        var animalSale = await farm.SellAnimal(animal.Id, 120, "LOCAL-ANIMAL-SAL", default);
        await farm.PostVoucher("LOCAL-JRN", [new() { AccountCode = "1000", Debit = 10 }, new() { AccountCode = "3000", Credit = 10 }], default);
        var posted = await f.Db.Vouchers.AsNoTracking().Where(x => x.Reference != "HISTORICAL").ToListAsync();
        Assert.Equal(9, posted.Count);
        Assert.All(posted, x => Assert.Equal(CompanyBusinessDate.AccountingDate(LocalToday), x.Date));
        Assert.Equal(CompanyBusinessDate.AccountingDate(LocalToday), animalSale.Date);
        Assert.Equal(historicalDate, (await f.Db.Vouchers.AsNoTracking().SingleAsync(x => x.Id == historical.Id)).Date);
        var after = DateTimeOffset.UtcNow;
        Assert.All(await f.Db.Audits.Where(x => x.Action != "Accounts.PeriodClosed").ToListAsync(), x => Assert.InRange(x.CreatedUtc, before, after));
        Assert.All(await f.Db.FinancePayments.ToListAsync(), x => Assert.InRange(x.CreatedUtc, before, after));
        Assert.All(await f.Db.StockMovements.ToListAsync(), x => Assert.InRange(x.CreatedUtc, before, after));
        Assert.InRange(sale.CreatedUtc, before, after); Assert.InRange(purchase.CreatedUtc, before, after);
    }

    [Fact]
    public async Task PurchaseOrderReceiptUsesSameLocalPostingDate()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var ops = Operations(f, Clock()); var stock = await Farm(f, Clock()).AddStock("ORDER-STOCK", "Order stock", 10, default, "KG");
        var supplier = await ops.CreateParty("Supplier", "LOCAL-SUP", "Local supplier", null, null, null, default);
        var order = await ops.CreateOrder(supplier.Id, stock.Id, 2, 10, "LOCAL-ORDER", default);
        await ops.ApproveOrder(order.Id, default); await ops.ReceiveOrder(order.Id, 1, "LOCAL-RECEIPT", default);
        Assert.Equal(CompanyBusinessDate.AccountingDate(LocalToday), (await f.Db.Vouchers.SingleAsync()).Date);
    }

    [Fact]
    public async Task BothTransferVouchersUseLocalDateWhileTransferAndMovementsKeepUtcInstants()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        f.Db.UserBranchAccess.Add(new UserBranchAccess { UserId = "operator-a", CompanyId = 1, BranchId = 12 });
        var destination = new StockItem { CompanyId = 1, BranchId = 12, SKU = "TRANSFER-DATE", Name = "Destination stock", UnitOfMeasure = "KG" };
        f.Db.StockItems.Add(destination); await f.Db.SaveChangesAsync();
        var source = await Farm(f, Clock()).AddStock("TRANSFER-DATE", "Source stock", 10, default, "KG");
        await Operations(f, Clock()).Receive(source.Id, 2, 10, "TRANSFER-RECEIPT", default);
        var closing = new AccountingPeriodService(f.Db, f.Tenant, f.Permissions, Clock());
        await closing.Close(LocalToday.AddDays(-1), "Source reconciled", default);
        f.ActAs(branch: 12); await closing.Close(LocalToday.AddDays(-1), "Destination reconciled", default); f.ActAs();
        var before = DateTimeOffset.UtcNow;
        var transfer = await new StockTransferService(f.Db, f.Tenant, f.Permissions, Clock()).Post(source.Id, 12, destination.Id, 1, "LOCAL-TRANSFER", null, default);
        Assert.InRange(transfer.CreatedUtc, before, DateTimeOffset.UtcNow);
        var vouchers = await f.Db.Vouchers.Where(x => x.Id == transfer.SourceVoucherId || x.Id == transfer.DestinationVoucherId).ToListAsync();
        Assert.Equal(2, vouchers.Count); Assert.All(vouchers, x => Assert.Equal(CompanyBusinessDate.AccountingDate(LocalToday), x.Date));
        var movements = await f.Db.StockMovements.Where(x => x.Id == transfer.SourceMovementId || x.Id == transfer.DestinationMovementId).ToListAsync();
        Assert.All(movements, x => Assert.Equal(transfer.CreatedUtc, x.CreatedUtc));
        Assert.Equal(transfer.CreatedUtc, (await f.Db.Audits.SingleAsync(x => x.Action == "Inventory.Transfer")).CreatedUtc);
    }

    [Fact]
    public async Task ExplicitExpenseDateIsPreservedAndClosedDateStillRejected()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var service = Business(f, Clock());
        ExpenseInput Input(DateOnly date, string reference) => new() { ExpenseDate = date, Reference = reference, Amount = 10, Category = "Veterinary", Description = "Recorded care" };
        await service.RecordExpense(Input(LocalToday.AddDays(-2), "HISTORIC-EXPENSE"));
        await service.RecordExpense(Input(LocalToday, "LOCAL-EXPENSE"));
        var dates = await f.Db.FarmExpenses.Include(x => x.Voucher).ToListAsync();
        Assert.All(dates, x => Assert.Equal(CompanyBusinessDate.AccountingDate(x.ExpenseDate), x.Voucher.Date));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordExpense(Input(LocalToday.AddDays(1), "FUTURE-EXPENSE")));
        await new AccountingPeriodService(f.Db, f.Tenant, f.Permissions, Clock()).Close(LocalToday.AddDays(-1), "Reviewed", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordExpense(Input(LocalToday.AddDays(-1), "CLOSED-EXPENSE")));
        Assert.Equal(2, await f.Db.FarmExpenses.CountAsync());
    }

    [Fact]
    public async Task FarmRecordedDatesAndPageDefaultsFollowCompanyToday()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var animal = await Animal(f); var service = Business(f, Clock());
        await service.StartPlan(new() { AnimalId = animal.Id, Reference = "LOCAL-PLAN", StartDate = LocalToday, TargetDate = LocalToday.AddDays(90), InitialWeightKg = 100, TargetWeightKg = 150 });
        var cycle = await service.StartBreeding(new() { AnimalId = animal.Id, Reference = "LOCAL-BREED", ServiceDate = LocalToday, ExpectedCalvingDate = LocalToday.AddDays(280), Method = "Natural", SireOrSemen = "TEST" });
        await service.RecordOutcome(new() { CycleId = cycle.Id, Reference = "LOCAL-OUTCOME", EventDate = LocalToday, Outcome = "Pregnant", Notes = "Recorded outcome" });
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordWeight(new() { AnimalId = animal.Id, Reference = "FUTURE-WEIGHT", WeighedOn = LocalToday.AddDays(1), WeightKg = 110 }));
        var fattening = await service.Fattening(); Assert.Equal(LocalToday, fattening.Plan.StartDate); Assert.Equal(LocalToday, fattening.Weight.WeighedOn);
        var expenses = await service.Expenses(); Assert.Equal(LocalToday, expenses.To); Assert.Equal(LocalToday, expenses.Expense.ExpenseDate);
        var breeding = await service.Breeding(); Assert.Equal(LocalToday, breeding.Today); Assert.Equal(LocalToday, breeding.OutcomeEntry.EventDate);
    }

    [Fact]
    public async Task HealthFeedAndDairyControllersAcceptAndShowLocalToday()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var animal = await Animal(f); var care = Care(f, Clock());
        var healthController = new AnimalHealthEntryController(f.Db, f.Tenant, f.Permissions, care, Clock());
        var result = await healthController.Record(new AnimalHealthInput { AnimalId = animal.Id, Reference = "LOCAL-HEALTH", EventDate = LocalToday, EventType = "Observation", NextDueDate = LocalToday });
        Assert.IsType<RedirectToActionResult>(result);
        var health = await f.Db.AnimalHealthRecords.SingleAsync(); await care.CompleteFollowUp(health.Id, LocalToday);
        await Assert.ThrowsAsync<ArgumentException>(() => care.RecordHealth(animal.Id, "FUTURE-HEALTH", "Observation", LocalToday.AddDays(1), null, "", default));
        var page = Assert.IsType<AnimalHealthPage>(Assert.IsType<ViewResult>(await healthController.Index(null)).Model);
        Assert.Equal(LocalToday, page.Today); Assert.Equal(LocalToday, page.Entry.EventDate);
        var stock = await Farm(f, Clock()).AddStock("LOCAL-FEED", "Local feed", 10, default, "KG");
        await Operations(f, Clock()).Receive(stock.Id, 2, 10, "FEED-PURCHASE", default);
        var feed = await care.ConsumeFeed(animal.Id, stock.Id, 1, "LOCAL-FEED", LocalToday, default);
        Assert.Equal(LocalToday, feed.FeedDate);
        Assert.Equal(CompanyBusinessDate.AccountingDate(LocalToday), (await f.Db.Vouchers.SingleAsync(x => x.Reference == "FEED:LOCAL-FEED")).Date);
        await Assert.ThrowsAsync<ArgumentException>(() => care.ConsumeFeed(animal.Id, stock.Id, 1, "FUTURE-FEED", LocalToday.AddDays(1), default));
        var dairyController = new DairyEntryController(f.Db, f.Tenant, f.Permissions, Farm(f, Clock()), Clock());
        var milkPage = Assert.IsType<MilkEntryPage>(Assert.IsType<ViewResult>(await dairyController.Index(null)).Model);
        Assert.Equal(LocalToday, milkPage.Day); Assert.Equal(LocalToday, milkPage.Entry.Date);
    }

    [Fact]
    public async Task AccountsAndReportsIncludeCurrentCompanyDayBeforeUtcMidnight()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        await Farm(f, Clock()).PostVoucher("LOCAL-REPORT", [new() { AccountCode = "1000", Debit = 25 }, new() { AccountCode = "3000", Credit = 25 }], default);
        var finance = Finance(f, Clock());
        var accounts = new OperationsWorkspaceController(f.Db, f.Tenant, f.Permissions, Operations(f, Clock()), Farm(f, Clock()), finance, Clock());
        var model = Assert.IsType<OperationsWorkspaceModel>(Assert.IsType<ViewResult>(await accounts.Accounts(null, null, null, default)).Model);
        Assert.Equal(LocalToday, model.Today); Assert.Equal(LocalToday, model.Through); Assert.Equal(2, model.Ledger.Count);
        var workspace = new WorkspaceController(f.Db, f.Tenant, f.Permissions, Care(f, Clock()), new ReportService(f.Db, f.Tenant, f.Permissions), finance, new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor), Clock());
        var report = Assert.IsType<ReportPage>(Assert.IsType<ViewResult>(await workspace.Reports(default)).Model);
        Assert.Equal(CompanyBusinessDate.AccountingDate(LocalToday.AddDays(1)), report.ThroughExclusive);
        Assert.Equal(25, report.Statement!.Assets);
    }

    [Fact]
    public async Task OpeningDateUsesCompanyTodayWithoutChangingExplicitDate()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var service = new OpeningBalanceService(f.Db, f.Tenant, f.Permissions, Clock());
        Assert.Equal(LocalToday, (await service.Page()).Opening.OpeningDate);
        var opening = await service.Post(new OpeningBalanceInput { OpeningDate = LocalToday, Reference = "LOCAL-OPEN", ConfirmReviewed = true,
            ReviewNote = "Synthetic reviewed opening", Ledger = [new() { AccountCode = "1000", Debit = 10 }, new() { AccountCode = "3000", Credit = 10 }] });
        Assert.Equal(CompanyBusinessDate.AccountingDate(LocalToday), opening.Voucher.Date);
    }

    [Fact]
    public async Task OmittedRecordedDatesCannotSilentlyUseTheServerDay()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Policy(f); await f.GrantAsync("*");
        var animal = await Animal(f);
        var milk = new MilkEntryInput { AnimalId = animal.Id, Litres = 1 };
        await Assert.ThrowsAsync<ArgumentException>(() => Farm(f, Clock()).RecordMilk(animal.Id, milk.Date, milk.Session, milk.Litres, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Business(f, Clock()).RecordExpense(new ExpenseInput { Reference = "NO-DATE", Amount = 10, Description = "Missing date" }));
        await Assert.ThrowsAsync<ArgumentException>(() => Care(f, Clock()).CompleteFollowUp(1, new HealthFollowUpInput().CompletedDate));
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.MilkYields.ToListAsync());
    }

    [Fact]
    public async Task OptionalClockDoesNotRequireProductionRegistration()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        Assert.NotNull(ActivatorUtilities.CreateInstance<FarmService>(f.Services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<FinanceService>(f.Services));
        Assert.NotNull(ActivatorUtilities.CreateInstance<AccountingPeriodService>(f.Services));
    }
}
