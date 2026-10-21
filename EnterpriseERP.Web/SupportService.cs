using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed record CreateTicketRequest(string TicketNumber, string Subject, string Description, string ContactName, string? ContactPhone, string Priority);
public sealed record UpdateTicketRequest(string Status, string? AssignedTo, string Note, byte[] RowVersion, bool Reopen = false);
public sealed record SupportAssignee(string Id, string Name);
public sealed record SupportTicketDetail(SupportTicket Ticket, IReadOnlyList<SupportTicketActivity> History, IReadOnlyList<SupportAssignee> Assignees);

public sealed class SupportService(AppDbContext db, TenantContext tenant, PermissionService permissions)
{
 public static IReadOnlyList<string> AllowedTransitions(string status) => status switch
 {
  "Open" => new[] { "Open", "InProgress", "WaitingCustomer", "Resolved" },
  "InProgress" => new[] { "InProgress", "WaitingCustomer", "Resolved" },
  "WaitingCustomer" => new[] { "WaitingCustomer", "InProgress", "Resolved" },
  "Resolved" => new[] { "Resolved", "Closed" },
  "Closed" => Array.Empty<string>(),
  _ => Array.Empty<string>()
 };
 public async Task<long> Create(CreateTicketRequest request, CancellationToken ct)
 {
  await permissions.Demand("CustomerCare.Create", ct); var t = await tenant.GetAsync(ct);
  if (string.IsNullOrWhiteSpace(request.TicketNumber) || request.TicketNumber.Length > 64 || string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 200
   || string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 8000 || string.IsNullOrWhiteSpace(request.ContactName) || request.ContactName.Length > 200
   || (request.ContactPhone?.Length ?? 0) > 40 || !new[] { "Low", "Normal", "High", "Critical" }.Contains(request.Priority)) throw new ArgumentException("Invalid ticket input");
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  var number = request.TicketNumber.Trim();
  if (await db.SupportTickets.AnyAsync(x => x.CompanyId == t.company && x.TicketNumber == number, ct)) throw new InvalidOperationException("Ticket number already exists in this company");
  var ticket = new SupportTicket { CompanyId = t.company, BranchId = t.branch, TicketNumber = number, Subject = request.Subject.Trim(), Description = request.Description.Trim(), ContactName = request.ContactName.Trim(), ContactPhone = request.ContactPhone?.Trim(), Priority = request.Priority, CreatedBy = t.user };
  db.SupportTickets.Add(ticket); await db.SaveChangesAsync(ct);
  db.SupportTicketActivities.Add(new() { CompanyId = t.company, TicketId = ticket.Id, ActorId = t.user, Action = "Created", Note = "Ticket opened" });
  await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return ticket.Id;
 }
 public async Task<object> List(string? status, int page, CancellationToken ct)
 {
  await permissions.Demand("CustomerCare.View", ct); var t = await tenant.GetAsync(ct); page = Math.Clamp(page, 1, 10000);
  var query = db.SupportTickets.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
  if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
  var total = await query.CountAsync(ct);
  var rows = await query.OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id).Skip((page - 1) * 25).Take(25)
   .Select(x => new { x.Id, x.TicketNumber, x.Subject, x.Priority, x.Status, x.AssignedTo, x.CreatedUtc, x.RowVersion }).ToListAsync(ct);
  return new { total, page, rows };
 }
 public async Task<SupportTicketDetail> Detail(long id, CancellationToken ct = default)
 {
  await permissions.Demand("CustomerCare.View", ct); var t = await tenant.GetAsync(ct);
  var ticket = await db.SupportTickets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new KeyNotFoundException("Ticket not found");
  var history = await db.SupportTicketActivities.AsNoTracking().Where(x => x.CompanyId == t.company && x.TicketId == id).OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id).ToListAsync(ct);
  var assignees = await (from branch in db.UserBranchAccess.AsNoTracking()
   join user in db.Users.AsNoTracking() on branch.UserId equals user.Id
   where branch.CompanyId == t.company && branch.BranchId == t.branch && db.UserCompanyAccess.Any(company => company.UserId == user.Id && company.CompanyId == t.company)
   orderby user.Email select new SupportAssignee(user.Id, user.Email ?? user.UserName ?? user.Id)).ToListAsync(ct);
  return new(ticket, history, assignees);
 }
 public async Task Update(long id, UpdateTicketRequest request, CancellationToken ct)
 {
  await permissions.Demand("CustomerCare.Update", ct); var t = await tenant.GetAsync(ct);
  if (string.IsNullOrWhiteSpace(request.Status) || string.IsNullOrWhiteSpace(request.Note) || request.Note.Length > 2000 || request.RowVersion is null || request.RowVersion.Length == 0 || request.RowVersion.Length > 64)
   throw new ArgumentException("Choose a status and provide an update note (max 2000 characters) and the current record version");
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  var ticket = await db.SupportTickets.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new KeyNotFoundException("Ticket not found");
  if (!ticket.RowVersion.SequenceEqual(request.RowVersion)) throw new DbUpdateConcurrencyException("Ticket changed; reload and review before updating");
  db.Entry(ticket).Property(x => x.RowVersion).OriginalValue = request.RowVersion;
  if (request.Reopen)
  {
   if (ticket.Status is not ("Resolved" or "Closed") || request.Status != "Open") throw new InvalidOperationException("Only resolved or closed tickets can be explicitly reopened to Open");
  }
  else if (!AllowedTransitions(ticket.Status).Contains(request.Status))
   throw new InvalidOperationException(ticket.Status == "Closed" ? "Closed tickets cannot be edited. Reopen the ticket explicitly first." : "Status transition is not allowed. Resolve a ticket before closing it.");
  var assignee = string.IsNullOrWhiteSpace(request.AssignedTo) ? null : request.AssignedTo.Trim();
  if (assignee != null && (!await db.Users.AnyAsync(x => x.Id == assignee, ct)
   || !await db.UserCompanyAccess.AnyAsync(x => x.CompanyId == t.company && x.UserId == assignee, ct)
   || !await db.UserBranchAccess.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.UserId == assignee, ct)))
   throw new ArgumentException("Assignee must be authorized for this company and branch");
  var previousStatus = ticket.Status;
  ticket.Status = request.Status; ticket.AssignedTo = assignee; ticket.UpdatedUtc = DateTimeOffset.UtcNow;
  db.SupportTicketActivities.Add(new() { CompanyId = t.company, TicketId = id, ActorId = t.user, Action = request.Reopen ? "Reopened" : previousStatus == request.Status ? "Updated" : "StatusChanged", Note = previousStatus == request.Status ? request.Note.Trim() : previousStatus + " → " + request.Status + ": " + request.Note.Trim() });
  await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
 }
 public async Task<object> History(long id, CancellationToken ct)
 {
  var detail = await Detail(id, ct);
  return detail.History.Select(x => new { x.Action, x.Note, x.ActorId, x.CreatedUtc }).ToList();
 }
}
