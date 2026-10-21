using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
public sealed record AutomationPage(List<AutomationRule> Rules, List<ScheduledJob> Jobs, List<InAppNotification> Notifications, List<AutomationApproval> Approvals, bool WorkerEnabled)
{
    public WorkspacePage ApprovalsPage { get; init; } = new(PageSize: 50);
    public string ApprovalStatus { get; init; } = "All";
}
[Authorize, Route("Automation")]
public sealed class AutomationWorkspaceController(AppDbContext db, TenantContext tenant, PermissionService permission, AutomationApprovalService approvals, OllamaChatService chat, IConfiguration configuration) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct, int approvalPage = 1, string? approvalSearch = null, string? approvalStatus = null)
    {
        await permission.Demand("Automation.Review", ct); var t = await tenant.GetAsync(ct);
        var search = WorkspacePage.NormalizeSearch(approvalSearch);
        var status = approvalStatus?.Trim().ToLowerInvariant() switch
        { "pending" => "Pending", "approved" => "Approved", _ => "All" };
        var approvalQuery = db.AutomationApprovals.AsNoTracking().Where(x => x.CompanyId == t.company);
        if (search.Length > 0)
        {
            var isId = long.TryParse(search, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var searchId);
            approvalQuery = approvalQuery.Where(x => x.RequesterId.Contains(search) || (isId && (x.Id == searchId || x.RuleId == searchId)));
        }
        if (status != "All") approvalQuery = approvalQuery.Where(x => x.Status == status);
        var page = await WorkspacePage.CreateAsync(approvalQuery, approvalPage, 50, search, ct);
        return View(new AutomationPage(
            await db.AutomationRules.AsNoTracking().Where(x => x.CompanyId == t.company).OrderBy(x => x.Name).ToListAsync(ct),
            await db.ScheduledJobs.AsNoTracking().Where(x => x.CompanyId == t.company).OrderByDescending(x => x.Id).Take(50).ToListAsync(ct),
            await db.Notifications.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderByDescending(x => x.Id).Take(50).ToListAsync(ct),
            await approvalQuery.OrderByDescending(x => x.Id).Skip(page.Offset).Take(page.PageSize).ToListAsync(ct),
            configuration.GetValue<bool>("Automation:WorkerEnabled")) { ApprovalsPage = page, ApprovalStatus = status });
    }
    [HttpPost("rule")]
    public async Task<IActionResult> CreateRule(string name, string trigger, CancellationToken ct)
    {
        await permission.Demand("Automation.Manage", ct); var t = await tenant.GetAsync(ct);
        name = BusinessInput.Text(name, "Rule name", 100);
        if (trigger is not ("Manual" or "LowStock" or "HealthDue")) throw new ArgumentException("Unsupported trigger");
        db.AutomationRules.Add(new AutomationRule { CompanyId = t.company, Name = name, Trigger = trigger, Action = "InAppAlert", Active = true });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Automation.RuleCreated", Reference = name });
        await db.SaveChangesAsync(ct); return RedirectToAction(nameof(Index));
    }
    [HttpPost("toggle")]
    public async Task<IActionResult> Toggle(long ruleId, bool? active, CancellationToken ct)
    {
        await permission.Demand("Automation.Manage", ct); var t = await tenant.GetAsync(ct);
        if (!ModelState.IsValid || !active.HasValue) throw new ArgumentException("Select an explicit active or inactive state.");
        var rule = await db.AutomationRules.SingleOrDefaultAsync(x => x.CompanyId == t.company && x.Id == ruleId, ct) ?? throw new KeyNotFoundException();
        rule.Active = active.Value; db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = active.Value ? "Automation.Enabled" : "Automation.Disabled", Reference = ruleId.ToString() });
        await db.SaveChangesAsync(ct); return RedirectToAction(nameof(Index));
    }
    [HttpPost("schedule")]
    public async Task<IActionResult> Schedule(long ruleId, DateTimeOffset dueUtc, string executionKey, CancellationToken ct)
    {
        await permission.Demand("Automation.Schedule", ct); var t = await tenant.GetAsync(ct);
        executionKey = BusinessInput.Reference(executionKey);
        if (dueUtc < DateTimeOffset.UtcNow.AddMinutes(-5) || dueUtc > DateTimeOffset.UtcNow.AddYears(1)) throw new ArgumentException("Schedule must be within the next year.");
        if (!await db.AutomationRules.AnyAsync(x => x.CompanyId == t.company && x.Id == ruleId && x.Active, ct)) throw new ArgumentException("Active rule required.");
        if (await db.ScheduledJobs.AnyAsync(x => x.CompanyId == t.company && x.ExecutionKey == executionKey, ct)) throw new ArgumentException("Execution key already exists.");
        db.ScheduledJobs.Add(new ScheduledJob { CompanyId = t.company, RuleId = ruleId, ExecutionKey = executionKey, DueUtc = dueUtc.ToUniversalTime() });
        await db.SaveChangesAsync(ct); TempData["Success"] = "One-time in-app notification scheduled. The worker must be enabled to process it."; return RedirectToAction(nameof(Index));
    }
    [HttpPost("proposal")]
    public async Task<IActionResult> Proposal(long ruleId, string payload, CancellationToken ct)
    { await approvals.Request(ruleId, payload, ct); TempData["Success"] = "Proposal saved for independent review. Approval does not execute a business transaction."; return RedirectToAction(nameof(Index)); }
    [HttpPost("approve")]
    public async Task<IActionResult> Approve(long approvalId, string payload, CancellationToken ct)
    { await approvals.Approve(approvalId, payload, ct); TempData["Success"] = "Proposal approved and audited. No business transaction was executed."; return RedirectToAction(nameof(Index)); }
    [HttpPost("ask")]
    public async Task<IActionResult> Ask(string question, CancellationToken ct)
    {
        try { ViewBag.Answer = await chat.Ask(question, ct); }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            Response.StatusCode = 503;
            ViewBag.Answer = !configuration.GetValue<bool>("Ollama:Enabled")
                ? "AI chat is disabled. Your farm records and verified reports remain available. Ask an administrator to configure the approved provider."
                : "The AI provider is unavailable or returned an invalid response. Your farm records and verified reports remain available; retry later.";
        }
        return View("Answer");
    }
}
