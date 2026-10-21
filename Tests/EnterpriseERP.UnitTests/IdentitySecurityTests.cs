using System.ComponentModel.DataAnnotations;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class IdentitySecurityTests
{
 [Fact] public void ChangePasswordRequiresMatchingConfirmation()
 {
  var input = new ChangePasswordInput { CurrentPassword = "CurrentPassword1!", NewPassword = "LongNewPassword1!", ConfirmPassword = "DifferentPassword1!" };
  var errors = new List<ValidationResult>();
  Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), errors, true));
  Assert.Contains(errors, x => x.MemberNames.Contains(nameof(ChangePasswordInput.ConfirmPassword)));
 }
 [Theory] [InlineData("")] [InlineData("short")]
 public void NewPasswordRequiresTwelveCharacters(string password)
 {
  var input = new ChangePasswordInput { CurrentPassword = "CurrentPassword1!", NewPassword = password, ConfirmPassword = password };
  Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), new List<ValidationResult>(), true));
 }
 [Fact] public void PasswordMutationRequiresAuthenticationPostAndAntiforgery()
 {
  var action = typeof(AccountController).GetMethod(nameof(AccountController.ChangePassword), new[] { typeof(ChangePasswordInput) })!;
  Assert.NotEmpty(action.GetCustomAttributes(typeof(AuthorizeAttribute), true));
  Assert.NotEmpty(action.GetCustomAttributes(typeof(HttpPostAttribute), true));
  Assert.NotEmpty(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
 }
 [Fact] public async Task IdentityRejectsWrongCurrentPasswordAndInvalidatesStampOnChange()
 {
  await using var f = await SecurityTestScope.CreateAsync(); var user = (await f.Users.FindByIdAsync("operator-a"))!;
  Assert.True((await f.Users.AddPasswordAsync(user, "InitialUnique123!" )).Succeeded);
  var originalStamp = await f.Users.GetSecurityStampAsync(user);
  Assert.False((await f.Users.ChangePasswordAsync(user, "WrongPassword123!", "NewUniquePassword123!" )).Succeeded);
  Assert.True(await f.Users.CheckPasswordAsync(user, "InitialUnique123!"));
  Assert.True((await f.Users.ChangePasswordAsync(user, "InitialUnique123!", "NewUniquePassword123!" )).Succeeded);
  Assert.NotEqual(originalStamp, await f.Users.GetSecurityStampAsync(user));
  Assert.False(await f.Users.CheckPasswordAsync(user, "InitialUnique123!")); Assert.True(await f.Users.CheckPasswordAsync(user, "NewUniquePassword123!"));
 }
 private static AccountController Account(SecurityTestScope f)
 {
  var context = new ActionContext(f.Http, new RouteData(), new ActionDescriptor());
  return new AccountController(f.Services.GetRequiredService<SignInManager<AppUser>>(), f.Services.GetRequiredService<AccountSecurityService>())
  { ControllerContext = new ControllerContext { HttpContext = f.Http }, Url = new UrlHelper(context) };
 }
 [Theory] [InlineData(null, null)] [InlineData("", "")] [InlineData("unknown@example.invalid", "AnyPassword123!")]
 public async Task InvalidLoginUsesGenericFailure(string? email, string? password)
 {
  await using var f = await SecurityTestScope.CreateAsync(); var account = Account(f);
  Assert.IsType<ViewResult>(await account.Login(email, password, "https://example.invalid"));
  Assert.Equal("Login failed", Assert.Single(account.ModelState[""]!.Errors).ErrorMessage);
 }
 [Theory] [InlineData("/Reports", "/Reports")] [InlineData("https://example.invalid", "/Context")] [InlineData("//example.invalid", "/Context")]
 public async Task LoginResolvesEmailAndRejectsExternalReturnUrl(string returnUrl, string expected)
 {
  await using var f = await SecurityTestScope.CreateAsync(); var user = (await f.Users.FindByIdAsync("operator-a"))!;
  Assert.True((await f.Users.SetUserNameAsync(user, "different-user-name")).Succeeded);
  Assert.True((await f.Users.AddPasswordAsync(user, "InitialUnique123!")).Succeeded);
  var result = Assert.IsType<LocalRedirectResult>(await Account(f).Login("operator-a@example.invalid", "InitialUnique123!", returnUrl));
  Assert.Equal(expected, result.Url);
 }
}
