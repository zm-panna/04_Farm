using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
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
public sealed class CRMRenderingTests
{
    private const string Untrusted = "Buyer <script>alert(1)</script> \" &";
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static CRMLead Lead() => new() { Id = 1, Name = Untrusted, Reference = "LEAD1", Email = "test@example.invalid", RowVersion = [1, 2, 3] };
    private static CRMOpportunity Opportunity() => new() { Id = 2, LeadId = 1, Reference = "OPP1", Title = Untrusted, ExpectedCloseDate = Today, RowVersion = [4, 5, 6] };
    private static StockItem Product() => new() { Id = 3, SKU = "SKU1", Name = Untrusted, UnitOfMeasure = "KG" };
    private static CRMQuote Quote() => new() { Id = 4, LeadId = 1, OpportunityId = 2, Reference = "QUOTE1", CustomerName = Untrusted, CurrencyCode = "BDT", CurrencyMinorUnits = 2, BusinessPolicyId = 1, BusinessPolicyVersion = 1, QuoteDate = Today, ValidUntil = Today.AddDays(7), Status = "Reviewed", RowVersion = [7, 8, 9], GrossAmount = 10, NetAmount = 10,
        Lines = [new() { LineNumber = 1, StockItemId = 3, SKU = "SKU1", ProductName = Untrusted, UnitOfMeasure = "KG", Quantity = 2, UnitPrice = 5, GrossAmount = 10, NetAmount = 10 }] };
    [Fact]
    public async Task QuoteReviewAndAcceptanceRenderCSRFThreeVersionsNetDisclosureAndEncodeSnapshots()
    {
        var quote = Quote(); var input = new CRMAcceptQuoteInput { QuoteId = quote.Id, RowVersion = quote.RowVersion, LeadRowVersion = Lead().RowVersion, OpportunityRowVersion = Opportunity().RowVersion };
        var html = await Render("Quote", new CRMQuoteDetail(quote, Opportunity(), Lead(), [new TradingParty { Id = 9, Code = "C9", Name = Untrusted }], input, null, null, true, true));
        Assert.Contains("action=\"/crm/quotes/accept\"", html); Assert.Contains("action=\"/crm/quotes/4/review\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\"", html); Assert.Equal("BwgJ", Value(html, "Acceptance.RowVersion")); Assert.Equal("AQID", Value(html, "Acceptance.LeadRowVersion")); Assert.Equal("BAUG", Value(html, "Acceptance.OpportunityRowVersion"));
        Assert.Contains("name=\"Acceptance.AcceptanceEvidence\"", html); Assert.Contains("name=\"Acceptance.ExistingCustomerId\"", html); Assert.Contains("name=\"Acceptance.Reviewed\"", html);
        Assert.Contains("All amounts are net estimates; tax is not calculated here.", html); Assert.Contains("reserves no stock", html); Assert.Contains("creates no sale, invoice, tax posting", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html); Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("data-i18n=\"Buyer", html);
    }
    [Fact]
    public async Task QuoteEditorPreservesIndexedMultiLineFieldsAndReferenceToken()
    {
        var html = await Render("QuoteEdit", new CRMQuoteEditor(new CRMQuoteInput { Id = 4, Reference = "QUOTE1", OpportunityId = 2, RowVersion = [1, 2, 3, 4], QuoteDate = Today, ValidUntil = Today.AddDays(7), Lines = [new() { StockItemId = 3, Quantity = 2.5m, UnitPrice = 4 }, new()] }, Opportunity(), Lead(), [Product()], "BDT", 2));
        Assert.Equal("AQIDBA==", Value(html, "Input.RowVersion")); Assert.Contains("name=\"Input.Lines[0].StockItemId\"", html); Assert.Contains("name=\"Input.Lines[1].Quantity\"", html); Assert.Contains("name=\"Input.Lines[0].DiscountAmount\"", html);
        Assert.Contains("name=\"Input.QuoteDate\"", html); Assert.Contains("readonly=\"readonly\"", html); Assert.Contains("Tax is not calculated or assumed to be zero", html); Assert.DoesNotContain("<script>alert(1)</script>", html);
    }
    [Fact]
    public async Task LeadAndActivityEditorsExposeConsentAndDueFieldsWithNoSendButtons()
    {
        var lead = await Render("LeadEdit", new CRMLeadInput { Id = 1, Reference = "L1", Name = Untrusted, RowVersion = [1], MarketingConsent = "Unknown" });
        Assert.Contains("name=\"MarketingConsent\"", lead); Assert.Contains("name=\"ConsentReviewed\"", lead); Assert.Contains("name=\"ConsentEvidence\"", lead); Assert.Contains("Unknown is the default", lead); Assert.Equal("AQ==", Value(lead, "RowVersion"));
        var activity = await Render("ActivityEdit", new CRMActivityEditor(new CRMActivityInput { LeadId = 1, Title = Untrusted, DueDate = Today }, Lead(), [Opportunity()]));
        Assert.Contains("name=\"Input.DueDate\"", activity); Assert.Contains("name=\"Input.OpportunityId\"", activity); Assert.Contains("It does not place a call, send a message", activity);
        Assert.DoesNotContain("<script>alert(1)</script>", lead + activity);
    }
    [Fact]
    public async Task AllDetailAndListViewsRenderEmptyAndPopulatedStates()
    {
        var lead = Lead(); var opp = Opportunity(); var quote = Quote();
        var activity = new CRMActivity { Id = 5, LeadId = 1, Reference = "TASK1", Title = Untrusted, DueDate = Today, RowVersion = [1] };
        var handoff = new CRMSalesReady { Id = 6, LeadId = 1, OpportunityId = 2, QuoteId = 4, CustomerId = 9, Reference = "READY1", CustomerName = Untrusted, CurrencyCode = "BDT", CurrencyMinorUnits = 2, NetAmount = 10 };
        var views = new (string name, object model)[]
        {
            ("Index", new CRMListModel { Section = "leads", Page = 1 }),
            ("Index", new CRMListModel { Section = "leads", Page = 2, Total = 51, Query = "Buyer", Status = "Active", Leads = [lead] }),
            ("Lead", new CRMLeadDetail(lead, [new CRMConsentEvent { Evidence = Untrusted }], [opp], [activity], true, 1, 1)),
            ("Opportunity", new CRMOpportunityDetail(opp, lead, [quote], true, true)),
            ("OpportunityEdit", new CRMOpportunityEditor(new CRMOpportunityInput { LeadId = 1, Reference = "O1", Title = Untrusted, LeadRowVersion = [1], RowVersion = [2] }, lead)),
            ("Activity", new CRMActivityDetail(activity, lead, true)),
            ("SalesReady", new CRMSalesReadyDetail(handoff, quote))
        };
        foreach (var view in views) { var html = await Render(view.name, view.model); Assert.NotEmpty(html); Assert.DoesNotContain("<script>alert(1)</script>", html); }
    }
    private static string? Value(string html, string name)
    {
        var tag = Regex.Matches(html, "<input\\b[^>]*>").Select(x => x.Value).Single(x => Regex.IsMatch(x, "name=\"" + Regex.Escape(name) + "\""));
        var match = Regex.Match(tag, "value=\"([^\"]*)\""); return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }
    // Isolated view rendering has no executing endpoint. This supplies link generation only;
    // scripts/http-enterprise-crm.py exercises the actual application endpoint routes over HTTP.
    private sealed class RenderOnlyRouter : IRouter
    {
        public Task RouteAsync(RouteContext context) => Task.CompletedTask;
        public VirtualPathData? GetVirtualPath(VirtualPathContext context)
        {
            var query = context.Values.Where(x => x.Key is not ("controller" or "action"))
                .ToDictionary(x => x.Key, x => x.Value?.ToString());
            return new VirtualPathData(this, Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("crm", query));
        }
    }
    private static async Task<string> Render(string view, object model)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(CRMController).Assembly.GetName().Name, EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CRMController).Assembly); builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        await using var app = builder.Build(); await using var scope = app.Services.CreateAsyncScope(); var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var routes = new RouteData(); routes.Values["controller"] = "CRM"; routes.Routers.Add(new RenderOnlyRouter());
        var action = new ActionContext(http, routes, new ActionDescriptor { RouteValues = new Dictionary<string, string?> { ["controller"] = "CRM" } });
        var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>(); var result = engine.GetView(null, "/Views/CRM/" + view + ".cshtml", false); Assert.True(result.Success, string.Join("; ", result.SearchedLocations ?? []));
        var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model }; var temp = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
        using var output = new StringWriter(CultureInfo.InvariantCulture); await result.View.RenderAsync(new ViewContext(action, result.View, data, temp, output, new HtmlHelperOptions())); return output.ToString();
    }
}
