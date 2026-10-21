using System.Security.Claims;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Identity;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class IdentitySessionOptionsTests
{
 [Fact] public async Task StampRefreshPreservesOnlySelectionNotStalePrivilege()
 {
  var options=new SecurityStampValidatorOptions();IdentitySessionOptions.Configure(options);
  var current=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("ActiveCompanyId","1"),new Claim("ActiveBranchId","11"),new Claim(ClaimTypes.Role,"PlatformAdmin"),new Claim("permission","*")},"Identity"));
  var fresh=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,"staff")},"Identity"));
  await options.OnRefreshingPrincipal!(new SecurityStampRefreshingPrincipalContext{CurrentPrincipal=current,NewPrincipal=fresh});
  Assert.Equal(TimeSpan.Zero,options.ValidationInterval);Assert.Equal("1",fresh.FindFirst("ActiveCompanyId")?.Value);Assert.Equal("11",fresh.FindFirst("ActiveBranchId")?.Value);
  Assert.False(fresh.IsInRole("PlatformAdmin"));Assert.False(fresh.HasClaim(x=>x.Type=="permission"));
 }
 [Fact] public async Task GlobalClaimsCannotOverrideOrDuplicateCurrentBrowserSelection()
 {
  var options=new SecurityStampValidatorOptions();IdentitySessionOptions.Configure(options);
  var current=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("ActiveCompanyId","1"),new Claim("ActiveBranchId","11")},"Identity"));
  var fresh=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("ActiveCompanyId","2"),new Claim("ActiveBranchId","21")},"Identity"));
  await options.OnRefreshingPrincipal!(new SecurityStampRefreshingPrincipalContext{CurrentPrincipal=current,NewPrincipal=fresh});
  Assert.Equal("1",Assert.Single(fresh.FindAll("ActiveCompanyId")).Value);Assert.Equal("11",Assert.Single(fresh.FindAll("ActiveBranchId")).Value);
 }
 [Fact] public async Task VerifiedMfaMethodSurvivesRefreshWithoutDatabasePromotion()
 {
  var options=new SecurityStampValidatorOptions();IdentitySessionOptions.Configure(options);
  var fresh=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("amr","pwd")},"Identity"));
  await options.OnRefreshingPrincipal!(new SecurityStampRefreshingPrincipalContext{CurrentPrincipal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("amr","mfa")},"Identity")),NewPrincipal=fresh});
  Assert.Equal("mfa",Assert.Single(fresh.FindAll("amr")).Value);
  var passwordOnly=new ClaimsPrincipal(new ClaimsIdentity([],"Identity"));
  await options.OnRefreshingPrincipal!(new SecurityStampRefreshingPrincipalContext{CurrentPrincipal=passwordOnly,NewPrincipal=fresh});
  Assert.Empty(fresh.FindAll("amr"));
 }
 [Theory][InlineData("0")][InlineData("-1")][InlineData("garbage")]
 public async Task InvalidSelectionsNotCopied(string value)
 {
  var options=new SecurityStampValidatorOptions();IdentitySessionOptions.Configure(options);
  var fresh=new ClaimsPrincipal(new ClaimsIdentity([],"Identity"));
  await options.OnRefreshingPrincipal!(new SecurityStampRefreshingPrincipalContext{CurrentPrincipal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("ActiveCompanyId",value),new Claim("ActiveBranchId","11"),new Claim("ActiveBranchId","12")},"Identity")),NewPrincipal=fresh});
  Assert.False(fresh.HasClaim(x=>x.Type=="ActiveCompanyId"||x.Type=="ActiveBranchId"));
 }
}
