using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnterpriseERP.UnitTests;

/// <summary>Independent reviewer probes; copied only into the frozen audit checkout, not implementation source.</summary>
public sealed class EnterpriseIndependentAuditTests
{
    [Theory]
    [InlineData(0, 1, 0, 1200)]
    [InlineData(2, .01, 0, 1200)]
    [InlineData(2, 100.01, .01, 7)]
    [InlineData(3, 100.001, .001, 13)]
    [InlineData(0, 105, 5, 3)]
    [InlineData(2, 1000, 100, 1)]
    public void MonthlyDepreciationReachesResidualExactlyWithoutNegativePeriods(int decimals, decimal cost, decimal residual, int life)
    {
        var asset = new FixedAsset { Status = "Active", DepreciationMethod = "StraightLineMonthly", Cost = cost,
            ResidualValue = residual, UsefulLifeMonths = life, CurrencyMinorUnits = decimals, DepreciationStart = new(2020, 1, 1) };
        decimal total = 0;
        for (var month = 0; month < life; month++)
        {
            var date = FixedAssetService.MonthEnd(asset.DepreciationStart.AddMonths(month));
            var value = FixedAssetService.DepreciationAmount(asset, date);
            Assert.InRange(value, 0, cost - residual - total);
            Assert.Equal(value, decimal.Round(value, decimals));
            total += value; asset.AccumulatedDepreciation += value; asset.LastDepreciationPeriod = date;
        }
        Assert.Equal(cost - residual, total);
        Assert.Equal(0, FixedAssetService.DepreciationAmount(asset, FixedAssetService.NextPeriod(asset)));
    }

    [Theory]
    [InlineData("sale")]
    [InlineData("adjustment")]
    [InlineData("feed")]
    [InlineData("transfer")]
    public async Task LegacyStockOutflowCannotConsumeReservedCustomerStock(string flow)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var stock = new StockItem { CompanyId=1, BranchId=11, SKU="RESERVED-AUDIT", Name="Reserved audit feed", UnitOfMeasure="KG", QuantityOnHand=10, ReservedQuantity=8, UnitCost=5 };
        f.Db.StockItems.Add(stock); await f.Db.SaveChangesAsync();
        f.Db.InventoryCostBalances.Add(new InventoryCostBalance {CompanyId=1, BranchId=11, StockItemId=stock.Id, Value=50});
        var animal = new Animal {CompanyId=1, BranchId=11, Tag="AUDIT-ANIMAL", Breed="Test"}; f.Db.Animals.Add(animal);
        var dest = new StockItem {CompanyId=1, BranchId=12, SKU=stock.SKU, Name=stock.Name, UnitOfMeasure="KG"}; f.Db.StockItems.Add(dest);
        f.Db.UserBranchAccess.Add(new UserBranchAccess {CompanyId=1,BranchId=12,UserId="operator-a"}); await f.Db.SaveChangesAsync();
        Func<Task> operation = flow switch {
            "sale" => async () => { await new IntegratedOperations(f.Db,f.Tenant,f.Permissions).Sell(stock.Id,3,10,"AUDIT-SALE",default); },
            "adjustment" => async () => { await new FarmService(f.Db,f.Tenant,f.Permissions).MoveStock(stock.Id,-3,"AUDIT-ADJUST",default); },
            "feed" => async () => { await new FarmHealthFeedService(f.Db,f.Tenant,f.Permissions).ConsumeFeed(animal.Id,stock.Id,3,"AUDIT-FEED",DateOnly.FromDateTime(DateTime.UtcNow),default); },
            _ => async () => { await new StockTransferService(f.Db,f.Tenant,f.Permissions).Post(stock.Id,12,dest.Id,3,"AUDIT-TRANSFER","Reservation test",default); }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(operation);
        f.Db.ChangeTracker.Clear();
        var persisted = await f.Db.StockItems.SingleAsync(x=>x.Id==stock.Id);
        Assert.Equal(10,persisted.QuantityOnHand); Assert.Equal(8,persisted.ReservedQuantity);
        Assert.Equal(50,(await f.Db.InventoryCostBalances.SingleAsync()).Value);
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task GrantingCrmConsentRequiresEvidenceAndReviewWithoutPartialLead()
    {
        await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("CRM.Manage");
        var service=new CRMService(f.Db,f.Tenant,f.Permissions);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveLead(new CRMLeadInput {Reference="AUDIT-CONSENT",Name="Audit customer",Email="audit@example.invalid",MarketingConsent="Granted",ConsentEvidence="Customer request",ConsentReviewed=false}));
        Assert.Empty(await f.Db.CRMLeads.ToListAsync()); Assert.Empty(await f.Db.CRMConsentEvents.ToListAsync()); Assert.Empty(await f.Db.Audits.ToListAsync());
        var lead=await service.SaveLead(new CRMLeadInput {Reference="AUDIT-CONSENT",Name="Audit customer",Email="audit@example.invalid"});
        Assert.Equal("Unknown",lead.MarketingConsent); Assert.Empty(await f.Db.CRMConsentEvents.ToListAsync());
    }

    [Fact]
    public async Task OrdinaryHrViewerCannotReadRatesOrPayrollAndDtoHasNoCompensation()
    {
        await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("HR.View");
        var service=new HRPayrollService(f.Db,f.Tenant,f.Permissions);
        Assert.Empty((await service.BrowseStaff(null,null)).Items);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.Rates(123));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.Register(null,null));
        var names=typeof(HRPayrollStaffInput).GetProperties().Select(x=>x.Name).ToArray();
        Assert.DoesNotContain("DailyRate",names); Assert.DoesNotContain("OvertimeHourlyRate",names); Assert.DoesNotContain("IdentityUserId",names);
    }

    [Fact]
    public async Task AnotherCompanyCannotActOnRealCrmOrAssetIdentifiers()
    {
        await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        await CompanyBusinessPolicyTestData.SeedAsync(f);
        var lead=await new CRMService(f.Db,f.Tenant,f.Permissions).SaveLead(new CRMLeadInput{Reference="AUDIT-A-LEAD",Name="Private A lead"});
        var asset=await new FixedAssetService(f.Db,f.Tenant,f.Permissions).Create(new FixedAssetInput {
            AssetCode="AUDIT-A-ASSET",Name="Private A asset",Category="Test",AcquisitionDate=new(2026,1,1),Cost=10,
            AcquisitionSettlement="Cash",SourceReference="AUDIT-A-SOURCE",DepreciationMethod="None",UsefulLifeMonths=0,
            ResidualValue=10,DepreciationStart=new(2026,1,1),PolicyReason="Synthetic nondepreciable fixture",Custodian="Test",Location="Test"});
        await f.GrantAsync("CRM.Manage","operator-b",2); await f.GrantAsync("FixedAssets.Post","operator-b",2); f.ActAs("operator-b",2,21);
        await Assert.ThrowsAsync<ArgumentException>(()=>new CRMService(f.Db,f.Tenant,f.Permissions).SaveLead(new CRMLeadInput{Id=lead.Id,Reference=lead.Reference,Name="Changed by B",RowVersion=lead.RowVersion}));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>new FixedAssetService(f.Db,f.Tenant,f.Permissions).PostAcquisition(new FixedAssetActionInput{Id=asset.Id,RowVersion=asset.RowVersion}));
        Assert.Equal("Private A lead",(await f.Db.CRMLeads.AsNoTracking().SingleAsync()).Name);
        Assert.Equal("Draft",(await f.Db.FixedAssets.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    private static HRPayrollPolicyInput MonthlyPolicy(DateOnly start, string mode, bool cap, decimal fixedUnits) => new() {
        Name="Independent monthly test",PayBasis="Monthly",MonthlyProrationMode=mode,MonthlyFixedUnits=fixedUnits,CapMonthlyPaidUnits=cap,
        Currency="BDT",CurrencyDecimals=2,EffectiveFrom=start,EffectiveThrough=start.AddMonths(1).AddDays(-1),
        JurisdictionAndBasis="Synthetic explicit monthly proration, no statutory assumptions",ExpenseAccount="6100",PayableAccount="2300",
        DeductionAccount="2310",EmployerExpenseAccount="6110",EmployerPayableAccount="2320"};
    private static async Task<int> ReviewedMonthlyPolicy(SecurityTestScope f, HRPayrollPolicyInput input) {
        var service=new HRPayrollService(f.Db,f.Tenant,f.Permissions); var id=await service.CreatePolicy(input);
        var policy=await f.Db.HRPayrollPolicies.SingleAsync(x=>x.Id==id);f.ActAs("approver-a");
        await service.ReviewPolicy(id,policy.Revision,"Independent monthly formula review",true);f.ActAs();return id;
    }
    [Theory]
    [InlineData(2024,2,1,"ActualCalendarDays",false,0,2900,2900)]
    [InlineData(2024,2,15,"ActualCalendarDays",false,0,2900,1500)]
    [InlineData(2026,7,1,"FixedPaidUnits",true,26,2600,2600)]
    [InlineData(2026,7,1,"FixedPaidUnits",false,26,2600,3100)]
    public async Task MonthlyProrationUsesReviewedDenominatorCapAndEmploymentDays(int year,int month,int startDay,string mode,bool cap,decimal units,decimal salary,decimal expected) {
        await using var f=await SecurityTestScope.CreateAsync();await CompanyBusinessPolicyTestData.SeedAsync(f);await f.GrantAsync("*");await f.GrantAsync("*","approver-a");
        var start=new DateOnly(year,month,1);var end=start.AddMonths(1).AddDays(-1);var service=new HRPayrollService(f.Db,f.Tenant,f.Permissions);
        var policy=await ReviewedMonthlyPolicy(f,MonthlyPolicy(start,mode,cap,units));
        var staff=await service.SaveStaff(new HRPayrollStaffInput{Code="AUDIT-MONTHLY",Name="Test salary",StartDate=new(year,month,startDay)});
        var record=await f.Db.HRPayrollStaff.SingleAsync();await service.SetRates(new HRPayrollRateInput{StaffId=staff,PayBasis="Monthly",EffectiveFrom=new DateOnly(year,month,startDay),MonthlySalary=salary,Revision=record.Revision,ReviewNote="Reviewed synthetic monthly salary"});
        for(var date=new DateOnly(year,month,startDay);date<=end;date=date.AddDays(1))await service.SaveAttendance(new HRPayrollAttendanceInput{StaffId=staff,Date=date,PaidDays=1,Note="Reviewed paid calendar day"});
        await service.Calculate(new HRPayrollRunInput{Reference="AUDIT-MONTHLY",PolicyId=policy,PeriodStart=start});
        var line=await f.Db.HRPayrollLines.SingleAsync();Assert.Equal(expected,line.BasePay);Assert.Equal(expected,line.NetPay);
        Assert.Equal(mode,line.ProrationMode);Assert.Equal(mode=="ActualCalendarDays"?DateTime.DaysInMonth(year,month):units,line.ProrationDenominator);
        Assert.Equal(cap?Math.Min(end.Day-startDay+1,line.ProrationDenominator):end.Day-startDay+1,line.PaidUnitsUsed);
    }
    [Fact]
    public async Task MonthlyPolicyReplacementRecalculatesDraftButCannotRewriteApproval() {
        await using var f=await SecurityTestScope.CreateAsync();await CompanyBusinessPolicyTestData.SeedAsync(f);await f.GrantAsync("*");await f.GrantAsync("*","approver-a");
        var start=new DateOnly(2026,7,1);var service=new HRPayrollService(f.Db,f.Tenant,f.Permissions);
        var oldPolicy=await ReviewedMonthlyPolicy(f,MonthlyPolicy(start,"ActualCalendarDays",false,0));
        var newPolicy=await ReviewedMonthlyPolicy(f,MonthlyPolicy(start,"FixedPaidUnits",false,26));
        var staff=await service.SaveStaff(new HRPayrollStaffInput{Code="AUDIT-REPLACE",Name="Test salary",StartDate=start});
        var record=await f.Db.HRPayrollStaff.SingleAsync();await service.SetRates(new HRPayrollRateInput{StaffId=staff,PayBasis="Monthly",EffectiveFrom=start,MonthlySalary=2600,Revision=record.Revision,ReviewNote="Reviewed monthly salary"});
        for(var day=1;day<=31;day++)await service.SaveAttendance(new HRPayrollAttendanceInput{StaffId=staff,Date=new(2026,7,day),PaidDays=1,Note="Reviewed paid day"});
        var runId=await service.Calculate(new HRPayrollRunInput{Reference="AUDIT-REPLACE",PolicyId=oldPolicy,PeriodStart=start});var run=await f.Db.HRPayrollRuns.SingleAsync();
        Assert.Equal(2600,(await f.Db.HRPayrollLines.SingleAsync()).BasePay);
        await service.RecalculateWithPolicy(runId,run.Revision,newPolicy);run=await f.Db.HRPayrollRuns.SingleAsync();Assert.Equal(3100,(await f.Db.HRPayrollLines.SingleAsync()).BasePay);
        f.ActAs("approver-a");await service.Approve(runId,run.Revision,"Reviewed replacement and exact total",true);run=await f.Db.HRPayrollRuns.SingleAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RecalculateWithPolicy(runId,run.Revision,oldPolicy));
        Assert.Equal(3100,(await f.Db.HRPayrollLines.SingleAsync()).BasePay);Assert.Equal(newPolicy,(await f.Db.HRPayrollRuns.SingleAsync()).PolicyId);
    }

    [Fact]
    public async Task MixedPayrollSelectsHistoricalRatesRatherThanLatestConvenienceValues() {
        await using var f=await SecurityTestScope.CreateAsync();await CompanyBusinessPolicyTestData.SeedAsync(f);await f.GrantAsync("*");await f.GrantAsync("*","approver-a");
        var start=new DateOnly(2026,7,1);var service=new HRPayrollService(f.Db,f.Tenant,f.Permissions);var input=MonthlyPolicy(start,"ActualCalendarDays",false,0);input.PayBasis="Mixed";
        var policy=await ReviewedMonthlyPolicy(f,input);
        var daily=await service.SaveStaff(new HRPayrollStaffInput{Code="AUDIT-DAILY",Name="Test daily",StartDate=start});
        var monthly=await service.SaveStaff(new HRPayrollStaffInput{Code="AUDIT-SALARY",Name="Test salary",StartDate=start});
        foreach(var id in new[]{daily,monthly}) {
            var record=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==id);
            await service.SetRates(new HRPayrollRateInput{StaffId=id,PayBasis=id==daily?"Daily":"Monthly",EffectiveFrom=start,DailyRate=id==daily?100:0,MonthlySalary=id==monthly?3100:0,Revision=record.Revision,ReviewNote="Reviewed July contract"});
            for(var day=1;day<=31;day++)await service.SaveAttendance(new HRPayrollAttendanceInput{StaffId=id,Date=new(2026,7,day),PaidDays=1,Note="Reviewed paid day"});
        }
        var monthlyRecord=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==monthly);
        await service.SetRates(new HRPayrollRateInput{StaffId=monthly,PayBasis="Monthly",EffectiveFrom=new(2026,8,1),MonthlySalary=6200,Revision=monthlyRecord.Revision,ReviewNote="Reviewed August raise"});
        var runId=await service.Calculate(new HRPayrollRunInput{Reference="AUDIT-MIXED",PolicyId=policy,PeriodStart=start});var lines=await f.Db.HRPayrollLines.OrderBy(x=>x.StaffId).ToListAsync();
        Assert.Equal(2,lines.Count);Assert.All(lines,line=>{Assert.Equal(3100,line.BasePay);Assert.Equal(start,line.CompensationEffectiveFrom);Assert.Equal(1,line.CompensationVersion);});
        Assert.Equal("Daily",lines.Single(x=>x.StaffId==daily).PayBasis);Assert.Equal("Monthly",lines.Single(x=>x.StaffId==monthly).PayBasis);
        var run=await f.Db.HRPayrollRuns.SingleAsync();f.ActAs("approver-a");await service.Approve(runId,run.Revision,"Reviewed mixed historical versions",true);
        var historic=await f.Db.HRPayrollCompensations.SingleAsync(x=>x.StaffId==monthly&&x.Version==1);historic.MonthlySalary=1;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());f.Db.ChangeTracker.Clear();
        Assert.All(await f.Db.HRPayrollLines.ToListAsync(),line=>Assert.Equal(3100,line.BasePay));
    }

    [Fact]
    public async Task ConfiguredMoneyRejectsFractionalCashButKeepsExactInternalCarryingCost() {
        await using var f=await SecurityTestScope.CreateAsync();await CompanyBusinessPolicyTestData.SeedAsync(f);await f.GrantAsync("Sales.Post");
        var stock=new StockItem{CompanyId=1,BranchId=11,SKU="AUDIT-PRECISION",Name="Cost precision",UnitOfMeasure="KG",QuantityOnHand=3,UnitCost=.333m};f.Db.StockItems.Add(stock);await f.Db.SaveChangesAsync();
        f.Db.InventoryCostBalances.Add(new InventoryCostBalance{CompanyId=1,BranchId=11,StockItemId=stock.Id,Value=1});await f.Db.SaveChangesAsync();
        var service=new IntegratedOperations(f.Db,f.Tenant,f.Permissions);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.Sell(stock.Id,1,10.001m,"AUDIT-FRACTION",default));
        Assert.Empty(await f.Db.Vouchers.ToListAsync());Assert.Equal(3,(await f.Db.StockItems.AsNoTracking().SingleAsync()).QuantityOnHand);
        await service.Sell(stock.Id,1,10,"AUDIT-VALID-CASH",default);
        var document=await f.Db.FinanceDocuments.SingleAsync();Assert.Equal(10,document.Amount);Assert.Equal(.333m,document.CostAmount);
        Assert.Equal(.667m,(await f.Db.InventoryCostBalances.SingleAsync()).Value);
        var voucher=await f.Db.Vouchers.Include(x=>x.Lines).SingleAsync();Assert.Equal(.333m,voucher.Lines.Single(x=>x.AccountCode=="5100").Debit);
        Assert.Equal(voucher.Lines.Sum(x=>x.Debit),voucher.Lines.Sum(x=>x.Credit));
    }

}
