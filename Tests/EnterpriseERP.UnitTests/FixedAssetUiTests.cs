using System.Globalization;
using System.Net;
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
public sealed class FixedAssetUiTests
{
    private const string UnsafeName = "Pump <script>alert(1)</script> & \"Owner\"";
    private static readonly HashSet<string> All = ["*"];
    private static FixedAsset Asset(string status = "Draft") => new() { Id = 41, AssetCode = "FA-41", Name = UnsafeName, Category = "Equipment", Status = status,
        CurrencyCode = "USD", CurrencyMinorUnits = 2, Cost = 1000, ResidualValue = 100, UsefulLifeMonths = 12, DepreciationMethod = "StraightLineMonthly",
        AcquisitionDate = new(2025, 1, 1), DepreciationStart = new(2025, 1, 1), RowVersion = [1, 2, 3, 4] };
    [Fact]
    public async Task MetadataEditorKeepsOriginalTokenAndCannotSubmitFinancialProperties()
    {
        var html = await Render("Edit", new FixedAssetMetadataInput { Id = 41, Name = UnsafeName, Category = "Equipment", RowVersion = [1, 2, 3, 4] });
        Assert.Contains("name=\"RowVersion\" value=\"AQIDBA==\"", html); Assert.Contains("action=\"/fixed-assets/41/edit\"", html);
        Assert.Contains("__RequestVerificationToken", html); Assert.DoesNotContain("name=\"Cost\"", html); Assert.DoesNotContain("name=\"AccumulatedDepreciation\"", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html); Assert.Contains("Cancel without saving", html);
    }
    [Fact]
    public async Task AcquisitionReviewFormsKeepIndependentApprovalAndSafeUserText()
    {
        var html = await Render("Details", new FixedAssetDetail(Asset(), [], [], [], [], All));
        Assert.Contains("action=\"/fixed-assets/41/approve\"", html); Assert.Contains("name=\"ConfirmReviewed\"", html);
        Assert.Contains("name=\"RowVersion\" value=\"AQIDBA==\"", html); Assert.Contains("action=\"/fixed-assets/41/cancel\"", html);
        Assert.DoesNotContain("action=\"/fixed-assets/41/post\"", html); Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.DoesNotContain("data-i18n=\"Pump", html); // Translation markers must never reinterpret user-entered descriptions.
        Assert.Contains("data-i18n-enum=\"asset\" data-i18n-value=\"Draft\"", html);
        Assert.Contains("data-i18n=\"Custody history starts when acquisition is posted.\"", html);
        Assert.Contains("data-i18n=\"I reviewed the source evidence, policy, amounts and journal.\"", html);
    }
    [Fact]
    public async Task ApprovedRunAndDisposalRenderExactPreviewAndCorrectPostTargets()
    {
        var run = new FixedAssetDepreciationRun { Id = 7, Reference = "DEP-7", Status = "Approved", CurrencyCode = "USD", CurrencyMinorUnits = 2, Total = 75, RowVersion = [1], Period = new(2025, 1, 31),
            Lines = [new() { FixedAssetId = 41, AssetCode = "FA-41", Cost = 1000, ResidualValue = 100, Amount = 75, AccumulatedAfter = 75 }] };
        var html = await Render("Run", new FixedAssetRunDetail(run, null, All));
        Assert.Contains("action=\"/fixed-assets/depreciation/7/post\"", html); Assert.Contains("75.00", html); Assert.Contains("925.00", html);
        var d = new FixedAssetDisposal { Id = 8, FixedAssetId = 41, Reference = "DSP-8", Status = "Approved", Cost = 1000, AccumulatedDepreciation = 75, CarryingValue = 925,
            Proceeds = 950, GainLoss = 25, Settlement = "Receivable", CurrencyCode = "USD", CurrencyMinorUnits = 2, RowVersion = [2], Counterparty = UnsafeName };
        html = await Render("Disposal", new FixedAssetDisposalDetail(d, Asset("Active"), null, All));
        Assert.Contains("action=\"/fixed-assets/disposals/8/post\"", html); Assert.Contains("1500 Other receivable", html); Assert.Contains("4800 Disposal gain", html); Assert.Contains("25.00", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
    }
    [Fact]
    public async Task AllCreationFormsRenderWithAntiforgeryAndNonMutatingCancelNavigation()
    {
        var asset = Asset("Active");
        var pages = new (string view, object model)[] { ("Create", new FixedAssetInput()), ("CreateRun", new FixedAssetRunInput()),
            ("CreateDisposal", new FixedAssetDisposalEditor(new() { AssetRowVersion = [3] }, asset)), ("Custody", new FixedAssetCustodyEditor(new() { RowVersion = [4] }, asset)) };
        foreach (var page in pages) { var html = await Render(page.view, page.model); Assert.Contains("__RequestVerificationToken", html); Assert.Contains("Cancel without saving", html); Assert.Contains("method=\"post\"", html); }
    }
    private static async Task<string> Render(string view, object model)
    {
        var old = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(FixedAssetsController).Assembly.GetName().Name, EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(FixedAssetsController).Assembly);
            builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            await using var app = builder.Build(); await using var scope = app.Services.CreateAsyncScope();
            var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider }; var routes = new RouteData(); routes.Values["controller"] = "FixedAssets";
            var action = new ActionContext(http, routes, new ActionDescriptor { RouteValues = new Dictionary<string, string?> { ["controller"] = "FixedAssets" } });
            var result = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null, "/Views/FixedAssets/" + view + ".cshtml", false);
            Assert.True(result.Success, string.Join("; ", result.SearchedLocations ?? []));
            var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            await result.View.RenderAsync(new ViewContext(action, result.View, data, new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>()), output, new HtmlHelperOptions()));
            return output.ToString();
        }
        finally { CultureInfo.CurrentCulture = old; }
    }
}
