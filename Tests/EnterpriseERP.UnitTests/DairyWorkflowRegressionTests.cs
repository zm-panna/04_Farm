using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class DairyWorkflowRegressionTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static async Task<FarmService> Prepare(SecurityTestScope f)
    {
        await f.GrantAsync("*");
        f.Db.Animals.AddRange(new Animal { Id = 1, CompanyId = 1, BranchId = 11, Tag = "DAIRY-1" },
            new Animal { Id = 2, CompanyId = 1, BranchId = 12, Tag = "OTHER-BRANCH" },
            new Animal { Id = 3, CompanyId = 2, BranchId = 21, Tag = "OTHER-COMPANY" },
            new Animal { Id = 4, CompanyId = 1, BranchId = 11, Tag = "SOLD", Sold = true });
        await f.Db.SaveChangesAsync();
        return new FarmService(f.Db, f.Tenant, f.Permissions);
    }
    [Theory] [InlineData("0")] [InlineData("-1")] [InlineData("0.0001")] [InlineData("100000.001")]
    public async Task MvcCannotBypassMilkPrecisionAndPositiveVolumeChecks(string amount)
    {
        await using var f = await SecurityTestScope.CreateAsync(); var service = await Prepare(f);
        var controller = new DairyEntryController(f.Db, f.Tenant, f.Permissions, service);
        // Invoke with valid ModelState to prove the service, not client attributes, rejects invalid precision.
        var result = await controller.Record(new MilkEntryInput { AnimalId = 1, Date = Today, Session = "Morning", Litres = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture) });
        Assert.IsType<ViewResult>(result); Assert.False(controller.ModelState.IsValid);
        Assert.Empty(await f.Db.MilkYields.ToListAsync()); Assert.Empty(await f.Db.Audits.ToListAsync());
    }
    [Theory] [InlineData(1)] [InlineData(-20000)]
    public async Task MvcRejectsFutureAndUninitializedHistoricalMilkDates(int offset)
    {
        await using var f = await SecurityTestScope.CreateAsync(); var service = await Prepare(f);
        var controller = new DairyEntryController(f.Db, f.Tenant, f.Permissions, service);
        Assert.IsType<ViewResult>(await controller.Record(new() { AnimalId = 1, Date = Today.AddDays(offset), Session = "Morning", Litres = 5 }));
        Assert.Empty(await f.Db.MilkYields.ToListAsync());
    }
    [Theory] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task MvcAndApiSharedServiceRejectUnavailableAnimals(int animal)
    {
        await using var f = await SecurityTestScope.CreateAsync(); var service = await Prepare(f);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordMilk(animal, Today, "Morning", 5, default));
        var controller = new DairyEntryController(f.Db, f.Tenant, f.Permissions, service);
        Assert.IsType<ViewResult>(await controller.Record(new() { AnimalId = animal, Date = Today, Session = "Morning", Litres = 5 }));
        Assert.Empty(await f.Db.MilkYields.ToListAsync());
    }
    [Fact] public async Task ValidMilkWritesAuditOnceAndDuplicateDoesNotWriteAgain()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var service = await Prepare(f);
        var controller = new DairyEntryController(f.Db, f.Tenant, f.Permissions, service);
        var input = new MilkEntryInput { AnimalId = 1, Date = Today, Session = "Morning", Litres = 5.125m };
        Assert.IsType<RedirectToActionResult>(await controller.Record(input));
        Assert.IsType<ViewResult>(await controller.Record(input));
        Assert.Equal(5.125m, (await f.Db.MilkYields.SingleAsync()).Litres);
        Assert.Single(await f.Db.Audits.ToListAsync());
    }
}
