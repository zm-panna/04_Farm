using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public record SubscriptionState(decimal Due, int DaysPastDue, string Status);
public sealed class SubscriptionService(AppDbContext db, TenantContext context, PermissionService permission, IHttpContextAccessor accessor)
{
 public async Task<SubscriptionState> State(DateOnly today, CancellationToken ct = default)
 {
  var t = await context.GetAsync(ct);
  var invoices = await db.SubscriptionInvoices.AsNoTracking()
   .Where(x => x.CompanyId == t.company && !x.Cancelled && x.Amount > x.Paid)
   .OrderBy(x => x.DueDate).ToListAsync(ct);
  var policy = await db.CompanySubscriptionPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company, ct);
  return Evaluate(invoices, policy, today);
 }

 public static SubscriptionState Evaluate(IEnumerable<SubscriptionInvoice> invoices, CompanySubscriptionPolicy? policy, DateOnly today)
 {
  var unpaid = invoices.Where(x => !x.Cancelled && x.Amount > x.Paid).ToArray();
  if (unpaid.Length == 0) return new(0, 0, "Current");
  var age = Math.Max(0, today.DayNumber - unpaid.Min(x => x.DueDate).DayNumber);
  var suspendDays = Math.Max(1, policy?.SuspendDays ?? 30);
  var graceDays = Math.Max(0, policy?.GraceDays ?? 0);
  var noticeDays = Math.Clamp(policy?.NoticeDays ?? 1, 1, suspendDays);
  var alertDays = Math.Clamp(policy?.AlertDays ?? 15, noticeDays, suspendDays);
  // Widen before adding: invalid, very large persisted policies must not overflow into suspension.
  var suspended = age >= (long)suspendDays + graceDays && !(policy?.OverrideUntil >= today);
  return new(unpaid.Sum(x => x.Amount - x.Paid), age,
   suspended ? "Suspended" : age >= alertDays ? "Alert" : age >= noticeDays ? "Notice" : "Current");
 }

 public async Task RecordPayment(long invoiceId, string reference, decimal amount, CancellationToken ct)
 {
  await permission.Demand("Subscription.Pay", ct);
  var principal = accessor.HttpContext?.User;
  if (principal?.Identity?.IsAuthenticated != true || !(principal.IsInRole("PlatformAdmin") || principal.HasClaim("permission", "platform.billing")))
   throw new UnauthorizedAccessException("Recording received subscription payments requires platform billing authority");
  var t = await context.GetAsync(ct);
  reference = reference?.Trim() ?? "";
  if (invoiceId <= 0 || amount <= 0 || amount > 999999999999999.999m || decimal.Round(amount, 3) != amount || reference.Length == 0 || reference.Length > 100)
   throw new ArgumentException("Invoice, payment reference (max 100 characters), and positive amount with at most 3 decimal places are required");
  await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, amount, ct);
  if (await db.SubscriptionPayments.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct))
   throw new InvalidOperationException("Duplicate payment");
  var inv = await db.SubscriptionInvoices.SingleOrDefaultAsync(x => x.Id == invoiceId && x.CompanyId == t.company && !x.Cancelled, ct)
   ?? throw new ArgumentException("Invoice unavailable");
  if (inv.Amount - inv.Paid < amount) throw new InvalidOperationException("Overpayment");
  try { await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, inv.Amount - inv.Paid, ct); }
  catch (ArgumentException) { throw new InvalidOperationException("The historical subscription balance cannot be represented in the reviewed currency precision. An authorized reviewed correction is required before recording payment; no amount was changed."); }
  inv.Paid += amount;
  db.SubscriptionPayments.Add(new() { CompanyId = t.company, InvoiceId = invoiceId, Reference = reference, Amount = amount });
  db.Audits.Add(new() { CompanyId = t.company, ActorId = t.user, Action = "SubscriptionPaymentRecorded", Reference = reference });
  await db.SaveChangesAsync(ct);
  await tx.CommitAsync(ct);
 }
}
