using System.Security.Claims;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class ReadinessSecurityTests
{
 [Fact] public async Task TenantWildcardDoesNotGrantPlatformReadiness()
 {
  await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
  var controller = new DeploymentReadinessController(f.Db) { ControllerContext = new ControllerContext { HttpContext = f.Http } };
  Assert.IsType<ForbidResult>(await controller.Get(default));
 }
 [Theory] [InlineData(true)] [InlineData(false)]
 public async Task DevelopmentSqliteReadinessNeverClaimsProductionReadiness(bool useRole)
 {
  await using var f = await SecurityTestScope.CreateAsync();
  ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(useRole ? new Claim(ClaimTypes.Role, "PlatformAdmin") : new Claim("permission", "ops.readiness"));
  var controller = new DeploymentReadinessController(f.Db) { ControllerContext = new ControllerContext { HttpContext = f.Http } };
  var result = Assert.IsType<OkObjectResult>(await controller.Get(default));
  Assert.Equal(200, result.StatusCode); // Demo schema readiness must not imply SQL Server production readiness.
  var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
  Assert.Contains("demo-database-ready", json); Assert.Contains("\"productionReady\":false", json);
  Assert.DoesNotContain("Data Source", json); Assert.DoesNotContain("Password", json); Assert.DoesNotContain("Server=", json);
 }
}
