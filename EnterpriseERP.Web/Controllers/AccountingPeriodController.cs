using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
[Authorize,Route("Accounts/periods")]
public sealed class AccountingPeriodController(AppDbContext db, TenantContext tenant, PermissionService permission, AccountingPeriodService service, TimeProvider? clock = null) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await permission.Demand("Accounts.View", ct); var t = await tenant.GetAsync(ct);
        ViewData["BusinessToday"] = (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date;
        return View(await db.AccountingPeriodCloses.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch, ct));
    }
    [HttpPost("close")]
    public async Task<IActionResult> Close(DateOnly through, string reason, CancellationToken ct)
    {
        await service.Close(through, reason, ct); TempData["Success"] = "Period closed. New postings on or before this date are blocked."; return RedirectToAction(nameof(Index));
    }
}
