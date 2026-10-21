using Microsoft.EntityFrameworkCore;
using System.Collections.Frozen;
namespace EnterpriseERP.Web;

public sealed class PermissionService(AppDbContext db, TenantContext context)
{
 // The grant UI and permission checks share the same operation names. Administrative recovery
 // privileges are intentionally excluded: a company administrator cannot mint platform privileges.
 // Identity.VerifyEmailOwnership is also elevated: ordinary grant/provisioning screens must not
 // let a Permissions.Manage actor self-enable ownership attestations. It requires an existing
 // trusted wildcard administrator or separately authorized administrative provisioning.
 public static IReadOnlySet<string> GrantablePermissions { get; } = new string[]
 {
  "Scanning.Use", "Scanning.ManageLabels",
  "Messaging.View", "Messaging.Send",
  "BusinessPolicy.View", "BusinessPolicy.Manage", "BusinessPolicy.Approve", "TaxPolicy.Manage", "TaxPolicy.Approve",
  "HR.View", "HR.Manage", "HR.Attendance", "HR.Leave.Approve", "HR.IdentityLink", "Employee.Self",
  "Payroll.View", "Payroll.Rates", "Payroll.Policy", "Payroll.Policy.Approve", "Payroll.Calculate", "Payroll.Approve", "Payroll.Post",
  "CRM.View", "CRM.Manage", "CRM.Review",
  "FixedAssets.View", "FixedAssets.Create", "FixedAssets.Edit", "FixedAssets.Approve", "FixedAssets.Post", "FixedAssets.Depreciate", "FixedAssets.Dispose", "FixedAssets.Custody",
  "Ecommerce.View", "Ecommerce.Manage", "Ecommerce.Fulfill",
  "Livestock.View", "Livestock.Create", "Livestock.Edit", "Livestock.Sell",
  "Dairy.View", "Dairy.Create", "AnimalHealth.View", "AnimalHealth.Create",
  "FeedManagement.View", "FeedManagement.Create", "Inventory.View", "Inventory.Create", "Inventory.Edit", "Inventory.Move", "Inventory.Transfer",
  "Purchase.View", "Parties.View", "Parties.Manage", "Purchase.Approve", "Purchase.Post", "Sales.View", "Sales.Post", "Accounts.View", "Accounts.Post", "Accounts.Reconcile", "Accounts.Close", "Accounts.Opening", "Reports.View",
  "CustomerCare.View", "CustomerCare.Create", "CustomerCare.Update",
  "AI.Read", "AI.Chat", "Automation.Review", "Automation.Request", "Automation.Approve", "Automation.Schedule", "Automation.Manage", "Fattening.View", "Fattening.Create", "Expenses.View", "Expenses.Create", "Breeding.View", "Breeding.Create", "Breeding.Update"
 }.ToFrozenSet(StringComparer.Ordinal);
 public async Task Demand(string permission, CancellationToken ct = default) => _ = await DemandScope(permission,ct);
 public async Task<(int company,int branch,string user)> DemandScope(string permission, CancellationToken ct = default)
 {
  if (string.IsNullOrWhiteSpace(permission) || permission == "*")
   throw new ArgumentException("A specific permission is required", nameof(permission));
  var t = await context.GetAsync(ct);
  if (!await db.UserPermissions.AsNoTracking().AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == permission || x.Permission == "*"), ct))
   throw new UnauthorizedAccessException("Permission denied: " + permission);
  return t;
 }
}
