using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

public sealed class TenantContext(AppDbContext db, IHttpContextAccessor accessor, UserManager<AppUser> users)
{
 public async Task<(int company, int branch, string user)> GetAsync(CancellationToken ct = default)
 {
  var principal = accessor.HttpContext?.User;
  if (principal?.Identity?.IsAuthenticated != true)
   throw new UnauthorizedAccessException("Login required");
  var id = users.GetUserId(principal);
  if (string.IsNullOrWhiteSpace(id) || !await db.Users.AsNoTracking().AnyAsync(x => x.Id == id, ct))
   throw new UnauthorizedAccessException("Login required");

  // Only authenticated, unambiguous selection claims count. Request headers are never tenant selectors.
  var companyClaims = principal.FindAll("ActiveCompanyId").ToArray();
  var branchClaims = principal.FindAll("ActiveBranchId").ToArray();
  if (companyClaims.Length != 1 || branchClaims.Length != 1
   || !int.TryParse(companyClaims[0].Value, out var company)
   || !int.TryParse(branchClaims[0].Value, out var branch) || company <= 0 || branch <= 0)
   throw new UnauthorizedAccessException("Select an authorized company and branch first");

  var authorized = await db.UserCompanyAccess.AsNoTracking().AnyAsync(x => x.UserId == id && x.CompanyId == company, ct)
   && await db.UserBranchAccess.AsNoTracking().AnyAsync(x => x.UserId == id && x.CompanyId == company && x.BranchId == branch, ct)
   && await db.Companies.AsNoTracking().AnyAsync(x => x.Id == company, ct)
   && await db.Branches.AsNoTracking().AnyAsync(x => x.Id == branch && x.CompanyId == company, ct);
  if (!authorized) throw new UnauthorizedAccessException("Company/branch not authorized");
  return (company, branch, id);
 }
}
