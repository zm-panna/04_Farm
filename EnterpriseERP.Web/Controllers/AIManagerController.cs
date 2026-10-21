using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
public sealed record EnterpriseAiPage(IReadOnlyList<string> AllowedModules, EnterpriseAiDraft? Draft);
[Authorize,Route("AIManager")]
public sealed class AIManagerController(AppDbContext db,TenantContext tenant,PermissionService permission,EnterpriseAiService ai):Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    { await permission.Demand("AI.Read",ct); return View(new EnterpriseAiPage(await Allowed(ct),null)); }
    [HttpPost("draft")]
    public async Task<IActionResult> Draft(string module,CancellationToken ct)
    { var draft=await ai.Draft(module,ct); return View("Index",new EnterpriseAiPage(await Allowed(ct),draft)); }
    [HttpGet("facts/{module}")]
    public async Task<IActionResult> Facts(string module,CancellationToken ct)=>Ok(await ai.Facts(module,ct));
    private async Task<IReadOnlyList<string>> Allowed(CancellationToken ct)
    { var t=await tenant.GetAsync(ct); var grants=await db.UserPermissions.Where(x=>x.CompanyId==t.company&&x.UserId==t.user).Select(x=>x.Permission).ToListAsync(ct); return EnterpriseAiService.Modules.Where(x=>grants.Contains("*")||grants.Contains(x.Value.Permission)).Select(x=>x.Key).ToList(); }
}
