using System.ComponentModel.DataAnnotations;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;
public sealed class AnimalEditor
{
 public int Id { get; set; }
 [Required, StringLength(70, MinimumLength=2)] public string Tag { get; set; } = "";
 [Required, StringLength(100, MinimumLength=2)] public string Breed { get; set; } = "";
 [Range(typeof(decimal), "0", "999999999999999.999", ParseLimitsInInvariantCulture=true)] public decimal PurchaseCost { get; set; }
 [Range(0.001, 100000)] public decimal? CurrentWeightKg { get; set; }
 public byte[]? RowVersion { get; set; }
}
[Authorize]
[Route("animals")]
public sealed class AnimalRegistryController(AppDbContext db,TenantContext tenant,PermissionService permissions,FarmService farm):Controller
{
 [HttpGet("")]
 public async Task<IActionResult> Index(string? q,int page=1,CancellationToken ct=default)
 {
  await permissions.Demand("Livestock.View",ct);
  var t=await tenant.GetAsync(ct);page=Math.Clamp(page,1,100000);
  var result=db.Animals.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);
  q=q?.Trim();if(!string.IsNullOrWhiteSpace(q))result=result.Where(x=>x.Tag.Contains(q)||x.Breed.Contains(q));
  ViewBag.Query=q??"";ViewBag.Page=page;ViewBag.Total=await result.CountAsync(ct);
  return View(await result.OrderBy(x=>x.Tag).Skip((page-1)*25).Take(25).ToListAsync(ct));
 }
 [HttpGet("new")]
 public async Task<IActionResult> Create(CancellationToken ct=default)
 {await permissions.Demand("Livestock.Create",ct);await tenant.GetAsync(ct);return View("Edit",new AnimalEditor());}
 [HttpGet("{id:int}/edit")]
 public async Task<IActionResult> Edit(int id,CancellationToken ct=default)
 {
  await permissions.Demand("Livestock.Edit",ct);var t=await tenant.GetAsync(ct);
  var animal=await db.Animals.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct);
  if(animal==null)return NotFound();
  return View(new AnimalEditor{Id=animal.Id,Tag=animal.Tag,Breed=animal.Breed,PurchaseCost=animal.PurchaseCost,CurrentWeightKg=animal.CurrentWeightKg,RowVersion=animal.RowVersion});
 }
 [HttpPost("save")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Save(AnimalEditor input,CancellationToken ct=default)
 {
  await permissions.Demand(input.Id==0?"Livestock.Create":"Livestock.Edit",ct);var t=await tenant.GetAsync(ct);
  input.Tag=(input.Tag??"").Trim().ToUpperInvariant();input.Breed=(input.Breed??"").Trim();
  try{input.Tag=BusinessInput.Text(input.Tag,"Tag",70);}catch(ArgumentException ex){ModelState.AddModelError(nameof(input.Tag),ex.Message);}
  try{input.Breed=BusinessInput.Text(input.Breed,"Breed",100);}catch(ArgumentException ex){ModelState.AddModelError(nameof(input.Breed),ex.Message);}
  if(input.Tag.Length<2||input.Breed.Length<2)ModelState.AddModelError("","Enter a tag and breed of at least 2 characters.");
  try { BusinessInput.Amount(input.PurchaseCost,"Purchase cost",false); if(input.CurrentWeightKg.HasValue)BusinessInput.Decimal(input.CurrentWeightKg.Value,"Weight",true,maximum:100000); }
  catch(ArgumentException ex){ModelState.AddModelError("",ex.Message);}
  if(!ModelState.IsValid)return View("Edit",input);
  if(await db.Animals.AnyAsync(x=>x.CompanyId==t.company&&x.Tag==input.Tag&&x.Id!=input.Id,ct))
  {ModelState.AddModelError(nameof(input.Tag),"Animal tag already exists");return View("Edit",input);}
  if(input.Id==0)
  {
   try {await farm.AddAnimal(input.Tag,input.Breed,input.PurchaseCost,ct,input.CurrentWeightKg);return RedirectToAction(nameof(Index));}
   catch(ArgumentException ex){ModelState.AddModelError("",ex.Message);return View("Edit",input);}
   catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);return View("Edit",input);}
   catch(DbUpdateException){ModelState.AddModelError("","Unable to save animal. Verify tag uniqueness and retry.");return View("Edit",input);}
  }
  else
  {
   var record=await db.Animals.FirstOrDefaultAsync(x=>x.Id==input.Id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct);
   if(record==null)return NotFound();
   if(record.Sold){ModelState.AddModelError("","Sold animals cannot be modified");return View("Edit",input);}
   if(input.RowVersion==null||!record.RowVersion.SequenceEqual(input.RowVersion))
   {ModelState.AddModelError("","Animal record has changed. Reload before saving.");return View("Edit",input);}
   // Historical acquisition costs should never be silently rewritten through the animal master.
   if(record.PurchaseCost!=input.PurchaseCost){ModelState.AddModelError(nameof(input.PurchaseCost),"Purchase cost is locked after creation");return View("Edit",input);}
   // Existing weights are append-only dated operational data; master editing cannot bypass Fattening.Create.
   if(record.CurrentWeightKg!=input.CurrentWeightKg){ModelState.AddModelError(nameof(input.CurrentWeightKg),"Existing weight is locked. Record a dated weight in Fattening.");return View("Edit",input);}
   record.Tag=input.Tag;record.Breed=input.Breed;
  }
  db.Audits.Add(new AuditEntry{CompanyId=t.company,ActorId=t.user,Action=input.Id==0?"Animal.Create":"Animal.Edit",Reference=input.Tag});
  try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){ModelState.AddModelError("","Concurrent edit detected. Reload and retry.");return View("Edit",input);}catch(DbUpdateException){ModelState.AddModelError("","Unable to save animal; verify tag uniqueness.");return View("Edit",input);}
  return RedirectToAction(nameof(Index));
 }
}
