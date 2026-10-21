using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnterpriseERP.UnitTests;
public sealed class FinanceWorkflowTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static FarmService Farm(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static IntegratedOperations Ops(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static FinanceService Finance(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static async Task<StockItem> Item(SecurityTestScope f, string sku = "FEED") => await Farm(f).AddStock(sku, "Test feed", 0, Ct, "KG");
    private static async Task AssertBalanced(SecurityTestScope f)
    {
        var vouchers = await f.Db.Vouchers.AsNoTracking().Include(x => x.Lines).ToListAsync();
        Assert.All(vouchers, v => { Assert.True(v.Posted); Assert.True(v.Lines.Sum(x => x.Debit) > 0); Assert.Equal(v.Lines.Sum(x => x.Debit), v.Lines.Sum(x => x.Credit)); });
    }
    [Theory]
    [InlineData("0.0004")]
    [InlineData("1.0004")]
    [InlineData("1000000000000000")]
    [InlineData("-0.001")]
    public void DecimalInputsRejectStorageLossOrOverflow(string text)
    {
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentException>(() => BusinessInput.Decimal(value, "Quantity", true));
    }
    [Fact]
    public void CalculatedTotalsRejectRoundToZeroAndOverflow()
    {
        Assert.Throws<ArgumentException>(() => BusinessInput.Total(.001m, .001m));
        Assert.Throws<ArgumentException>(() => BusinessInput.Total(BusinessInput.Maximum, BusinessInput.Maximum));
        Assert.Equal("REF-1", BusinessInput.Reference(" REF-1 "));
        Assert.Throws<ArgumentException>(() => BusinessInput.Voucher(1,11,"invalid-account",("7000",1,0),("1000",0,1)));
    }
    [Fact]
    public async Task PurchaseAndDepletionPreserveExactCarryingValue()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var item = await Item(f); var ops = Ops(f);
        await ops.Receive(item.Id, 1, 1, " p1 ", Ct); await ops.Receive(item.Id, 2, 2, "p2", Ct);
        Assert.Equal(3, item.QuantityOnHand); Assert.Equal(1.667m, item.UnitCost);
        Assert.Equal(5, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
        await ops.Sell(item.Id, 1, 3, "s1", Ct);
        Assert.Equal(3.333m, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
        await ops.Sell(item.Id, 2, 3, "s2", Ct);
        Assert.Equal(0, item.QuantityOnHand); Assert.Equal(0, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
        Assert.Equal(5, (await f.Db.FinanceDocuments.Where(x => x.Kind == "Sale").ToListAsync()).Sum(x => x.CostAmount));
        var inventory = await Finance(f).Ledger("1300", DateTimeOffset.MinValue, DateTimeOffset.MaxValue, Ct);
        Assert.Equal(0, inventory.Sum(x => x.Debit - x.Credit)); await AssertBalanced(f);
    }
    [Fact]
    public async Task SalesReturnCreatesRefundAndPreventsOverSettlement()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var item = await Item(f); await Ops(f).Receive(item.Id, 10, 2, "purchase", Ct);
        await Ops(f).Sell(item.Id, 3, 5, "sale", Ct);
        var doc = await f.Db.FinanceDocuments.SingleAsync(x => x.Kind == "Sale");
        await Finance(f).RecordDocumentPayment(doc.Id, " receipt ", 15, Ct);
        await Ops(f).Return(doc.Id, 1, " return ", Ct);
        var due = (await Finance(f).Documents(Ct)).Single(x => x.Id == doc.Id); Assert.Equal(-5, due.Outstanding);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Finance(f).RecordDocumentPayment(doc.Id, "too-much", 6, Ct));
        await Finance(f).RecordDocumentPayment(doc.Id, "refund", 5, Ct);
        Assert.Equal(0, (await Finance(f).Documents(Ct)).Single(x => x.Id == doc.Id).Outstanding);
        Assert.Equal("CustomerRefund", (await f.Db.FinancePayments.SingleAsync(x => x.Reference == "refund")).PartyType);
        Assert.Equal(8, item.QuantityOnHand); Assert.Equal(16, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ops(f).Return(doc.Id, 3, "excess", Ct));
        await AssertBalanced(f);
    }
    [Fact]
    public async Task PurchaseReturnPostsVarianceAndSupplierRefund()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var item = await Item(f); var ops = Ops(f);
        await ops.Receive(item.Id, 1, 1, "p1", Ct); await ops.Receive(item.Id, 2, 2, "p2", Ct);
        var doc = await f.Db.FinanceDocuments.SingleAsync(x => x.Reference == "PUR-p1");
        await Finance(f).RecordDocumentPayment(doc.Id, "pay", 1, Ct);
        var returned = await ops.Return(doc.Id, 1, "ret", Ct);
        Assert.Equal(1, returned.Amount); Assert.Equal(1.667m, returned.CostAmount);
        Assert.Equal(3.333m, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
        await Finance(f).RecordDocumentPayment(doc.Id, "supplier-refund", 1, Ct);
        Assert.Equal("SupplierRefund", (await f.Db.FinancePayments.SingleAsync(x => x.Reference == "supplier-refund")).PartyType);
        Assert.Equal(0, (await Finance(f).Documents(Ct)).Single(x => x.Id == doc.Id).Outstanding);
        await AssertBalanced(f);
    }
    [Fact]
    public async Task AnimalAcquisitionSaleAndPaymentPostAllLegs()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var animal = await Farm(f).AddAnimal(" a-01 ", "Local", 100, Ct, 200);
        Assert.Equal("A-01", animal.Tag); Assert.Single(await f.Db.FinanceDocuments.Where(x => x.Kind == "AnimalPurchase").ToListAsync());
        await Farm(f).SellAnimal(animal.Id, 140, "animal-sale", Ct);
        var doc = await f.Db.FinanceDocuments.SingleAsync(x => x.Kind == "AnimalSale");
        await Finance(f).RecordDocumentPayment(doc.Id, "animal-pay", 140, Ct);
        Assert.True(animal.Sold);
        var ledger = await Finance(f).Ledger("1400", DateTimeOffset.MinValue, DateTimeOffset.MaxValue, Ct); Assert.Equal(0, ledger.Sum(x => x.Debit - x.Credit));
        var statement = await Finance(f).Statement(DateTimeOffset.MaxValue, Ct); Assert.Equal(40, statement.Profit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Farm(f).SellAnimal(animal.Id, 140, "animal-sale-2", Ct));
        await AssertBalanced(f);
    }
    [Fact]
    public async Task AdjustmentsAndFeedUseExactValuationAndRejectDuplicateReferences()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var item = await Farm(f).AddStock("ADJUST", "Adjustment item", 1.667m, Ct, "KG");
        await Farm(f).MoveStock(item.Id, 3, " start ", Ct);
        Assert.Equal(5.001m, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Farm(f).MoveStock(item.Id, 3, "start", Ct));
        var animal = await Farm(f).AddAnimal("feed-animal", "Local", 0, Ct);
        var care = new FarmHealthFeedService(f.Db, f.Tenant, f.Permissions);
        var feed = await care.ConsumeFeed(animal.Id, item.Id, 1, " feed-1 ", DateOnly.FromDateTime(DateTime.UtcNow), Ct);
        Assert.Equal(1.667m, feed.CostAmount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => care.ConsumeFeed(animal.Id, item.Id, 1, "feed-1", DateOnly.FromDateTime(DateTime.UtcNow), Ct));
        await Farm(f).MoveStock(item.Id, -2, "remove", Ct);
        Assert.Equal(0, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
        var lines = await Finance(f).Ledger("1300", DateTimeOffset.MinValue, DateTimeOffset.MaxValue, Ct); Assert.Equal(0, lines.Sum(x => x.Debit - x.Credit));
        await AssertBalanced(f);
    }
    [Fact]
    public async Task InvalidPrecisionCannotMutateStockOrLedger()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var item = await Item(f);
        await Assert.ThrowsAsync<ArgumentException>(() => Ops(f).Receive(item.Id, .0004m, 1000, "bad", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Ops(f).Receive(item.Id, 1, 1.0004m, "bad2", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Farm(f).MoveStock(item.Id, .0004m, "bad3", Ct));
        Assert.Equal(0, item.QuantityOnHand); Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.StockMovements.ToListAsync());
    }
    [Fact]
    public async Task ReconciliationMatchesOnceAndRejectsReuse()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var voucher = await Farm(f).PostVoucher("opening-cash", new() { new() { AccountCode = "1000", Debit = 10 }, new() { AccountCode = "3000", Credit = 10 } }, Ct);
        var first = await Finance(f).RecordBankLine("bank-1", 10, DateOnly.FromDateTime(DateTime.UtcNow), Ct);
        var second = await Finance(f).RecordBankLine("bank-2", 10, DateOnly.FromDateTime(DateTime.UtcNow), Ct);
        await Finance(f).Reconcile(first.Id, voucher.Id, Ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Finance(f).Reconcile(second.Id, voucher.Id, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Finance(f).Reconcile(first.Id, voucher.Id, Ct));
        Assert.Null(second.MatchedVoucherId);
        // The database unique index also rejects a bypass of the service's reuse check.
        second.MatchedVoucherId = voucher.Id;
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task MissingPermissionAndForeignBranchCannotPostTransactions()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Ops(f).Receive(1, 1, 1, "no-permission", Ct));
        await f.GrantAsync("*");
        var foreign = new StockItem { CompanyId = 2, BranchId = 21, SKU = "FOREIGN", Name = "Foreign" }; f.Db.StockItems.Add(foreign); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ops(f).Receive(foreign.Id, 1, 1, "foreign", Ct));
        Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    [Fact]
    public async Task LegacyUnvaluedStockAndUnallocatedPaymentsAreBlocked()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var legacy = new StockItem { CompanyId = 1, BranchId = 11, SKU = "LEGACY", Name = "Needs opening review", QuantityOnHand = 3, UnitCost = 2 }; f.Db.StockItems.Add(legacy); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ops(f).Sell(legacy.Id, 1, 3, "legacy-sale", Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Finance(f).RecordPayment("unallocated", "Customer", 1, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Farm(f).PostVoucher("control-bypass", new() { new() { AccountCode = "1300", Debit = 1 }, new() { AccountCode = "3000", Credit = 1 } }, Ct));
    }
    [Fact]
    public async Task ConcurrentBankMatchCannotOverwriteAnExistingMatch()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var firstVoucher = await Farm(f).PostVoucher("cash1", new() { new() { AccountCode = "1000", Debit = 10 }, new() { AccountCode = "3000", Credit = 10 } }, Ct);
        var secondVoucher = await Farm(f).PostVoucher("cash2", new() { new() { AccountCode = "1000", Debit = 10 }, new() { AccountCode = "3000", Credit = 10 } }, Ct);
        var line = await Finance(f).RecordBankLine("match-race", 10, DateOnly.FromDateTime(DateTime.UtcNow), Ct);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.Db.Database.GetDbConnection()).Options;
        await using var staleContext = new AppDbContext(options);
        var staleLine = await staleContext.BankStatementLines.SingleAsync(x => x.Id == line.Id);
        await Finance(f).Reconcile(line.Id, firstVoucher.Id, Ct);
        staleLine.MatchedVoucherId = secondVoucher.Id;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
        Assert.Equal(firstVoucher.Id, (await f.Db.BankStatementLines.AsNoTracking().SingleAsync(x => x.Id == line.Id)).MatchedVoucherId);
    }
    [Fact]
    public async Task PartialSalesReturnsRestoreExactOriginalCostIncludingRemainder()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var item = await Item(f);
        await Ops(f).Receive(item.Id, 1, 1, "p1", Ct); await Ops(f).Receive(item.Id, 2, 2, "p2", Ct);
        await Ops(f).Sell(item.Id, 3, 3, "sale-all", Ct); var doc = await f.Db.FinanceDocuments.SingleAsync(x => x.Kind == "Sale");
        await Ops(f).Return(doc.Id, 1, "r1", Ct); await Ops(f).Return(doc.Id, 1, "r2", Ct); await Ops(f).Return(doc.Id, 1, "r3", Ct);
        var returns = await f.Db.StockDocumentReturns.ToListAsync(); Assert.Equal(5, returns.Sum(x => x.CostAmount)); Assert.Equal(9, returns.Sum(x => x.Amount));
        Assert.Equal(5, (await f.Db.InventoryCostBalances.SingleAsync()).Value); Assert.Equal(3, item.QuantityOnHand);
        await AssertBalanced(f);
    }

    [Fact]
    public async Task PurchaseOrderRequiresApprovalThenReceivesExactlyOnceWithLinkedSupplier()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var item = await Item(f); var ops = Ops(f);
        var supplier = await ops.CreateParty("Supplier", " sup-1 ", "Supplier One", null, null, null, Ct);
        var order = await ops.CreateOrder(supplier.Id, item.Id, 3, 1.667m, " order-1 ", Ct);
        Assert.Equal("Draft", order.Status); Assert.Equal(0, item.QuantityOnHand); Assert.Empty(await f.Db.Vouchers.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => ops.ReceiveOrder(order.Id, 1, "before-approval", Ct));
        await ops.ApproveOrder(order.Id, Ct); Assert.Equal("Approved", order.Status);
        await ops.ReceiveOrder(order.Id, 1, " receipt-1 ", Ct); Assert.Equal("PartiallyReceived", order.Status); Assert.Equal(1, order.ReceivedQuantity);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ops.ReceiveOrder(order.Id, 3, "over-receipt", Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ops.ReceiveOrder(order.Id, 1, "receipt-1", Ct));
        await ops.ReceiveOrder(order.Id, 2, "receipt-2", Ct);
        Assert.Equal("Received", order.Status); Assert.Equal(3, order.ReceivedQuantity); Assert.Equal(5.001m, order.ReceivedAmount);
        Assert.Equal(3, item.QuantityOnHand); Assert.Equal(2, await f.Db.PurchaseOrderReceipts.CountAsync());
        Assert.All(await f.Db.FinanceDocuments.ToListAsync(), d => Assert.Equal(supplier.Id, d.PartyId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ops.ReceiveOrder(order.Id, 1, "repeat", Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ops.CancelOrder(order.Id, Ct));
        await AssertBalanced(f);
    }
    [Fact]
    public async Task CancelledOrderRetainsReceiptsAndBlocksFurtherReceiving()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var item = await Item(f); var ops = Ops(f);
        var supplier = await ops.CreateParty("Supplier", "SUP", "Supplier", null, null, null, Ct);
        var order = await ops.CreateOrder(supplier.Id, item.Id, 10, 2, "cancel-order", Ct);
        await ops.ApproveOrder(order.Id, Ct); await ops.ReceiveOrder(order.Id, 4, "partial", Ct); await ops.CancelOrder(order.Id, Ct);
        Assert.Equal("Cancelled", order.Status); Assert.Equal(4, order.ReceivedQuantity); Assert.Equal(4, item.QuantityOnHand);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ops.ReceiveOrder(order.Id, 1, "blocked", Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ops.ApproveOrder(order.Id, Ct));
        Assert.Single(await f.Db.PurchaseOrderReceipts.ToListAsync());
    }
    [Fact]
    public async Task PurchaseApprovalAndPartyTypesAreEnforced()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        foreach(var permission in new[] { "Inventory.Create", "Parties.Manage", "Purchase.Post" }) await f.GrantAsync(permission);
        var item = await Item(f); var ops = Ops(f);
        var supplier = await ops.CreateParty("Supplier", "SUP", "Supplier", null, null, null, Ct);
        var customer = await ops.CreateParty("Customer", "CUS", "Customer", null, null, null, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => ops.CreateOrder(customer.Id, item.Id, 1, 1, "wrong-kind", Ct));
        var order = await ops.CreateOrder(supplier.Id, item.Id, 1, 1, "approval", Ct);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => ops.ApproveOrder(order.Id, Ct)); Assert.Equal("Draft", order.Status);
        await ops.SetPartyActive(supplier.Id, false, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => ops.Receive(item.Id, 1, 1, "inactive", Ct, supplier.Id));
    }
    [Fact]
    public async Task PartyAndPurchaseOrderIsolationRejectForeignTenantIds()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        await f.GrantAsync("*", "operator-b", 2); var localItem = await Item(f);
        f.ActAs("operator-b", 2, 21); var foreignItem = await Item(f, "FOREIGN");
        var foreignSupplier = await Ops(f).CreateParty("Supplier", "SUP-B", "Foreign supplier", null, null, null, Ct);
        var foreignOrder = await Ops(f).CreateOrder(foreignSupplier.Id, foreignItem.Id, 1, 1, "foreign-order", Ct);
        f.ActAs();
        await Assert.ThrowsAsync<ArgumentException>(() => Ops(f).CreateOrder(foreignSupplier.Id, localItem.Id, 1, 1, "foreign-supplier", Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ops(f).ApproveOrder(foreignOrder.Id, Ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Ops(f).ReceiveOrder(foreignOrder.Id, 1, "foreign-receive", Ct));
        Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    [Fact]
    public async Task CustomerLinkedSaleAndReorderPolicyArePersisted()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var item = await Item(f); var ops = Ops(f);
        var customer = await ops.CreateParty("Customer", "CUSTOMER", "Customer One", "buyer@example.invalid", null, null, Ct);
        await ops.SetReorderPolicy(item.Id, 3, 12, Ct); var policy = await f.Db.StockReorderPolicies.SingleAsync(); Assert.Equal(12, policy.TargetQuantity);
        await Assert.ThrowsAsync<ArgumentException>(() => ops.SetReorderPolicy(item.Id, 3, 2, Ct));
        await ops.Receive(item.Id, 4, 1, "stock", Ct); await ops.Sell(item.Id, 1, 2, "sale", Ct, customer.Id);
        Assert.Equal("Customer One", (await Finance(f).Documents(Ct)).Single(x => x.Kind == "Sale").Party);
    }

    [Fact]
    public async Task TinyPartialReceiptCannotConsumeAllValueAndStrandRemainingQuantity()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var item = await Item(f); var ops = Ops(f);
        var supplier = await ops.CreateParty("Supplier", "TINY", "Small supplier", null, null, null, Ct);
        var order = await ops.CreateOrder(supplier.Id, item.Id, 1, .001m, "tiny-order", Ct); await ops.ApproveOrder(order.Id, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => ops.ReceiveOrder(order.Id, .5m, "tiny-partial", Ct));
        Assert.Equal(0, order.ReceivedQuantity); Assert.Equal(0, item.QuantityOnHand);
        await ops.ReceiveOrder(order.Id, 1, "full-tiny-receipt", Ct); Assert.Equal("Received", order.Status); Assert.Equal(.001m, order.ReceivedAmount);
    }
    [Fact]
    public async Task TinyPartialReturnCannotConsumeAllCreditAndStrandRemainingQuantity()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var item = await Item(f); var ops = Ops(f);
        await ops.Receive(item.Id, 1, 1, "p", Ct); await ops.Sell(item.Id, 1, .001m, "tiny-sale", Ct);
        var doc = await f.Db.FinanceDocuments.SingleAsync(x => x.Kind == "Sale");
        await Assert.ThrowsAsync<ArgumentException>(() => ops.Return(doc.Id, .5m, "tiny-return", Ct));
        Assert.Empty(await f.Db.StockDocumentReturns.ToListAsync());
        await ops.Return(doc.Id, 1, "full-return", Ct); Assert.Equal(1, item.QuantityOnHand);
        Assert.Equal(0, (await Finance(f).Documents(Ct)).Single(x => x.Id == doc.Id).Outstanding);
    }

    [Fact]
    public async Task PositiveAdjustmentUsesExactWeightedCostRatherThanRoundedDisplayCost()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var item = await Item(f);
        await Ops(f).Receive(item.Id, 1, 1, "p1", Ct); await Ops(f).Receive(item.Id, 2, 2, "p2", Ct);
        Assert.Equal(1.667m, item.UnitCost);
        await Farm(f).MoveStock(item.Id, 3, "found", Ct);
        Assert.Equal(6, item.QuantityOnHand); Assert.Equal(10, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
    }

}
