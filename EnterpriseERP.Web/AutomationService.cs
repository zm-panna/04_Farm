using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public record AutomationPreview(long RuleId,string Name,string Trigger,string Action,string Status);
public sealed class AutomationService(AppDbContext db,TenantContext context,PermissionService permission)
{
 public async Task<AutomationPreview> DryRun(long id,CancellationToken ct)
 {
  await permission.Demand("Automation.Review",ct);var t=await context.GetAsync(ct);
  var rule=await db.AutomationRules.AsNoTracking().SingleAsync(x=>x.CompanyId==t.company&&x.Id==id,ct);
  return new(rule.Id,rule.Name,rule.Trigger,rule.Action,"Preview only; no side effects");
 }
}
