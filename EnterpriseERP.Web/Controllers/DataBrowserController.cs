using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
[Authorize]
public sealed class DataBrowserController(GridService grids,AppDbContext db,TenantContext tenant,PermissionService permission):Controller
{
 [HttpGet("/data")]
 public async Task<IActionResult> Index(CancellationToken ct)
 {
  var t=await tenant.GetAsync(ct);var grants=await db.UserPermissions.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.UserId==t.user).Select(x=>x.Permission).ToListAsync(ct);
  return View(GridCatalog.All.Where(x=>grants.Contains("*")||grants.Contains(x.Permission)).ToArray());
 }
 [HttpGet("/data/{key}")]
 public async Task<IActionResult> Table(string key,CancellationToken ct)
 {GridDescriptor d;try{d=GridCatalog.Find(key);}catch(KeyNotFoundException){return NotFound();}await permission.Demand(d.Permission,ct);return View(d);}
 [HttpGet("/api/tables/{key}"),EnableRateLimiting("data-tables")]
 public async Task<IActionResult> Rows(string key,CancellationToken ct)
 {
  Response.Headers.CacheControl="no-store";
  try{var request=GridRequest.Parse(Request.Query);var page=await grids.Get(key,request,ct);return Json(new{draw=request.Draw,recordsTotal=page.RecordsTotal,recordsFiltered=page.RecordsFiltered,data=page.Data});}
  catch(KeyNotFoundException){return NotFound();}
  catch(ArgumentException e){return BadRequest(new{error=e.Message});}
 }
}
