using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
namespace EnterpriseERP.Web.Controllers;

public sealed class ChangePasswordInput
{
 [Required, DataType(DataType.Password), StringLength(1024)] public string CurrentPassword { get; set; } = "";
 [Required, DataType(DataType.Password), StringLength(1024, MinimumLength = 12)] public string NewPassword { get; set; } = "";
 [Required, DataType(DataType.Password), StringLength(1024), Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
 [StringLength(32)] public string? AuthenticatorCode { get; set; }
}

public sealed class RegisterInput
{
 [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
 [Required, DataType(DataType.Password), StringLength(1024, MinimumLength = 12)] public string Password { get; set; } = "";
 [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = "";
}

public sealed class AccountController(SignInManager<AppUser> signIn, AccountSecurityService security) : Controller
{
 [AllowAnonymous, HttpGet]
 public IActionResult Login(string? returnUrl = null)
 { if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Context"); ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null; return View(); }

 [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> Login(string? email, string? password, string? returnUrl)
 {
  ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
  await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
  if (!string.IsNullOrWhiteSpace(email) && email.Length <= 256 && !string.IsNullOrEmpty(password) && password.Length <= 1024)
  {
   var user = await signIn.UserManager.FindByEmailAsync(email.Trim());
   if (user != null)
   {
    if (!await signIn.UserManager.GetLockoutEnabledAsync(user))
    {
     var normalized = await signIn.UserManager.SetLockoutEnabledAsync(user, true);
     if (!normalized.Succeeded) { ModelState.AddModelError("", "Login failed"); return View(); }
    }
    var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
    if (result.Succeeded && !await signIn.UserManager.GetTwoFactorEnabledAsync(user))
    {
     await signIn.SignInAsync(user, isPersistent: false);
     return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/Context");
    }
    if (result.Succeeded)
    {
     // Bind the short-lived password-verified challenge to the current security stamp.
     var pending = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, user.Id), new Claim("FarmMfaStamp", await signIn.UserManager.GetSecurityStampAsync(user)) }, IdentityConstants.TwoFactorUserIdScheme));
     await HttpContext.SignInAsync(IdentityConstants.TwoFactorUserIdScheme, pending, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5) });
     return RedirectToAction(nameof(TwoFactor), new { returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
    }
   }
  }
  ModelState.AddModelError("", "Login failed");
  return View();
 }

 [AllowAnonymous, HttpGet]
 public IActionResult Register()
 {
  if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Context");
  return View(new RegisterInput());
 }
 [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> Register(RegisterInput input)
 {
  if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Context");
  if (!ModelState.IsValid) return View(input);
  var email = input.Email.Trim();
  var user = new AppUser { UserName = email, Email = email, LockoutEnabled = true };
  var result = await signIn.UserManager.CreateAsync(user, input.Password);
  if (!result.Succeeded)
  {
   foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
   return View(input);
  }
  TempData["Success"] = "Account created. Sign in to continue. An administrator must assign your company and branch access.";
  return RedirectToAction(nameof(Login));
 }

 [Authorize, HttpGet]
 public IActionResult ChangePassword() => View(new ChangePasswordInput());

 [Authorize, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
 {
  if (!ModelState.IsValid) return View(input);
  var user = await signIn.UserManager.GetUserAsync(User);
  if (user is null) return Challenge();
  try { await security.VerifyMfaForPasswordChange(input.CurrentPassword, input.AuthenticatorCode); }
  catch (UnauthorizedAccessException ex) { ModelState.AddModelError("", ex.Message); return View(input); }
  var result = await signIn.UserManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
  if (!result.Succeeded)
  {
   foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
   return View(input);
  }
  // ChangePassword updates the security stamp, invalidating other sessions when their stamp is checked.
  await RenewSession(user);
  TempData["Success"] = "Your password has been changed.";
  return RedirectToAction(nameof(ChangePassword));
 }

 [AllowAnonymous, HttpGet]
 public async Task<IActionResult> TwoFactor(string? returnUrl = null)
 {
  if (await PendingMfaUser() is null) return RedirectToAction(nameof(Login));
  return View(new TwoFactorLoginInput { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
 }
 [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> TwoFactor(TwoFactorLoginInput input)
 {
  if (await PendingMfaUser() is null) return RedirectToAction(nameof(Login));
  if (!ModelState.IsValid) return View(input);
  var result = await signIn.TwoFactorAuthenticatorSignInAsync(AccountSecurityService.NormalizeAuthenticatorCode(input.Code), isPersistent: false, rememberClient: false);
  if (result.Succeeded) return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : "/Context");
  ModelState.AddModelError("", "Second-factor verification failed. Check the code or try again later.");
  return View(input);
 }
 [AllowAnonymous, HttpGet]
 public async Task<IActionResult> RecoveryLogin(string? returnUrl = null)
 {
  if (await PendingMfaUser() is null) return RedirectToAction(nameof(Login));
  return View(new TwoFactorLoginInput { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
 }
 [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> RecoveryLogin(TwoFactorLoginInput input)
 {
  var user = await PendingMfaUser();
  if (user is null) return RedirectToAction(nameof(Login));
  if (!ModelState.IsValid) return View(input);
  var result = await signIn.TwoFactorRecoveryCodeSignInAsync(input.Code.Trim());
  if (result.Succeeded) return LocalRedirect(Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : "/Context");
  if (!result.IsLockedOut) await security.RecordRecoveryLoginFailure(user);
  ModelState.AddModelError("", "Recovery-code verification failed. Each code can be used once.");
  return View(input);
 }
 [Authorize, HttpGet]
 public async Task<IActionResult> Security() => View(await security.State());

 [Authorize, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> BeginAuthenticator(ReauthenticateInput input, CancellationToken ct)
 {
  if (!ModelState.IsValid) { TempData["Error"] = "Enter your current password."; return RedirectToAction(nameof(Security)); }
  try
  {
   var enrollment = await security.BeginEnrollment(input.CurrentPassword, ct);
   await RenewSession(await security.CurrentUser());
   return View("SetupAuthenticator", enrollment);
  }
  catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
  { TempData["Error"] = ex.Message; return RedirectToAction(nameof(Security)); }
 }
 [Authorize, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> CompleteAuthenticator(CompleteAuthenticatorInput input, CancellationToken ct)
 {
  try
  {
   if (!ModelState.IsValid) throw new InvalidOperationException("Enter your password and the current six-digit authenticator code.");
   var codes = await security.CompleteEnrollment(input, ct);
   await RenewSession(await security.CurrentUser(), mfaVerified: true);
   return View("RecoveryCodes", codes);
  }
  catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
  {
   ModelState.AddModelError("", ex.Message);
   try { return View("SetupAuthenticator", await security.PendingEnrollment(input.Proof)); }
   catch (InvalidOperationException) { TempData["Error"] = "Setup expired or changed. Start again after confirming your password."; return RedirectToAction(nameof(Security)); }
  }
 }
 [Authorize, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> DisableAuthenticator(ReauthenticateInput input, CancellationToken ct)
 {
  try
  {
   if (!ModelState.IsValid) throw new InvalidOperationException("Enter your current password and authenticator code.");
   await security.Disable(input, ct);
   await signIn.ForgetTwoFactorClientAsync(); await signIn.SignOutAsync();
   TempData["Success"] = "Authenticator MFA disabled. Sign in again with your password.";
   return RedirectToAction(nameof(Login));
  }
  catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
  { TempData["Error"] = ex.Message; return RedirectToAction(nameof(Security)); }
 }
 [Authorize, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> RegenerateRecoveryCodes(ReauthenticateInput input, CancellationToken ct)
 {
  try
  {
   if (!ModelState.IsValid) throw new InvalidOperationException("Enter your current password and authenticator code.");
   var codes = await security.RegenerateRecoveryCodes(input, ct);
   await RenewSession(await security.CurrentUser(), mfaVerified: true);
   return View("RecoveryCodes", codes);
  }
  catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException)
  { TempData["Error"] = ex.Message; return RedirectToAction(nameof(Security)); }
 }
 private async Task RenewSession(AppUser user, bool mfaVerified = false)
 {
  var preserved = await security.ValidatedSessionClaims(user, mfaVerified);
  var principal = await signIn.CreateUserPrincipalAsync(user);
  // Rebuild roles/stamp from Identity; selection and MFA method belong only to this authenticated browser.
  foreach (var identity in principal.Identities)
   foreach (var stale in identity.Claims.Where(c => c.Type is "ActiveCompanyId" or "ActiveBranchId" or "amr").ToArray()) identity.RemoveClaim(stale);
  var current = principal.Identities.FirstOrDefault(x => x.IsAuthenticated) ?? throw new UnauthorizedAccessException("Sign in required");
  current.AddClaims(preserved);
  await HttpContext.SignInAsync(IdentityConstants.ApplicationScheme, principal, new AuthenticationProperties { IsPersistent = false });
  HttpContext.User = principal;
 }
 private async Task<AppUser?> PendingMfaUser()
 {
  var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.TwoFactorUserIdScheme);
  var user = await signIn.GetTwoFactorAuthenticationUserAsync();
  if (user is null || authentication.Principal is null || authentication.Properties?.ExpiresUtc is not DateTimeOffset expires || expires <= DateTimeOffset.UtcNow
   || authentication.Principal.FindFirstValue("FarmMfaStamp") != await signIn.UserManager.GetSecurityStampAsync(user)
   || !await signIn.UserManager.GetTwoFactorEnabledAsync(user) || await signIn.UserManager.IsLockedOutAsync(user) || !await signIn.CanSignInAsync(user))
  { await HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme); return null; }
  return user;
 }
 [AllowAnonymous, HttpGet]
 public IActionResult RecoveryHelp() => View();

 [Authorize, HttpPost, ValidateAntiForgeryToken]
 public async Task<IActionResult> Logout()
 { await signIn.SignOutAsync(); return RedirectToAction(nameof(Login)); }
}
