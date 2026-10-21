using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;

public sealed record BusinessPolicyPage(List<CompanyBusinessPolicy> Policies, List<TaxPolicy> Taxes, CompanyCurrencyLock? Currency,
    string Search, string Status, int Page, int Total, int TaxPage, int TaxTotal, bool CanManage, bool CanApprove, bool CanManageTax, bool CanApproveTax);
[Authorize, Route("business-policy")]
public sealed class BusinessPolicyController(AppDbContext db, TenantContext tenant, PermissionService permission,
    CompanyBusinessPolicyService policies, TaxPolicyService taxes) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? status, int page = 1, int taxPage = 1, CancellationToken ct = default)
    {
        await permission.Demand("BusinessPolicy.View", ct); var t = await tenant.GetAsync(ct);
        search = (search ?? "").Trim(); if (search.Length > 100) search = search[..100];
        status = status is "Draft" or "Approved" or "Rejected" ? status : "";
        var p = db.CompanyBusinessPolicies.AsNoTracking().Where(x => x.CompanyId == t.company);
        var q = db.TaxPolicies.AsNoTracking().Where(x => x.CompanyId == t.company);
        if (status != "") { p = p.Where(x => x.Status == status); q = q.Where(x => x.Status == status); }
        if (search != "") { var term=search.ToLowerInvariant(); p = p.Where(x => x.CountryCode.ToLower().Contains(term) || x.RegionCode.ToLower().Contains(term) || x.CurrencyCode.ToLower().Contains(term) || x.ReviewBasis.ToLower().Contains(term)); q = q.Where(x => x.Code.ToLower().Contains(term) || x.Name.ToLower().Contains(term) || x.Reason.ToLower().Contains(term)); }
        var total = await p.CountAsync(ct); var taxTotal = await q.CountAsync(ct);
        page = Math.Clamp(page, 1, Math.Max(1, (total + 19) / 20)); taxPage = Math.Clamp(taxPage, 1, Math.Max(1, (taxTotal + 19) / 20));
        var grants = await db.UserPermissions.AsNoTracking().Where(x => x.CompanyId == t.company && x.UserId == t.user).Select(x => x.Permission).ToListAsync(ct);
        bool Can(string name) => grants.Contains("*") || grants.Contains(name);
        return View(new BusinessPolicyPage(await p.OrderByDescending(x => x.Version).Skip((page - 1) * 20).Take(20).ToListAsync(ct),
            await q.OrderBy(x => x.Code).ThenByDescending(x => x.Version).Skip((taxPage - 1) * 20).Take(20).ToListAsync(ct),
            await db.CompanyCurrencyLocks.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company, ct), search, status, page, total, taxPage, taxTotal,
            Can("BusinessPolicy.Manage"), Can("BusinessPolicy.Approve"), Can("TaxPolicy.Manage"), Can("TaxPolicy.Approve")));
    }
    [HttpPost("propose")]
    public async Task<IActionResult> Propose(BusinessPolicyDraft input, CancellationToken ct) => await Run(async () => { if (!Request.Form.ContainsKey("CurrencyMinorUnits")) throw new ArgumentException("Select currency decimal places explicitly."); await policies.Propose(input, ct); }, "Company policy proposal saved for independent review.");
    [HttpPost("review")]
    public async Task<IActionResult> Review(long id, string versionToken, bool approve, CancellationToken ct) => await Run(() => policies.Review(id, versionToken, approve, ct), "Company policy review recorded.");
    [HttpPost("tax/propose")]
    public async Task<IActionResult> ProposeTax(TaxPolicyDraft input, CancellationToken ct) => await Run(async () => { if (!Request.Form.ContainsKey("RatePercent")) throw new ArgumentException("Enter the tax rate explicitly, including zero for a reviewed exemption."); await taxes.Propose(input, ct); }, "Tax policy proposal saved for independent review.");
    [HttpPost("tax/review")]
    public async Task<IActionResult> ReviewTax(long id, string versionToken, bool approve, CancellationToken ct) => await Run(() => taxes.Review(id, versionToken, approve, ct), "Tax policy review recorded.");
    private async Task<IActionResult> Run(Func<Task> action, string success)
    {
        if (!ModelState.IsValid) { TempData["Error"] = "Correct the submitted dates, numbers and required fields."; return RedirectToAction(nameof(Index)); }
        try { await action(); TempData["Success"] = success; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or DbUpdateException) { TempData["Error"] = e is DbUpdateException ? "The record changed or conflicts with an existing policy. Refresh and review before retrying." : e.Message; }
        return RedirectToAction(nameof(Index));
    }
}
