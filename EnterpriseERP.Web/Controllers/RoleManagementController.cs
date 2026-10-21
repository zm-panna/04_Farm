using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EnterpriseERP.Domain;
namespace EnterpriseERP.Web.Controllers;
public sealed record PermissionUserOption(string Id, string Name);
[Authorize, Route("admin/permissions")]
public sealed class RoleManagementController(AppDbContext db, TenantContext tenant, PermissionService permission, UserManager<AppUser> users) : Controller
{
 [HttpGet("")]
 public async Task<IActionResult> Index(CancellationToken ct)
 {
  await permission.Demand("Permissions.Manage", ct);
  var t = await tenant.GetAsync(ct);
  var rows = await db.UserPermissions.AsNoTracking().Where(x => x.CompanyId == t.company).OrderBy(x => x.UserId).ToListAsync(ct);
  ViewBag.Allowed = PermissionService.GrantablePermissions.OrderBy(x => x).ToArray();
  ViewBag.Users = await (from access in db.UserCompanyAccess.AsNoTracking()
   join user in db.Users.AsNoTracking() on access.UserId equals user.Id
   where access.CompanyId == t.company
   orderby user.Email select new PermissionUserOption(user.Id, user.Email ?? user.UserName ?? user.Id)).ToListAsync(ct);
  return View(rows);
 }

 [HttpPost("grant"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Grant(string userId, string permissionName, CancellationToken ct)
 {
  await permission.Demand("Permissions.Manage", ct);
  var t = await tenant.GetAsync(ct);
  if (string.IsNullOrWhiteSpace(permissionName) || !PermissionService.GrantablePermissions.Contains(permissionName)
   || string.IsNullOrWhiteSpace(userId) || await users.FindByIdAsync(userId) == null
   || !await db.UserCompanyAccess.AnyAsync(x => x.CompanyId == t.company && x.UserId == userId, ct))
   return BadRequest("Invalid user/permission");
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  if (!await db.UserPermissions.AnyAsync(x => x.CompanyId == t.company && x.UserId == userId && x.Permission == permissionName, ct))
  {
   db.UserPermissions.Add(new UserPermission { CompanyId = t.company, UserId = userId, Permission = permissionName });
   db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "PermissionGranted", Reference = userId + ":" + permissionName });
   await db.SaveChangesAsync(ct);
  }
  await tx.CommitAsync(ct);
  return RedirectToAction(nameof(Index));
 }

 [HttpPost("revoke"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Revoke(long id, CancellationToken ct)
 {
  await permission.Demand("Permissions.Manage", ct);
  var t = await tenant.GetAsync(ct);
  var row = await db.UserPermissions.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company, ct);
  if (row == null) return NotFound();
  if (row.Permission is "Permissions.Manage" or "*") return BadRequest("Administrative permissions require the protected recovery procedure");
  db.UserPermissions.Remove(row);
  db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "PermissionRevoked", Reference = row.UserId + ":" + row.Permission });
  await db.SaveChangesAsync(ct);
  return RedirectToAction(nameof(Index));
 }
}
