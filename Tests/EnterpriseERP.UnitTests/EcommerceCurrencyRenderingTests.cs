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
public sealed class EcommerceCurrencyRenderingTests
{
    [Theory]
    [InlineData("JPY",0,"1","12","13")]
    [InlineData("USD",2,"0.01","12.00","13.00")]
    [InlineData("KWD",3,"0.001","12.000","13.000")]
    public async Task Actual_MVC_catalog_cart_order_and_price_forms_follow_currency_precision(string currency,int units,string step,string netText,string grossText)
    {
        var culture=CultureInfo.CurrentCulture;CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        try
        {
            var product=new EcommerceProduct{Id=1,Title="Synthetic display",CurrencyCode=currency,NetUnitPrice=12m};
            var historical=new EcommerceProduct{Id=2,Title="Historical fraction",CurrencyCode=currency,NetUnitPrice=12.123m};
            var browse=new EcommerceBrowsePage([new(product,"KG",1.125m),new(historical,"KG",1m)],1,1,2,"","",false){CurrencyMinorUnits=units};
            var catalog=await Render("Index",browse);
            Assert.Contains(currency+" "+netText+" <small",catalog);Assert.Contains("12.123",catalog);Assert.Contains("1.125 KG",catalog);
            var cart=new EcommerceCartPage(new EcommerceCart{Lines=[new EcommerceCartLine{Title="Synthetic display",CurrencyCode=currency,Quantity=1.125m,NetAmount=12m,TaxAmount=1m,GrossAmount=13m}]},"Synthetic customer"){CurrencyMinorUnits=units};
            var cartHtml=await Render("Cart",cart);Assert.Contains(currency+" "+netText+"</td>",cartHtml);Assert.Contains(currency+" "+grossText+"</strong>",cartHtml);Assert.Contains("step=\"0.001\"",cartHtml);
            var manage=new EcommerceManagePage([product,historical],[],[],[],[],[]){CurrencyMinorUnits=units};
            var management=await Render("Manage",manage);Assert.Contains("min=\""+step+"\" step=\""+step+"\"",management);Assert.Contains(currency+" "+netText+" ·",management);Assert.Contains("12.123",management);
            var order=new EcommerceOrder{Id=1,Reference="SYNTHETIC-RENDER",CurrencyCode=currency,CurrencyMinorUnits=units,Status="Fulfilled",NetAmount=12m,TaxAmount=1m,GrossAmount=13m,Lines=[new EcommerceOrderLine{Id=1,Title="Synthetic display",UnitOfMeasure="KG",Quantity=1.125m,NetAmount=12m,TaxAmount=1m,GrossAmount=13m}]};
            var detail=await Render("Order",new EcommerceOrderPage(order,false,[],[]));Assert.Contains(">"+netText+"</td>",detail);Assert.Contains(currency+" "+grossText+"</strong>",detail);Assert.Contains("1.125 KG",detail);
            var list=await Render("Orders",new EcommerceOrdersPage([order],1,1,1,"","",false));Assert.Contains(currency+" "+grossText+"</td>",list);
        }
        finally {CultureInfo.CurrentCulture=culture;}
    }
    private static async Task<string> Render(string view, object model)
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(WorkspaceController).Assembly.GetName().Name,EnvironmentName="Development",ContentRootPath=AppContext.BaseDirectory});
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(WorkspaceController).Assembly);builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        await using var app=builder.Build();await using var scope=app.Services.CreateAsyncScope();var http=new DefaultHttpContext{RequestServices=scope.ServiceProvider};http.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role,"PlatformAdmin")],"test"));
        var routes=new RouteData();routes.Values["controller"]="Ecommerce";var action=new ActionContext(http,routes,new ActionDescriptor{RouteValues=new Dictionary<string,string?>{["controller"]="Ecommerce"}});
        var engine=scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();var result=engine.GetView(null,"/Views/Ecommerce/"+view+".cshtml",isMainPage:false);Assert.True(result.Success);
        var data=new ViewDataDictionary(new EmptyModelMetadataProvider(),new ModelStateDictionary()){Model=model};var temp=new TempDataDictionary(http,scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
        using var output=new StringWriter(CultureInfo.InvariantCulture);await result.View.RenderAsync(new ViewContext(action,result.View,data,temp,output,new HtmlHelperOptions()));return output.ToString();
    }
}
