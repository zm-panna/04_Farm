using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
public sealed class SupportTicketUpdateInput
{
 [Required, StringLength(32)] public string Status { get; set; } = "Open";
 [StringLength(450)] public string? AssignedTo { get; set; }
 [Required, StringLength(2000)] public string Note { get; set; } = "";
 [Required, StringLength(256)] public string RowVersion { get; set; } = "";
 public bool Reopen { get; set; }
}
public sealed record SupportTicketDetailPage(SupportTicketDetail Detail, SupportTicketUpdateInput Update, bool CanUpdate);
[Authorize]
public sealed class SupportController(SupportService support, AppDbContext db, TenantContext tenant, PermissionService permissions) : Controller
{
 [HttpGet("support/tickets")]
 public async Task<IActionResult> Tickets(CancellationToken ct)
 {
  await permissions.Demand("CustomerCare.View", ct); var t = await tenant.GetAsync(ct);
  ViewBag.CanCreate = await db.UserPermissions.AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == "CustomerCare.Create" || x.Permission == "*"), ct);
  return View();
 }
 [HttpGet("support/tickets/{id:long}")]
 public async Task<IActionResult> Detail(long id, CancellationToken ct) => View(await Page(id, null, ct));

 [HttpPost("support/tickets/{id:long}/update"), ValidateAntiForgeryToken]
 public async Task<IActionResult> Update(long id, [Bind(Prefix = "Update")] SupportTicketUpdateInput input, CancellationToken ct)
 {
  await permissions.Demand("CustomerCare.Update", ct);
  try
  {
   if (!ModelState.IsValid) throw new ArgumentException("Enter a status, note, and current record version");
   byte[] version;
   try { version = Convert.FromBase64String(input.RowVersion); }
   catch (FormatException) { throw new ArgumentException("Invalid record version. Reload the ticket."); }
   await support.Update(id, new UpdateTicketRequest(input.Status, input.AssignedTo, input.Note, version, input.Reopen), ct);
   TempData["Success"] = input.Reopen ? "Ticket reopened." : "Ticket updated.";
   return RedirectToAction(nameof(Detail), new { id });
  }
  catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
  {
   db.ChangeTracker.Clear();
   ModelState.AddModelError("", ex is DbUpdateConcurrencyException ? "This ticket changed. Review the latest state below before submitting your note again." : ex is DbUpdateException ? "The update could not be saved. Review the current ticket and retry." : ex.Message);
   ModelState.Remove("Update.RowVersion");
   return View("Detail", await Page(id, input, ct));
  }
 }
 private async Task<SupportTicketDetailPage> Page(long id, SupportTicketUpdateInput? input, CancellationToken ct)
 {
  var detail = await support.Detail(id, ct); var t = await tenant.GetAsync(ct);
  var canUpdate = await db.UserPermissions.AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == "CustomerCare.Update" || x.Permission == "*"), ct);
  input ??= new() { Status = detail.Ticket.Status, AssignedTo = detail.Ticket.AssignedTo };
  input.RowVersion = Convert.ToBase64String(detail.Ticket.RowVersion);
  return new(detail, input, canUpdate);
 }
}
