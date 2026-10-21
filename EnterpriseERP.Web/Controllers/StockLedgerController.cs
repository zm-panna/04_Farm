using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
[Authorize]
[Route("stock-ledger")]
public sealed class StockLedgerController(AppDbContext db,TenantContext tenant,PermissionService permission):Controller
{
 [HttpGet("")]
 public async Task<IActionResult> Index(int? productId,int page=1,CancellationToken ct=default)
 {
  await permission.Demand("Inventory.View",ct);var t=await tenant.GetAsync(ct);
  page=Math.Clamp(page,1,100000);
  var query=db.StockMovements.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);
  if(productId.HasValue)query=query.Where(x=>x.StockItemId==productId.Value);
  ViewBag.Page=page;ViewBag.ProductId=productId;ViewBag.Total=await query.CountAsync(ct);
  return View(await query.OrderByDescending(x=>x.CreatedUtc).ThenByDescending(x=>x.Id).Skip((page-1)*25).Take(25).ToListAsync(ct));
 }
}
