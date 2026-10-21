using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class SupportSecurityTests
{
 private static SupportService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
 private static CreateTicketRequest Create(string number = "CASE-1") => new(number, "Milk collection issue", "Please review the collection record.", "Farm customer", "+8801000000000", "Normal");
 private static async Task<SupportTicket> Ticket(SecurityTestScope f)
 {
  await f.GrantAsync("*"); var id = await Service(f).Create(Create(), default);
  return await f.Db.SupportTickets.SingleAsync(x => x.Id == id);
 }
 private static UpdateTicketRequest Update(SupportTicket ticket, string status, bool reopen = false, string? assigned = null) => new(status, assigned, "Reviewed by support staff", ticket.RowVersion.ToArray(), reopen);
 [Fact] public async Task CreationAndHistoryAreAtomic()
 {
  var failure = new FailActivitySave(); await using var f = await SecurityTestScope.CreateAsync(failure); await f.GrantAsync("CustomerCare.Create");
  await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Create(Create(), default));
  f.Db.ChangeTracker.Clear(); Assert.Empty(await f.Db.SupportTickets.ToListAsync()); Assert.Empty(await f.Db.SupportTicketActivities.ToListAsync());
 }
 [Fact] public async Task TicketCreationNormalizesReferenceAndRejectsDuplicate()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
  await Service(f).Create(Create(" CASE-1 "), default);
  await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Create(Create(), default));
  Assert.Equal("CASE-1", (await f.Db.SupportTickets.SingleAsync()).TicketNumber); Assert.Single(await f.Db.SupportTicketActivities.ToListAsync());
 }
 [Fact] public async Task LifecycleResolvesThenClosesAndExplicitlyReopens()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f); var service = Service(f);
  await service.Update(ticket.Id, Update(ticket, "InProgress", assigned: "approver-a"), default);
  await service.Update(ticket.Id, Update(ticket, "WaitingCustomer", assigned: "approver-a"), default);
  await service.Update(ticket.Id, Update(ticket, "Resolved", assigned: "approver-a"), default);
  await service.Update(ticket.Id, Update(ticket, "Closed", assigned: "approver-a"), default);
  await service.Update(ticket.Id, Update(ticket, "Open", reopen: true, assigned: "approver-a"), default);
  Assert.Equal("Open", ticket.Status); Assert.Equal("approver-a", ticket.AssignedTo);
  var history = await f.Db.SupportTicketActivities.OrderBy(x => x.Id).ToListAsync(); Assert.Equal(6, history.Count); Assert.Equal("Reopened", history[^1].Action);
 }
 [Theory] [InlineData("Closed")] [InlineData("Unknown")]
 public async Task InvalidStateTransitionDoesNotWrite(string target)
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f);
  await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Update(ticket.Id, Update(ticket, target), default));
  Assert.Equal("Open", (await f.Db.SupportTickets.AsNoTracking().SingleAsync()).Status); Assert.Single(await f.Db.SupportTicketActivities.ToListAsync());
 }
 [Fact] public async Task ClosedTicketCannotBeEditedWithoutExplicitReopen()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f); var service = Service(f);
  await service.Update(ticket.Id, Update(ticket, "Resolved"), default); await service.Update(ticket.Id, Update(ticket, "Closed"), default);
  await Assert.ThrowsAsync<InvalidOperationException>(() => service.Update(ticket.Id, Update(ticket, "Closed", assigned: "approver-a"), default));
  await Assert.ThrowsAsync<InvalidOperationException>(() => service.Update(ticket.Id, Update(ticket, "Open"), default));
  Assert.Equal("Closed", ticket.Status); Assert.Equal(3, await f.Db.SupportTicketActivities.CountAsync());
 }
 [Fact] public async Task OpenTicketCannotUseReopenShortcut()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f);
  await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Update(ticket.Id, Update(ticket, "Open", reopen: true), default));
 }
 [Fact] public async Task StaleVersionCannotOverwriteTicketOrAppendHistory()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f); var stale = Update(ticket, "Resolved");
  await Service(f).Update(ticket.Id, Update(ticket, "InProgress"), default);
  await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(f).Update(ticket.Id, stale, default));
  Assert.Equal("InProgress", (await f.Db.SupportTickets.AsNoTracking().SingleAsync()).Status); Assert.Equal(2, await f.Db.SupportTicketActivities.CountAsync());
 }
 [Fact] public async Task UpdateHistoryFailureRollsBackTicketState()
 {
  var failure = new FailActivitySave { Enabled = false }; await using var f = await SecurityTestScope.CreateAsync(failure); var ticket = await Ticket(f); failure.Enabled = true;
  await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Update(ticket.Id, Update(ticket, "InProgress"), default));
  f.Db.ChangeTracker.Clear(); Assert.Equal("Open", (await f.Db.SupportTickets.SingleAsync()).Status); Assert.Single(await f.Db.SupportTicketActivities.ToListAsync());
 }
 [Fact] public async Task AssigneeMustBelongToTicketCompanyAndBranch()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f);
  await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Update(ticket.Id, Update(ticket, "InProgress", assigned: "operator-b"), default));
  var access = await f.Db.UserBranchAccess.SingleAsync(x => x.UserId == "approver-a"); f.Db.UserBranchAccess.Remove(access);
  f.Db.UserBranchAccess.Add(new UserBranchAccess { UserId = "approver-a", CompanyId = 1, BranchId = 12 }); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Update(ticket.Id, Update(ticket, "InProgress", assigned: "approver-a"), default));
  Assert.Single(await f.Db.SupportTicketActivities.ToListAsync());
 }
 [Fact] public async Task DetailAndHistoryDoNotCrossCompanyBoundary()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f); await f.GrantAsync("*", "operator-b", 2); f.ActAs("operator-b", 2, 21);
  await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).Detail(ticket.Id));
  await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).History(ticket.Id, default));
  await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).Update(ticket.Id, Update(ticket, "InProgress"), default));
 }
 [Fact] public async Task DetailAndHistoryDoNotCrossBranchBoundary()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f);
  f.Db.UserBranchAccess.Add(new UserBranchAccess { UserId = "operator-a", CompanyId = 1, BranchId = 12 }); await f.Db.SaveChangesAsync(); f.ActAs(branch: 12);
  await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).Detail(ticket.Id));
  await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).History(ticket.Id, default));
 }
 [Fact] public async Task DetailShowsOnlyEligibleAssigneesAndMatchingHistory()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f);
  var detail = await Service(f).Detail(ticket.Id);
  Assert.Equal(ticket.Id, detail.Ticket.Id); Assert.Single(detail.History); Assert.Equal(2, detail.Assignees.Count);
  Assert.DoesNotContain(detail.Assignees, x => x.Id == "operator-b"); Assert.NotEmpty(detail.Ticket.RowVersion);
 }
 [Theory] [InlineData("")] [InlineData(" ")]
 public async Task NoteIsRequiredAndInvalidUpdateDoesNotWrite(string note)
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f);
  await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Update(ticket.Id, new("InProgress", null, note, ticket.RowVersion), default));
  Assert.Single(await f.Db.SupportTicketActivities.ToListAsync());
 }
 [Fact] public async Task InvalidMvcVersionPreservesNoteAndRefreshesSafeVersion()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var ticket = await Ticket(f);
  var controller = new SupportController(Service(f), f.Db, f.Tenant, f.Permissions) { ControllerContext = new ControllerContext { HttpContext = f.Http } };
  var result = Assert.IsType<ViewResult>(await controller.Update(ticket.Id, new SupportTicketUpdateInput { Status = "InProgress", Note = "Keep this operator note", RowVersion = "%%%invalid" }, default));
  var page = Assert.IsType<SupportTicketDetailPage>(result.Model);
  Assert.Equal("Keep this operator note", page.Update.Note); Assert.Equal(Convert.ToBase64String(ticket.RowVersion), page.Update.RowVersion);
  Assert.False(controller.ModelState.IsValid); Assert.Equal("Open", page.Detail.Ticket.Status); Assert.Single(await f.Db.SupportTicketActivities.ToListAsync());
 }
 private sealed class FailActivitySave : SaveChangesInterceptor
 {
  public bool Enabled { get; set; } = true;
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
  {
   if (Enabled && eventData.Context!.ChangeTracker.Entries<SupportTicketActivity>().Any(x => x.State == EntityState.Added)) throw new InvalidOperationException("Simulated activity storage failure");
   return ValueTask.FromResult(result);
  }
 }
}
