using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EnterpriseERP.Web.Controllers;

public sealed class ProductEditor
{
 public int Id { get; set; }
 [Required, StringLength(60, MinimumLength=2)] public string SKU { get; set; } = "";
 [Required, StringLength(160, MinimumLength=2)] public string Name { get; set; } = "";
 [Required, StringLength(30)] public string UnitOfMeasure { get; set; } = "";
 public bool ReviewedExistingUnit { get; set; }
 public bool RequiresUnitReview { get; set; }
 public bool IsUnitEstablished { get; set; }
 // Unit cost is controlled by receipts. No direct cost edits in the master screen.
 public decimal QuantityOnHand { get; set; }
 public decimal UnitCost { get; set; }
 public byte[]? RowVersion { get; set; }
}

[Authorize]
[Route("products")]
public sealed class ProductCatalogController(AppDbContext db, TenantContext context, PermissionService permission) : Controller
{
 [HttpGet("")]
 public async Task<IActionResult> Index(string? q, int page=1, CancellationToken ct=default)
 {
  await permission.Demand("Inventory.View", ct);
  var t=await context.GetAsync(ct);
  page=Math.Clamp(page,1,100000);
  var items=db.StockItems.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);
  q=q?.Trim();
  if(!string.IsNullOrEmpty(q)) items=items.Where(x=>x.SKU.Contains(q)||x.Name.Contains(q));
  ViewBag.Total=await items.CountAsync(ct);ViewBag.Page=page;ViewBag.Query=q;
  return View(await items.OrderBy(x=>x.Name).ThenBy(x=>x.Id).Skip((page-1)*25).Take(25).ToListAsync(ct));
 }
 [HttpGet("new")]
 public async Task<IActionResult> Create(CancellationToken ct)
 {await permission.Demand("Inventory.Create",ct);await context.GetAsync(ct);return View("Edit",new ProductEditor());}
 [HttpGet("{id:int}/edit")]
 public async Task<IActionResult> Edit(int id,CancellationToken ct)
 {
  await permission.Demand("Inventory.Edit",ct);var t=await context.GetAsync(ct);
  var p=await db.StockItems.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct);
  if(p is null)return NotFound();
  return View(new ProductEditor{Id=p.Id,SKU=p.SKU,Name=p.Name,UnitOfMeasure=p.UnitOfMeasure,IsUnitEstablished=p.UnitOfMeasure!="",QuantityOnHand=p.QuantityOnHand,UnitCost=p.UnitCost,RowVersion=p.RowVersion,
   RequiresUnitReview=p.UnitOfMeasure==""&&(p.QuantityOnHand!=0||await db.StockMovements.AnyAsync(x=>x.StockItemId==p.Id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct))});
 }
 [HttpPost("save")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Save(ProductEditor input,CancellationToken ct)
 {
  await permission.Demand(input.Id==0?"Inventory.Create":"Inventory.Edit",ct);var t=await context.GetAsync(ct);
  input.SKU=(input.SKU??"").Trim().ToUpperInvariant();input.Name=(input.Name??"").Trim();
  try{input.SKU=BusinessInput.Text(input.SKU,"SKU",60);}catch(ArgumentException ex){ModelState.AddModelError(nameof(input.SKU),ex.Message);}
  try{input.Name=BusinessInput.Text(input.Name,"Name",160);}catch(ArgumentException ex){ModelState.AddModelError(nameof(input.Name),ex.Message);}
  try{input.UnitOfMeasure=StockUnitPolicy.Normalize(input.UnitOfMeasure);}catch(ArgumentException ex){ModelState.AddModelError(nameof(input.UnitOfMeasure),ex.Message);}
  if(input.SKU.Length<2||input.SKU.Length>60||input.Name.Length<2||input.Name.Length>160) ModelState.AddModelError("", "Enter a SKU of 2–60 characters and product name of 2–160 characters.");
  await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
  StockItem? existing=null;
  if(input.Id!=0)
  {
   existing=await db.StockItems.FirstOrDefaultAsync(x=>x.Id==input.Id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct)??throw new KeyNotFoundException("Product not found in active branch");
   input.QuantityOnHand=existing.QuantityOnHand;input.UnitCost=existing.UnitCost;input.IsUnitEstablished=existing.UnitOfMeasure!="";
   input.RequiresUnitReview=!input.IsUnitEstablished&&(existing.QuantityOnHand!=0||await db.StockMovements.AnyAsync(x=>x.StockItemId==existing.Id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct));
   // The token belongs to the original edit form, not the row loaded by this POST.
   // Compare before touching metadata or queuing audits, then protect the actual UPDATE too.
   if(input.RowVersion is not {Length: >0}||!existing.RowVersion.SequenceEqual(input.RowVersion))
   {
    ModelState.AddModelError("","Product record has changed or its edit token is missing. Reload before saving. No changes were saved.");
    var conflict=View("Edit",input);conflict.StatusCode=StatusCodes.Status409Conflict;return conflict;
   }
   db.Entry(existing).Property(x=>x.RowVersion).OriginalValue=input.RowVersion.ToArray();
   if(input.IsUnitEstablished&&existing.UnitOfMeasure!=input.UnitOfMeasure)ModelState.AddModelError(nameof(input.UnitOfMeasure),"An established stock unit cannot be changed. Create a new SKU for a different physical unit.");
   if(input.RequiresUnitReview&&!input.ReviewedExistingUnit)ModelState.AddModelError(nameof(input.ReviewedExistingUnit),"Confirm that existing stock quantities and movement history were reviewed in this unit. This does not convert stock.");
  }
  if(!ModelState.IsValid)return View("Edit",input);
  if(await db.StockItems.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.SKU==input.SKU&&x.Id!=input.Id,ct))
  {ModelState.AddModelError(nameof(input.SKU),"SKU already exists in this branch");return View("Edit",input);}
  if(await db.StockItems.AnyAsync(x=>x.CompanyId==t.company&&x.SKU.Trim().ToUpper()==input.SKU&&x.Id!=input.Id&&x.UnitOfMeasure!=""&&x.UnitOfMeasure!=input.UnitOfMeasure,ct))
  {ModelState.AddModelError(nameof(input.UnitOfMeasure),"The same SKU must use one stock unit across this company.");return View("Edit",input);}
  var entity=existing??new StockItem{CompanyId=t.company,BranchId=t.branch};
  if(existing is null)db.StockItems.Add(entity);
  if(entity.UnitOfMeasure=="")db.Audits.Add(new AuditEntry{CompanyId=t.company,ActorId=t.user,Action=input.RequiresUnitReview?"Product.UnitReviewed":"Product.UnitAssigned",Reference=$"{input.SKU}; unit {input.UnitOfMeasure}"});
  entity.SKU=input.SKU;entity.Name=input.Name;entity.UnitOfMeasure=input.UnitOfMeasure;
  db.Audits.Add(new AuditEntry{CompanyId=t.company,ActorId=t.user,Action=input.Id==0?"Product.Create":"Product.Update",Reference=input.SKU});
  try{await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);}
  catch(DbUpdateConcurrencyException)
  {
   await transaction.RollbackAsync(ct);db.ChangeTracker.Clear();
   ModelState.AddModelError("","Product record has changed. Reload before saving. No changes were saved.");
   var conflict=View("Edit",input);conflict.StatusCode=StatusCodes.Status409Conflict;return conflict;
  }
  catch(Exception ex)when(ex is DbUpdateException or InvalidOperationException)
  {await transaction.RollbackAsync(ct);db.ChangeTracker.Clear();ModelState.AddModelError("",ex is DbUpdateException?"Could not save. Check for duplicate SKU or conflicting updates.":ex.Message);return View("Edit",input);}
  TempData["Success"]="Product saved";
  return RedirectToAction(nameof(Index));
 }
}
