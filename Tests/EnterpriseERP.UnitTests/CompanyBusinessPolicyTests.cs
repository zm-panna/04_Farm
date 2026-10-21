using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class CompanyBusinessPolicyTests
{
    private static BusinessPolicyDraft Draft(string currency = "USD", int minor = 2, bool confirmed = false, DateOnly? from = null) => new("US", "NY", currency, minor, "America/New_York", from ?? new DateOnly(2020,1,1), null, "Synthetic reviewed accounting basis", confirmed);
    private static CompanyBusinessPolicyService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static TaxPolicyService Taxes(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static async Task Grants(SecurityTestScope f)
    { foreach(var p in new[]{"BusinessPolicy.Manage","BusinessPolicy.Approve","TaxPolicy.Manage","TaxPolicy.Approve"}) { await f.GrantAsync(p); await f.GrantAsync(p,"approver-a"); } }
    private static string Token(CompanyBusinessPolicy p) => Convert.ToBase64String(p.RowVersion);
    [Fact] public async Task NoPolicyNeverAssumesCurrencyOrJurisdiction()
    { await using var f=await SecurityTestScope.CreateAsync(); await Assert.ThrowsAsync<InvalidOperationException>(()=>CompanyBusinessPolicyService.Require(f.Db,1,new DateOnly(2026,1,1))); }
    [Fact] public async Task ProposalNeedsPermission()
    { await using var f=await SecurityTestScope.CreateAsync(); await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Propose(Draft())); Assert.Empty(await f.Db.CompanyBusinessPolicies.ToListAsync()); }
    [Fact] public async Task MakerCannotApproveOrLockCurrency()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); var p=await Service(f).Propose(Draft()); await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Review(p.Id,Token(p),true)); Assert.Empty(await f.Db.CompanyCurrencyLocks.ToListAsync()); Assert.Equal("Draft",(await f.Db.CompanyBusinessPolicies.SingleAsync()).Status); }
    [Fact] public async Task IndependentApprovalLocksCurrencyAndAudit()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); var p=await Service(f).Propose(Draft()); f.ActAs("approver-a"); await Service(f).Review(p.Id,Token(p),true); var live=await CompanyBusinessPolicyService.Require(f.Db,1,new DateOnly(2026,1,1)); Assert.Equal("USD",live.CurrencyCode); Assert.Equal("approver-a",live.ApprovedBy); Assert.Single(await f.Db.CompanyCurrencyLocks.ToListAsync()); Assert.Contains(await f.Db.Audits.ToListAsync(),x=>x.Action=="BusinessPolicy.Approve"); }
    [Fact] public async Task RejectedPolicyDoesNotLockOrBecomeEffective()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); var p=await Service(f).Propose(Draft()); f.ActAs("approver-a"); await Service(f).Review(p.Id,Token(p),false); Assert.Empty(await f.Db.CompanyCurrencyLocks.ToListAsync()); await Assert.ThrowsAsync<InvalidOperationException>(()=>CompanyBusinessPolicyService.Current(f.Db,1)); }
    [Fact] public async Task CurrencyAndPrecisionCannotChangeAfterApproval()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); await CompanyBusinessPolicyTestData.SeedAsync(f); await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Propose(Draft("USD",2))); await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Propose(Draft("BDT",3))); }
    [Fact] public async Task ApprovedVersionAndCurrencyLockAreImmutable()
    { await using var f=await SecurityTestScope.CreateAsync(); var p=await CompanyBusinessPolicyTestData.SeedAsync(f); p.RegionCode="ALTERED"; await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear(); var l=await f.Db.CompanyCurrencyLocks.SingleAsync(); l.CurrencyCode="USD"; await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync()); }
    [Fact] public async Task ExistingMoneyNeedsExplicitReview()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); f.Db.StockItems.Add(new StockItem{CompanyId=1,BranchId=11,SKU="A",UnitCost=10}); await f.Db.SaveChangesAsync(); await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Propose(Draft())); var p=await Service(f).Propose(Draft(confirmed:true)); Assert.True(p.ExistingLedgerCurrencyConfirmed); }
    [Fact] public async Task MoneyAddedAfterProposalRequiresUpdatedReviewBeforeApproval()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); var p=await Service(f).Propose(Draft()); f.Db.StockItems.Add(new StockItem{CompanyId=1,BranchId=11,SKU="A",UnitCost=10}); await f.Db.SaveChangesAsync(); f.ActAs("approver-a"); await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Review(p.Id,Token(p),true)); Assert.Empty(await f.Db.CompanyCurrencyLocks.ToListAsync()); }
    [Fact] public async Task CompetingPendingVersionsCannotBeApprovedOutOfOrder()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); var a=await Service(f).Propose(Draft(from:new DateOnly(2020,1,1))); var b=await Service(f).Propose(Draft(from:new DateOnly(2021,1,1))); f.ActAs("approver-a"); await Service(f).Review(b.Id,Token(b),true); await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Review(a.Id,Token(a),true)); Assert.Single(await f.Db.CompanyBusinessPolicies.Where(x=>x.Status=="Approved").ToListAsync()); }
    [Fact] public async Task CompanyLocalDateWorksBeforeUtcMidnight()
    { await using var f=await SecurityTestScope.CreateAsync(); f.Db.CompanyCurrencyLocks.Add(new CompanyCurrencyLock{CompanyId=1,CurrencyCode="BDT",CurrencyMinorUnits=2}); f.Db.CompanyBusinessPolicies.Add(new CompanyBusinessPolicy{CompanyId=1,Version=1,CountryCode="BD",CurrencyCode="BDT",CurrencyMinorUnits=2,TimeZoneId="Asia/Dhaka",EffectiveFrom=new DateOnly(2026,10,19),Status="Approved",CreatedBy="a",ApprovedBy="b"}); await f.Db.SaveChangesAsync(); var p=await CompanyBusinessPolicyService.Current(f.Db,1,atUtc:new DateTimeOffset(2026,10,18,18,30,0,TimeSpan.Zero)); Assert.Equal(new DateOnly(2026,10,19),CompanyBusinessPolicyService.BusinessDate(p,new DateTimeOffset(2026,10,18,18,30,0,TimeSpan.Zero))); }
    [Fact] public async Task ExpiredLatestVersionDoesNotFallback()
    { await using var f=await SecurityTestScope.CreateAsync(); await CompanyBusinessPolicyTestData.SeedAsync(f); f.Db.CompanyBusinessPolicies.Add(new CompanyBusinessPolicy{CompanyId=1,Version=2,CountryCode="BD",CurrencyCode="BDT",CurrencyMinorUnits=2,TimeZoneId="Asia/Dhaka",EffectiveFrom=new DateOnly(2025,1,1),EffectiveTo=new DateOnly(2025,12,31),Status="Approved",CreatedBy="a",ApprovedBy="b"}); await f.Db.SaveChangesAsync(); await Assert.ThrowsAsync<InvalidOperationException>(()=>CompanyBusinessPolicyService.Require(f.Db,1,new DateOnly(2026,1,1))); await Assert.ThrowsAsync<InvalidOperationException>(()=>CompanyBusinessPolicyService.Current(f.Db,1,atUtc:new DateTimeOffset(2026,1,1,12,0,0,TimeSpan.Zero))); }
    [Theory] [InlineData(0,12.5,13)] [InlineData(2,12.345,12.35)] [InlineData(3,12.3455,12.346)] public void MoneyRoundingHonorsExplicitMinorUnits(int places,decimal amount,decimal expected) => Assert.Equal(expected,CompanyBusinessPolicyService.RoundMoney(amount,places));
    [Theory] [InlineData("",2)] [InlineData("US",4)] public async Task MissingStateOrInvalidPrecisionRejected(string state,int minor)
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Propose(Draft() with {RegionCode=state,CurrencyMinorUnits=minor})); }
    [Fact] public async Task StaleApprovalTokenRejected()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); var p=await Service(f).Propose(Draft()); f.ActAs("approver-a"); await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Review(p.Id,Convert.ToBase64String(Guid.NewGuid().ToByteArray()),true)); Assert.Empty(await f.Db.CompanyCurrencyLocks.ToListAsync()); }
    [Fact] public async Task TaxHasExplicitRateIndependentReviewAndScopedLookup()
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); await CompanyBusinessPolicyTestData.SeedAsync(f); var p=await Taxes(f).Propose(new("LOCAL","Local test","Standard",7.25m,"2200","Synthetic review",new DateOnly(2020,1,1),null)); await Assert.ThrowsAsync<InvalidOperationException>(()=>Taxes(f).Review(p.Id,Convert.ToBase64String(p.RowVersion),true)); p=await f.Db.TaxPolicies.SingleAsync(); f.ActAs("approver-a"); await Taxes(f).Review(p.Id,Convert.ToBase64String(p.RowVersion),true); var tax=await TaxPolicyService.Require(f.Db,1,p.Id,new DateOnly(2026,1,1)); Assert.Equal(7.25m,TaxPolicyService.Calculate(100m,tax,2)); await Assert.ThrowsAsync<InvalidOperationException>(()=>TaxPolicyService.Require(f.Db,2,p.Id,new DateOnly(2026,1,1))); }
    [Theory] [InlineData("Standard",0)] [InlineData("Exempt",5)] [InlineData("Unknown",0)] public async Task ZeroTaxNeverInferredOrMislabeled(string treatment,decimal rate)
    { await using var f=await SecurityTestScope.CreateAsync(); await Grants(f); await CompanyBusinessPolicyTestData.SeedAsync(f); await Assert.ThrowsAsync<ArgumentException>(()=>Taxes(f).Propose(new("T","test",treatment,rate,"2200","Review",new DateOnly(2020,1,1),null))); }
    [Fact] public async Task HistoricalApprovedTaxCannotBeEdited()
    { await using var f=await SecurityTestScope.CreateAsync(); var p=await CompanyBusinessPolicyTestData.SeedTaxAsync(f,rate:10); p.RatePercent=20; await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync()); }
}
