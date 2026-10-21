using System.ComponentModel.DataAnnotations;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;

public sealed class StockTransferInput
{
    [Range(1, int.MaxValue)] public int SourceItemId { get; set; }
    [Range(1, int.MaxValue)] public int DestinationBranchId { get; set; }
    [Range(1, int.MaxValue)] public int DestinationItemId { get; set; }
    [Range(typeof(decimal), "0.001", "999999999999999.999")] public decimal Quantity { get; set; }
    [Required, StringLength(80)] public string Reference { get; set; } = "";
    [StringLength(500)] public string? Note { get; set; }
}
public sealed record StockTransferList(List<StockTransfer> Transfers, int Page, int Total, string? Query, bool CanTransfer);
public sealed record StockTransferEditor(StockTransferInput Input, string SourceBranch, List<StockItem> Sources, List<Branch> Destinations, List<StockItem> DestinationItems);
public sealed record StockTransferDetail(StockTransfer Transfer, List<Voucher> Vouchers, int ActiveBranchId);

[Authorize, Route("stock-transfers")]
public sealed class StockTransfersController(AppDbContext db, TenantContext tenant, PermissionService permission, StockTransferService transfers) : Controller
{
    private async Task<(int company, int branch, string user, int[] branches)> Scope(CancellationToken ct)
    {
        await permission.Demand("Inventory.View", ct); var t = await tenant.GetAsync(ct);
        var branches = await db.UserBranchAccess.AsNoTracking().Where(x => x.UserId == t.user && x.CompanyId == t.company).Select(x => x.BranchId).ToArrayAsync(ct);
        return (t.company, t.branch, t.user, branches);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int page = 1, CancellationToken ct = default)
    {
        var t = await Scope(ct); page = Math.Clamp(page, 1, 100000);
        var query = db.StockTransfers.AsNoTracking().Where(x => x.CompanyId == t.company
            && (x.SourceBranchId == t.branch || x.DestinationBranchId == t.branch)
            && t.branches.Contains(x.SourceBranchId) && t.branches.Contains(x.DestinationBranchId));
        q = q?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(q)) query = query.Where(x => x.Reference.Contains(q));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        var canTransfer = await db.UserPermissions.AsNoTracking().AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == "Inventory.Transfer" || x.Permission == "*"), ct);
        return View(new StockTransferList(rows, page, total, q, canTransfer));
    }

    [HttpGet("new")]
    public async Task<IActionResult> Create(int sourceItemId = 0, int destinationBranchId = 0, CancellationToken ct = default)
    {
        await permission.Demand("Inventory.Transfer", ct);
        return View("Create", await Editor(new StockTransferInput { SourceItemId = sourceItemId, DestinationBranchId = destinationBranchId, Quantity = 1 }, ct));
    }

    private async Task<StockTransferEditor> Editor(StockTransferInput input, CancellationToken ct)
    {
        var t = await Scope(ct);
        var sources = await db.StockItems.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderBy(x => x.SKU).ToListAsync(ct);
        var destinations = await db.Branches.AsNoTracking().Where(x => x.CompanyId == t.company && x.Id != t.branch && t.branches.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync(ct);
        var source = sources.SingleOrDefault(x => x.Id == input.SourceItemId);
        var destinationItems = new List<StockItem>();
        if (source is not null && destinations.Any(x => x.Id == input.DestinationBranchId))
        {
            // Master creation normalizes SKU; comparison also handles pre-existing mixed-case stock.
            var items = await db.StockItems.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == input.DestinationBranchId).OrderBy(x => x.SKU).ToListAsync(ct);
            destinationItems = items.Where(x => string.Equals(source.SKU.Trim(), x.SKU.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }
        var branch = await db.Branches.AsNoTracking().Where(x => x.Id == t.branch && x.CompanyId == t.company).Select(x => x.Name).SingleAsync(ct);
        return new StockTransferEditor(input, branch, sources, destinations, destinationItems);
    }

    [HttpPost("post"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Post([Bind(Prefix = "Input")] StockTransferInput input, CancellationToken ct)
    {
        await permission.Demand("Inventory.Transfer", ct);
        if (ModelState.IsValid)
        {
            try
            {
                var transfer = await transfers.Post(input.SourceItemId, input.DestinationBranchId, input.DestinationItemId, input.Quantity, input.Reference, input.Note, ct);
                TempData["Success"] = "Stock transfer posted to both branches.";
                return RedirectToAction(nameof(Details), new { id = transfer.Id });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
            {
                ModelState.AddModelError("", ex is DbUpdateException
                    ? "The transfer could not be posted. Reload the balances and check whether this reference was already posted before retrying."
                    : ex.Message);
            }
        }
        return View("Create", await Editor(input, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var t = await Scope(ct);
        var transfer = await db.StockTransfers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company
            && (x.SourceBranchId == t.branch || x.DestinationBranchId == t.branch)
            && t.branches.Contains(x.SourceBranchId) && t.branches.Contains(x.DestinationBranchId), ct);
        if (transfer is null) return NotFound();
        var vouchers = await db.Vouchers.AsNoTracking().Include(x => x.Lines).Where(x => x.CompanyId == t.company
            && ((x.Id == transfer.SourceVoucherId && x.BranchId == transfer.SourceBranchId) || (x.Id == transfer.DestinationVoucherId && x.BranchId == transfer.DestinationBranchId))).OrderBy(x => x.Id).ToListAsync(ct);
        return View(new StockTransferDetail(transfer, vouchers, t.branch));
    }
}
