using System.ComponentModel.DataAnnotations;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
public sealed class AnimalHealthInput
{
 [Range(1,int.MaxValue)] public int AnimalId {get;set;}
 [Required,StringLength(80)] public string Reference {get;set;}="";
 [Required] public string EventType {get;set;}="Vaccination";
 public DateOnly EventDate {get;set;}
 public DateOnly? NextDueDate {get;set;}
 [StringLength(4000)] public string Details {get;set;}="";
}
public sealed class HealthFollowUpInput
{
 [Range(1,long.MaxValue)] public long RecordId {get;set;}
 public DateOnly CompletedDate {get;set;}
}
public sealed class AnimalHealthPage
{
 public DateOnly Today {get;set;}
 public AnimalHealthInput Entry {get;set;}=new();
 public List<Animal> Animals {get;set;}=new();
 public List<AnimalHealthRecord> Records {get;set;}=new();
 public List<AnimalHealthRecord> DueRecords {get;set;}=new();
 public Dictionary<int,string> AnimalTags {get;set;}=new();
}
[Authorize]
[Route("animal-health")]
public sealed class AnimalHealthEntryController(AppDbContext db,TenantContext tenant,PermissionService permissions,FarmHealthFeedService service, TimeProvider? clock = null):Controller
{
 [HttpGet("")]
 public async Task<IActionResult> Index(int? animalId,CancellationToken ct=default)
 {
  return View(await Page(animalId,ct));
 }
 [HttpPost("record")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Record([Bind(Prefix="Entry")]AnimalHealthInput input,CancellationToken ct=default)
 {
  await permissions.Demand("AnimalHealth.Create",ct);
  if(!new[]{"Vaccination","Deworming","Treatment","VetVisit","Observation"}.Contains(input.EventType))ModelState.AddModelError("Entry.EventType","Unsupported event type");
  if(input.NextDueDate.HasValue&&input.NextDueDate.Value<input.EventDate)ModelState.AddModelError("Entry.NextDueDate","Due date must be on or after event date");
  if(!ModelState.IsValid)return await InvalidPage(input,ct);
  try
  {
   await service.RecordHealth(input.AnimalId,input.Reference,input.EventType,input.EventDate,input.NextDueDate,input.Details??"",ct);
   return RedirectToAction(nameof(Index),new{animalId=input.AnimalId});
  }
  catch(ArgumentException ex){ModelState.AddModelError("",ex.Message);return await InvalidPage(input,ct);}
  catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);return await InvalidPage(input,ct);}
  catch(DbUpdateException){db.ChangeTracker.Clear();ModelState.AddModelError("","Unable to save record; the reference may already exist.");return await InvalidPage(input,ct);}
 }
 [HttpPost("follow-up/complete")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Complete([Bind(Prefix="FollowUp")]HealthFollowUpInput input,CancellationToken ct=default)
 {
  await permissions.Demand("AnimalHealth.Create",ct);
  if(ModelState.IsValid)
  {
   try {await service.CompleteFollowUp(input.RecordId,input.CompletedDate,ct);TempData["Success"]="Follow-up completion recorded. Original due date and history are preserved.";return RedirectToAction(nameof(Index));}
   catch(ArgumentException ex){ModelState.AddModelError("",ex.Message);}
   catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
   catch(DbUpdateException){db.ChangeTracker.Clear();ModelState.AddModelError("","Unable to record completion. Refresh and check the current record.");}
  }
  return View("Index",await Page(null,ct));
 }
 private async Task<IActionResult> InvalidPage(AnimalHealthInput input,CancellationToken ct)
 {
  var page=await Page(null,ct);page.Entry=input;return View("Index",page);
 }
 private async Task<AnimalHealthPage> Page(int? animalId,CancellationToken ct)
 {
  await permissions.Demand("AnimalHealth.View",ct);var t=await tenant.GetAsync(ct);
  var animals=await db.Animals.AsNoTracking().Where(a=>a.CompanyId==t.company&&a.BranchId==t.branch).OrderBy(a=>a.Tag).ToListAsync(ct);
  var q=db.AnimalHealthRecords.AsNoTracking().Where(r=>r.CompanyId==t.company&&r.BranchId==t.branch);
  if(animalId.HasValue)q=q.Where(r=>r.AnimalId==animalId.Value);
  var today=(await CompanyBusinessDate.Current(db,t.company,ct,clock)).Date;
  var through=today.AddDays(30);
  var activeIds=animals.Where(x=>!x.Sold).Select(x=>x.Id).ToArray();
  return new AnimalHealthPage{Today=today,Entry=new AnimalHealthInput{AnimalId=animalId??0,EventDate=today},Animals=animals.Where(x=>!x.Sold).ToList(),AnimalTags=animals.ToDictionary(x=>x.Id,x=>x.Tag),
   Records=await q.OrderByDescending(r=>r.EventDate).ThenByDescending(r=>r.Id).Take(100).ToListAsync(ct),
   DueRecords=await q.Where(r=>r.NextDueDate.HasValue&&r.FollowUpCompletedDate==null&&r.NextDueDate<=through&&activeIds.Contains(r.AnimalId)).OrderBy(r=>r.NextDueDate).Take(100).ToListAsync(ct)};
 }
}
