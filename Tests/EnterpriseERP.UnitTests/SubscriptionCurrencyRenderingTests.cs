using System.Globalization;
using System.Security.Claims;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class SubscriptionCurrencyRenderingTests
{
    [Theory]
    [InlineData("JPY",0,"1","12")]
    [InlineData("USD",2,"0.01","12.00")]
    [InlineData("KWD",3,"0.001","12.000")]
    public async Task Actual_MVC_receipt_controls_follow_locked_precision_and_historical_fractions_remain_visible(string currency,int units,string step,string represented)
    {
        var culture=CultureInfo.CurrentCulture; CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        try
        {
            var model=new BillingPage(new(12.123m,1,"Notice"),[new SubscriptionInvoice{Id=1,CompanyId=1,Reference="EXACT",Amount=12m},new SubscriptionInvoice{Id=2,CompanyId=1,Reference="HISTORICAL",Amount=12.123m}],[]){CurrencyCode=currency,CurrencyMinorUnits=units};
            var html=await Render(model);
            Assert.Contains($"min=\"{step}\"",html);Assert.Contains($"step=\"{step}\"",html);Assert.Contains(currency,html);Assert.Contains(">"+represented+"</td>",html);Assert.Contains("12.123",html);
            Assert.Equal(represented,model.Money(12m));Assert.Equal("12.123",model.Money(12.123m));
        }
        finally {CultureInfo.CurrentCulture=culture;}
    }
    [Fact] public void Unconfigured_view_preserves_legacy_three_decimal_display_and_input()
    {
        var culture=CultureInfo.CurrentCulture; CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        try {var model=new BillingPage(new(0,0,"Current"),[],[]);Assert.Equal("0.001",model.MoneyStep);Assert.Equal("1.234",model.Money(1.234m));}
        finally {CultureInfo.CurrentCulture=culture;}
    }
    private static async Task<string> Render(BillingPage model)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(WorkspaceController).Assembly.GetName().Name,EnvironmentName="Development",ContentRootPath=AppContext.BaseDirectory});
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(WorkspaceController).Assembly);builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        await using var app=builder.Build();await using var scope=app.Services.CreateAsyncScope();var http=new DefaultHttpContext{RequestServices=scope.ServiceProvider};http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,"PlatformAdmin")],"test"));
        var routes=new RouteData();routes.Values["controller"]="Workspace";var action=new ActionContext(http,routes,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{["controller"]="Workspace"}});
        var engine=scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();var result=engine.GetView(null,"/Views/Workspace/Subscription.cshtml",isMainPage:false);Assert.True(result.Success);
        var data=new ViewDataDictionary(new EmptyModelMetadataProvider(),new ModelStateDictionary()){Model=model};var temp=new TempDataDictionary(http,scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
        using var output=new StringWriter(CultureInfo.InvariantCulture);await result.View.RenderAsync(new ViewContext(action,result.View,data,temp,output,new HtmlHelperOptions()));return output.ToString();
    }
}
