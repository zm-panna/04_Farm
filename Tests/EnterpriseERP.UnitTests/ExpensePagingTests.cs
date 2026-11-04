using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class ExpensePagingTests
{
    private static FarmBusinessService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static ExpenseInput Input(string reference, decimal amount, string category = "Feed", DateOnly? date = null) => new()
    { Reference = reference, Amount = amount, Category = category, ExpenseDate = date ?? Today, Description = "Synthetic expense" };

    [Fact]
    public async Task PagingKeepsExactWholeRangeTotalsAndStableRows()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var service = Service(f);
        for (var i = 0; i < 205; i++)
            await service.RecordExpense(Input($"PAGE-{i:D3}", i < 200 ? .001m : 123.456m, i < 200 ? "Feed" : "Veterinary"));
        await service.RecordExpense(Input("OUTSIDE-RANGE", 999m, date: Today.AddDays(-31)));
        var first = await service.Expenses(page: int.MinValue);
        var middle = await service.Expenses(page: 2);
        var last = await service.Expenses(page: int.MaxValue);
        Assert.Equal(1, first.Page.PageNumber); Assert.Equal(3, last.Page.PageNumber);
        Assert.Equal(100, first.Records.Count); Assert.Equal(100, middle.Records.Count); Assert.Equal(5, last.Records.Count);
        foreach (var page in new[] { first, middle, last })
        {
            Assert.Equal(205, page.Page.TotalCount); Assert.Equal(617.480m, page.Total);
            Assert.Equal(.200m, page.CategoryTotals.Single(x => x.Category == "Feed").Amount);
            Assert.Equal(617.280m, page.CategoryTotals.Single(x => x.Category == "Veterinary").Amount);
        }
        var rows = first.Records.Concat(middle.Records).Concat(last.Records).ToList();
        Assert.Equal(205, rows.Select(x => x.Id).Distinct().Count());
        Assert.Equal("PAGE-204", rows[0].Reference); Assert.Equal("PAGE-000", rows[^1].Reference);
        Assert.DoesNotContain(rows, x => x.Reference == "OUTSIDE-RANGE");
        Assert.Contains("from=" + first.From.ToString("yyyy-MM-dd"), last.PageLink(2));
        Assert.Contains("to=" + first.To.ToString("yyyy-MM-dd"), last.PageLink(2));
        Assert.EndsWith("page=2#expense-ledger", last.PageLink(2));
    }

    [Fact]
    public async Task TotalsAndRecordsExcludeOtherCompaniesAndBranches()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); await f.GrantAsync("*", "operator-b", 2);
        await Service(f).RecordExpense(Input("OWN", 1.125m));
        f.Db.UserBranchAccess.Add(new EnterpriseERP.Domain.UserBranchAccess { UserId = "operator-a", CompanyId = 1, BranchId = 12 }); await f.Db.SaveChangesAsync();
        f.ActAs(branch: 12); await Service(f).RecordExpense(Input("OTHER-BRANCH", 900m));
        f.ActAs("operator-b", 2, 21); await Service(f).RecordExpense(Input("OTHER-COMPANY", 800m));
        f.ActAs(); var result = await Service(f).Expenses();
        Assert.Equal("OWN", Assert.Single(result.Records).Reference); Assert.Equal(1.125m, result.Total);
        Assert.Equal(1.125m, Assert.Single(result.CategoryTotals).Amount); Assert.Equal(1, result.Page.TotalCount);
    }

    [Fact]
    public async Task EmptyRangeAndInvalidRangeRemainSafe()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Expenses.View");
        var page = await Service(f).Expenses(Today, Today, page: int.MaxValue);
        Assert.Empty(page.Records); Assert.Empty(page.CategoryTotals); Assert.Equal(0m, page.Total);
        Assert.Equal(1, page.Page.PageNumber); Assert.Equal(1, page.Page.TotalPages);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Expenses(Today, Today.AddDays(-1)));
    }

    [Fact]
    public async Task ReadPermissionAndMalformedInputAreEnforced()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Expenses.Create");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).Expenses(page: 2));
        var controller = new ExpensesController(Service(f));
        controller.ModelState.AddModelError("from", "Malformed date");
        Assert.IsType<BadRequestObjectResult>(await controller.Index(null, null));
    }
}
