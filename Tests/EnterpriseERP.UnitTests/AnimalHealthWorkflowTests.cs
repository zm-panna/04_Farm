using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class AnimalHealthWorkflowTests
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
    private static FarmHealthFeedService Service(SecurityTestScope f) => new(f.Db,f.Tenant,f.Permissions);
    private static async Task Prepare(SecurityTestScope f)
    {
        await f.GrantAsync("*");
        f.Db.Animals.AddRange(new Animal { Id=1,CompanyId=1,BranchId=11,Tag="HEALTH-A" },
            new Animal { Id=2,CompanyId=1,BranchId=12,Tag="HEALTH-BRANCH" },
            new Animal { Id=3,CompanyId=2,BranchId=21,Tag="HEALTH-OTHER" });
        await f.Db.SaveChangesAsync();
    }
    [Fact] public async Task NormalizedHealthReferenceCannotDuplicateOrDoubleAudit()
    {
        await using var f=await SecurityTestScope.CreateAsync();await Prepare(f);var service=Service(f);
        await service.RecordHealth(1," HEALTH-1 ","Observation",Today.AddDays(-5),Today,"Vet follow-up\nStaff review",default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RecordHealth(1,"HEALTH-1","Observation",Today.AddDays(-5),Today,"",default));
        Assert.Equal("HEALTH-1",(await f.Db.AnimalHealthRecords.SingleAsync()).Reference);
        Assert.Single(await f.Db.Audits.Where(x=>x.Action=="AnimalHealth.Record").ToListAsync());
    }
    [Fact] public async Task CompletedFollowUpPreservesDueHistoryAndLeavesDueScreen()
    {
        await using var f=await SecurityTestScope.CreateAsync();await Prepare(f);var service=Service(f);
        var record=await service.RecordHealth(1,"H1","Observation",Today.AddDays(-5),Today.AddDays(-1),"Staff reminder",default);
        var controller=new AnimalHealthEntryController(f.Db,f.Tenant,f.Permissions,service);
        var before=Assert.IsType<AnimalHealthPage>(Assert.IsType<ViewResult>(await controller.Index(null)).Model);
        Assert.Single(before.DueRecords);
        await service.CompleteFollowUp(record.Id,Today);
        var after=Assert.IsType<AnimalHealthPage>(Assert.IsType<ViewResult>(await controller.Index(null)).Model);
        Assert.Empty(after.DueRecords);var saved=Assert.Single(after.Records);
        Assert.Equal(Today.AddDays(-1),saved.NextDueDate);Assert.Equal(Today,saved.FollowUpCompletedDate);
        Assert.Single(await f.Db.Audits.Where(x=>x.Action=="AnimalHealth.FollowUpComplete").ToListAsync());
    }
    [Fact] public async Task FollowUpCannotBeCompletedTwice()
    {
        await using var f=await SecurityTestScope.CreateAsync();await Prepare(f);var service=Service(f);
        var record=await service.RecordHealth(1,"H1","Vaccination",Today.AddDays(-5),Today,"",default);
        await service.CompleteFollowUp(record.Id,Today);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CompleteFollowUp(record.Id,Today));
        Assert.Single(await f.Db.Audits.Where(x=>x.Action=="AnimalHealth.FollowUpComplete").ToListAsync());
    }
    [Theory][InlineData(-6)][InlineData(1)]
    public async Task CompletionCannotPrecedeEventOrBeFuture(int days)
    {
        await using var f=await SecurityTestScope.CreateAsync();await Prepare(f);var service=Service(f);
        var record=await service.RecordHealth(1,"H1","Vaccination",Today.AddDays(-5),Today,"",default);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CompleteFollowUp(record.Id,Today.AddDays(days)));
        Assert.Null((await f.Db.AnimalHealthRecords.AsNoTracking().SingleAsync()).FollowUpCompletedDate);
    }
    [Fact] public async Task NoScheduledFollowUpCannotBeCompleted()
    {
        await using var f=await SecurityTestScope.CreateAsync();await Prepare(f);var service=Service(f);
        var record=await service.RecordHealth(1,"H1","Observation",Today,null,"",default);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CompleteFollowUp(record.Id,Today));
    }
    [Theory][InlineData(1,12,2)][InlineData(2,21,3)]
    public async Task CompletionRejectsForeignBranchAndCompany(int company,int branch,int animal)
    {
        await using var f=await SecurityTestScope.CreateAsync();await Prepare(f);
        var record=new AnimalHealthRecord { CompanyId=company,BranchId=branch,AnimalId=animal,Reference="OTHER",EventType="Observation",EventDate=Today.AddDays(-5),NextDueDate=Today };
        f.Db.AnimalHealthRecords.Add(record);await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).CompleteFollowUp(record.Id,Today));
        Assert.Null((await f.Db.AnimalHealthRecords.AsNoTracking().SingleAsync()).FollowUpCompletedDate);
    }
    [Fact] public async Task CompletionRequiresHealthWritePermission()
    {
        await using var f=await SecurityTestScope.CreateAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).CompleteFollowUp(1,Today));
    }
    [Fact] public async Task FutureHealthRecordCannotBeWritten()
    {
        await using var f=await SecurityTestScope.CreateAsync();await Prepare(f);
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).RecordHealth(1,"FUTURE","Treatment",Today.AddDays(1),null,"",default));
        Assert.Empty(await f.Db.AnimalHealthRecords.ToListAsync());
    }
}
