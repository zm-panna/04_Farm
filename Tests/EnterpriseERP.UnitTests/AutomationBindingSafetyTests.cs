using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class AutomationBindingSafetyTests
{
 private static AutomationWorkspaceController Controller(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions,null!,null!,new ConfigurationBuilder().Build()){ControllerContext=new ControllerContext{HttpContext=f.Http},Url=new UrlHelper(new ActionContext(f.Http,new RouteData(),new ActionDescriptor()))};
 [Theory][InlineData(true)][InlineData(false)]
 public async Task MissingOrMalformedStateCannotDisableRule(bool malformed)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Automation.Manage");
  var rule=new AutomationRule{CompanyId=1,Name="Synthetic rule",Trigger="Manual",Action="InAppAlert",Active=true};f.Db.AutomationRules.Add(rule);await f.Db.SaveChangesAsync();
  var controller=Controller(f);if(malformed)controller.ModelState.AddModelError("active","Invalid boolean");
  await Assert.ThrowsAsync<ArgumentException>(()=>controller.Toggle(rule.Id,malformed?false:null,default));
  Assert.True((await f.Db.AutomationRules.AsNoTracking().SingleAsync()).Active);Assert.Empty(await f.Db.Audits.ToListAsync());
 }
 [Fact] public async Task ExplicitFalseIsAnIntentionalAuditedDisable()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Automation.Manage");var rule=new AutomationRule{CompanyId=1,Name="Synthetic rule",Trigger="Manual",Action="InAppAlert",Active=true};f.Db.AutomationRules.Add(rule);await f.Db.SaveChangesAsync();
  Assert.IsType<RedirectToActionResult>(await Controller(f).Toggle(rule.Id,false,default));Assert.False(rule.Active);Assert.Equal("Automation.Disabled",(await f.Db.Audits.SingleAsync()).Action);
 }
}
