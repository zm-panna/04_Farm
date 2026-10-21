using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
namespace EnterpriseERP.Web;
/// <summary>Revocation is immediate without losing the user's checked tenant selection on stamp refresh.</summary>
public static class IdentitySessionOptions
{
 public static void Configure(SecurityStampValidatorOptions options)
 {
  options.ValidationInterval=TimeSpan.Zero;
  options.OnRefreshingPrincipal=context=>
  {
   if(context.CurrentPrincipal?.Identity?.IsAuthenticated==true&&context.NewPrincipal?.Identity is ClaimsIdentity identity)
   {
    foreach(var type in new[]{"ActiveCompanyId","ActiveBranchId"})
    {
     // Active selection belongs to this browser session, not mutable global Identity claims.
     foreach(var stale in identity.FindAll(type).ToArray())identity.RemoveClaim(stale);
     var selected=context.CurrentPrincipal.FindAll(type).ToArray();
     if(selected.Length==1&&int.TryParse(selected[0].Value,out var value)&&value>0)
      identity.AddClaim(new Claim(type,value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
    foreach(var method in identity.FindAll("amr").ToArray())identity.RemoveClaim(method);
    var originalMethods=context.CurrentPrincipal.FindAll("amr").ToArray();
    if(originalMethods.Length==1&&originalMethods[0].Value=="mfa")identity.AddClaim(new Claim("amr","mfa"));
   }
   // Roles and grants are rebuilt by Identity. TenantContext revalidates these selected IDs against current membership.
   return Task.CompletedTask;
  };
 }
}
