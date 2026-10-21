using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
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

// Execute the compiled MVC views, including tag helpers and HTML encoding, without a browser/network listener.
public sealed class UiLocalizationRenderingTests
{
    private const string Name = "Paid <script>alert(1)</script> \" & {amount} $&";
    private const string Reference = "REF-\"<>&{balance}";
    private static StockItem Stock() => new() { Id = 72, CompanyId = 1, BranchId = 11, SKU = "SKU-001", Name = Name, UnitOfMeasure = "KG", QuantityOnHand = 12.345m };

    [Fact]
    public async Task TradeOptionsEncodeStructuredValuesAndKeepSubmittedIds()
    {
        var model = new OperationsWorkspaceModel { Kind = "Purchase", Stock = [Stock()], Documents = [new DocumentBalance(91, "Purchase", Reference, 123.456m, 0, 0, 123.456m, Name)], Orders = [new PurchaseOrder { Id = 14, Reference = "ORDER", StockItemId = 72, Status = "Approved", Quantity = 2, UnitPrice = 3, Amount = 6 }] };
        var html = await Render("OperationsWorkspace/Trade", model);
        var option = Template(html, "stock-availability");
        Assert.Equal("72", option.Id); Assert.Equal(Name, option.Values.GetProperty("name").GetString());
        Assert.Equal("12.345", option.Values.GetProperty("quantity").GetString());
        var document = Template(html, "original-document");
        Assert.Equal("91", document.Id); Assert.Equal(Reference, document.Values.GetProperty("reference").GetString());
        Assert.Contains("data-i18n-enum=\"order\" data-i18n-value=\"Approved\"", html);
        Assert.Contains("name=\"orderId\" value=\"14\"", html); Assert.Contains("action=\"/Purchase/order/receive\"", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
    }

    [Theory]
    [InlineData("AnimalSale")]
    [InlineData("AnimalPurchase")]
    public async Task AccountSettlementKeepsReferencePartySignedAmountAndUnknownPartyTextSeparate(string kind)
    {
        var model = new OperationsWorkspaceModel { Kind = "Accounts", Statement = new(0, 0, 0, 0, 0, 0, 0), Documents = [new DocumentBalance(912, kind, Reference, 100, 0, 102.345m, -2.345m, Name)], Payments = [new FinancePayment { Id = 5, PartyType = "CustomerRefund", Reference = Reference, Amount = 2.345m, VoucherId = 444 }], BankLines = [new BankStatementLine { Id = 3, ExternalReference = Reference, Amount = 2.345m, MatchedVoucherId = 444 }] };
        var html = await Render("OperationsWorkspace/Accounts", model);
        var option = Template(html, "document-settlement");
        Assert.Equal("912", option.Id); Assert.Equal(Name, option.Values.GetProperty("party").GetString());
        Assert.Equal(Reference, option.Values.GetProperty("reference").GetString());
        Assert.Equal(kind, option.Values.GetProperty("kind").GetString());
        Assert.Contains("data-i18n-enum=\"document\" data-i18n-value=\"" + kind + "\"", html);
        Assert.Equal("-2.345", option.Values.GetProperty("amount").GetString()); Assert.Equal("refund", option.Values.GetProperty("balance").GetString());
        Assert.Contains("data-i18n-enum=\"payment\" data-i18n-value=\"CustomerRefund\"", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
    }

    [Fact]
    public async Task FeedAndBreedingOptionsKeepNamesAndReferencesSeparateFromSystemWords()
    {
        var feed = await Render("Workspace/Feed", new FeedPage([], [Stock()], []));
        var stock = Template(feed, "feed-availability"); Assert.Equal("72", stock.Id); Assert.Equal(Name, stock.Values.GetProperty("name").GetString());
        var breeding = new BreedingPage { AnimalTags = new() { [19] = Name }, Cycles = [new BreedingCycle { Id = 33, AnimalId = 19, Reference = Reference, Status = "Bred", Method = "Natural", ServiceDate = new DateOnly(2026, 1, 1), ExpectedCalvingDate = new DateOnly(2026, 10, 1) }] };
        var html = await Render("Breeding/Index", breeding);
        var cycle = Template(html, "breeding-cycle"); Assert.Equal("33", cycle.Id); Assert.Equal(Name, cycle.Values.GetProperty("tag").GetString()); Assert.Equal("Bred", cycle.Values.GetProperty("status").GetString());
        Assert.Equal(Reference, cycle.Values.GetProperty("reference").GetString());
    }

    [Fact]
    public async Task SubscriptionOptionsKeepInvoiceIdsAndAmounts()
    {
        var model = new BillingPage(new(22.345m, 2, "Notice"), [new SubscriptionInvoice { Id = 44, CompanyId = 1, Reference = Reference, Amount = 25.345m, Paid = 3 }], []);
        var html = await Render("Workspace/Subscription", model, platformAdmin: true);
        var invoice = Template(html, "invoice-balance"); Assert.Equal("44", invoice.Id); Assert.Equal(Reference, invoice.Values.GetProperty("reference").GetString()); Assert.Equal("22.345", invoice.Values.GetProperty("amount").GetString());
        Assert.Contains("data-i18n-enum=\"subscription\" data-i18n-value=\"Notice\"", html);
    }

    [Fact]
    public async Task AutomationOptionsPreserveLargeIdsAndLiteralNames()
    {
        var model = new AutomationPage([new AutomationRule { Id = 23, Name = Name, Trigger = "LowStock", Action = "InAppAlert", Active = true }], [new ScheduledJob { Id = 1, Status = "ReviewRequired", LastError = "Unknown provider: " + Name }], [new InAppNotification { Category = "Manual", Message = "Scheduled review: " + Name }], [new AutomationApproval { Id = 9007199254740993, RuleId = 23, RequesterId = Name, Status = "Pending" }], false);
        var html = await Render("AutomationWorkspace/Index", model);
        var rule = Template(html, "automation-rule"); Assert.Equal("23", rule.Id); Assert.Equal(Name, rule.Values.GetProperty("name").GetString()); Assert.Equal("LowStock", rule.Values.GetProperty("trigger").GetString());
        var approval = Template(html, "approval-request"); Assert.Equal("9007199254740993", approval.Id); Assert.Equal("9007199254740993", approval.Values.GetProperty("id").GetString()); Assert.Equal(Name, approval.Values.GetProperty("requester").GetString());
        Assert.Contains("data-i18n-system-message=\"notification\"", html); Assert.DoesNotContain("data-i18n=\"Unknown provider", html);
    }

    [Fact]
    public async Task MasterEditorsPreserveConcurrencyTokenAndThreeDecimalCostControls()
    {
        var product = await Render("ProductCatalog/Edit", new ProductEditor { Id = 72, SKU = "S1", Name = Name, UnitOfMeasure = "KG", RowVersion = [1, 2, 3, 4] });
        var token = Regex.Matches(product, "<input\\b[^>]*>").Select(x => x.Value).Single(x => Attribute(x, "name") == "RowVersion");
        Assert.Equal("hidden", Attribute(token, "type")); Assert.Equal("AQIDBA==", Attribute(token, "value"));
        var animal = await Render("AnimalRegistry/Edit", new AnimalEditor { Id = 19, Tag = "A1", Breed = "B1", PurchaseCost = 1.234m, RowVersion = [1] });
        var cost = Regex.Matches(animal, "<input\\b[^>]*>").Select(x => x.Value).Single(x => Attribute(x, "name") == "PurchaseCost");
        Assert.Equal("0.001", Attribute(cost, "step")); Assert.Equal("999999999999999.999", Attribute(cost, "max")); Assert.NotNull(Attribute(cost, "readonly"));
    }

    [Fact]
    public async Task SupportTransitionsSeparateSystemStatesFromTheExactCustomerNote()
    {
        var history = new List<SupportTicketActivity>
        {
            new() { Action = "StatusChanged", Note = "Open → InProgress: " + Name, ActorId = "staff-1" },
            new() { Action = "Updated", Note = "Open → Closed: user-entered text", ActorId = "staff-1" }
        };
        var ticket = new SupportTicket { Id = 8, TicketNumber = "T8", Subject = Name, ContactName = Name, Status = "InProgress", Priority = "High", AssignedTo = "staff-1" };
        var model = new SupportTicketDetailPage(new(ticket, history, [new SupportAssignee("staff-1", "Unassigned")]), new() { Status = "InProgress", RowVersion = "AQID" }, true);
        var html = await Render("Support/Detail", model);
        var transition = Template(html, "support-transition"); Assert.Equal(Name, transition.Values.GetProperty("note").GetString());
        Assert.Equal("Open", transition.Values.GetProperty("previous").GetString()); Assert.Equal("InProgress", transition.Values.GetProperty("next").GetString());
        Assert.Single(Regex.Matches(html, "data-i18n-template=\"support-transition\""));
        var assignee = Regex.Match(html, "<dd[^>]*>Unassigned</dd>"); Assert.True(assignee.Success);
        Assert.True(string.IsNullOrEmpty(Attribute(assignee.Value, "data-i18n"))); // A real assignee named Unassigned is not the fallback label.
        Assert.Contains("value=\"Resolved\" data-i18n-enum=\"ticket\" data-i18n-value=\"Resolved\"", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
    }

    [Fact]
    public async Task MilkSessionAndClosedPeriodMarkersLeaveAmountsDatesAndReasonsUntouched()
    {
        var milk = await Render("DairyEntry/Index", new MilkEntryPage { Records = [new MilkYield { AnimalId = 1, Session = "Evening", Litres = 12.345m, Date = new DateOnly(2026, 1, 2) }] });
        Assert.Contains("data-i18n-enum=\"milk\" data-i18n-value=\"Evening\"", milk); Assert.Contains("12.345", milk);
        var period = await Render("AccountingPeriod/Index", new AccountingPeriodClose { ClosedThrough = new DateOnly(2026, 1, 2), Reason = Name }, businessToday: new DateOnly(2026, 1, 3));
        Assert.Contains("data-i18n=\"Closed through\"", period); Assert.Contains("2026-01-02", period); Assert.DoesNotContain("2026-01-02 UTC", period); Assert.Contains("max=\"2026-01-02\"", period); Assert.DoesNotContain("data-i18n=\"" + Name, period);
    }

    [Fact]
    public async Task MissingPartyFallbackAndRealUnassignedPartyUseDifferentDisplayPaths()
    {
        var documents = new List<DocumentBalance>
        {
            new(1, "Purchase", "REAL-PARTY", 12.345m, 0, 0, 12.345m, "Unassigned", PartyAssigned: true),
            new(2, "Purchase", "NO-PARTY", 12.345m, 0, 0, 12.345m, "Unassigned", PartyAssigned: false)
        };
        var model = new OperationsWorkspaceModel { Kind = "Accounts", Statement = new(0, 0, 0, 0, 0, 0, 0), Documents = documents };
        var accounts = await Render("OperationsWorkspace/Accounts", model);
        Assert.Equal("1", Template(accounts, "document-settlement").Id);
        Assert.Equal("2", Template(accounts, "unassigned-settlement").Id);
        Assert.Equal("Unassigned", Template(accounts, "document-settlement").Values.GetProperty("party").GetString());
        model.Kind = "Purchase";
        var trade = await Render("OperationsWorkspace/Trade", model);
        foreach (var html in new[] { accounts, trade })
        {
            var cells = Regex.Matches(html, "<td[^>]*>Unassigned</td>").Select(x => x.Value).ToList();
            Assert.Equal(2, cells.Count);
            Assert.Single(cells, cell => Attribute(cell, "data-i18n") == "Unassigned");
            Assert.Single(cells, cell => string.IsNullOrEmpty(Attribute(cell, "data-i18n")));
        }
    }

    private static (string? Id, JsonElement Values) Template(string html, string name)
    {
        var tag = Regex.Matches(html, "<(?:option|p)\\b[^>]*>").Select(x => x.Value).Single(x => Attribute(x, "data-i18n-template") == name);
        using var values = JsonDocument.Parse(Attribute(tag, "data-i18n-values")!);
        return (Attribute(tag, "value"), values.RootElement.Clone());
    }
    private static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, "(?:^|\\s)" + Regex.Escape(name) + "=\"([^\"]*)\"");
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : null;
    }
    private static async Task<string> Render(string view, object model, bool platformAdmin = false, DateOnly? businessToday = null)
    {
        var culture = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(OperationsWorkspaceController).Assembly.GetName().Name, EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(OperationsWorkspaceController).Assembly);
            builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            await using var app = builder.Build(); await using var scope = app.Services.CreateAsyncScope();
            var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            if (platformAdmin) http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "PlatformAdmin")], "test"));
            var routes = new RouteData(); routes.Values["controller"] = view.Split('/')[0];
            var action = new ActionContext(http, routes, new ActionDescriptor { RouteValues = new Dictionary<string, string?> { ["controller"] = view.Split('/')[0] } });
            var engine = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>();
            var result = engine.GetView(null, "/Views/" + view + ".cshtml", isMainPage: false);
            Assert.True(result.Success, string.Join("; ", result.SearchedLocations ?? []));
            var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };
            if (businessToday.HasValue) data["BusinessToday"] = businessToday.Value;
            var temp = new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>());
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            await result.View.RenderAsync(new ViewContext(action, result.View, data, temp, output, new HtmlHelperOptions()));
            return output.ToString();
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
