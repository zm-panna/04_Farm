using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed class ServerGridsViewComponent(AppDbContext db,TenantContext tenant):ViewComponent
{
 public async Task<IViewComponentResult> InvokeAsync(string path)
 {
  if(HttpContext.User.Identity?.IsAuthenticated != true)return Content("");
  var current=path.TrimEnd('/');
  var inventory=current.Equals("/Inventory",StringComparison.OrdinalIgnoreCase)||current.Equals("/Inventory/Home",StringComparison.OrdinalIgnoreCase);
  var candidates=GridCatalog.All.Where(x=>string.Equals(x.Home,current,StringComparison.OrdinalIgnoreCase)||(inventory&&x.Key is "products" or "stock-movements")).ToArray();
  if(candidates.Length==0)return Content("");
  try
  {
   var t=await tenant.GetAsync(HttpContext.RequestAborted);var grants=await db.UserPermissions.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.UserId==t.user).Select(x=>x.Permission).ToListAsync(HttpContext.RequestAborted);
   return View(candidates.Where(x=>grants.Contains("*")||grants.Contains(x.Permission)).ToArray());
  }
  catch(UnauthorizedAccessException){return Content("");}
 }
}
