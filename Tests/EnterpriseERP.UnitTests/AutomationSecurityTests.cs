using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class AutomationSecurityTests
{
 private static async Task<(AutomationApprovalService Service, AutomationRule Rule, long ApprovalId)> Pending(SecurityTestScope f)
 {
  await f.GrantAsync("Automation.Request"); await f.GrantAsync("Automation.Approve"); await f.GrantAsync("Automation.Approve", "approver-a");
  var rule = new AutomationRule { CompanyId = 1, Name = "Review stock", Trigger = "Manual", Action = "Review", Active = true };
  f.Db.AutomationRules.Add(rule); await f.Db.SaveChangesAsync();
  var service = new AutomationApprovalService(f.Db, f.Tenant, f.Permissions);
  return (service, rule, await service.Request(rule.Id, "Review item 100", default));
 }
 [Fact] public async Task RequesterCannotApproveOwnProposal()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var p = await Pending(f);
  await Assert.ThrowsAsync<InvalidOperationException>(() => p.Service.Approve(p.ApprovalId, "Review item 100", default));
  Assert.Equal("Pending", (await f.Db.AutomationApprovals.AsNoTracking().SingleAsync()).Status);
 }
 [Fact] public async Task ChangedPayloadRequiresNewApproval()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var p = await Pending(f); f.ActAs("approver-a");
  await Assert.ThrowsAsync<InvalidOperationException>(() => p.Service.Approve(p.ApprovalId, "Pay supplier 100", default));
 }
 [Fact] public async Task ChangedRuleBehaviorInvalidatesOldProposal()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var p = await Pending(f); p.Rule.Action = "DifferentAction"; await f.Db.SaveChangesAsync(); f.ActAs("approver-a");
  await Assert.ThrowsAsync<InvalidOperationException>(() => p.Service.Approve(p.ApprovalId, "Review item 100", default));
 }
 [Fact] public async Task DisabledRuleCannotBeApproved()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var p = await Pending(f); p.Rule.Active = false; await f.Db.SaveChangesAsync(); f.ActAs("approver-a");
  await Assert.ThrowsAsync<InvalidOperationException>(() => p.Service.Approve(p.ApprovalId, "Review item 100", default));
 }
 [Fact] public async Task ApprovalIsSingleUseAndCannotExecuteBusinessActions()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var p = await Pending(f); f.ActAs("approver-a");
  await p.Service.Approve(p.ApprovalId, "Review item 100", default);
  await Assert.ThrowsAsync<InvalidOperationException>(() => p.Service.Approve(p.ApprovalId, "Review item 100", default));
  var approval = await f.Db.AutomationApprovals.AsNoTracking().SingleAsync();
  Assert.Equal("Approved", approval.Status); Assert.Equal("approver-a", approval.ApproverId);
  Assert.Empty(await f.Db.AutomationExecutions.ToListAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
  Assert.Equal(1, await f.Db.Audits.CountAsync(x => x.Action == "AutomationApprovedForReview"));
  Assert.DoesNotContain(await f.Db.Audits.ToListAsync(), x => x.Reference.Contains("Review item 100"));
 }
 [Fact] public async Task ApprovalCannotCrossCompanyBoundary()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var p = await Pending(f); await f.GrantAsync("Automation.Approve", "operator-b", 2); f.ActAs("operator-b", 2, 21);
  await Assert.ThrowsAsync<ArgumentException>(() => p.Service.Approve(p.ApprovalId, "Review item 100", default));
 }
 [Theory] [InlineData("")] [InlineData(" ")]
 public async Task EmptyProposalIsRejectedWithoutRecord(string payload)
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Automation.Request");
  await Assert.ThrowsAsync<ArgumentException>(() => new AutomationApprovalService(f.Db, f.Tenant, f.Permissions).Request(1, payload, default));
  Assert.Empty(await f.Db.AutomationApprovals.ToListAsync());
 }
 [Fact] public async Task RequestCannotUseOtherCompanyRule()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Automation.Request");
  var rule = new AutomationRule { CompanyId = 2, Name = "Private", Active = true }; f.Db.AutomationRules.Add(rule); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<ArgumentException>(() => new AutomationApprovalService(f.Db, f.Tenant, f.Permissions).Request(rule.Id, "Review", default));
 }
}
