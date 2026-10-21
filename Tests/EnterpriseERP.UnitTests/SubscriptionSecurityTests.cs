using System.Security.Claims;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class SubscriptionSecurityTests
{
 private static void PlatformBilling(SecurityTestScope f) => ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim("permission", "platform.billing"));
 private static readonly DateOnly Today = new(2026, 10, 19);
 private static SubscriptionInvoice Invoice(int days, decimal amount = 100, decimal paid = 0) => new() { CompanyId = 1, Reference = "BILL", DueDate = Today.AddDays(-days), Amount = amount, Paid = paid };
 [Theory] [InlineData(0, "Current")] [InlineData(1, "Notice")] [InlineData(14, "Notice")] [InlineData(15, "Alert")] [InlineData(29, "Alert")] [InlineData(30, "Suspended")]
 public void DefaultPolicyHonorsExactDayBoundaries(int age, string expected)
 { var state = SubscriptionService.Evaluate(new[] { Invoice(age) }, null, Today); Assert.Equal(expected, state.Status); Assert.Equal(age, state.DaysPastDue); }
 [Theory] [InlineData(4, "Current")] [InlineData(5, "Notice")] [InlineData(9, "Notice")] [InlineData(10, "Alert")] [InlineData(22, "Alert")] [InlineData(23, "Suspended")]
 public void ConfiguredNoticeAlertAndGraceAreUsed(int age, string expected)
 {
  var policy = new CompanySubscriptionPolicy { NoticeDays = 5, AlertDays = 10, SuspendDays = 20, GraceDays = 3 };
  Assert.Equal(expected, SubscriptionService.Evaluate(new[] { Invoice(age) }, policy, Today).Status);
 }
 [Fact] public void OverrideIsInclusiveButDoesNotHideTheDebt()
 {
  var policy = new CompanySubscriptionPolicy { OverrideUntil = Today };
  Assert.Equal(new SubscriptionState(75, 40, "Alert"), SubscriptionService.Evaluate(new[] { Invoice(40, paid: 25) }, policy, Today));
  Assert.Equal("Suspended", SubscriptionService.Evaluate(new[] { Invoice(40) }, policy, Today.AddDays(1)).Status);
 }
 [Fact] public void CancelledAndPaidInvoicesDoNotSuspend()
 {
  var cancelled = Invoice(60); cancelled.Cancelled = true;
  Assert.Equal(new SubscriptionState(0, 0, "Current"), SubscriptionService.Evaluate(new[] { cancelled, Invoice(60, paid: 100) }, null, Today));
 }
 [Fact] public void FutureInvoiceHasZeroOverdueDays()
 { Assert.Equal(new SubscriptionState(100, 0, "Current"), SubscriptionService.Evaluate(new[] { Invoice(-7) }, null, Today)); }
 [Fact] public void InvalidLargePolicyCannotOverflowIntoSuspension()
 { Assert.Equal("Alert", SubscriptionService.Evaluate(new[] { Invoice(40) }, new() { SuspendDays = int.MaxValue, GraceDays = int.MaxValue }, Today).Status); }
 [Fact] public async Task OtherCompanyOverdueInvoiceCannotSuspendActiveCompany()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var invoice = Invoice(90); invoice.CompanyId = 2; f.Db.SubscriptionInvoices.Add(invoice); await f.Db.SaveChangesAsync();
  Assert.Equal(new SubscriptionState(0, 0, "Current"), await new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor).State(Today));
 }
 [Fact] public async Task PaymentCannotTargetOtherCompany()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Subscription.Pay"); PlatformBilling(f); var invoice = Invoice(40); invoice.CompanyId = 2; f.Db.SubscriptionInvoices.Add(invoice); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<ArgumentException>(() => new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor).RecordPayment(invoice.Id, "P1", 10, default));
  Assert.Equal(0, await f.Db.SubscriptionPayments.CountAsync()); Assert.Equal(0, invoice.Paid);
 }
 [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(0.0001)]
 public async Task InvalidPaymentAmountsDoNotWrite(decimal amount)
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Subscription.Pay"); PlatformBilling(f); var invoice = Invoice(40); f.Db.SubscriptionInvoices.Add(invoice); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<ArgumentException>(() => new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor).RecordPayment(invoice.Id, "P1", amount, default));
  Assert.Empty(await f.Db.SubscriptionPayments.ToListAsync());
 }
 [Fact] public async Task PaymentRejectsOverpaymentAndNormalizedDuplicateReference()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Subscription.Pay"); PlatformBilling(f); var invoice = Invoice(40); f.Db.SubscriptionInvoices.Add(invoice); await f.Db.SaveChangesAsync();
  var service = new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor);
  await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordPayment(invoice.Id, "P1", 101, default));
  await service.RecordPayment(invoice.Id, " P1 ", 40, default);
  await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordPayment(invoice.Id, "P1", 40, default));
  Assert.Equal(40, (await f.Db.SubscriptionInvoices.AsNoTracking().SingleAsync()).Paid);
  Assert.Equal(1, await f.Db.SubscriptionPayments.CountAsync()); Assert.Equal(1, await f.Db.Audits.CountAsync(x => x.Action == "SubscriptionPaymentRecorded"));
 }
 [Fact] public async Task CompanyWildcardCannotSelfCertifySubscriptionPayment()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var invoice = Invoice(40); f.Db.SubscriptionInvoices.Add(invoice); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor).RecordPayment(invoice.Id, "SELF-PAY", 100, default));
  Assert.Equal(0, invoice.Paid); Assert.Empty(await f.Db.SubscriptionPayments.ToListAsync());
  Assert.DoesNotContain("Subscription.Pay", PermissionService.GrantablePermissions);
 }
 [Fact] public async Task PlatformBillingClaimStillRequiresTenantPaymentPermission()
 {
  await using var f = await SecurityTestScope.CreateAsync(); PlatformBilling(f); var invoice = Invoice(40); f.Db.SubscriptionInvoices.Add(invoice); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor).RecordPayment(invoice.Id, "P1", 100, default));
 }
 [Fact] public async Task PlatformAdminCanRecordAuditedReceivedPaymentWithinAuthorizedTenant()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Subscription.Pay"); ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, "PlatformAdmin"));
  var invoice = Invoice(40); f.Db.SubscriptionInvoices.Add(invoice); await f.Db.SaveChangesAsync();
  await new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor).RecordPayment(invoice.Id, "PROVIDER-RECONCILED", 100, default);
  Assert.Equal(100, invoice.Paid); Assert.Equal("Current", (await new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor).State(Today)).Status);
 }
 [Theory]
 [InlineData("GET", "/Reports", true)] [InlineData("GET", "/Reports/export", true)] [InlineData("POST", "/Reports/export", false)]
 [InlineData("GET", "/api/reports-other", false)] [InlineData("GET", "/Contextual", false)] [InlineData("GET", "/api/operations/subscription-extra", false)]
 [InlineData("GET", "/api/operations/summary", true)] [InlineData("POST", "/api/operations/sale", false)]
 [InlineData("POST", "/api/operations/subscription/payment", true)] [InlineData("POST", "/Subscription/payment", true)]
 [InlineData("GET", "/Support", true)] [InlineData("POST", "/Support/create", true)] [InlineData("POST", "/Subscription/other-action", false)]
 public async Task SuspensionExemptionsRespectSegmentAndMethodBoundaries(string method, string path, bool permitted)
 {
  await using var f = await SecurityTestScope.CreateAsync(); var invoice = Invoice(90); invoice.DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90); f.Db.SubscriptionInvoices.Add(invoice); await f.Db.SaveChangesAsync();
  f.Http.Request.Method = method; f.Http.Request.Path = path; var nextRan = false;
  var middleware = new SubscriptionGuardMiddleware(_ => { nextRan = true; return Task.CompletedTask; });
  await middleware.Invoke(f.Http, new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor));
  Assert.Equal(permitted, nextRan); Assert.Equal(permitted ? 200 : 402, f.Http.Response.StatusCode);
 }
}
