using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseERP.Web;
using EnterpriseERP.Domain;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class MfaSecurityTests
{
 private const string Password = "SyntheticPassword123!";
 private static AccountSecurityService Security(SecurityTestScope f) => f.Services.GetRequiredService<AccountSecurityService>();
 private static async Task<AppUser> UserWithPassword(SecurityTestScope f, string id = "operator-a")
 {
  var user = (await f.Users.FindByIdAsync(id))!;
  if (!await f.Users.HasPasswordAsync(user)) Assert.True((await f.Users.AddPasswordAsync(user, Password)).Succeeded);
  return user;
 }
 private static async Task<(AppUser User, string Key, IReadOnlyList<string> Codes)> Enabled(SecurityTestScope f)
 {
  var user = await UserWithPassword(f); var enrollment = await Security(f).BeginEnrollment(Password);
  var codes = await Security(f).CompleteEnrollment(new() { CurrentPassword = Password, Code = Totp(enrollment.SharedKey), Proof = enrollment.Proof });
  return (user, enrollment.SharedKey, codes);
 }
 [Fact] public async Task EnrollmentRequiresCurrentPasswordAndDoesNotEnableMfaYet()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var user = await UserWithPassword(f);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).BeginEnrollment("wrong-password"));
  Assert.Null(await f.Users.GetAuthenticatorKeyAsync(user));
  var enrollment = await Security(f).BeginEnrollment(Password);
  Assert.NotEmpty(enrollment.SharedKey); Assert.False(await f.Users.GetTwoFactorEnabledAsync(user)); Assert.Equal(0, await f.Users.CountRecoveryCodesAsync(user));
 }
 [Fact] public async Task EnrollmentRequiresValidTotpAndProvidesTenOneTimeRecoveryCodes()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var user = await UserWithPassword(f); var enrollment = await Security(f).BeginEnrollment(Password);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).CompleteEnrollment(new() { CurrentPassword = Password, Code = "invalid", Proof = enrollment.Proof }));
  Assert.False(await f.Users.GetTwoFactorEnabledAsync(user));
  var codes = await Security(f).CompleteEnrollment(new() { CurrentPassword = Password, Code = Totp(enrollment.SharedKey), Proof = enrollment.Proof });
  Assert.True(await f.Users.GetTwoFactorEnabledAsync(user)); Assert.Equal(10, codes.Count); Assert.Equal(10, await f.Users.CountRecoveryCodesAsync(user));
  Assert.True((await f.Users.RedeemTwoFactorRecoveryCodeAsync(user, codes[0])).Succeeded);
  Assert.False((await f.Users.RedeemTwoFactorRecoveryCodeAsync(user, codes[0])).Succeeded); Assert.Equal(9, await f.Users.CountRecoveryCodesAsync(user));
 }
 [Fact] public async Task EnrollmentProofCannotCrossAccountOrSurviveKeyRestart()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await UserWithPassword(f); var first = await Security(f).BeginEnrollment(Password);
  f.ActAs("approver-a"); await Assert.ThrowsAsync<InvalidOperationException>(() => Security(f).PendingEnrollment(first.Proof));
  f.ActAs(); await Security(f).BeginEnrollment(Password);
  await Assert.ThrowsAsync<InvalidOperationException>(() => Security(f).PendingEnrollment(first.Proof));
 }
 [Fact] public async Task ExpiredEnrollmentProofIsRejected()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var user = await UserWithPassword(f); var enrollment = await Security(f).BeginEnrollment(Password);
  var protector = f.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("FarmERP.AuthenticatorEnrollment.v1");
  var proof = protector.Protect(JsonSerializer.Serialize(new { UserId = user.Id, Stamp = await f.Users.GetSecurityStampAsync(user), KeyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(enrollment.SharedKey))), ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1).UtcTicks }));
  await Assert.ThrowsAsync<InvalidOperationException>(() => Security(f).PendingEnrollment(proof));
 }
 [Fact] public async Task EnabledAuthenticatorCannotBeReplacedWithoutDisable()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var enabled = await Enabled(f);
  await Assert.ThrowsAsync<InvalidOperationException>(() => Security(f).BeginEnrollment(Password));
  Assert.Equal(enabled.Key, await f.Users.GetAuthenticatorKeyAsync(enabled.User));
 }
 [Fact] public async Task DisablingRequiresPasswordAndTotpThenInvalidatesKeyCodesAndStamp()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var enabled = await Enabled(f); var stamp = await f.Users.GetSecurityStampAsync(enabled.User);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).Disable(new() { CurrentPassword = "wrong", Code = Totp(enabled.Key) }));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).Disable(new() { CurrentPassword = Password, Code = "invalid" }));
  Assert.True(await f.Users.GetTwoFactorEnabledAsync(enabled.User));
  await Security(f).Disable(new() { CurrentPassword = Password, Code = Totp(enabled.Key) });
  Assert.False(await f.Users.GetTwoFactorEnabledAsync(enabled.User)); Assert.NotEqual(enabled.Key, await f.Users.GetAuthenticatorKeyAsync(enabled.User));
  Assert.NotEqual(stamp, await f.Users.GetSecurityStampAsync(enabled.User)); Assert.Equal(0, await f.Users.CountRecoveryCodesAsync(enabled.User));
  Assert.False((await f.Users.RedeemTwoFactorRecoveryCodeAsync(enabled.User, enabled.Codes[0])).Succeeded);
 }
 [Fact] public async Task RecoveryCodeRegenerationInvalidatesPreviousCodesAndSessions()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var enabled = await Enabled(f); var stamp = await f.Users.GetSecurityStampAsync(enabled.User);
  var replacement = await Security(f).RegenerateRecoveryCodes(new() { CurrentPassword = Password, Code = Totp(enabled.Key) });
  Assert.Equal(10, replacement.Count); Assert.NotEqual(stamp, await f.Users.GetSecurityStampAsync(enabled.User));
  Assert.False((await f.Users.RedeemTwoFactorRecoveryCodeAsync(enabled.User, enabled.Codes[0])).Succeeded);
  Assert.True((await f.Users.RedeemTwoFactorRecoveryCodeAsync(enabled.User, replacement[0])).Succeeded);
 }
 [Fact] public async Task ReauthenticationFailuresAccumulateAndLockOutNonMfaAccount()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var user = await UserWithPassword(f);
  for (var i = 0; i < 5; i++) await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).VerifyMfaForPasswordChange("wrong", null));
  Assert.True(await f.Users.IsLockedOutAsync(user));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).BeginEnrollment(Password));
 }
 [Fact] public async Task MfaAccountPasswordChangeNeedsAuthenticator()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var enabled = await Enabled(f);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).VerifyMfaForPasswordChange(Password, null));
  await Security(f).VerifyMfaForPasswordChange(Password, Totp(enabled.Key));
 }
 [Fact] public async Task SecurityAuditNeverContainsSecrets()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var enabled = await Enabled(f);
  var audit = string.Join("|", (await f.Db.Audits.ToListAsync()).Select(x => x.Action + x.Reference));
  Assert.Contains("AuthenticatorMfaEnabled", audit); Assert.DoesNotContain(Password, audit); Assert.DoesNotContain(enabled.Key, audit);
  Assert.All(enabled.Codes, code => Assert.DoesNotContain(code, audit));
 }
 [Theory] [InlineData(false)] [InlineData(true)] public async Task PasswordLoginRequiresSecondFactorBeforeApplicationCookie(bool rememberedBrowser)
 {
  var auth = new TestAuthentication(); await using var f = await SecurityTestScope.CreateWithAuthenticationAsync(auth); var enabled = await Enabled(f); f.ActAs(authenticated: false);
  var controller = Controller(f);
  if(rememberedBrowser) auth.Tickets[IdentityConstants.TwoFactorRememberMeScheme] = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,enabled.User.Id)},IdentityConstants.TwoFactorRememberMeScheme)),IdentityConstants.TwoFactorRememberMeScheme);
  var result = Assert.IsType<RedirectToActionResult>(await controller.Login(enabled.User.Email, Password, "/Reports"));
  Assert.Equal(nameof(AccountController.TwoFactor), result.ActionName); Assert.False(auth.Tickets.ContainsKey(IdentityConstants.ApplicationScheme));
  Assert.Equal(await f.Users.GetSecurityStampAsync(enabled.User), auth.Tickets[IdentityConstants.TwoFactorUserIdScheme].Principal.FindFirstValue("FarmMfaStamp"));
  var verified = Assert.IsType<LocalRedirectResult>(await controller.TwoFactor(new TwoFactorLoginInput { Code = Totp(enabled.Key), ReturnUrl = "https://outside.example.invalid" }));
  Assert.Equal("/Context", verified.Url); Assert.True(auth.Tickets.ContainsKey(IdentityConstants.ApplicationScheme));
 }
 [Theory] [InlineData("expired")] [InlineData("stamp-changed")] [InlineData("locked")]
 public async Task RecoveryChallengeCannotSurviveExpiryResetOrLockout(string scenario)
 {
  var auth = new TestAuthentication(); await using var f = await SecurityTestScope.CreateWithAuthenticationAsync(auth); var enabled = await Enabled(f); f.ActAs(authenticated: false);
  var controller = Controller(f); await controller.Login(enabled.User.Email, Password, null);
  if (scenario == "expired") auth.Tickets[IdentityConstants.TwoFactorUserIdScheme].Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
  if (scenario == "stamp-changed") await f.Users.UpdateSecurityStampAsync(enabled.User);
  if (scenario == "locked") { await f.Users.SetLockoutEnabledAsync(enabled.User, true); await f.Users.SetLockoutEndDateAsync(enabled.User, DateTimeOffset.UtcNow.AddMinutes(15)); }
  Assert.IsType<RedirectToActionResult>(await controller.RecoveryLogin(new TwoFactorLoginInput { Code = enabled.Codes[0] }));
  Assert.False(auth.Tickets.ContainsKey(IdentityConstants.ApplicationScheme)); Assert.Equal(10, await f.Users.CountRecoveryCodesAsync(enabled.User));
 }
 [Fact] public async Task RecoveryLoginConsumesCodeOnlyOnceAndCountsFailures()
 {
  var auth = new TestAuthentication(); await using var f = await SecurityTestScope.CreateWithAuthenticationAsync(auth); var enabled = await Enabled(f); f.ActAs(authenticated: false);
  var controller = Controller(f); await controller.Login(enabled.User.Email, Password, null);
  Assert.IsType<LocalRedirectResult>(await controller.RecoveryLogin(new TwoFactorLoginInput { Code = enabled.Codes[0] }));
  await controller.Login(enabled.User.Email, Password, null);
  Assert.IsType<ViewResult>(await controller.RecoveryLogin(new TwoFactorLoginInput { Code = enabled.Codes[0] }));
  Assert.Equal(9, await f.Users.CountRecoveryCodesAsync(enabled.User)); Assert.True(await f.Users.GetAccessFailedCountAsync(enabled.User) > 0);
 }
 [Theory] [InlineData(nameof(AccountController.BeginAuthenticator))] [InlineData(nameof(AccountController.CompleteAuthenticator))] [InlineData(nameof(AccountController.DisableAuthenticator))] [InlineData(nameof(AccountController.RegenerateRecoveryCodes))]
 public void SecurityMutationsRequireAuthorizationPostAndAntiforgery(string action)
 {
  var method = typeof(AccountController).GetMethods().Single(x => x.Name == action);
  Assert.NotEmpty(method.GetCustomAttributes(typeof(AuthorizeAttribute), true)); Assert.NotEmpty(method.GetCustomAttributes(typeof(HttpPostAttribute), true)); Assert.NotEmpty(method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
 }
 [Fact] public async Task LocalRecoveryRejectsTenantWildcardAndForgedPlatformClaim()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var enabled = await Enabled(f); await UserWithPassword(f, "operator-b"); await f.GrantAsync("*");
  ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, "PlatformAdmin"));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).RecoverLocally(new(enabled.User.Email!, Password, Totp(enabled.Key), "operator-b@example.invalid", "RecoveredPassword123!", true, "CASE-RECOVERY-1")));
  Assert.True(await f.Users.CheckPasswordAsync((await f.Users.FindByIdAsync("operator-b"))!, Password));
 }
 [Fact] public async Task PlatformLocalRecoveryRequiresMfaAndPreservesTenantPermissions()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var enabled = await Enabled(f); var target = await UserWithPassword(f, "operator-b");
  var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>(); Assert.True((await roles.CreateAsync(new IdentityRole("PlatformAdmin"))).Succeeded); Assert.True((await f.Users.AddToRoleAsync(enabled.User, "PlatformAdmin")).Succeeded);
  var oldStamp = await f.Users.GetSecurityStampAsync(target); var memberships = await f.Db.UserCompanyAccess.CountAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).RecoverLocally(new(enabled.User.Email!, Password, "invalid", target.Email!, "RecoveredPassword123!", true, "CASE-RECOVERY-1")));
  await Security(f).RecoverLocally(new(enabled.User.Email!, Password, Totp(enabled.Key), target.Email!, "RecoveredPassword123!", true, "CASE-RECOVERY-1"));
  Assert.True(await f.Users.CheckPasswordAsync(target, "RecoveredPassword123!")); Assert.False(await f.Users.CheckPasswordAsync(target, Password));
  Assert.NotEqual(oldStamp, await f.Users.GetSecurityStampAsync(target)); Assert.Equal(memberships, await f.Db.UserCompanyAccess.CountAsync());
  Assert.Contains(await f.Db.Audits.ToListAsync(), x => x.CompanyId == 2 && x.ActorId == enabled.User.Id && x.Action == "LocalAccountAndMfaRecovered");
 }
 [Theory] [InlineData(false)] [InlineData(true)]
 public async Task LocalRecoveryExplicitlyPreservesOrResetsTargetMfa(bool resetMfa)
 {
  await using var f = await SecurityTestScope.CreateAsync(); var actor = await Enabled(f);
  var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>(); await roles.CreateAsync(new IdentityRole("PlatformAdmin")); await f.Users.AddToRoleAsync(actor.User, "PlatformAdmin");
  f.ActAs("operator-b", 2, 21); var target = await UserWithPassword(f, "operator-b"); var pending = await Security(f).BeginEnrollment(Password);
  var codes = await Security(f).CompleteEnrollment(new() { CurrentPassword = Password, Code = Totp(pending.SharedKey), Proof = pending.Proof });
  f.ActAs(); await Security(f).RecoverLocally(new(actor.User.Email!, Password, Totp(actor.Key), target.Email!, "RecoveredPassword123!", resetMfa, "CASE-RECOVERY-2"));
  Assert.Equal(!resetMfa, await f.Users.GetTwoFactorEnabledAsync(target)); Assert.True(await f.Users.GetLockoutEnabledAsync(target));
  Assert.Equal(resetMfa ? 0 : 10, await f.Users.CountRecoveryCodesAsync(target));
  Assert.Equal(!resetMfa, (await f.Users.RedeemTwoFactorRecoveryCodeAsync(target, codes[0])).Succeeded);
  if(resetMfa) Assert.NotEqual(pending.SharedKey, await f.Users.GetAuthenticatorKeyAsync(target));
  else Assert.Equal(pending.SharedKey, await f.Users.GetAuthenticatorKeyAsync(target));
 }
 [Fact] public async Task FailedAuditRollsBackEnrollmentAndRecoveryCodes()
 {
  var failure = new FailSecurityAudit(); await using var f = await SecurityTestScope.CreateAsync(failure); var user = await UserWithPassword(f);
  var setup = await Security(f).BeginEnrollment(Password); var stamp = await f.Users.GetSecurityStampAsync(user); failure.Enabled = true;
  await Assert.ThrowsAsync<InvalidOperationException>(() => Security(f).CompleteEnrollment(new() { CurrentPassword = Password, Code = Totp(setup.SharedKey), Proof = setup.Proof }));
  user = (await f.Users.FindByIdAsync(user.Id))!;
  Assert.False(await f.Users.GetTwoFactorEnabledAsync(user)); Assert.Equal(0, await f.Users.CountRecoveryCodesAsync(user)); Assert.Equal(stamp, await f.Users.GetSecurityStampAsync(user));
 }
 [Fact] public async Task SecondPlatformAdministratorGetsNoTenantAccessAndCanEnrollWithAudit()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var actor = await Enabled(f);
  var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>(); await roles.CreateAsync(new IdentityRole("PlatformAdmin")); await f.Users.AddToRoleAsync(actor.User, "PlatformAdmin");
  var id = await Security(f).ProvisionPlatformAdministrator(new(actor.User.Email!, Password, Totp(actor.Key), "recovery-admin@example.invalid", Password, "CASE-ADMIN-1"));
  var created = (await f.Users.FindByIdAsync(id))!; Assert.True(await f.Users.IsInRoleAsync(created, "PlatformAdmin"));
  Assert.False(await f.Db.UserCompanyAccess.AnyAsync(x => x.UserId == id)); Assert.False(await f.Db.UserBranchAccess.AnyAsync(x => x.UserId == id)); Assert.False(await f.Db.UserPermissions.AnyAsync(x => x.UserId == id));
  Assert.Contains(await f.Users.GetClaimsAsync(created), x => x.Type == "PlatformAuditCompanyId" && x.Value == "1");
  f.ActAs(id); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Permissions.Demand("Animals.View"));
  var setup = await Security(f).BeginEnrollment(Password); await Security(f).CompleteEnrollment(new() { CurrentPassword = Password, Code = Totp(setup.SharedKey), Proof = setup.Proof });
  Assert.True(await f.Users.GetTwoFactorEnabledAsync(created)); Assert.Contains(await f.Db.Audits.ToListAsync(), x => x.Action == "AuthenticatorMfaEnabled" && x.ActorId == id);
 }
 [Theory] [InlineData("tenant")] [InlineData("wrong-password")] [InlineData("no-totp")] [InlineData("locked")]
 public async Task PlatformAdministratorProvisioningCannotBypassCredentialsOrLockout(string scenario)
 {
  await using var f = await SecurityTestScope.CreateAsync(); var actor = await Enabled(f); await f.GrantAsync("*");
  if(scenario != "tenant") { var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>(); await roles.CreateAsync(new IdentityRole("PlatformAdmin")); await f.Users.AddToRoleAsync(actor.User, "PlatformAdmin"); }
  if(scenario == "locked") { await f.Users.SetLockoutEnabledAsync(actor.User,true); await f.Users.SetLockoutEndDateAsync(actor.User,DateTimeOffset.UtcNow.AddMinutes(15)); }
  await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Security(f).ProvisionPlatformAdministrator(new(actor.User.Email!, scenario == "wrong-password" ? "wrong" : Password, scenario == "no-totp" ? "" : Totp(actor.Key), "new-admin@example.invalid", Password, "CASE-ADMIN-2")));
  Assert.Null(await f.Users.FindByEmailAsync("new-admin@example.invalid"));
 }
 [Fact] public async Task PlatformProvisioningCannotElevateOrResetExistingUser()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var actor = await Enabled(f); var target = await UserWithPassword(f,"operator-b");
  var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>(); await roles.CreateAsync(new IdentityRole("PlatformAdmin")); await f.Users.AddToRoleAsync(actor.User, "PlatformAdmin");
  await Assert.ThrowsAsync<InvalidOperationException>(() => Security(f).ProvisionPlatformAdministrator(new(actor.User.Email!,Password,Totp(actor.Key),target.Email!,"NewAttemptPassword123!","CASE-ADMIN-3")));
  Assert.False(await f.Users.IsInRoleAsync(target,"PlatformAdmin")); Assert.True(await f.Users.CheckPasswordAsync(target,Password));
 }
 [Fact] public async Task ContextSelectionIsBrowserScopedAndDoesNotOverwriteAnotherSession()
 {
  var auth = new TestAuthentication(); await using var f = await SecurityTestScope.CreateWithAuthenticationAsync(auth); var user = await UserWithPassword(f);
  f.Db.UserBranchAccess.Add(new UserBranchAccess { UserId=user.Id,CompanyId=1,BranchId=12 }); await f.Db.SaveChangesAsync();
  Assert.True((await f.Users.AddClaimsAsync(user,new[]{new Claim("ActiveCompanyId","1"),new Claim("ActiveBranchId","11")})).Succeeded);
  var context = new ContextController(f.Db,f.Users,f.Services.GetRequiredService<SignInManager<AppUser>>()) { ControllerContext=new ControllerContext { HttpContext=f.Http }, Url=new UrlHelper(new ActionContext(f.Http,new RouteData(),new ActionDescriptor())) };
  Assert.IsType<RedirectToActionResult>(await context.Select(1,11,default));
  var firstBrowser = auth.Tickets[IdentityConstants.ApplicationScheme].Principal;
  Assert.IsType<RedirectToActionResult>(await context.Select(1,12,default));
  var secondBrowser = auth.Tickets[IdentityConstants.ApplicationScheme].Principal;
  Assert.Equal("11",firstBrowser.FindFirstValue("ActiveBranchId")); Assert.Equal("12",secondBrowser.FindFirstValue("ActiveBranchId"));
  Assert.DoesNotContain(await f.Users.GetClaimsAsync(user),c=>c.Type is "ActiveCompanyId" or "ActiveBranchId");
  var fresh = await f.Services.GetRequiredService<SignInManager<AppUser>>().CreateUserPrincipalAsync(user);
  var options = new SecurityStampValidatorOptions(); IdentitySessionOptions.Configure(options);
  await options.OnRefreshingPrincipal!(new SecurityStampRefreshingPrincipalContext { CurrentPrincipal=firstBrowser,NewPrincipal=fresh });
  Assert.Equal("11",fresh.FindFirstValue("ActiveBranchId")); f.Http.User=fresh; Assert.Equal(11,(await f.Tenant.GetAsync()).branch);
 }
 private sealed class FailSecurityAudit : SaveChangesInterceptor
 {
  public bool Enabled { get; set; }
  public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,InterceptionResult<int> result,CancellationToken cancellationToken=default)
  {
   if(Enabled && eventData.Context!.ChangeTracker.Entries<AuditEntry>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Synthetic audit storage failure");
   return ValueTask.FromResult(result);
  }
 }
 [Fact] public async Task PasswordLoginNormalizesLegacyLockoutPolicyAndLimitsGuesses()
 {
  var auth = new TestAuthentication(); await using var f = await SecurityTestScope.CreateWithAuthenticationAsync(auth); var user=await UserWithPassword(f);
  Assert.False(await f.Users.GetLockoutEnabledAsync(user)); var controller=Controller(f);
  for(var attempt=0;attempt<5;attempt++) Assert.IsType<ViewResult>(await controller.Login(user.Email,"invalid",null));
  Assert.True(await f.Users.GetLockoutEnabledAsync(user)); Assert.True(await f.Users.IsLockedOutAsync(user));
  Assert.IsType<ViewResult>(await controller.Login(user.Email,Password,null)); Assert.False(auth.Tickets.ContainsKey(IdentityConstants.ApplicationScheme));
 }
 [Fact] public async Task AccountControllerRenewalsKeepAuthorizedBrowserSelectionAndFreshMfaMethod()
 {
  var auth=new TestAuthentication(); await using var f=await SecurityTestScope.CreateWithAuthenticationAsync(auth); var user=await UserWithPassword(f);
  // A legacy global selection must never replace or duplicate the browser's selected company.
  Assert.True((await f.Users.AddClaimsAsync(user,new[]{new Claim("ActiveCompanyId","2"),new Claim("ActiveBranchId","21"),new Claim("amr","untrusted-global-value")})).Succeeded);
  ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim(ClaimTypes.Role,"PlatformAdmin"));
  var controller=Controller(f);
  var setupView=Assert.IsType<ViewResult>(await controller.BeginAuthenticator(new(){CurrentPassword=Password},default));
  var setup=Assert.IsType<AuthenticatorEnrollment>(setupView.Model);
  async Task AssertSelection(bool mfa)
  {
   var principal=auth.Tickets[IdentityConstants.ApplicationScheme].Principal;
   Assert.Equal("1",Assert.Single(principal.FindAll("ActiveCompanyId")).Value); Assert.Equal("11",Assert.Single(principal.FindAll("ActiveBranchId")).Value);
   Assert.False(principal.IsInRole("PlatformAdmin")); Assert.Equal(mfa,principal.HasClaim("amr","mfa"));
   Assert.False(principal.HasClaim("amr","untrusted-global-value")); Assert.Equal(11,(await f.Tenant.GetAsync()).branch);
  }
  await AssertSelection(false);
  Assert.IsType<ViewResult>(await controller.CompleteAuthenticator(new(){CurrentPassword=Password,Code=Totp(setup.SharedKey),Proof=setup.Proof},default));
  await AssertSelection(true);
  const string nextPassword="ChangedSyntheticPassword123!";
  Assert.IsType<RedirectToActionResult>(await controller.ChangePassword(new(){CurrentPassword=Password,NewPassword=nextPassword,ConfirmPassword=nextPassword,AuthenticatorCode=Totp(setup.SharedKey)}));
  await AssertSelection(true);
  Assert.IsType<ViewResult>(await controller.RegenerateRecoveryCodes(new(){CurrentPassword=nextPassword,Code=Totp(setup.SharedKey)},default));
  await AssertSelection(true);
 }
 [Fact] public async Task AccountControllerRenewalNeverPreservesRevokedTenantSelection()
 {
  var auth=new TestAuthentication(); await using var f=await SecurityTestScope.CreateWithAuthenticationAsync(auth); await UserWithPassword(f);
  f.Db.UserBranchAccess.Remove(await f.Db.UserBranchAccess.SingleAsync(x=>x.UserId=="operator-a")); await f.Db.SaveChangesAsync();
  Assert.IsType<ViewResult>(await Controller(f).BeginAuthenticator(new(){CurrentPassword=Password},default));
  var principal=auth.Tickets[IdentityConstants.ApplicationScheme].Principal;
  Assert.Empty(principal.FindAll("ActiveCompanyId")); Assert.Empty(principal.FindAll("ActiveBranchId"));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>f.Tenant.GetAsync());
 }
 private static AccountController Controller(SecurityTestScope f) => new(f.Services.GetRequiredService<SignInManager<AppUser>>(), Security(f))
 { ControllerContext = new ControllerContext { HttpContext = f.Http }, Url = new UrlHelper(new ActionContext(f.Http, new RouteData(), new ActionDescriptor())) };
 private static string Totp(string key)
 {
  const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"; var bytes = new List<byte>(); var accumulator = 0; var bits = 0;
  foreach (var character in key) { accumulator = (accumulator << 5) | alphabet.IndexOf(char.ToUpperInvariant(character)); bits += 5; if (bits >= 8) { bits -= 8; bytes.Add((byte)(accumulator >> bits)); } }
  var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30); if (BitConverter.IsLittleEndian) Array.Reverse(counter);
  var hash = HMACSHA1.HashData(bytes.ToArray(), counter); var offset = hash[^1] & 15;
  var value = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
  return (value % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
 }
 private sealed class TestAuthentication : IAuthenticationService
 {
  public Dictionary<string, AuthenticationTicket> Tickets { get; } = new();
  public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => Task.FromResult(scheme != null && Tickets.TryGetValue(scheme, out var ticket) ? AuthenticateResult.Success(ticket) : AuthenticateResult.NoResult());
  public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
  public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
  public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) { Tickets[scheme!] = new AuthenticationTicket(principal, properties ?? new(), scheme!); return Task.CompletedTask; }
  public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) { if (scheme != null) Tickets.Remove(scheme); return Task.CompletedTask; }
 }
}
