using System.Security.Claims;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
public sealed record CompanyOption(int Id,string Name);
public sealed record BranchOption(int Id,int CompanyId,string Name);
[Authorize]
public sealed class ContextController(AppDbContext db,UserManager<AppUser> users,SignInManager<AppUser> signIn):Controller
{
 [HttpGet] public async Task<IActionResult> Index(CancellationToken ct)
 {
  var userId=users.GetUserId(User);
  if(string.IsNullOrWhiteSpace(userId)||await users.GetUserAsync(User) is null)return Challenge();
  var companies=await (from a in db.UserCompanyAccess.AsNoTracking() join c in db.Companies.AsNoTracking() on a.CompanyId equals c.Id where a.UserId==userId select new CompanyOption(c.Id,c.Name)).ToListAsync(ct);
  var branches=await (from a in db.UserBranchAccess.AsNoTracking() join b in db.Branches.AsNoTracking() on a.BranchId equals b.Id where a.UserId==userId&&a.CompanyId==b.CompanyId&&db.UserCompanyAccess.Any(c=>c.UserId==userId&&c.CompanyId==b.CompanyId) select new BranchOption(b.Id,b.CompanyId,b.Name)).ToListAsync(ct);
  ViewBag.Companies=companies;ViewBag.Branches=branches;return View();
 }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Select(int companyId,int branchId,CancellationToken ct)
 {
  var user=await users.GetUserAsync(User); if(user is null) return Challenge();
  var authorized=await db.UserCompanyAccess.AnyAsync(x=>x.UserId==user.Id&&x.CompanyId==companyId,ct)
   &&await db.UserBranchAccess.AnyAsync(x=>x.UserId==user.Id&&x.CompanyId==companyId&&x.BranchId==branchId,ct)
   &&await db.Branches.AnyAsync(x=>x.Id==branchId&&x.CompanyId==companyId,ct);
  if(!authorized||!await db.Companies.AnyAsync(x=>x.Id==companyId,ct)) return Forbid();
  await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
  // Selection is browser-session state. Never persist it as a global Identity user claim.
  if(!await db.UserCompanyAccess.AnyAsync(x=>x.UserId==user.Id&&x.CompanyId==companyId,ct)
   ||!await db.UserBranchAccess.AnyAsync(x=>x.UserId==user.Id&&x.CompanyId==companyId&&x.BranchId==branchId,ct)
   ||!await db.Branches.AnyAsync(x=>x.Id==branchId&&x.CompanyId==companyId,ct)) return Forbid();
  var claims=await users.GetClaimsAsync(user);
  foreach(var c in claims.Where(x=>x.Type=="ActiveCompanyId"||x.Type=="ActiveBranchId").ToList())
  {var r=await users.RemoveClaimAsync(user,c);if(!r.Succeeded)throw new InvalidOperationException("Unable to update context");}
  db.Audits.Add(new EnterpriseERP.Domain.AuditEntry { CompanyId=companyId, ActorId=user.Id, Action="TenantContextSelected", Reference="branch:"+branchId });
  await db.SaveChangesAsync(ct);
  await tx.CommitAsync(ct);
  var selected = new List<Claim>{new Claim("ActiveCompanyId",companyId.ToString(System.Globalization.CultureInfo.InvariantCulture)),new Claim("ActiveBranchId",branchId.ToString(System.Globalization.CultureInfo.InvariantCulture))};
  if(User.HasClaim("amr","mfa"))selected.Add(new Claim("amr","mfa"));
  await signIn.SignInWithClaimsAsync(user,isPersistent:false,selected);
  return RedirectToAction("Index","Home");
 }
}
