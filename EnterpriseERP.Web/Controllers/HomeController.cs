using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;
public sealed record DashboardPage(FarmSummary? Summary, int? HealthDue, int? OpenTickets, bool CanManageStaff = false);
[Authorize]
public sealed class HomeController(AppDbContext db, TenantContext tenant, ReportService reports) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!User.HasClaim(c => c.Type == "ActiveCompanyId") || !User.HasClaim(c => c.Type == "ActiveBranchId")) return Redirect("/Context");
        var t = await tenant.GetAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var grants = await db.UserPermissions.AsNoTracking().Where(x => x.CompanyId == t.company && x.UserId == t.user).Select(x => x.Permission).ToListAsync(ct);
        bool Can(string operation) => grants.Contains("*") || grants.Contains(operation);
        int? healthDue = Can("AnimalHealth.View") ? await db.AnimalHealthRecords.CountAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.NextDueDate != null && x.FollowUpCompletedDate == null && x.NextDueDate <= today && db.Animals.Any(a => a.Id == x.AnimalId && a.CompanyId == x.CompanyId && a.BranchId == x.BranchId && !a.Sold), ct) : null;
        int? tickets = Can("CustomerCare.View") ? await db.SupportTickets.CountAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Status != "Closed" && x.Status != "Resolved", ct) : null;
        return View(new DashboardPage(Can("Reports.View") ? await reports.Summary(ct) : null, healthDue, tickets, Can("Permissions.Manage")));
    }
    [AllowAnonymous] public IActionResult Error() => Problem("An unexpected error occurred. Contact your administrator with the request time.");
}
