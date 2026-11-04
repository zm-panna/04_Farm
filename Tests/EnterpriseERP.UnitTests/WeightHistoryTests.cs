using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class WeightHistoryTests
{
    private static FarmBusinessService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static async Task Seed(SecurityTestScope f)
    {
        f.Db.Animals.AddRange(
            new Animal { Id = 1, CompanyId = 1, BranchId = 11, Tag = "ACTIVE" },
            new Animal { Id = 2, CompanyId = 1, BranchId = 11, Tag = "SOLD", Sold = true },
            new Animal { Id = 3, CompanyId = 1, BranchId = 12, Tag = "OTHER-BRANCH" },
            new Animal { Id = 4, CompanyId = 2, BranchId = 21, Tag = "OTHER-COMPANY" });
        for (var i = 0; i < 205; i++)
            f.Db.AnimalWeightRecords.Add(new() { CompanyId = 1, BranchId = 11, AnimalId = 1,
                Reference = $"WEIGHT-{i:D3}", WeighedOn = new DateOnly(2020, 1, 1).AddDays(i), WeightKg = 100 + i, RecordedBy = "operator-a" });
        f.Db.AnimalWeightRecords.AddRange(
            new AnimalWeightRecord { CompanyId = 1, BranchId = 11, AnimalId = 2, Reference = "SOLD-REF", WeighedOn = new DateOnly(2020, 1, 1), WeightKg = 150, Notes = "review 50%_done", RecordedBy = "operator-a" },
            new AnimalWeightRecord { CompanyId = 1, BranchId = 12, AnimalId = 3, Reference = "PRIVATE-BRANCH", WeighedOn = new DateOnly(2021, 1, 1), WeightKg = 150, RecordedBy = "operator-a" },
            new AnimalWeightRecord { CompanyId = 2, BranchId = 21, AnimalId = 4, Reference = "PRIVATE-COMPANY", WeighedOn = new DateOnly(2021, 1, 1), WeightKg = 150, RecordedBy = "operator-b" });
        await f.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task AllOlderRecordsAreReachableWithStableBoundedTenantScopedPages()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Seed(f); await f.GrantAsync("Fattening.View");
        var service = Service(f);
        var first = await service.WeightHistory(int.MinValue);
        var second = await service.WeightHistory(2);
        var last = await service.WeightHistory(int.MaxValue);
        Assert.Equal(206, first.Page.TotalCount); Assert.Equal(1, first.Page.PageNumber);
        Assert.Equal(3, last.Page.PageNumber); Assert.Equal(100, first.Rows.Count); Assert.Equal(100, second.Rows.Count); Assert.Equal(6, last.Rows.Count);
        var all = first.Rows.Concat(second.Rows).Concat(last.Rows).ToList();
        Assert.Equal(206, all.Select(x => x.Weight.Id).Distinct().Count());
        Assert.All(all, x => { Assert.Equal(1, x.Weight.CompanyId); Assert.Equal(11, x.Weight.BranchId); });
        Assert.Equal("WEIGHT-204", all[0].Weight.Reference);
        Assert.Equal("SOLD-REF", all[^2].Weight.Reference); // Same date, higher ID first.
        Assert.Equal("WEIGHT-000", all[^1].Weight.Reference);
        Assert.Contains(all, x => x.AnimalTag == "SOLD");
        Assert.Equal(first.Rows.Select(x => x.Weight.Id), (await service.WeightHistory()).Rows.Select(x => x.Weight.Id));
    }

    [Theory]
    [InlineData(" SOLD ", "SOLD-REF")]
    [InlineData("WEIGHT-000", "WEIGHT-000")]
    [InlineData("50%_done", "SOLD-REF")]
    public async Task SearchFindsTagsReferencesAndLiteralNotes(string term, string reference)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Seed(f); await f.GrantAsync("Fattening.View");
        var page = await Service(f).WeightHistory(99, term);
        Assert.Equal(reference, Assert.Single(page.Rows).Weight.Reference);
        Assert.Equal(1, page.Page.TotalCount); Assert.Equal(1, page.Page.PageNumber); Assert.Equal(term.Trim(), page.Page.Search);
    }

    [Fact]
    public async Task ForeignSearchIsEmptyAndSearchLengthIsBounded()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Seed(f); await f.GrantAsync("Fattening.View");
        var page = await Service(f).WeightHistory(int.MaxValue, "PRIVATE");
        Assert.Empty(page.Rows); Assert.Equal(0, page.Page.TotalCount); Assert.Equal(1, page.Page.TotalPages); Assert.Equal(1, page.Page.PageNumber);
        Assert.Equal(80, (await Service(f).WeightHistory(search: new string('x', 200))).Page.Search.Length);
    }

    [Fact]
    public async Task HistoryRequiresViewPermissionEvenForEmptyResults()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Fattening.Create");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).WeightHistory());
    }

    [Fact]
    public async Task ControllerRejectsMalformedPagingAndReturnsHistoryModel()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Fattening.View");
        var controller = new FatteningController(Service(f));
        Assert.IsType<WeightHistoryPage>(Assert.IsType<ViewResult>(await controller.Weights()).Model);
        controller.ModelState.AddModelError("page", "Invalid page");
        Assert.IsType<BadRequestObjectResult>(await controller.Weights());
    }
}
