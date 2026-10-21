using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace EnterpriseERP.Web;

public sealed class AutomationApprovalService(AppDbContext db, TenantContext tenant, PermissionService permissions)
{
 // Bind the decision to both the submitted payload and the exact tenant/rule behavior reviewed.
 // Old payload-only approvals deliberately cannot approve a changed/new definition.
 static string Hash(AutomationRule rule, string payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
  JsonSerializer.Serialize(new { rule.CompanyId, rule.Id, rule.Trigger, rule.Action, Payload = payload }))));
 static void ValidatePayload(string payload)
 { if (string.IsNullOrWhiteSpace(payload) || payload.Length > 10000) throw new ArgumentException("Proposal required (max 10000 characters)"); }

 public async Task<long> Request(long ruleId, string payload, CancellationToken ct)
 {
  await permissions.Demand("Automation.Request", ct);
  var t = await tenant.GetAsync(ct);
  ValidatePayload(payload);
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  var rule = await db.AutomationRules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ruleId && x.CompanyId == t.company && x.Active, ct)
   ?? throw new ArgumentException("Active rule unavailable");
  var approval = new AutomationApproval { CompanyId = t.company, RuleId = ruleId, RequesterId = t.user, PayloadHash = Hash(rule, payload) };
  db.AutomationApprovals.Add(approval);
  db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "AutomationApprovalRequested", Reference = ruleId.ToString(System.Globalization.CultureInfo.InvariantCulture) });
  await db.SaveChangesAsync(ct);
  await tx.CommitAsync(ct);
  return approval.Id;
 }

 public async Task Approve(long approvalId, string unchangedPayload, CancellationToken ct)
 {
  await permissions.Demand("Automation.Approve", ct);
  var t = await tenant.GetAsync(ct);
  ValidatePayload(unchangedPayload);
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  var approval = await db.AutomationApprovals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == approvalId && x.CompanyId == t.company, ct)
   ?? throw new ArgumentException("Approval unavailable");
  var rule = await db.AutomationRules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == approval.RuleId && x.CompanyId == t.company && x.Active, ct)
   ?? throw new InvalidOperationException("Rule is no longer active; submit a new request");
  var hash = Hash(rule, unchangedPayload);
  if (approval.Status != "Pending" || approval.RequesterId == t.user || approval.PayloadHash != hash)
   throw new InvalidOperationException("Approval invalid, requester cannot approve own request, or proposal/rule changed");
  var updated = await db.AutomationApprovals
   .Where(x => x.Id == approvalId && x.CompanyId == t.company && x.Status == "Pending" && x.RequesterId != t.user && x.PayloadHash == hash)
   .ExecuteUpdateAsync(update => update.SetProperty(x => x.Status, "Approved").SetProperty(x => x.ApproverId, t.user), ct);
  if (updated != 1) throw new InvalidOperationException("Approval has already been decided");
  db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "AutomationApprovedForReview", Reference = approvalId.ToString(System.Globalization.CultureInfo.InvariantCulture) });
  await db.SaveChangesAsync(ct);
  await tx.CommitAsync(ct);
 }
}
