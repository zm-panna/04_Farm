using System.Security.Claims;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class TenantSecurityTests
{
 [Fact] public async Task TenantIgnoresSpoofedHeaders()
 {
  await using var f = await SecurityTestScope.CreateAsync();
  f.Http.Request.Headers["X-Company-Id"] = "2"; f.Http.Request.Headers["X-Branch-Id"] = "21";
  Assert.Equal((1, 11, "operator-a"), await f.Tenant.GetAsync());
 }
 [Fact] public async Task UnauthenticatedClaimsNeverAuthorizeTenant()
 { await using var f = await SecurityTestScope.CreateAsync(); f.ActAs(authenticated: false); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Tenant.GetAsync()); }
 [Theory] [InlineData(1, 12)] [InlineData(1, 21)] [InlineData(2, 21)] [InlineData(0, 11)]
 public async Task UnassignedOrMismatchedContextIsDenied(int company, int branch)
 { await using var f = await SecurityTestScope.CreateAsync(); f.ActAs(company: company, branch: branch); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Tenant.GetAsync()); }
 [Fact] public async Task DuplicateContextClaimsAreRejected()
 {
  await using var f = await SecurityTestScope.CreateAsync(); ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim("ActiveCompanyId", "2"));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Tenant.GetAsync());
 }
 [Fact] public async Task RevokedBranchAssignmentImmediatelyDeniesExistingSession()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.Tenant.GetAsync();
  f.Db.UserBranchAccess.Remove(await f.Db.UserBranchAccess.SingleAsync(x => x.UserId == "operator-a")); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Tenant.GetAsync());
 }
 [Fact] public async Task DeletedAccountCannotReuseStaleTenantClaims()
 {
  await using var f = await SecurityTestScope.CreateAsync(); f.Db.Users.Remove(await f.Db.Users.SingleAsync(x => x.Id == "operator-a")); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Tenant.GetAsync());
 }
 [Fact] public async Task WildcardIsCompanyScoped()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*", company: 2);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Permissions.Demand("Livestock.Create"));
  await f.GrantAsync("*"); await f.Permissions.Demand("Livestock.Create");
 }
 [Fact] public async Task PermissionRevocationTakesEffectWithoutNewLogin()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Reports.View"); await f.Permissions.Demand("Reports.View");
  f.Db.UserPermissions.Remove(await f.Db.UserPermissions.SingleAsync()); await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Permissions.Demand("Reports.View"));
 }
 [Fact] public async Task CompanyAdminCannotGrantToAnotherCompanyUser()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Permissions.Manage");
  var controller = new RoleManagementController(f.Db, f.Tenant, f.Permissions, f.Users);
  Assert.IsType<BadRequestObjectResult>(await controller.Grant("operator-b", "Purchase.Post", default));
  Assert.False(await f.Db.UserPermissions.AnyAsync(x => x.UserId == "operator-b"));
 }
 [Theory] [InlineData("*")] [InlineData("Permissions.Manage")]
 public async Task RecoveryPrivilegesCannotBeGrantedOrRevoked(string reserved)
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
  if (reserved != "*") await f.GrantAsync(reserved);
  var controller = new RoleManagementController(f.Db, f.Tenant, f.Permissions, f.Users);
  Assert.IsType<BadRequestObjectResult>(await controller.Grant("approver-a", reserved, default));
  var row = await f.Db.UserPermissions.SingleAsync(x => x.Permission == reserved);
  Assert.IsType<BadRequestObjectResult>(await controller.Revoke(row.Id, default));
  Assert.True(await f.Db.UserPermissions.AnyAsync(x => x.Id == row.Id));
 }
 [Fact] public async Task PermissionGrantIsIdempotentAndAudited()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Permissions.Manage");
  var controller = new RoleManagementController(f.Db, f.Tenant, f.Permissions, f.Users);
  await controller.Grant("approver-a", "Purchase.Post", default); await controller.Grant("approver-a", "Purchase.Post", default);
  Assert.Equal(1, await f.Db.UserPermissions.CountAsync(x => x.UserId == "approver-a" && x.Permission == "Purchase.Post"));
  Assert.Equal(1, await f.Db.Audits.CountAsync(x => x.Action == "PermissionGranted"));
 }
 [Fact] public void GrantCatalogMatchesRealOperationsAndExcludesObsoleteNames()
 {
  foreach (var name in new[] { "Purchase.View", "Purchase.Post", "Sales.View", "Sales.Post", "AI.Read", "AI.Chat", "Automation.Review", "Automation.Request", "Automation.Approve", "Livestock.Create", "Dairy.Create", "AnimalHealth.Create", "FeedManagement.Create" }) Assert.Contains(name, PermissionService.GrantablePermissions);
  foreach (var name in new[] { "Purchase.Create", "Sales.Create", "Automation.View", "*", "Permissions.Manage", "ops.readiness" }) Assert.DoesNotContain(name, PermissionService.GrantablePermissions);
 }
 [Fact] public void TenantSelectorHasExplicitAuthorization()
 { Assert.NotEmpty(typeof(ContextController).GetCustomAttributes(typeof(AuthorizeAttribute), true)); }
}
