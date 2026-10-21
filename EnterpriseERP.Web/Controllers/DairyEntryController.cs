using System.ComponentModel.DataAnnotations;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;

public sealed class MilkEntryInput
{
    [Range(1, int.MaxValue)] public int AnimalId { get; set; }
    [Required] public DateOnly Date { get; set; }
    [Required] public string Session { get; set; } = "Morning";
    [Range(typeof(decimal), "0.001", "100000")] public decimal Litres { get; set; }
}
public sealed class MilkEntryPage
{
    public MilkEntryInput Entry { get; set; } = new();
    public List<Animal> Animals { get; set; } = new();
    public List<MilkYield> Records { get; set; } = new();
    public DateOnly Day { get; set; }
    public decimal TotalLitres => Records.Sum(x => x.Litres);
}

[Authorize]
[Route("milk-entry")]
public sealed class DairyEntryController(AppDbContext db, TenantContext tenant, PermissionService permission, FarmService farm, TimeProvider? clock = null):Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(DateOnly? day, CancellationToken ct=default)
    {
        await permission.Demand("Dairy.View",ct);
        var t=await tenant.GetAsync(ct);
        var selected=day??(await CompanyBusinessDate.Current(db,t.company,ct,clock)).Date;
        var records=await db.MilkYields.AsNoTracking().Where(x=>x.CompanyId==t.company && x.BranchId==t.branch && x.Date==selected)
            .OrderBy(x=>x.AnimalId).ThenBy(x=>x.Session).ToListAsync(ct);
        var animals=await db.Animals.AsNoTracking().Where(x=>x.CompanyId==t.company && x.BranchId==t.branch && !x.Sold).OrderBy(x=>x.Tag).ToListAsync(ct);
        return View(new MilkEntryPage{Day=selected, Entry=new MilkEntryInput{Date=selected},Animals=animals,Records=records});
    }

    [HttpPost("record")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Record([Bind(Prefix="Entry")]MilkEntryInput entry,CancellationToken ct=default)
    {
        await permission.Demand("Dairy.Create", ct);
        if (ModelState.IsValid)
        {
            try
            {
                await farm.RecordMilk(entry.AnimalId, entry.Date, entry.Session, entry.Litres, ct);
                return RedirectToAction(nameof(Index), new { day = entry.Date.ToString("yyyy-MM-dd") });
            }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                ModelState.AddModelError("", "Milk record could not be saved (possible duplicate). Reload and retry.");
            }
        }
        // Error rendering is tenant scoped and requires the same read permission as the browse screen.
        await permission.Demand("Dairy.View", ct);
        var t = await tenant.GetAsync(ct);
        return View("Index", new MilkEntryPage
        {
            Day = entry.Date, Entry = entry,
            Animals = await db.Animals.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && !x.Sold).OrderBy(x => x.Tag).ToListAsync(ct),
            Records = await db.MilkYields.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Date == entry.Date).ToListAsync(ct)
        });
    }
}
