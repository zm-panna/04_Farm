using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class WorkspaceActionPagingTests
{
    private static OperationsWorkspaceController Controller(SecurityTestScope f) => new(
        f.Db, f.Tenant, f.Permissions, new(f.Db, f.Tenant, f.Permissions),
        new(f.Db, f.Tenant, f.Permissions), new(f.Db, f.Tenant, f.Permissions))
    {
        ControllerContext = new ControllerContext { HttpContext = f.Http },
        TempData = new TempDataDictionary(f.Http, f.Services.GetRequiredService<ITempDataProvider>())
    };

    private static OperationsWorkspaceModel Model(IActionResult result) => Assert.IsType<OperationsWorkspaceModel>(Assert.IsType<ViewResult>(result).Model);
    private static async Task<OperationsWorkspaceModel> Accounts(SecurityTestScope f, OperationsWorkspaceQuery? query = null) =>
        Model(await Controller(f).Accounts(null, null, null, default, query));

    [Fact]
    public async Task InventoryAndPartyFallbacksPageBeforeLoadingActionsAndScopeTheirCounts()
    {
        await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.View");await f.GrantAsync("Parties.View");
        for(var i=0;i<105;i++)
        {
            f.Db.StockItems.Add(new(){CompanyId=1,BranchId=11,SKU=$"S{i:000}",Name="Synthetic stock",UnitOfMeasure="KG"});
            f.Db.TradingParties.Add(new(){CompanyId=1,BranchId=11,Code=$"P{i:000}",Name=$"Party {i:000}",Kind="Supplier"});
        }
        f.Db.StockItems.Add(new(){CompanyId=1,BranchId=12,SKU="SECRET",Name="Synthetic stock",UnitOfMeasure="KG"});
        f.Db.TradingParties.Add(new(){CompanyId=2,BranchId=21,Code="SECRET",Name="Party secret",Kind="Supplier"});await f.Db.SaveChangesAsync();
        var first=Model(await Controller(f).Inventory(default));Assert.Equal(100,first.Stock.Count);Assert.Equal(105,first.StockPage.TotalCount);
        var second=Model(await Controller(f).Inventory(default,2));Assert.Equal(5,second.Stock.Count);Assert.Equal("S100",second.Stock[0].SKU);
        var searched=Model(await Controller(f).Inventory(default,1,"S104"));Assert.Single(searched.Stock);Assert.Equal(1,searched.StockPage.TotalCount);
        var parties=Model(await Controller(f).Parties(default,2));Assert.Equal(5,parties.Parties.Count);Assert.Equal(105,parties.PartiesPage.TotalCount);
        Assert.Single(Model(await Controller(f).Parties(default,1,"P104")).Parties);
    }

    private static async Task<(List<BankStatementLine> Banks, List<Voucher> Vouchers)> SeedBankRows(SecurityTestScope f)
    {
        var vouchers = Enumerable.Range(0, 201).Select(i => BusinessInput.Voucher(1, 11, $"CASH-{i:D4}", ("1000", 10m, 0m), ("3000", 0m, 10m))).ToList();
        var banks = Enumerable.Range(0, 101).Select(i => new BankStatementLine { CompanyId = 1, BranchId = 11, ExternalReference = $"BANK-{i:D4}", Amount = 10, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow) }).ToList();
        f.Db.Vouchers.AddRange(vouchers); f.Db.BankStatementLines.AddRange(banks);
        // Similar references in a different branch and company must not affect results or counts.
        foreach (var (company, branch) in new[] { (1, 12), (2, 21) })
        {
            f.Db.Vouchers.Add(BusinessInput.Voucher(company, branch, $"CASH-0000-PRIVATE-{branch}", ("1000", 10m, 0m), ("3000", 0m, 10m)));
            f.Db.BankStatementLines.Add(new BankStatementLine { CompanyId = company, BranchId = branch, ExternalReference = "BANK-0000", Amount = 10, ValueDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        }
        await f.Db.SaveChangesAsync();
        return (banks, vouchers);
    }

    [Fact]
    public async Task OlderBankLineAndVoucherAreReachableByPageAndSearchAndCanBeReconciled()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Accounts.View");
        var (banks, vouchers) = await SeedBankRows(f);
        var recent = await Accounts(f);
        Assert.Equal(100, recent.BankLines.Count); Assert.Equal(200, recent.CashVouchers.Count);
        Assert.Equal(101, recent.BankLinesPage.TotalCount); Assert.Equal(201, recent.CashVouchersPage.TotalCount);
        Assert.DoesNotContain(recent.BankLines, x => x.Id == banks[0].Id);
        Assert.DoesNotContain(recent.CashVouchers, x => x.Id == vouchers[0].Id);
        var older = await Accounts(f, new() { BankPage = 2, VoucherPage = 2 });
        Assert.Equal(banks[0].Id, Assert.Single(older.BankLines).Id);
        Assert.Equal(vouchers[0].Id, Assert.Single(older.CashVouchers).Id);
        Assert.Equal(2, older.BankLinesPage.PageNumber); Assert.False(older.BankLinesPage.HasNext);
        var searched = await Accounts(f, new() { BankSearch = "  BANK-0000  ", VoucherSearch = " CASH-0000 " });
        Assert.Equal(banks[0].Id, Assert.Single(searched.BankLines).Id);
        Assert.Equal(vouchers[0].Id, Assert.Single(searched.CashVouchers).Id);
        Assert.Equal(1, searched.BankLinesPage.TotalCount); Assert.Equal(1, searched.CashVouchersPage.TotalCount);
        Assert.Equal("BANK-0000", searched.BankLinesPage.Search);
        Assert.Equal(10, searched.CashVouchers[0].Lines.Where(x => x.AccountCode == "1000").Sum(x => x.Debit - x.Credit));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller(f).Reconcile(searched.BankLines[0].Id, searched.CashVouchers[0].Id, default));
        Assert.Null(banks[0].MatchedVoucherId);
        await f.GrantAsync("Accounts.Reconcile");
        await Controller(f).Reconcile(searched.BankLines[0].Id, searched.CashVouchers[0].Id, default);
        Assert.Equal(vouchers[0].Id, banks[0].MatchedVoucherId);
        var completed = await Accounts(f, new() { BankSearch = "BANK-0000", BankUnmatchedOnly = true, VoucherSearch = "CASH-0000" });
        Assert.Empty(completed.BankLines); Assert.Empty(completed.CashVouchers);
    }

    [Fact]
    public async Task UnmatchedFilterRunsBeforeBankPaginationAndCashSelectionExcludesIneligibleVouchers()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Accounts.View");
        var (banks, vouchers) = await SeedBankRows(f);
        for (var i = 1; i <= 100; i++) banks[i].MatchedVoucherId = vouchers[i].Id;
        var draft = new Voucher { CompanyId = 1, BranchId = 11, Reference = "NOT-POSTED", Posted = false };
        f.Db.Vouchers.AddRange(draft, BusinessInput.Voucher(1, 11, "NO-CASH", ("6100", 10m, 0m), ("3000", 0m, 10m)));
        await f.Db.SaveChangesAsync();
        Assert.All((await Accounts(f)).BankLines, x => Assert.NotNull(x.MatchedVoucherId));
        var model = await Accounts(f, new() { BankUnmatchedOnly = true });
        Assert.Equal(banks[0].Id, Assert.Single(model.BankLines).Id);
        Assert.Equal(1, model.BankLinesPage.TotalCount); Assert.Equal(101, model.CashVouchersPage.TotalCount);
        Assert.All(model.CashVouchers, x => Assert.True(x.Posted && x.Lines.Any(l => l.AccountCode == "1000")));
        Assert.DoesNotContain(model.CashVouchers, x => banks.Any(b => b.MatchedVoucherId == x.Id));
    }

    private static async Task<PurchaseOrder> SeedOrders(SecurityTestScope f, string oldestStatus)
    {
        var party = new TradingParty { CompanyId = 1, BranchId = 11, Code = "SUP", Name = "Supplier", Kind = "Supplier" };
        var item = new StockItem { CompanyId = 1, BranchId = 11, SKU = "ITEM", Name = "Item" };
        f.Db.AddRange(party, item); await f.Db.SaveChangesAsync();
        var orders = Enumerable.Range(0, 201).Select(i => new PurchaseOrder
        {
            CompanyId = 1, BranchId = 11, SupplierId = party.Id, StockItemId = item.Id,
            Reference = i == 0 ? "OLDEST-ORDER" : $"NEW-ORDER-{i:D4}", Quantity = 2, UnitPrice = 5, Amount = 10,
            Status = i == 0 ? oldestStatus : "Received", ReceivedQuantity = i == 0 ? (oldestStatus == "PartiallyReceived" ? 1 : 0) : 2,
            ReceivedAmount = i == 0 ? (oldestStatus == "PartiallyReceived" ? 5 : 0) : 10
        }).ToList();
        f.Db.PurchaseOrders.AddRange(orders); await f.Db.SaveChangesAsync();
        foreach (var (company, branch) in new[] { (1, 12), (2, 21) })
        {
            var otherParty = new TradingParty { CompanyId = company, BranchId = branch, Code = "SUP", Name = "Private supplier", Kind = "Supplier" };
            var otherItem = new StockItem { CompanyId = company, BranchId = branch, SKU = "ITEM", Name = "Private item" };
            f.Db.AddRange(otherParty, otherItem); await f.Db.SaveChangesAsync();
            f.Db.PurchaseOrders.Add(new PurchaseOrder { CompanyId = company, BranchId = branch, SupplierId = otherParty.Id, StockItemId = otherItem.Id, Reference = $"OLDEST-ORDER-PRIVATE-{branch}", Quantity = 2, UnitPrice = 5, Amount = 10 });
        }
        await f.Db.SaveChangesAsync();
        return orders[0];
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Approved")]
    [InlineData("PartiallyReceived")]
    public async Task OlderOpenOrdersKeepApprovalReceiptAndCancelAccess(string status)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Purchase.View");
        var oldest = await SeedOrders(f, status);
        var recent = Model(await Controller(f).Purchase(null, default));
        Assert.Equal(200, recent.Orders.Count); Assert.Equal(201, recent.OrdersPage.TotalCount);
        Assert.DoesNotContain(recent.Orders, x => x.Id == oldest.Id);
        var paged = Model(await Controller(f).Purchase(null, default, new() { OrderPage = 2 }));
        Assert.Equal(oldest.Id, Assert.Single(paged.Orders).Id);
        var filtered = Model(await Controller(f).Purchase(null, default, new() { OrderSearch = " OLDEST-ORDER ", OrdersOpenOnly = true }));
        Assert.Equal(oldest.Id, Assert.Single(filtered.Orders).Id); Assert.Equal(1, filtered.OrdersPage.TotalCount);
        var open = Model(await Controller(f).Purchase(null, default, new() { OrdersOpenOnly = true }));
        Assert.Equal(oldest.Id, Assert.Single(open.Orders).Id);
        if (status == "Draft")
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller(f).ApproveOrder(oldest.Id, default));
            await f.GrantAsync("Purchase.Approve"); await Controller(f).ApproveOrder(filtered.Orders[0].Id, default);
            Assert.Equal("Approved", oldest.Status);
        }
        else if (status == "Approved")
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller(f).ReceiveOrder(oldest.Id, 1, "OLD-RECEIPT", default));
            await f.GrantAsync("Purchase.Post"); await Controller(f).ReceiveOrder(filtered.Orders[0].Id, 1, "OLD-RECEIPT", default);
            Assert.Equal("PartiallyReceived", oldest.Status);
            var updated = Model(await Controller(f).Purchase(null, default, new() { OrderSearch = "OLDEST-ORDER" }));
            Assert.Equal(oldest.Id, Assert.Single(updated.OrderReceipts).PurchaseOrderId);
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller(f).CancelOrder(oldest.Id, default));
            await f.GrantAsync("Purchase.Approve"); await Controller(f).CancelOrder(filtered.Orders[0].Id, default);
            Assert.Equal("Cancelled", oldest.Status); Assert.Equal(1, oldest.ReceivedQuantity);
        }
    }

    [Theory]
    [InlineData("Purchase")]
    [InlineData("Sale")]
    public async Task OlderStockDocumentsRemainSelectableForReturnsWithCorrectBalances(string kind)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var farm = new FarmService(f.Db, f.Tenant, f.Permissions); var ops = new IntegratedOperations(f.Db, f.Tenant, f.Permissions);
        var item = await farm.AddStock("RETURN-ITEM", "Return item", 0, default, "KG");
        await ops.Receive(item.Id, 20, 1, kind == "Purchase" ? "OLD-RETURN" : "SOURCE-STOCK", default);
        if (kind == "Sale") await ops.Sell(item.Id, 10, 1, "OLD-RETURN", default);
        var oldest = await f.Db.FinanceDocuments.SingleAsync(x => x.Kind == kind);
        await ops.Return(oldest.Id, 1, "OLD-PRIOR-RETURN", default);
        await new FinanceService(f.Db, f.Tenant, f.Permissions).RecordDocumentPayment(oldest.Id, "OLD-PAYMENT", 2, default);
        f.Db.FinanceDocuments.AddRange(Enumerable.Range(1, 200).Select(i => new FinanceDocument { CompanyId = 1, BranchId = 11, Kind = kind, SourceId = 1000 + i, Reference = $"NEW-DOCUMENT-{i:D4}", Amount = 10, CostAmount = 10, VoucherId = oldest.VoucherId }));
        foreach (var (company, branch) in new[] { (1, 12), (2, 21) })
        {
            var voucher = BusinessInput.Voucher(company, branch, $"PRIVATE-DOCUMENT-{branch}", ("1000", 10m, 0m), ("3000", 0m, 10m));
            f.Db.Vouchers.Add(voucher); await f.Db.SaveChangesAsync();
            f.Db.FinanceDocuments.Add(new FinanceDocument { CompanyId = company, BranchId = branch, Kind = kind, SourceId = 5000 + branch, Reference = oldest.Reference, Amount = 10, CostAmount = 10, VoucherId = voucher.Id });
        }
        await f.Db.SaveChangesAsync();
        async Task<OperationsWorkspaceModel> Trade(OperationsWorkspaceQuery? query = null) => Model(kind == "Purchase" ? await Controller(f).Purchase(null, default, query) : await Controller(f).Sales(default, query));
        var recent = await Trade(); Assert.Equal(200, recent.Documents.Count); Assert.Equal(201, recent.DocumentsPage.TotalCount);
        Assert.DoesNotContain(recent.Documents, x => x.Id == oldest.Id); Assert.Empty(recent.Returns);
        var paged = await Trade(new() { DocumentPage = 2 }); Assert.Equal(oldest.Id, Assert.Single(paged.Documents).Id);
        var searched = await Trade(new() { DocumentSearch = " OLD-RETURN " });
        var selected = Assert.Single(searched.Documents); Assert.Equal(oldest.Id, selected.Id); Assert.Equal(1, searched.DocumentsPage.TotalCount);
        Assert.Equal(1, selected.Returned); Assert.Equal(2, selected.Paid); Assert.Equal(oldest.Amount - 3, selected.Outstanding);
        Assert.Equal(oldest.Id, Assert.Single(searched.Returns).FinanceDocumentId);
        if (kind == "Purchase") await Controller(f).PurchaseReturn(selected.Id, 1, "OLD-PAGED-RETURN", default);
        else await Controller(f).SalesReturn(selected.Id, 1, "OLD-PAGED-RETURN", default);
        Assert.Equal(2, await f.Db.StockDocumentReturns.CountAsync(x => x.FinanceDocumentId == oldest.Id));
    }

    [Fact]
    public async Task GeneratedUnassignedPartyLabelIsDistinguishedFromARealPartyWithThatName()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var farm = new FarmService(f.Db, f.Tenant, f.Permissions); var ops = new IntegratedOperations(f.Db, f.Tenant, f.Permissions);
        var item = await farm.AddStock("PARTY-ITEM", "Party test item", 0, default, "KG");
        var party = await ops.CreateParty("Supplier", "REAL", "Unassigned", null, null, null, default);
        await ops.Receive(item.Id, 1, 1, "NO-PARTY", default);
        await ops.Receive(item.Id, 1, 1, "REAL-PARTY", default, party.Id);
        var model = Model(await Controller(f).Purchase(null, default));
        var absent = Assert.Single(model.Documents, x => x.Reference == "PUR-NO-PARTY");
        var assigned = Assert.Single(model.Documents, x => x.Reference == "PUR-REAL-PARTY");
        Assert.Equal("Unassigned", absent.Party); Assert.False(absent.PartyAssigned);
        Assert.Equal("Unassigned", assigned.Party); Assert.True(assigned.PartyAssigned);
    }

    [Fact]
    public async Task PageBoundsAreClampedBeforeSkipAndEmptySearchResultsRemainSafe()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        await SeedBankRows(f); await SeedOrders(f, "Draft");
        var older = await Accounts(f, new() { BankPage = int.MaxValue, VoucherPage = int.MaxValue });
        Assert.Equal(2, older.BankLinesPage.PageNumber); Assert.Single(older.BankLines);
        Assert.Equal(2, older.CashVouchersPage.PageNumber); Assert.Single(older.CashVouchers);
        var recent = await Accounts(f, new() { BankPage = int.MinValue, VoucherPage = 0 });
        Assert.Equal(1, recent.BankLinesPage.PageNumber); Assert.Equal(100, recent.BankLines.Count);
        Assert.Equal(1, recent.CashVouchersPage.PageNumber); Assert.Equal(200, recent.CashVouchers.Count);
        var empty = await Accounts(f, new() { BankSearch = "not-found", BankPage = int.MaxValue, VoucherSearch = new string('x', 1000), VoucherPage = int.MinValue });
        Assert.Empty(empty.BankLines); Assert.Equal(1, empty.BankLinesPage.PageNumber); Assert.Equal(1, empty.BankLinesPage.TotalPages);
        Assert.Empty(empty.CashVouchers); Assert.Equal(80, empty.CashVouchersPage.Search.Length);
        var orders = Model(await Controller(f).Purchase(null, default, new() { OrderPage = int.MaxValue, DocumentPage = int.MaxValue }));
        Assert.Equal(2, orders.OrdersPage.PageNumber); Assert.Single(orders.Orders);
        Assert.Empty(orders.Documents); Assert.Equal(1, orders.DocumentsPage.PageNumber);
        orders = Model(await Controller(f).Purchase(null, default, new() { OrderPage = -1 }));
        Assert.Equal(1, orders.OrdersPage.PageNumber); Assert.Equal(200, orders.Orders.Count);
    }

    [Theory]
    [InlineData("Purchase")]
    [InlineData("Sale")]
    [InlineData("Accounts")]
    public async Task SearchAndPaginationNeverBypassWorkspaceReadPermission(string kind)
    {
        await using var f = await SecurityTestScope.CreateAsync(); var controller = Controller(f);
        var query = new OperationsWorkspaceQuery { OrderPage = 2, DocumentPage = 2, BankPage = 2, VoucherPage = 2, BankSearch = "private", DocumentSearch = "private" };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            if (kind == "Purchase") await controller.Purchase(null, default, query);
            else if (kind == "Sale") await controller.Sales(default, query);
            else await controller.Accounts(null, null, null, default, query);
        });
    }

    [Fact]
    public async Task QueryingAnUngrantedBranchDoesNotExposeCountsOrSelectors()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        await SeedBankRows(f); f.ActAs(branch: 12);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Accounts(f, new() { BankSearch = "BANK-0000", VoucherSearch = "CASH-0000" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Controller(f).Purchase(null, default, new() { OrdersOpenOnly = true }));
    }

    [Fact]
    public void PagerAndSearchFormsPreserveOtherSelectorAndFinancialReportState()
    {
        var model = new OperationsWorkspaceModel
        {
            Kind = "Accounts", From = new DateOnly(2026, 1, 1), Through = new DateOnly(2026, 2, 28), Account = "1000",
            BankUnmatchedOnly = true, BankLinesPage = new(2, 100, 301, "BANK & old"), CashVouchersPage = new(3, 200, 601, "VOUCHER + old")
        };
        var bank = new WorkspaceBrowseModel(model, WorkspaceBrowseKind.BankLines);
        var bankLink = QueryHelpers.ParseQuery(new Uri("https://example.invalid" + bank.PageLink(3)).Query);
        Assert.Equal("3", bankLink["BankPage"].ToString()); Assert.Equal("BANK & old", bankLink["BankSearch"].ToString());
        Assert.Equal("3", bankLink["VoucherPage"].ToString()); Assert.Equal("VOUCHER + old", bankLink["VoucherSearch"].ToString());
        Assert.Equal("2026-01-01", bankLink["from"].ToString()); Assert.Equal("2026-02-28", bankLink["through"].ToString()); Assert.Equal("1000", bankLink["account"].ToString());
        Assert.DoesNotContain("BankPage", bank.PreservedState.Keys); Assert.DoesNotContain("BankSearch", bank.PreservedState.Keys); Assert.DoesNotContain("BankUnmatchedOnly", bank.PreservedState.Keys);
        Assert.Equal("3", bank.PreservedState["VoucherPage"]); // Searching resets only its own page.
        var voucher = new WorkspaceBrowseModel(model, WorkspaceBrowseKind.CashVouchers);
        Assert.Equal("2", voucher.PreservedState["BankPage"]); Assert.Equal("True", voucher.PreservedState["BankUnmatchedOnly"]);
        var voucherLink = QueryHelpers.ParseQuery(new Uri("https://example.invalid" + voucher.PageLink(2)).Query);
        Assert.Equal("2", voucherLink["BankPage"].ToString()); Assert.Equal("2", voucherLink["VoucherPage"].ToString());
        var trade = new OperationsWorkspaceModel { Kind = "Purchase", SelectedStockItemId = 9, OrdersOpenOnly = true, OrdersPage = new(2, 200, 401, "old-order"), DocumentsPage = new(3, 200, 601, "old-document") };
        var documents = new WorkspaceBrowseModel(trade, WorkspaceBrowseKind.Documents);
        var link = QueryHelpers.ParseQuery(new Uri("https://example.invalid" + documents.PageLink(2)).Query);
        Assert.Equal("2", link["OrderPage"].ToString()); Assert.Equal("old-order", link["OrderSearch"].ToString()); Assert.Equal("True", link["OrdersOpenOnly"].ToString()); Assert.Equal("9", link["itemId"].ToString());
        Assert.Equal("2", link["DocumentPage"].ToString()); Assert.Equal("old-document", link["DocumentSearch"].ToString());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, true)]
    public async Task MissingOrMalformedPartyStatusCannotSilentlyDeactivate(bool? active, bool invalidModel)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Parties.Manage");
        var party = new TradingParty { CompanyId = 1, BranchId = 11, Code = "ACTIVE", Name = "Active supplier", Active = true };
        f.Db.TradingParties.Add(party); await f.Db.SaveChangesAsync();
        var controller = Controller(f); if (invalidModel) controller.ModelState.AddModelError("active", "The value 'garbage' is not valid.");
        await controller.PartyStatus(party.Id, active, default);
        Assert.True(party.Active); Assert.Empty(await f.Db.Audits.ToListAsync()); Assert.NotNull(controller.TempData["Error"]);
        var valid = Controller(f); await valid.PartyStatus(party.Id, false, default);
        Assert.False(party.Active); Assert.Equal("Party.Deactivate", Assert.Single(await f.Db.Audits.ToListAsync()).Action);
    }
}
