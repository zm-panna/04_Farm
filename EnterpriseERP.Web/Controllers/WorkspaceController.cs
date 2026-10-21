using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;

public sealed record FeedPage(List<Animal> Animals, List<StockItem> Stock, List<AnimalFeedRecord> Records)
{
    public DateOnly Today { get; init; }
}
public sealed record ReportPage(FarmSummary Summary, IReadOnlyList<TrialBalanceLine> TrialBalance, FinancialStatement? Statement)
{
    public DateTimeOffset ThroughExclusive { get; init; }
}
public sealed record BillingPage(SubscriptionState State, List<SubscriptionInvoice> Invoices, List<SubscriptionPayment> Payments)
{
    public WorkspacePage InvoicesPage { get; init; } = new(PageSize: 100);
    public string InvoiceStatus { get; init; } = "All";
    public string CurrencyCode { get; init; } = "";
    public int? CurrencyMinorUnits { get; init; }
    public string MoneyStep => CurrencyMinorUnits switch { 0 => "1", 1 => "0.1", 2 => "0.01", _ => "0.001" };
    public string Money(decimal amount) => amount.ToString("N" + (decimal.Round(amount, CurrencyMinorUnits ?? 3) == amount ? CurrencyMinorUnits ?? 3 : 3));
}

[Authorize]
public sealed class WorkspaceController(AppDbContext db, TenantContext tenant, PermissionService permissions,
    FarmHealthFeedService care, ReportService reports, FinanceService finance, SubscriptionService subscriptions, TimeProvider? clock = null) : Controller
{
    [HttpGet("/FeedManagement")]
    public async Task<IActionResult> Feed(CancellationToken ct)
    {
        await permissions.Demand("FeedManagement.View", ct); var t = await tenant.GetAsync(ct);
        return View(new FeedPage(
            await db.Animals.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && !x.Sold).OrderBy(x => x.Tag).ToListAsync(ct),
            await db.StockItems.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderBy(x => x.SKU).ToListAsync(ct),
            await db.AnimalFeedRecords.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderByDescending(x => x.Id).Take(100).ToListAsync(ct))
            { Today = (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date });
    }
    [HttpPost("/FeedManagement/consume")]
    public async Task<IActionResult> ConsumeFeed(int animalId, int stockItemId, decimal quantity, string reference, DateOnly feedDate, CancellationToken ct)
    {
        try { await care.ConsumeFeed(animalId, stockItemId, quantity, reference, feedDate, ct); TempData["Success"] = "Feed consumed; stock and expense journal posted."; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        { TempData["Error"] = ex is DbUpdateException ? "Posting conflict. Check the reference and current stock, then retry." : ex.Message; }
        return RedirectToAction(nameof(Feed));
    }
    [HttpGet("/Reports")]
    public async Task<IActionResult> Reports(CancellationToken ct)
    {
        var summary = await reports.Summary(ct);
        var t = await tenant.GetAsync(ct);
        var today = (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date;
        var through = CompanyBusinessDate.AccountingDate(today.AddDays(1));
        var canAccount = await db.UserPermissions.AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == "Accounts.View" || x.Permission == "*"), ct);
        return View(new ReportPage(summary, canAccount ? await finance.TrialBalance(through, ct) : Array.Empty<TrialBalanceLine>(), canAccount ? await finance.Statement(through, ct) : null) { ThroughExclusive = through });
    }
    [HttpGet("/Subscription")]
    public async Task<IActionResult> Subscription(CancellationToken ct, int invoicePage = 1, string? invoiceSearch = null, string? invoiceStatus = null)
    {
        var t = await tenant.GetAsync(ct);
        var search = WorkspacePage.NormalizeSearch(invoiceSearch);
        var status = invoiceStatus?.Trim().ToLowerInvariant() switch
        { "open" => "Open", "paid" => "Paid", "cancelled" => "Cancelled", _ => "All" };
        var invoices = db.SubscriptionInvoices.AsNoTracking().Where(x => x.CompanyId == t.company);
        if (search.Length > 0) invoices = invoices.Where(x => x.Reference.Contains(search));
        invoices = status switch
        {
            "Open" => invoices.Where(x => !x.Cancelled && x.Amount > x.Paid),
            "Paid" => invoices.Where(x => !x.Cancelled && x.Paid >= x.Amount),
            "Cancelled" => invoices.Where(x => x.Cancelled),
            _ => invoices
        };
        var page = await WorkspacePage.CreateAsync(invoices, invoicePage, 100, search, ct);
        var currency = await db.CompanyCurrencyLocks.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company, ct);
        return View(new BillingPage(await subscriptions.State(DateOnly.FromDateTime(DateTime.UtcNow), ct),
            await invoices.OrderByDescending(x => x.DueDate).ThenByDescending(x => x.Id).Skip(page.Offset).Take(page.PageSize).ToListAsync(ct),
            await db.SubscriptionPayments.AsNoTracking().Where(x => x.CompanyId == t.company).OrderByDescending(x => x.Id).Take(100).ToListAsync(ct))
            { InvoicesPage = page, InvoiceStatus = status, CurrencyCode = currency?.CurrencyCode ?? "", CurrencyMinorUnits = currency?.CurrencyMinorUnits });
    }
    [HttpPost("/Subscription/payment")]
    public async Task<IActionResult> SubscriptionPayment(long invoiceId, string reference, decimal amount, CancellationToken ct)
    {
        try { await subscriptions.RecordPayment(invoiceId, reference, amount, ct); TempData["Success"] = "Previously received payment recorded. No payment provider was charged."; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
        { TempData["Error"] = ex is DbUpdateException ? "Payment conflict. Check whether this reference was already recorded." : ex.Message; }
        return RedirectToAction(nameof(Subscription));
    }
    [HttpGet("/Livestock")][HttpGet("/Livestock/Home")]
    public IActionResult Livestock() => Redirect("/animals");
    [HttpGet("/Dairy")][HttpGet("/Dairy/Home")]
    public IActionResult Dairy() => Redirect("/milk-entry");
    [HttpGet("/AnimalHealth")][HttpGet("/AnimalHealth/Home")]
    public IActionResult Health() => Redirect("/animal-health");
    [HttpGet("/CustomerCare")][HttpGet("/CustomerCare/Home")]
    public IActionResult Support() => Redirect("/support/tickets");
}
