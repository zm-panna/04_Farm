using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

public sealed record AccountSecurityState(bool Enabled, int RecoveryCodesRemaining);
public sealed record AuthenticatorEnrollment(string SharedKey, string AuthenticatorUri, string Proof);
public class ReauthenticateInput
{
 [Required, DataType(DataType.Password), StringLength(1024)] public string CurrentPassword { get; set; } = "";
 [StringLength(32)] public string Code { get; set; } = "";
}
public sealed class CompleteAuthenticatorInput : ReauthenticateInput
{
 [Required, StringLength(4096)] public string Proof { get; set; } = "";
}
public sealed class TwoFactorLoginInput
{
 [Required, StringLength(64)] public string Code { get; set; } = "";
 [StringLength(2048)] public string? ReturnUrl { get; set; }
}
public sealed record LocalPlatformAdminRequest(string ActorEmail, string ActorPassword, string ActorCode, string NewEmail, string NewPassword, string ChangeReference);
public sealed record LocalRecoveryRequest(string ActorEmail, string ActorPassword, string ActorCode, string TargetEmail, string NewPassword, bool ResetMfa, string ChangeReference);

/// <summary>Self-service security changes require fresh credentials, never just a session cookie.</summary>
public sealed class AccountSecurityService(AppDbContext db, UserManager<AppUser> users, IHttpContextAccessor accessor,
 IDataProtectionProvider protection)
{
 private readonly IDataProtector enrollmentProtection = protection.CreateProtector("FarmERP.AuthenticatorEnrollment.v1");
 private sealed record EnrollmentProof(string UserId, string Stamp, string KeyHash, long ExpiresUtc);
 public async Task<AppUser> CurrentUser()
 {
  var principal = accessor.HttpContext?.User;
  if (principal?.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException("Sign in required");
  return await users.GetUserAsync(principal) ?? throw new UnauthorizedAccessException("Sign in required");
 }
 public async Task<IReadOnlyList<Claim>> ValidatedSessionClaims(AppUser user, bool mfaVerified = false)
 {
  var principal = accessor.HttpContext?.User;
  if (principal?.Identity?.IsAuthenticated != true || users.GetUserId(principal) != user.Id) throw new UnauthorizedAccessException("Sign in required");
  var selectedCompany = principal.FindAll("ActiveCompanyId").ToArray(); var selectedBranch = principal.FindAll("ActiveBranchId").ToArray();
  var result = new List<Claim>();
  if (selectedCompany.Length == 1 && selectedBranch.Length == 1 && int.TryParse(selectedCompany[0].Value, out var company) && company > 0
   && int.TryParse(selectedBranch[0].Value, out var branch) && branch > 0
   && await db.UserCompanyAccess.AnyAsync(x => x.UserId == user.Id && x.CompanyId == company)
   && await db.UserBranchAccess.AnyAsync(x => x.UserId == user.Id && x.CompanyId == company && x.BranchId == branch)
   && await db.Branches.AnyAsync(x => x.Id == branch && x.CompanyId == company))
  {
   result.Add(new Claim("ActiveCompanyId", company.ToString(System.Globalization.CultureInfo.InvariantCulture)));
   result.Add(new Claim("ActiveBranchId", branch.ToString(System.Globalization.CultureInfo.InvariantCulture)));
  }
  var methods = principal.FindAll("amr").ToArray();
  if (mfaVerified || (methods.Length == 1 && methods[0].Value == "mfa")) result.Add(new Claim("amr", "mfa"));
  return result;
 }
 public async Task<AccountSecurityState> State()
 {
  var user = await CurrentUser();
  return new(await users.GetTwoFactorEnabledAsync(user), await users.CountRecoveryCodesAsync(user));
 }
 public async Task<AuthenticatorEnrollment> BeginEnrollment(string password, CancellationToken ct = default)
 {
  var user = await CurrentUser(); await RequirePassword(user, password);
  if (await users.GetTwoFactorEnabledAsync(user)) throw new InvalidOperationException("Authenticator MFA is already enabled. Disable it with your password and current code before replacing it.");
  return await Atomic(async () =>
  {
   Ensure(await users.ResetAuthenticatorKeyAsync(user));
   var key = await users.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException("Authenticator setup failed");
   var proof = enrollmentProtection.Protect(JsonSerializer.Serialize(new EnrollmentProof(user.Id, await users.GetSecurityStampAsync(user), Hash(key), DateTimeOffset.UtcNow.AddMinutes(10).UtcTicks)));
   await Audit(user, user, "AuthenticatorEnrollmentStarted", "self-service", ct);
   return Enrollment(user, key, proof);
  }, ct);
 }
 public async Task<AuthenticatorEnrollment> PendingEnrollment(string proof)
 {
  var user = await CurrentUser(); var key = await ValidateProof(user, proof);
  return Enrollment(user, key, proof);
 }
 public async Task<IReadOnlyList<string>> CompleteEnrollment(CompleteAuthenticatorInput input, CancellationToken ct = default)
 {
  var user = await CurrentUser(); await RequirePassword(user, input.CurrentPassword); await ValidateProof(user, input.Proof);
  if (await users.GetTwoFactorEnabledAsync(user)) throw new InvalidOperationException("Authenticator MFA is already enabled");
  await RequireAuthenticator(user, input.Code, requireEnabled: false);
  return await Atomic(async () =>
  {
   Ensure(await users.SetTwoFactorEnabledAsync(user, true));
   var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray() ?? throw new InvalidOperationException("Recovery code generation failed");
   Ensure(await users.ResetAccessFailedCountAsync(user)); Ensure(await users.UpdateSecurityStampAsync(user));
   await Audit(user, user, "AuthenticatorMfaEnabled", "self-service", ct);
   return (IReadOnlyList<string>)codes;
  }, ct);
 }
 public async Task Disable(ReauthenticateInput input, CancellationToken ct = default)
 {
  var user = await CurrentUser(); await RequirePassword(user, input.CurrentPassword); await RequireAuthenticator(user, input.Code);
  await Atomic(async () =>
  {
   Ensure(await users.SetTwoFactorEnabledAsync(user, false)); Ensure(await users.ResetAuthenticatorKeyAsync(user));
   _ = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 0) ?? throw new InvalidOperationException("Recovery code removal failed");
   Ensure(await users.ResetAccessFailedCountAsync(user)); Ensure(await users.UpdateSecurityStampAsync(user));
   await Audit(user, user, "AuthenticatorMfaDisabled", "self-service", ct); return true;
  }, ct);
 }
 public async Task<IReadOnlyList<string>> RegenerateRecoveryCodes(ReauthenticateInput input, CancellationToken ct = default)
 {
  var user = await CurrentUser(); await RequirePassword(user, input.CurrentPassword); await RequireAuthenticator(user, input.Code);
  return await Atomic(async () =>
  {
   var codes = (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray() ?? throw new InvalidOperationException("Recovery code generation failed");
   Ensure(await users.ResetAccessFailedCountAsync(user)); Ensure(await users.UpdateSecurityStampAsync(user));
   await Audit(user, user, "MfaRecoveryCodesReplaced", "self-service", ct); return (IReadOnlyList<string>)codes;
  }, ct);
 }
 public async Task VerifyMfaForPasswordChange(string password, string? code)
 {
  var user = await CurrentUser();
  await RequirePassword(user, password);
  if (await users.GetTwoFactorEnabledAsync(user)) await RequireAuthenticator(user, code);
 }
 public async Task<string> RecoverLocally(LocalRecoveryRequest input, CancellationToken ct = default)
 {
  if (string.IsNullOrWhiteSpace(input.ActorEmail) || input.ActorEmail.Length > 256 || string.IsNullOrWhiteSpace(input.TargetEmail) || input.TargetEmail.Length > 256
   || string.IsNullOrWhiteSpace(input.NewPassword) || input.NewPassword.Length is < 12 or > 1024
   || string.IsNullOrWhiteSpace(input.ChangeReference) || input.ChangeReference.Length is < 3 or > 120 || input.ChangeReference.Any(char.IsControl))
   throw new ArgumentException("Provide exact actor/target emails, a strong new password, and a non-secret recovery approval reference (3–120 characters)");
  var actor = await users.FindByEmailAsync(input.ActorEmail.Trim()) ?? throw new UnauthorizedAccessException("Platform administrator verification failed");
  await RequirePassword(actor, input.ActorPassword);
  if (!await users.IsInRoleAsync(actor, "PlatformAdmin")) throw new UnauthorizedAccessException("A real platform administrator is required; tenant administrator grants are insufficient");
  await RequireAuthenticator(actor, input.ActorCode);
  var actorStamp = await users.GetSecurityStampAsync(actor);
  var target = await users.FindByEmailAsync(input.TargetEmail.Trim()) ?? throw new ArgumentException("The exact recovery target is unavailable");
  return await Atomic(async () =>
  {
   if (!await users.IsInRoleAsync(actor, "PlatformAdmin") || !await db.Users.AsNoTracking().AnyAsync(x => x.Id == actor.Id && x.SecurityStamp == actorStamp, ct))
    throw new UnauthorizedAccessException("Platform verification changed. Start recovery again.");
   // This token never leaves this process and is never logged or returned to the caller.
   var token = await users.GeneratePasswordResetTokenAsync(target);
   Ensure(await users.ResetPasswordAsync(target, token, input.NewPassword));
   if (input.ResetMfa)
   {
    Ensure(await users.SetTwoFactorEnabledAsync(target, false)); Ensure(await users.ResetAuthenticatorKeyAsync(target));
    _ = await users.GenerateNewTwoFactorRecoveryCodesAsync(target, 0) ?? throw new InvalidOperationException("Recovery code removal failed");
   }
   // Legacy accounts may predate lockout policy; normalize before clearing a recovered account.
   Ensure(await users.SetLockoutEnabledAsync(target, true));
   Ensure(await users.SetLockoutEndDateAsync(target, null)); Ensure(await users.ResetAccessFailedCountAsync(target)); Ensure(await users.UpdateSecurityStampAsync(target));
   await Audit(target, actor, input.ResetMfa ? "LocalAccountAndMfaRecovered" : "LocalAccountPasswordRecovered", input.ChangeReference.Trim(), ct);
   return target.Id;
  }, ct);
 }
 public async Task<string> ProvisionPlatformAdministrator(LocalPlatformAdminRequest input, CancellationToken ct = default)
 {
  if (string.IsNullOrWhiteSpace(input.ActorEmail) || input.ActorEmail.Length > 256 || string.IsNullOrWhiteSpace(input.NewEmail) || input.NewEmail.Length > 256
   || !new EmailAddressAttribute().IsValid(input.NewEmail.Trim()) || string.IsNullOrWhiteSpace(input.NewPassword) || input.NewPassword.Length is < 12 or > 1024
   || string.IsNullOrWhiteSpace(input.ChangeReference) || input.ChangeReference.Length is < 3 or > 120 || input.ChangeReference.Any(char.IsControl))
   throw new ArgumentException("Provide exact actor/new account emails, a strong password and a non-secret approval reference (3–120 characters)");
  var actor = await users.FindByEmailAsync(input.ActorEmail.Trim()) ?? throw new UnauthorizedAccessException("Platform administrator verification failed");
  await RequirePassword(actor, input.ActorPassword);
  if (!await users.IsInRoleAsync(actor, "PlatformAdmin")) throw new UnauthorizedAccessException("A real platform administrator is required");
  await RequireAuthenticator(actor, input.ActorCode);
  var actorStamp = await users.GetSecurityStampAsync(actor);
  return await Atomic(async () =>
  {
   if (!await users.IsInRoleAsync(actor, "PlatformAdmin") || !await db.Users.AsNoTracking().AnyAsync(x => x.Id == actor.Id && x.SecurityStamp == actorStamp, ct))
    throw new UnauthorizedAccessException("Platform verification changed. Start provisioning again.");
   if (await users.FindByEmailAsync(input.NewEmail.Trim()) != null) throw new InvalidOperationException("Use a new unique email. Existing accounts cannot be elevated or reset by this command.");
   var auditCompanies = await AuditCompanies(actor, actor, ct);
   if (auditCompanies.Count == 0) throw new InvalidOperationException("The platform operator has no valid audit destination. Resolve audit configuration before provisioning.");
   var created = new AppUser { UserName = input.NewEmail.Trim(), Email = input.NewEmail.Trim(), EmailConfirmed = false, LockoutEnabled = true };
   Ensure(await users.CreateAsync(created, input.NewPassword));
   Ensure(await users.AddToRoleAsync(created, "PlatformAdmin"));
   // Audit routing only: these claims never provide company, branch or business permissions.
   Ensure(await users.AddClaimsAsync(created, auditCompanies.Select(id => new Claim("PlatformAuditCompanyId", id.ToString(System.Globalization.CultureInfo.InvariantCulture)))));
   await Audit(created, actor, "RecoveryPlatformAdministratorProvisioned", input.ChangeReference.Trim(), ct);
   return created.Id;
  }, ct);
 }
 private async Task RequirePassword(AppUser user, string? password)
 {
  if (await users.IsLockedOutAsync(user)) throw new UnauthorizedAccessException("Verification failed. Try again later or contact the platform operator.");
  if (string.IsNullOrEmpty(password) || password.Length > 1024 || !await users.CheckPasswordAsync(user, password))
  { await FailedAttempt(user); throw new UnauthorizedAccessException("Credential verification failed"); }
 }
 private async Task RequireAuthenticator(AppUser user, string? code, bool requireEnabled = true)
 {
  if (await users.IsLockedOutAsync(user)) throw new UnauthorizedAccessException("Verification failed. Try again later or contact the platform operator.");
  var normalized = NormalizeAuthenticatorCode(code);
  if ((requireEnabled && !await users.GetTwoFactorEnabledAsync(user)) || normalized.Length != 6 || normalized.Any(c => !char.IsAsciiDigit(c))
   || !await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, normalized))
  { await FailedAttempt(user); throw new UnauthorizedAccessException("Authenticator verification failed"); }
 }
 public async Task RecordRecoveryLoginFailure(AppUser user)
 { await FailedAttempt(user); }
 private async Task FailedAttempt(AppUser user)
 {
  if (!await users.GetLockoutEnabledAsync(user)) Ensure(await users.SetLockoutEnabledAsync(user, true));
  Ensure(await users.AccessFailedAsync(user));
 }
 public static string NormalizeAuthenticatorCode(string? code) => (code ?? "").Replace(" ", "").Replace("-", "").Trim();
 private async Task<string> ValidateProof(AppUser user, string proof)
 {
  try
  {
   if (string.IsNullOrEmpty(proof) || proof.Length > 4096) throw new InvalidOperationException();
   var data = JsonSerializer.Deserialize<EnrollmentProof>(enrollmentProtection.Unprotect(proof));
   var key = await users.GetAuthenticatorKeyAsync(user);
   if (data == null || data.UserId != user.Id || data.Stamp != await users.GetSecurityStampAsync(user)
    || string.IsNullOrEmpty(key) || data.KeyHash != Hash(key) || data.ExpiresUtc < DateTimeOffset.UtcNow.UtcTicks) throw new InvalidOperationException();
   return key;
  }
  catch (Exception ex) when (ex is CryptographicException or JsonException or InvalidOperationException or FormatException)
  { throw new InvalidOperationException("Authenticator setup expired or changed. Start setup again after confirming your password."); }
 }
 private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
 private static AuthenticatorEnrollment Enrollment(AppUser user, string key, string proof)
 {
  const string issuer = "Farm ERP";
  var uri = "otpauth://totp/" + Uri.EscapeDataString(issuer) + ":" + Uri.EscapeDataString(user.Email ?? user.Id)
   + "?secret=" + Uri.EscapeDataString(key) + "&issuer=" + Uri.EscapeDataString(issuer) + "&digits=6&period=30";
  return new(key, uri, proof);
 }
 private async Task Audit(AppUser target, AppUser actor, string action, string reference, CancellationToken ct)
 {
  var companies = await AuditCompanies(target, actor, ct);
  if (companies.Count == 0) throw new InvalidOperationException("No valid security audit destination is configured for this account.");
  foreach (var company in companies) db.Audits.Add(new AuditEntry { CompanyId = company, ActorId = actor.Id, Action = action, Reference = target.Id + ":" + reference });
  await db.SaveChangesAsync(ct);
 }
 private async Task<List<int>> AuditCompanies(AppUser target, AppUser actor, CancellationToken ct)
 {
  var memberships = await db.UserCompanyAccess.Where(x => x.UserId == target.Id || x.UserId == actor.Id).Select(x => x.CompanyId).ToListAsync(ct);
  var anchors = await db.UserClaims.Where(x => (x.UserId == target.Id || x.UserId == actor.Id) && x.ClaimType == "PlatformAuditCompanyId").Select(x => x.ClaimValue).ToListAsync(ct);
  foreach (var anchor in anchors) if (int.TryParse(anchor, out var id) && id > 0) memberships.Add(id);
  return await db.Companies.Where(x => memberships.Contains(x.Id)).Select(x => x.Id).Distinct().ToListAsync(ct);
 }
 private async Task<T> Atomic<T>(Func<Task<T>> operation, CancellationToken ct)
 {
  await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
  try { var result = await operation(); await transaction.CommitAsync(ct); return result; }
  catch { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
 }
 private static void Ensure(IdentityResult result)
 { if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description))); }
}

/// <summary>Trusted local-host operation only. Never expose this command as a web endpoint.</summary>
public static class LocalAccountRecovery
{
 public static async Task<int> RunAsync(IServiceProvider services)
 {
  if (Environment.GetEnvironmentVariable("FARM_ALLOW_ACCOUNT_RECOVERY") != "YES") throw new InvalidOperationException("Local account recovery must be explicitly enabled for this invocation.");
  string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException("Missing recovery setting: " + name);
  using var scope = services.CreateScope();
  var service = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();
  var reset = Required("FARM_RECOVERY_RESET_MFA");
  if (reset is not ("YES" or "NO")) throw new InvalidOperationException("FARM_RECOVERY_RESET_MFA must explicitly be YES or NO.");
  await service.RecoverLocally(new(Required("FARM_RECOVERY_ACTOR_EMAIL"), Required("FARM_RECOVERY_ACTOR_PASSWORD"), Required("FARM_RECOVERY_ACTOR_TOTP"),
   Required("FARM_RECOVERY_TARGET_EMAIL"), Required("FARM_RECOVERY_NEW_PASSWORD"), reset == "YES", Required("FARM_RECOVERY_REFERENCE")));
  Console.WriteLine("Local recovery completed. Previous sessions were invalidated. Deliver credentials only through the approved secure process; do not print or log them.");
  return 0;
 }
}

/// <summary>Creates a second independently held recovery administrator; never elevates an existing account.</summary>
public static class LocalPlatformAdministrator
{
 public static async Task<int> RunAsync(IServiceProvider services)
 {
  if (Environment.GetEnvironmentVariable("FARM_ALLOW_PLATFORM_ADMIN_PROVISION") != "YES") throw new InvalidOperationException("Platform administrator provisioning must be explicitly enabled for this invocation.");
  string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : throw new InvalidOperationException("Missing provisioning setting: " + name);
  using var scope = services.CreateScope();
  await scope.ServiceProvider.GetRequiredService<AccountSecurityService>().ProvisionPlatformAdministrator(new(
   Required("FARM_PLATFORM_ADMIN_ACTOR_EMAIL"), Required("FARM_PLATFORM_ADMIN_ACTOR_PASSWORD"), Required("FARM_PLATFORM_ADMIN_ACTOR_TOTP"),
   Required("FARM_PLATFORM_ADMIN_NEW_EMAIL"), Required("FARM_PLATFORM_ADMIN_NEW_PASSWORD"), Required("FARM_PLATFORM_ADMIN_REFERENCE")));
  Console.WriteLine("Recovery administrator created with no tenant access. The owner must sign in to /Account/Security, enroll their own authenticator and save recovery codes. No credentials are printed.");
  return 0;
 }
}
