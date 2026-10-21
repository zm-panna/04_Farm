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
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using System.Text.RegularExpressions;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class HRPayrollFirstLiveGuardsTests
{
    private static HRPayrollService Service(SecurityTestScope scope) => new(scope.Db, scope.Tenant, scope.Permissions);
    private static HRPayrollPolicyInput Policy() => new()
    {
        Name = "Explicit synthetic zero-rate policy", PayBasis = "Daily", MonthlyProrationMode = "NotApplicable",
        MonthlyFixedUnits = 0, CapMonthlyPaidUnits = false, Currency = "BDT", CurrencyDecimals = 2,
        EffectiveFrom = new(2025, 1, 1), EffectiveThrough = new(2025, 12, 31),
        DeductionPercent = 0, EmployerContributionPercent = 0,
        JurisdictionAndBasis = "Synthetic test only. Zero percentages and daily basis were explicitly reviewed; no statutory assumption.",
        ExpenseAccount = "6100", PayableAccount = "2300", DeductionAccount = "2310",
        EmployerExpenseAccount = "6110", EmployerPayableAccount = "2320"
    };
    private static Dictionary<string, StringValues> Fields() => new()
    {
        ["Entry.PayBasis"] = "Daily", ["Entry.MonthlyProrationMode"] = "NotApplicable",
        ["Entry.MonthlyFixedUnits"] = "0", ["Entry.CapMonthlyPaidUnits"] = "false",
        ["Entry.CurrencyDecimals"] = "2", ["Entry.EffectiveFrom"] = "2025-01-01",
        ["Entry.EffectiveThrough"] = "2025-12-31", ["Entry.DeductionPercent"] = "0",
        ["Entry.EmployerContributionPercent"] = "0"
    };
    private static HRPayrollController Controller(SecurityTestScope scope, Dictionary<string, StringValues> fields)
    {
        scope.Http.Request.ContentType = "application/x-www-form-urlencoded";
        scope.Http.Request.Form = new FormCollection(fields);
        return new(Service(scope))
        {
            ControllerContext = new ControllerContext { HttpContext = scope.Http },
            Url = new UrlHelper(new ActionContext(scope.Http, new RouteData(), new ActionDescriptor())),
            TempData = new TempDataDictionary(scope.Http, scope.Services.GetRequiredService<ITempDataProvider>())
        };
    }
    public static IEnumerable<object[]> MissingChoices()
    {
        foreach (var key in Fields().Keys)
            foreach (var kind in new[] { "missing", "blank", "duplicate" }) yield return new object[] { key, kind };
    }
    [Theory, MemberData(nameof(MissingChoices))]
    public async Task PolicyPostCannotSilentlyUseDefaultForAbsentBlankOrAmbiguousChoice(string key, string kind)
    {
        await using var scope = await SecurityTestScope.CreateAsync();
        await CompanyBusinessPolicyTestData.SeedAsync(scope); await scope.GrantAsync("*");
        var fields = Fields();
        if (kind == "missing") fields.Remove(key);
        else fields[key] = kind == "blank" ? new StringValues(" ") : new StringValues(new[] { fields[key].ToString(), fields[key].ToString() });
        var controller = Controller(scope, fields);
        Assert.IsType<ViewResult>(await controller.CreatePolicy(Policy(), default));
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(controller.ModelState[key]!.Errors);
        Assert.Empty(await scope.Db.HRPayrollPolicies.ToListAsync());
        Assert.Empty(await scope.Db.Audits.Where(x => x.Action == "Payroll.Policy.Create").ToListAsync());
    }
    [Theory]
    [InlineData("Daily", "NotApplicable")]
    [InlineData("Monthly", "ActualCalendarDays")]
    [InlineData("Mixed", "FixedPaidUnits")]
    public async Task ExplicitZeroPercentagesAndNoCapRemainValidReviewedChoices(string basis, string mode)
    {
        await using var scope = await SecurityTestScope.CreateAsync();
        await CompanyBusinessPolicyTestData.SeedAsync(scope); await scope.GrantAsync("*");
        var input = Policy(); input.PayBasis = basis; input.MonthlyProrationMode = mode;
        input.MonthlyFixedUnits = mode == "FixedPaidUnits" ? 30 : 0;
        var fields = Fields(); fields["Entry.PayBasis"] = basis; fields["Entry.MonthlyProrationMode"] = mode;
        fields["Entry.MonthlyFixedUnits"] = input.MonthlyFixedUnits.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var controller = Controller(scope, fields);
        Assert.IsType<RedirectToActionResult>(await controller.CreatePolicy(input, default));
        var stored = await scope.Db.HRPayrollPolicies.SingleAsync();
        Assert.Equal(basis, stored.PayBasis); Assert.Equal(0, stored.DeductionPercent);
        Assert.Equal(0, stored.EmployerContributionPercent); Assert.False(stored.CapMonthlyPaidUnits);
        Assert.False(stored.Reviewed);
    }
    [Fact]
    public void InitialPolicyPageDoesNotPreselectStatutoryOrCurrencyChoices()
    {
        var entry = new HRPayrollPolicyPage().Entry;
        Assert.Empty(entry.PayBasis); Assert.Empty(entry.MonthlyProrationMode);
        Assert.Equal(-1, entry.CurrencyDecimals); Assert.Equal(-1, entry.DeductionPercent);
        Assert.Equal(-1, entry.EmployerContributionPercent);
    }
    [Fact]
    public async Task RenderedNewPolicyRequiresAnExplicitBasisPrecisionRatesAndCapChoice()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HRPayrollController).Assembly.GetName().Name,
            EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory
        });
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HRPayrollController).Assembly);
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        await using var app = builder.Build(); await using var scope = app.Services.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var routes = new RouteData(); routes.Values["controller"] = "HRPayroll"; routes.Routers.Add(new RenderOnlyRouter());
        var action = new ActionContext(http, routes, new ActionDescriptor
        { RouteValues = new Dictionary<string, string?> { ["controller"] = "HRPayroll" } });
        var view = scope.ServiceProvider.GetRequiredService<IRazorViewEngine>().GetView(null, "/Views/HRPayroll/Policies.cshtml", false);
        Assert.True(view.Success);
        var data = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = new HRPayrollPolicyPage() };
        using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        await view.View.RenderAsync(new ViewContext(action, view.View, data,
            new TempDataDictionary(http, scope.ServiceProvider.GetRequiredService<ITempDataProvider>()), output, new HtmlHelperOptions()));
        var html = output.ToString();
        foreach (var field in new[] { "DeductionPercent", "EmployerContributionPercent" })
        {
            var input = Regex.Matches(html, "<input\\b[^>]*>").Select(x => x.Value).Single(x => x.Contains("name=\"Entry." + field + "\""));
            Assert.Contains("value=\"\"", input); Assert.Contains("required", input);
        }
        foreach (var field in new[] { "PayBasis", "MonthlyProrationMode", "CurrencyDecimals", "CapMonthlyPaidUnits" })
        {
            var select = Regex.Matches(html, "<select\\b[^>]*>[\\s\\S]*?</select>").Select(x => x.Value).Single(x => x.Contains("name=\"Entry." + field + "\""));
            Assert.Contains("required", select);
            var options = Regex.Matches(select, "<option\\b[^>]*>").Select(x => x.Value).ToList();
            Assert.Contains("value=\"\"", options[0]);
            Assert.DoesNotContain(options, x => x.Contains("selected") && !x.Contains("value=\"\""));
        }
    }
    // This fixture renders compiled Razor without an executing endpoint. Supply link generation
    // for the navigation/form tag helpers; the real HTTP suite checks application routes.
    private sealed class RenderOnlyRouter : IRouter
    {
        public Task RouteAsync(RouteContext context) => Task.CompletedTask;
        public VirtualPathData? GetVirtualPath(VirtualPathContext context) =>
            new(this, "hr-payroll/" + Uri.EscapeDataString(context.Values["action"]?.ToString() ?? ""));
    }
    [Theory]
    [InlineData(2025, 1, 31)] [InlineData(2025, 2, 28)] [InlineData(2024, 2, 29)] [InlineData(2025, 4, 30)]
    public void MonthRemainsOpenForItsEntireLastBusinessDate(int year, int month, int days)
    {
        var start = new DateOnly(year, month, 1); var last = new DateOnly(year, month, days);
        Assert.Throws<ArgumentException>(() => HRPayrollService.CompletedMonthEnd(start, last));
        Assert.Equal(last, HRPayrollService.CompletedMonthEnd(start, last.AddDays(1)));
    }
    [Theory]
    [InlineData("2025-04-30T06:00:00Z", false)] // Noon in Bangladesh.
    [InlineData("2025-04-30T17:59:59Z", false)]
    [InlineData("2025-04-30T18:00:00Z", true)] // May 1 locally, still April in UTC.
    [InlineData("2025-05-01T00:00:00Z", true)]
    public void BangladeshMonthBoundaryUsesCompanyLocalMidnight(string utc, bool ended)
    {
        var policy = new CompanyBusinessPolicy { TimeZoneId = "Asia/Dhaka" };
        var today = CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));
        if (ended) Assert.Equal(new DateOnly(2025, 4, 30), HRPayrollService.CompletedMonthEnd(new(2025, 4, 1), today));
        else Assert.Throws<ArgumentException>(() => HRPayrollService.CompletedMonthEnd(new(2025, 4, 1), today));
    }
    [Fact]
    public async Task ActualCalculationRejectsCurrentLocalMonthBeforeReadingPolicyOrCreatingRows()
    {
        await using var scope = await SecurityTestScope.CreateAsync();
        var policy = await CompanyBusinessPolicyTestData.SeedAsync(scope); await scope.GrantAsync("*");
        var today = CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Service(scope).Calculate(new()
        { Reference = "CURRENT-MONTH-MUST-NOT-POST", PeriodStart = new(today.Year, today.Month, 1), PolicyId = 0 }));
        Assert.Contains("fully ended", error.Message);
        Assert.Empty(await scope.Db.HRPayrollRuns.ToListAsync());
        Assert.Empty(await scope.Db.HRPayrollLines.ToListAsync());
    }
}
