using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;
public sealed record FixedAssetList(List<FixedAsset> Assets, int Total, int Page, string? Query, string? Status, HashSet<string> Permissions)
{ public bool Can(string grant) => Permissions.Contains("*") || Permissions.Contains(grant); }
public sealed record FixedAssetDetail(FixedAsset Asset, List<FixedAssetCustody> Custody, List<FixedAssetDepreciationLine> Depreciation, List<FixedAssetDisposal> Disposals, List<Voucher> Vouchers, HashSet<string> Permissions)
{ public bool Can(string grant) => Permissions.Contains("*") || Permissions.Contains(grant); }
public sealed record FixedAssetRunList(List<FixedAssetDepreciationRun> Runs, int Total, int Page, string? Query, string? Status, HashSet<string> Permissions)
{ public bool Can(string grant) => Permissions.Contains("*") || Permissions.Contains(grant); }
public sealed record FixedAssetRunDetail(FixedAssetDepreciationRun Run, Voucher? Voucher, HashSet<string> Permissions)
{ public bool Can(string grant) => Permissions.Contains("*") || Permissions.Contains(grant); }
public sealed record FixedAssetDisposalDetail(FixedAssetDisposal Disposal, FixedAsset Asset, Voucher? Voucher, HashSet<string> Permissions)
{ public bool Can(string grant) => Permissions.Contains("*") || Permissions.Contains(grant); }
public sealed record FixedAssetDisposalEditor(FixedAssetDisposalInput Input, FixedAsset Asset);
public sealed record FixedAssetCustodyEditor(FixedAssetCustodyInput Input, FixedAsset Asset);
public sealed record FixedAssetWorkflowActions(string Path, byte[] Token, string Status, bool CanApprove, bool CanPost, bool CanCancel);
public sealed record FixedAssetActionError(string Message, string ReturnUrl);

[Authorize, Route("fixed-assets")]
public sealed class FixedAssetsController(AppDbContext db, TenantContext tenant, PermissionService permission, FixedAssetService service) : Controller
{
    private async Task<(int company, int branch, string user)> Scope(CancellationToken ct)
    { await permission.Demand("FixedAssets.View", ct); return await tenant.GetAsync(ct); }
    private Task<HashSet<string>> Grants((int company, int branch, string user) t, CancellationToken ct) =>
        db.UserPermissions.AsNoTracking().Where(x => x.CompanyId == t.company && x.UserId == t.user).Select(x => x.Permission).ToHashSetAsync(ct);
    private async Task<FixedAsset?> Asset(long id, CancellationToken ct)
    { var t = await Scope(ct); return await db.FixedAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); }
    private static bool Expected(Exception ex) => ex is ArgumentException or InvalidOperationException or DbUpdateException or KeyNotFoundException;
    private static string Error(Exception ex) => ex is DbUpdateConcurrencyException ? ex.Message : ex is DbUpdateException
        ? "A duplicate reference or concurrent change prevented saving. Open the current record and review it before retrying. No partial changes were saved." : ex.Message;
    private IActionResult Failed(Exception ex, string returnUrl)
    { if (ex is KeyNotFoundException) return NotFound(); var result = View("ActionError", new FixedAssetActionError(Error(ex), returnUrl)); result.StatusCode = ex is DbUpdateConcurrencyException ? 409 : 400; return result; }
    private async Task<IActionResult> Action(Func<Task> action, string returnUrl)
    {
        if (!ModelState.IsValid) return Failed(new ArgumentException("The submitted form is invalid. Reload the record and review your input."), returnUrl);
        try { await action(); TempData["Success"] = "Fixed asset workflow updated."; return LocalRedirect(returnUrl); }
        catch (Exception ex) when (Expected(ex)) { return Failed(ex, returnUrl); }
    }
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, string? status, int page = 1, CancellationToken ct = default)
    {
        var t = await Scope(ct); q = q?.Trim(); page = Math.Clamp(page, 1, 100000);
        var query = db.FixedAssets.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
        if (!string.IsNullOrEmpty(q)) { var term = q.ToUpperInvariant(); query = query.Where(x => x.AssetCode.ToUpper().Contains(term) || x.Name.ToUpper().Contains(term) || x.SerialNumber.ToUpper().Contains(term) || x.Custodian.ToUpper().Contains(term)); }
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct); page = Math.Min(page, Math.Max(1, (total + 24) / 25));
        return View(new FixedAssetList(await query.OrderBy(x => x.AssetCode).ThenBy(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct), total, page, q, status, await Grants(t, ct)));
    }
    [HttpGet("new")]
    public async Task<IActionResult> Create(CancellationToken ct) { await permission.Demand("FixedAssets.Create", ct); await Scope(ct); return View(new FixedAssetInput()); }
    [HttpPost("new"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FixedAssetInput input, CancellationToken ct)
    {
        await permission.Demand("FixedAssets.Create", ct);
        if (ModelState.IsValid) try { var a = await service.Create(input, ct); return RedirectToAction(nameof(Details), new { id = a.Id }); }
        catch (Exception ex) when (Expected(ex)) { ModelState.AddModelError("", Error(ex)); }
        return View(input);
    }
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id, CancellationToken ct)
    {
        var t = await Scope(ct); var a = await Asset(id, ct); if (a is null) return NotFound();
        var custody = await db.FixedAssetCustodies.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FixedAssetId == id).OrderByDescending(x => x.Id).ToListAsync(ct);
        var dep = await db.FixedAssetDepreciationLines.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FixedAssetId == id && x.Posted).OrderByDescending(x => x.Period).ToListAsync(ct);
        var disposals = await db.FixedAssetDisposals.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FixedAssetId == id).OrderByDescending(x => x.Id).ToListAsync(ct);
        var vouchers = await db.Vouchers.AsNoTracking().Include(x => x.Lines).Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Id == a.AcquisitionVoucherId).ToListAsync(ct);
        return View(new FixedAssetDetail(a, custody, dep, disposals, vouchers, await Grants(t, ct)));
    }
    [HttpPost("{id:long}/approve"), ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.ApproveAcquisition(input, ct); }, $"/fixed-assets/{id}"); }
    [HttpPost("{id:long}/post"), ValidateAntiForgeryToken]
    public Task<IActionResult> Post(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.PostAcquisition(input, ct); }, $"/fixed-assets/{id}"); }
    [HttpPost("{id:long}/cancel"), ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.CancelAcquisition(input, ct); }, $"/fixed-assets/{id}"); }
    [HttpGet("{id:long}/edit")]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        await permission.Demand("FixedAssets.Edit", ct); var a = await Asset(id, ct); if (a is null) return NotFound();
        return View(new FixedAssetMetadataInput { Id = id, Name = a.Name, Category = a.Category, SerialNumber = a.SerialNumber, Notes = a.Notes, RowVersion = a.RowVersion });
    }
    [HttpPost("{id:long}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, FixedAssetMetadataInput input, CancellationToken ct)
    {
        await permission.Demand("FixedAssets.Edit", ct); input.Id = id;
        if (ModelState.IsValid) try { await service.EditMetadata(input, ct); return RedirectToAction(nameof(Details), new { id }); }
        catch (Exception ex) when (Expected(ex)) { if (ex is KeyNotFoundException) return NotFound(); ModelState.AddModelError("", Error(ex)); if (ex is DbUpdateConcurrencyException) Response.StatusCode = 409; }
        return View(input); // Preserve the user's original row token; never silently replace a stale token.
    }
    [HttpGet("{id:long}/custody")]
    public async Task<IActionResult> Custody(long id, CancellationToken ct)
    {
        await permission.Demand("FixedAssets.Custody", ct); var a = await Asset(id, ct); if (a is null) return NotFound();
        return View(new FixedAssetCustodyEditor(new FixedAssetCustodyInput { Id = id, RowVersion = a.RowVersion, Custodian = a.Custodian, Location = a.Location }, a));
    }
    [HttpPost("{id:long}/custody"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Custody(long id, [Bind(Prefix = "Input")] FixedAssetCustodyInput input, CancellationToken ct)
    {
        await permission.Demand("FixedAssets.Custody", ct); input.Id = id;
        if (ModelState.IsValid) try { await service.ChangeCustody(input, ct); return RedirectToAction(nameof(Details), new { id }); }
        catch (Exception ex) when (Expected(ex)) { if (ex is KeyNotFoundException) return NotFound(); ModelState.AddModelError("", Error(ex)); if (ex is DbUpdateConcurrencyException) Response.StatusCode = 409; }
        var a = await Asset(id, ct); return a is null ? NotFound() : View(new FixedAssetCustodyEditor(input, a));
    }
    [HttpGet("depreciation")]
    public async Task<IActionResult> Runs(string? q, string? status, int page = 1, CancellationToken ct = default)
    {
        var t = await Scope(ct); q = q?.Trim(); page = Math.Clamp(page, 1, 100000);
        var query = db.FixedAssetDepreciationRuns.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
        if (!string.IsNullOrEmpty(q)) { var term = q.ToUpperInvariant(); query = query.Where(x => x.Reference.ToUpper().Contains(term)); }
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct); page = Math.Min(page, Math.Max(1, (total + 24) / 25));
        return View(new FixedAssetRunList(await query.OrderByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct), total, page, q, status, await Grants(t, ct)));
    }
    [HttpGet("depreciation/new")]
    public async Task<IActionResult> CreateRun(CancellationToken ct) { await permission.Demand("FixedAssets.Depreciate", ct); await Scope(ct); return View(new FixedAssetRunInput()); }
    [HttpPost("depreciation/new"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRun(FixedAssetRunInput input, CancellationToken ct)
    {
        await permission.Demand("FixedAssets.Depreciate", ct);
        if (ModelState.IsValid) try { var r = await service.CreateRun(input, ct); return RedirectToAction(nameof(Run), new { id = r.Id }); }
        catch (Exception ex) when (Expected(ex)) { ModelState.AddModelError("", Error(ex)); }
        return View(input);
    }
    [HttpGet("depreciation/{id:long}")]
    public async Task<IActionResult> Run(long id, CancellationToken ct)
    {
        var t = await Scope(ct); var r = await db.FixedAssetDepreciationRuns.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (r is null) return NotFound(); var v = await db.Vouchers.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Id == r.VoucherId, ct);
        return View(new FixedAssetRunDetail(r, v, await Grants(t, ct)));
    }
    [HttpPost("depreciation/{id:long}/approve"), ValidateAntiForgeryToken]
    public Task<IActionResult> ApproveRun(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.ApproveRun(input, ct); }, $"/fixed-assets/depreciation/{id}"); }
    [HttpPost("depreciation/{id:long}/post"), ValidateAntiForgeryToken]
    public Task<IActionResult> PostRun(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.PostRun(input, ct); }, $"/fixed-assets/depreciation/{id}"); }
    [HttpPost("depreciation/{id:long}/cancel"), ValidateAntiForgeryToken]
    public Task<IActionResult> CancelRun(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.CancelRun(input, ct); }, $"/fixed-assets/depreciation/{id}"); }
    [HttpGet("{id:long}/dispose")]
    public async Task<IActionResult> CreateDisposal(long id, CancellationToken ct)
    {
        await permission.Demand("FixedAssets.Dispose", ct); var a = await Asset(id, ct); if (a is null) return NotFound();
        return View(new FixedAssetDisposalEditor(new FixedAssetDisposalInput { FixedAssetId = id, AssetRowVersion = a.RowVersion }, a));
    }
    [HttpPost("{id:long}/dispose"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDisposal(long id, [Bind(Prefix = "Input")] FixedAssetDisposalInput input, CancellationToken ct)
    {
        await permission.Demand("FixedAssets.Dispose", ct); input.FixedAssetId = id;
        if (ModelState.IsValid) try { var d = await service.CreateDisposal(input, ct); return RedirectToAction(nameof(Disposal), new { id = d.Id }); }
        catch (Exception ex) when (Expected(ex)) { if (ex is KeyNotFoundException) return NotFound(); ModelState.AddModelError("", Error(ex)); if (ex is DbUpdateConcurrencyException) Response.StatusCode = 409; }
        var a = await Asset(id, ct); return a is null ? NotFound() : View(new FixedAssetDisposalEditor(input, a));
    }
    [HttpGet("disposals/{id:long}")]
    public async Task<IActionResult> Disposal(long id, CancellationToken ct)
    {
        var t = await Scope(ct); var d = await db.FixedAssetDisposals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (d is null) return NotFound(); var a = await Asset(d.FixedAssetId, ct); if (a is null) return NotFound();
        var v = await db.Vouchers.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Id == d.VoucherId, ct);
        return View(new FixedAssetDisposalDetail(d, a, v, await Grants(t, ct)));
    }
    [HttpPost("disposals/{id:long}/approve"), ValidateAntiForgeryToken]
    public Task<IActionResult> ApproveDisposal(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.ApproveDisposal(input, ct); }, $"/fixed-assets/disposals/{id}"); }
    [HttpPost("disposals/{id:long}/post"), ValidateAntiForgeryToken]
    public Task<IActionResult> PostDisposal(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.PostDisposal(input, ct); }, $"/fixed-assets/disposals/{id}"); }
    [HttpPost("disposals/{id:long}/cancel"), ValidateAntiForgeryToken]
    public Task<IActionResult> CancelDisposal(long id, FixedAssetActionInput input, CancellationToken ct) { input.Id = id; return Action(async () => { await service.CancelDisposal(input, ct); }, $"/fixed-assets/disposals/{id}"); }
}
