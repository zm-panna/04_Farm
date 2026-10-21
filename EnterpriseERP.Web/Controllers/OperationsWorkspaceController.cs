using System.ComponentModel.DataAnnotations;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;

public sealed class StockPostingInput
{
    public int? PartyId { get; set; }
    [Range(1, int.MaxValue)] public int ItemId { get; set; }
    [Range(typeof(decimal), "0.001", "999999999999999.999")] public decimal Quantity { get; set; }
    [Range(typeof(decimal), "0.001", "999999999999999.999")] public decimal Price { get; set; }
    [Required, StringLength(80)] public string Reference { get; set; } = "";
}
public sealed class OperationsWorkspaceModel
{
    public string Kind { get; set; } = "Purchase";
    public string? CurrencyCode { get; set; }
    public int? CurrencyMinorUnits { get; set; }
    public string MoneyStep => CurrencyMinorUnits switch { 0 => "1", 1 => "0.1", 2 => "0.01", _ => "0.001" };
    // Keep legacy fractional amounts visible rather than formatting them away.
    public string Money(decimal amount) => amount.ToString("N" + (CurrencyMinorUnits.HasValue && decimal.Round(amount,CurrencyMinorUnits.Value)==amount ? CurrencyMinorUnits.Value : 3));
    public List<TradingParty> Parties { get; set; } = new();
    public List<PurchaseOrder> Orders { get; set; } = new();
    public List<PurchaseOrderReceipt> OrderReceipts { get; set; } = new();
    public List<StockReorderPolicy> ReorderPolicies { get; set; } = new();
    public int? SelectedStockItemId { get; set; }
    public decimal SuggestedQuantity { get; set; } = 1;
    public List<StockItem> Stock { get; set; } = new();
    public WorkspacePage StockPage { get; set; } = new(PageSize:100);
    public WorkspacePage PartiesPage { get; set; } = new(PageSize:100);
    public Dictionary<int,decimal> OpenOrdered { get; set; } = new();
    public List<InventoryCostBalance> InventoryBalances { get; set; } = new();
    public List<Animal> Animals { get; set; } = new();
    public IReadOnlyList<DocumentBalance> Documents { get; set; } = Array.Empty<DocumentBalance>();
    public List<StockDocumentReturn> Returns { get; set; } = new();
    public List<FinancePayment> Payments { get; set; } = new();
    public List<BankStatementLine> BankLines { get; set; } = new();
    public List<Voucher> CashVouchers { get; set; } = new();
    public WorkspacePage OrdersPage { get; set; } = new();
    public WorkspacePage DocumentsPage { get; set; } = new();
    public WorkspacePage BankLinesPage { get; set; } = new(PageSize: 100);
    public WorkspacePage CashVouchersPage { get; set; } = new();
    public bool OrdersOpenOnly { get; set; }
    public bool BankUnmatchedOnly { get; set; }
    public IReadOnlyList<LedgerLine> Ledger { get; set; } = Array.Empty<LedgerLine>();
    public IReadOnlyList<TrialBalanceLine> TrialBalance { get; set; } = Array.Empty<TrialBalanceLine>();
    public FinancialStatement? Statement { get; set; }
    public decimal InventoryCarryingValue { get; set; }
    public decimal InventoryLedgerValue { get; set; }
    public int MissingInventoryValuations { get; set; }
    public bool CanSellAnimals { get; set; }
    public DateOnly Today { get; set; }
    public DateOnly From { get; set; }
    public DateOnly Through { get; set; }
    public string? Account { get; set; }
    public int LedgerCount { get; set; }
}
[Authorize]
public sealed class OperationsWorkspaceController(AppDbContext db, TenantContext tenant, PermissionService permissions, IntegratedOperations operations, FarmService farm, FinanceService finance, TimeProvider? clock = null) : Controller
{
    [HttpGet("/Inventory")][HttpGet("/Inventory/Home")]
    public async Task<IActionResult> Inventory(CancellationToken ct, int page = 1, string? search = null)
    {
        await permissions.Demand("Inventory.View",ct);var t=await tenant.GetAsync(ct);
        var term=WorkspacePage.NormalizeSearch(search);
        var query=db.StockItems.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);
        if(term.Length>0)query=query.Where(x=>x.SKU.Contains(term)||x.Name.Contains(term));
        var paging=await WorkspacePage.CreateAsync(query,page,100,term,ct);
        var stock=await query.OrderBy(x=>x.SKU).ThenBy(x=>x.Id).Skip(paging.Offset).Take(paging.PageSize).ToListAsync(ct);
        var ids=stock.Select(x=>x.Id).ToArray();
        var pending=await db.PurchaseOrders.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&ids.Contains(x.StockItemId)&&(x.Status=="Approved"||x.Status=="PartiallyReceived"))
            .GroupBy(x=>x.StockItemId).Select(g=>new{Id=g.Key,Quantity=g.Sum(x=>x.Quantity-x.ReceivedQuantity)}).ToListAsync(ct);
        return View(new OperationsWorkspaceModel
        {
            Kind="Inventory", Stock=stock, StockPage=paging, OpenOrdered=pending.ToDictionary(x=>x.Id,x=>x.Quantity),
            InventoryBalances=await db.InventoryCostBalances.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&ids.Contains(x.StockItemId)).ToListAsync(ct),
            ReorderPolicies=await db.StockReorderPolicies.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&ids.Contains(x.StockItemId)).ToListAsync(ct)
        });
    }
    private async Task Currency(OperationsWorkspaceModel model,int company,CancellationToken ct)
    {
        var denomination=await db.CompanyCurrencyLocks.AsNoTracking().SingleOrDefaultAsync(x=>x.CompanyId==company,ct);
        model.CurrencyCode=denomination?.CurrencyCode;model.CurrencyMinorUnits=denomination?.CurrencyMinorUnits;
    }
    [HttpPost("/Inventory/adjust"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int itemId,decimal delta,string reference,CancellationToken ct)=>await Execute("/Inventory",async()=>
    {
        if(!ModelState.IsValid)throw new ArgumentException("Enter a stock item, signed quantity and unique reference.");
        await farm.MoveStock(itemId,delta,reference,ct);
    });
    [HttpGet("/Purchase")]
    public async Task<IActionResult> Purchase(int? itemId, CancellationToken ct, [FromQuery] OperationsWorkspaceQuery? browse = null) => await TradeWorkspace("Purchase", ct, itemId, browse);
    [HttpGet("/Sales")]
    public async Task<IActionResult> Sales(CancellationToken ct, [FromQuery] OperationsWorkspaceQuery? browse = null) => await TradeWorkspace("Sale", ct, browse: browse);
    private async Task<IActionResult> TradeWorkspace(string kind, CancellationToken ct, int? itemId = null, OperationsWorkspaceQuery? browse = null)
    {
        await permissions.Demand(kind == "Purchase" ? "Purchase.View" : "Sales.View", ct); var t = await tenant.GetAsync(ct);
        browse ??= new();
        var model = new OperationsWorkspaceModel { Kind = kind, OrdersOpenOnly = browse.OrdersOpenOnly, Stock = await db.StockItems.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderBy(x => x.SKU).ToListAsync(ct) };
        var documentSearch = WorkspacePage.NormalizeSearch(browse.DocumentSearch);
        var documentQuery = db.FinanceDocuments.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && (x.Kind == kind || (kind == "Sale" && x.Kind == "AnimalSale")));
        if (documentSearch.Length != 0) documentQuery = documentQuery.Where(x => x.Reference.Contains(documentSearch));
        model.DocumentsPage = await WorkspacePage.CreateAsync(documentQuery, browse.DocumentPage, 200, documentSearch, ct);
        var docs = await documentQuery.OrderByDescending(x => x.Id).Skip(model.DocumentsPage.Offset).Take(model.DocumentsPage.PageSize).ToListAsync(ct);
        model.Parties = await db.TradingParties.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == (kind == "Purchase" ? "Supplier" : "Customer")).OrderBy(x => x.Name).ToListAsync(ct);
        if (kind == "Purchase")
        {
            var orderSearch = WorkspacePage.NormalizeSearch(browse.OrderSearch);
            var orderQuery = db.PurchaseOrders.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
            if (orderSearch.Length != 0) orderQuery = orderQuery.Where(x => x.Reference.Contains(orderSearch));
            if (browse.OrdersOpenOnly) orderQuery = orderQuery.Where(x => x.Status == "Draft" || x.Status == "Approved" || x.Status == "PartiallyReceived");
            model.OrdersPage = await WorkspacePage.CreateAsync(orderQuery, browse.OrderPage, 200, orderSearch, ct);
            model.Orders = await orderQuery.OrderByDescending(x => x.Id).Skip(model.OrdersPage.Offset).Take(model.OrdersPage.PageSize).ToListAsync(ct);
            var orderIds = model.Orders.Select(x => x.Id).ToArray();
            model.OrderReceipts = await db.PurchaseOrderReceipts.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && orderIds.Contains(x.PurchaseOrderId)).ToListAsync(ct);
            if (itemId.HasValue && model.Stock.Any(x => x.Id == itemId))
            {
                model.SelectedStockItemId = itemId;
                var policy = await db.StockReorderPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.StockItemId == itemId, ct);
                var pending = await db.PurchaseOrders.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.StockItemId == itemId && (x.Status == "Approved" || x.Status == "PartiallyReceived")).Select(x => new { x.Quantity, x.ReceivedQuantity }).ToListAsync(ct);
                model.SuggestedQuantity = Math.Max(.001m, (policy?.TargetQuantity ?? 10) - model.Stock.Single(x => x.Id == itemId).QuantityOnHand - pending.Sum(x => x.Quantity - x.ReceivedQuantity));
            }
        }
        var ids = docs.Select(x => x.Id).ToArray();
        model.Returns = await db.StockDocumentReturns.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && ids.Contains(x.FinanceDocumentId)).OrderByDescending(x => x.Id).ToListAsync(ct);
        var payments = await db.FinancePaymentAllocations.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && ids.Contains(x.FinanceDocumentId)).ToListAsync(ct);
        model.Documents = docs.Select(d => { var returned = model.Returns.Where(x => x.FinanceDocumentId == d.Id).Sum(x => x.Amount); var paid = payments.Where(x => x.FinanceDocumentId == d.Id).Sum(x => x.SignedAmount); var party = model.Parties.FirstOrDefault(p => p.Id == d.PartyId); return new DocumentBalance(d.Id, d.Kind, d.Reference, d.Amount, returned, paid, d.Amount - returned - paid, party?.Name ?? "Unassigned", party is not null); }).ToList();
        if (kind == "Sale")
        {
            model.CanSellAnimals = await db.UserPermissions.AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == "*" || x.Permission == "Livestock.Sell"), ct);
            if (model.CanSellAnimals) model.Animals = await db.Animals.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && !x.Sold).OrderBy(x => x.Tag).ToListAsync(ct);
        }
        await Currency(model,t.company,ct);
        return View("Trade", model);
    }
    [HttpPost("/Purchase/post"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchasePost(StockPostingInput input, CancellationToken ct) => await Execute("/Purchase", async () => { if (!ModelState.IsValid) throw new ArgumentException("Enter a stock item, positive quantity/price and a reference of at most 80 characters."); await operations.Receive(input.ItemId, input.Quantity, input.Price, input.Reference, ct, input.PartyId); });
    [HttpPost("/Sales/post"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SalesPost(StockPostingInput input, CancellationToken ct) => await Execute("/Sales", async () => { if (!ModelState.IsValid) throw new ArgumentException("Enter a stock item, positive quantity/price and a reference of at most 80 characters."); await operations.Sell(input.ItemId, input.Quantity, input.Price, input.Reference, ct, input.PartyId); });
    [HttpPost("/Sales/animal"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AnimalSale(int animalId, decimal amount, string reference, int? customerId, CancellationToken ct) => await Execute("/Sales", async () => { if (!ModelState.IsValid) throw new ArgumentException("Invalid sale values."); await farm.SellAnimal(animalId, amount, reference, ct, customerId); });
    [HttpPost("/Purchase/return"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseReturn(long documentId, decimal quantity, string reference, CancellationToken ct) => await Return(documentId, quantity, reference, "Purchase", ct);
    [HttpPost("/Sales/return"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SalesReturn(long documentId, decimal quantity, string reference, CancellationToken ct) => await Return(documentId, quantity, reference, "Sale", ct);
    private async Task<IActionResult> Return(long documentId, decimal quantity, string reference, string kind, CancellationToken ct) => await Execute(kind == "Purchase" ? "/Purchase" : "/Sales", async () =>
    {
        if (!ModelState.IsValid) throw new ArgumentException("Invalid return values.");
        var t = await tenant.GetAsync(ct);
        if (!await db.FinanceDocuments.AnyAsync(x => x.Id == documentId && x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == kind, ct)) throw new ArgumentException("Choose a matching document in this branch.");
        await operations.Return(documentId, quantity, reference, ct);
    });
    [HttpGet("/Parties")]
    public async Task<IActionResult> Parties(CancellationToken ct, int page=1, string? search=null)
    {
        await permissions.Demand("Parties.View",ct);var t=await tenant.GetAsync(ct);var term=WorkspacePage.NormalizeSearch(search);
        var query=db.TradingParties.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);
        if(term.Length>0)query=query.Where(x=>x.Code.Contains(term)||x.Name.Contains(term));
        var paging=await WorkspacePage.CreateAsync(query,page,100,term,ct);
        return View(new OperationsWorkspaceModel { Kind="Parties",PartiesPage=paging,Parties=await query.OrderBy(x=>x.Kind).ThenBy(x=>x.Name).ThenBy(x=>x.Id).Skip(paging.Offset).Take(paging.PageSize).ToListAsync(ct) });
    }
    [HttpPost("/Parties/create"),ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateParty(string kind,string code,string name,string? email,string? phone,string? address,CancellationToken ct)=>await Execute("/Parties",async()=>
    {
        if(!ModelState.IsValid)throw new ArgumentException("Enter valid party details.");
        await operations.CreateParty(kind,code,name,email,phone,address,ct);
    });
    [HttpPost("/Parties/status"),ValidateAntiForgeryToken]
    public async Task<IActionResult> PartyStatus(int partyId,bool? active,CancellationToken ct)=>await Execute("/Parties",async()=>
    {
        if (!ModelState.IsValid || !active.HasValue) throw new ArgumentException("Choose a valid active status.");
        await operations.SetPartyActive(partyId,active.Value,ct);
    });
    [HttpPost("/Purchase/order"),ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOrder(int supplierId,StockPostingInput input,CancellationToken ct)=>await Execute("/Purchase",async()=>
    {
        if(!ModelState.IsValid)throw new ArgumentException("Enter supplier, item, quantity, agreed price and order reference.");
        await operations.CreateOrder(supplierId,input.ItemId,input.Quantity,input.Price,input.Reference,ct);
    });
    [HttpPost("/Purchase/order/approve"),ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveOrder(long orderId,CancellationToken ct)=>await Execute("/Purchase",()=>operations.ApproveOrder(orderId,ct));
    [HttpPost("/Purchase/order/cancel"),ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(long orderId,CancellationToken ct)=>await Execute("/Purchase",()=>operations.CancelOrder(orderId,ct));
    [HttpPost("/Purchase/order/receive"),ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceiveOrder(long orderId,decimal quantity,string reference,CancellationToken ct)=>await Execute("/Purchase",async()=>
    {
        if(!ModelState.IsValid)throw new ArgumentException("Enter valid receipt quantity and reference.");
        await operations.ReceiveOrder(orderId,quantity,reference,ct);
    });
    [HttpPost("/Inventory/reorder-policy"),ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderPolicy(int itemId,decimal reorderPoint,decimal targetQuantity,CancellationToken ct)=>await Execute("/Inventory",async()=>
    {
        if(!ModelState.IsValid)throw new ArgumentException("Enter valid reorder values.");
        await operations.SetReorderPolicy(itemId,reorderPoint,targetQuantity,ct);
    });
    [HttpGet("/Accounts")]
    public async Task<IActionResult> Accounts(DateOnly? from, DateOnly? through, string? account, CancellationToken ct, [FromQuery] OperationsWorkspaceQuery? browse = null)
    {
        await permissions.Demand("Accounts.View", ct); var t = await tenant.GetAsync(ct);
        browse ??= new();
        var today = (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date;
        var start = from ?? new DateOnly(today.Year, today.Month, 1); var end = through ?? today;
        if (end < start || end == DateOnly.MaxValue) { TempData["Error"] = "Choose a valid date range."; return Redirect("/Accounts"); }
        var endExclusive = new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var lines = await finance.Ledger(account, new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), endExclusive, ct);
        var trial = await finance.TrialBalance(endExclusive, ct);
        var values = await db.InventoryCostBalances.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).Select(x => x.Value).ToListAsync(ct);
        // Stock is current, so its reconciliation is compared with current GL rather than the report's historical cutoff.
        var currentTrial = end == today ? trial : await finance.TrialBalance(CompanyBusinessDate.AccountingDate(today.AddDays(1)), ct);
        var inventory = currentTrial.SingleOrDefault(x => x.Account == "1300");
        var bankSearch = WorkspacePage.NormalizeSearch(browse.BankSearch);
        var bankQuery = db.BankStatementLines.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
        if (bankSearch.Length != 0) bankQuery = bankQuery.Where(x => x.ExternalReference.Contains(bankSearch));
        if (browse.BankUnmatchedOnly) bankQuery = bankQuery.Where(x => x.MatchedVoucherId == null);
        var bankPage = await WorkspacePage.CreateAsync(bankQuery, browse.BankPage, 100, bankSearch, ct);
        var voucherSearch = WorkspacePage.NormalizeSearch(browse.VoucherSearch);
        var voucherQuery = db.Vouchers.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Posted && x.Lines.Any(l => l.AccountCode == "1000") && !db.BankStatementLines.Any(b => b.CompanyId == t.company && b.BranchId == t.branch && b.MatchedVoucherId == x.Id));
        if (voucherSearch.Length != 0) voucherQuery = voucherQuery.Where(x => x.Reference.Contains(voucherSearch));
        var voucherPage = await WorkspacePage.CreateAsync(voucherQuery, browse.VoucherPage, 200, voucherSearch, ct);
        var model = new OperationsWorkspaceModel
        {
            Kind = "Accounts", Today = today, From = start, Through = end, Account = account, LedgerCount = lines.Count, Ledger = lines.TakeLast(500).ToList(), TrialBalance = trial,
            Statement = await finance.Statement(endExclusive, ct), Documents = await finance.Documents(ct), InventoryCarryingValue = values.Sum(), InventoryLedgerValue = inventory == null ? 0 : inventory.Debit - inventory.Credit,
            MissingInventoryValuations = await db.StockItems.CountAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.QuantityOnHand != 0 && !db.InventoryCostBalances.Any(v => v.StockItemId == x.Id && v.CompanyId == t.company && v.BranchId == t.branch), ct),
            Payments = await db.FinancePayments.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderByDescending(x => x.Id).Take(100).ToListAsync(ct),
            BankLinesPage = bankPage, CashVouchersPage = voucherPage, BankUnmatchedOnly = browse.BankUnmatchedOnly,
            BankLines = await bankQuery.OrderByDescending(x => x.Id).Skip(bankPage.Offset).Take(bankPage.PageSize).ToListAsync(ct),
            CashVouchers = await voucherQuery.Include(x => x.Lines).OrderByDescending(x => x.Id).Skip(voucherPage.Offset).Take(voucherPage.PageSize).ToListAsync(ct)
        };
        await Currency(model,t.company,ct);
        return View(model);
    }
    [HttpPost("/Accounts/payment"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(long documentId, decimal amount, string reference, CancellationToken ct) => await Execute("/Accounts", async () => { if (!ModelState.IsValid) throw new ArgumentException("Invalid payment values."); await finance.RecordDocumentPayment(documentId, reference, amount, ct); });
    [HttpPost("/Accounts/bank-line"), ValidateAntiForgeryToken]
    public async Task<IActionResult> BankLine(decimal amount, string reference, DateOnly date, CancellationToken ct) => await Execute("/Accounts", async () => { if (!ModelState.IsValid) throw new ArgumentException("Invalid bank statement values."); await finance.RecordBankLine(reference, amount, date, ct); });
    [HttpPost("/Accounts/reconcile"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reconcile(long statementLineId, long voucherId, CancellationToken ct) => await Execute("/Accounts", async () => { if (!ModelState.IsValid) throw new ArgumentException("Select the bank line and voucher."); await finance.Reconcile(statementLineId, voucherId, ct); });
    [HttpPost("/Accounts/journal"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Journal(string reference, string debitAccount, string creditAccount, decimal amount, CancellationToken ct) => await Execute("/Accounts", async () =>
    {
        if (!ModelState.IsValid) throw new ArgumentException("Invalid journal values.");
        if (debitAccount == creditAccount) throw new ArgumentException("Debit and credit accounts must differ.");
        await farm.PostVoucher(reference, new List<VoucherLine> { new() { AccountCode = debitAccount, Debit = amount }, new() { AccountCode = creditAccount, Credit = amount } }, ct);
    });
    private async Task<IActionResult> Execute(string destination, Func<Task> action)
    {
        try { await action(); TempData["Success"] = "Saved successfully."; }
        catch (ArgumentException ex) { TempData["Error"] = ex.Message; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        catch (OverflowException) { TempData["Error"] = "The calculated value exceeds the supported range."; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Another user changed this record. Reload and retry."; }
        catch (DbUpdateException) { TempData["Error"] = "Nothing was posted. Check duplicate references and reload before retrying."; }
        return Redirect(destination);
    }
}
