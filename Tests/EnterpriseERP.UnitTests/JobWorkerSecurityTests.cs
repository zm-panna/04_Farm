using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class JobWorkerSecurityTests
{
 private static JobWorker Worker(SecurityTestScope f) => new(f.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance, TestRuntimeGate.Available);
 private static async Task<AutomationRule> Rule(SecurityTestScope f, string action = "InAppAlert", string trigger = "Manual", int company = 1)
 {
  var rule = new AutomationRule { CompanyId = company, Name = "Scheduled review", Action = action, Trigger = trigger, Active = true };
  f.Db.AutomationRules.Add(rule); await f.Db.SaveChangesAsync(); return rule;
 }
 private static ScheduledJob Job(AutomationRule rule, string key = "EXEC-1", int? company = null) => new()
 { CompanyId = company ?? rule.CompanyId, RuleId = rule.Id, ExecutionKey = key, DueUtc = DateTimeOffset.UtcNow.AddMinutes(-5), Status = "Pending" };
 [Fact] public async Task NotificationWorkerIsTenantScopedAndIdempotent()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var rule = await Rule(f); var job = Job(rule); f.Db.ScheduledJobs.Add(job); await f.Db.SaveChangesAsync();
  Assert.Equal(1, await Worker(f).ReviewDueJobsAsync()); Assert.Equal(0, await Worker(f).ReviewDueJobsAsync());
  var notifications = await f.Db.Notifications.AsNoTracking().ToListAsync();
  Assert.Equal(2, notifications.Count); Assert.All(notifications, x => Assert.Equal(1, x.CompanyId));
  Assert.Equal(new[] { 11, 12 }, notifications.Select(x => x.BranchId).OrderBy(x => x).ToArray());
  Assert.Equal(1, await f.Db.AutomationExecutions.CountAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
  Assert.Equal("Completed", (await f.Db.ScheduledJobs.AsNoTracking().SingleAsync()).Status);
 }
 [Fact] public async Task ReviewRuleNeverExecutesDomainWrites()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var rule = await Rule(f, action: "Review"); f.Db.ScheduledJobs.Add(Job(rule)); await f.Db.SaveChangesAsync();
  await Worker(f).ReviewDueJobsAsync(); Assert.Equal("ReviewRequired", (await f.Db.ScheduledJobs.AsNoTracking().SingleAsync()).Status);
  Assert.Empty(await f.Db.AutomationExecutions.ToListAsync()); Assert.Empty(await f.Db.Notifications.ToListAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
 }
 [Fact] public async Task RuleCompanyMismatchIsRejected()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var rule = await Rule(f, company: 2); f.Db.ScheduledJobs.Add(Job(rule, company: 1));
  await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
  f.Db.ChangeTracker.Clear();
  Assert.Empty(await f.Db.ScheduledJobs.ToListAsync()); Assert.Empty(await f.Db.AutomationExecutions.ToListAsync()); Assert.Empty(await f.Db.Notifications.ToListAsync());
 }
 [Fact] public async Task FutureAndLockedJobsStayPending()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var rule = await Rule(f);
  var future = Job(rule, "FUTURE"); future.DueUtc = DateTimeOffset.UtcNow.AddHours(1);
  var locked = Job(rule, "LOCKED"); locked.LockedUntilUtc = DateTimeOffset.UtcNow.AddHours(1);
  f.Db.ScheduledJobs.AddRange(future, locked); await f.Db.SaveChangesAsync();
  Assert.Equal(0, await Worker(f).ReviewDueJobsAsync()); Assert.All(await f.Db.ScheduledJobs.AsNoTracking().ToListAsync(), x => Assert.Equal("Pending", x.Status));
 }
 [Theory] [InlineData("ExecuteSql")] [InlineData("PaySupplier")] [InlineData("SendEmail")]
 public async Task UnrecognizedActionsNeverExecute(string action)
 {
  await using var f = await SecurityTestScope.CreateAsync(); var rule = await Rule(f, action); f.Db.ScheduledJobs.Add(Job(rule)); await f.Db.SaveChangesAsync();
  await Worker(f).ReviewDueJobsAsync(); Assert.Equal("ReviewRequired", (await f.Db.ScheduledJobs.AsNoTracking().SingleAsync()).Status);
  Assert.Empty(await f.Db.AutomationExecutions.ToListAsync()); Assert.Empty(await f.Db.Notifications.ToListAsync());
 }
 [Fact] public async Task LowStockNotificationDoesNotIncludeAnotherCompanyOrHealthyStock()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var rule = await Rule(f, trigger: "LowStock"); f.Db.ScheduledJobs.Add(Job(rule));
  f.Db.StockItems.AddRange(new StockItem { CompanyId = 1, BranchId = 11, SKU = "LOW", QuantityOnHand = 5 }, new StockItem { CompanyId = 1, BranchId = 12, SKU = "HEALTHY", QuantityOnHand = 6 }, new StockItem { CompanyId = 2, BranchId = 21, SKU = "PRIVATE", QuantityOnHand = 0 });
  await f.Db.SaveChangesAsync(); await Worker(f).ReviewDueJobsAsync();
  var notification = Assert.Single(await f.Db.Notifications.AsNoTracking().ToListAsync()); Assert.Equal(1, notification.CompanyId); Assert.Equal(11, notification.BranchId);
  Assert.Contains("1 stock items", notification.Message);
 }
 [Fact] public async Task UnavailableTrustedRuntimeLeavesDueJobsUntouched()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var rule = await Rule(f); f.Db.ScheduledJobs.Add(Job(rule)); await f.Db.SaveChangesAsync();
  var worker = new JobWorker(f.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance, new TestRuntimeGate { IsAvailable = false });
  await Assert.ThrowsAsync<TrustedInfrastructure.TrustedVaultUnavailableException>(() => worker.ReviewDueJobsAsync());
  Assert.Equal("Pending", await f.Db.ScheduledJobs.Select(x => x.Status).SingleAsync()); Assert.Empty(await f.Db.Notifications.ToListAsync());
 }
}
