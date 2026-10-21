using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class HRPayrollMixedTests
{
 private static readonly DateOnly Start=new(2025,1,1),End=new(2025,1,31);
 private static HRPayrollService Service(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
 private static async Task<(int daily,int monthly,int policy)> Setup(SecurityTestScope f,string basis="Mixed",bool approve=true)
 {
  await CompanyBusinessPolicyTestData.SeedAsync(f);await f.GrantAsync("*");await f.GrantAsync("*","approver-a");var daily=await Service(f).SaveStaff(new(){Code="DAILY",Name="Daily employee",StartDate=End});var monthly=await Service(f).SaveStaff(new(){Code="MONTHLY",Name="Monthly employee",StartDate=End});
  var s=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==daily);await Service(f).SetRates(new(){EffectiveFrom=Start,StaffId=daily,PayBasis="Daily",DailyRate=100,Revision=s.Revision,ReviewNote="Explicit daily contract"});s=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==monthly);await Service(f).SetRates(new(){EffectiveFrom=Start,StaffId=monthly,PayBasis="Monthly",MonthlySalary=3100,Revision=s.Revision,ReviewNote="Explicit monthly contract"});
  foreach(var id in new[]{daily,monthly})await Service(f).SaveAttendance(new(){StaffId=id,Date=End,PaidDays=1,Note="Reviewed paid day"});
  var policy=await Service(f).CreatePolicy(new(){Name="Mixed contract test",PayBasis=basis,MonthlyProrationMode="ActualCalendarDays",Currency="BDT",CurrencyDecimals=2,EffectiveFrom=Start,EffectiveThrough=End,DeductionPercent=5,EmployerContributionPercent=3,JurisdictionAndBasis="Explicit fictional daily and monthly treatment; actual calendar denominator, no cap; no statutory defaults.",ExpenseAccount="6100",PayableAccount="2300",DeductionAccount="2310",EmployerExpenseAccount="6110",EmployerPayableAccount="2320"});if(approve){var p=await f.Db.HRPayrollPolicies.SingleAsync();f.ActAs("approver-a");await Service(f).ReviewPolicy(policy,p.Revision,"Independently checked Mixed rules",true);f.ActAs();}return(daily,monthly,policy);
 }
 [Fact] public async Task MixedRunPaysEachContractOnceAndAccruesExactTotals()
 {
  await using var f=await SecurityTestScope.CreateAsync();var setup=await Setup(f);var id=await Service(f).Calculate(new(){Reference="MIXED",PeriodStart=Start,PolicyId=setup.policy});var lines=await f.Db.HRPayrollLines.OrderBy(x=>x.StaffId).ToListAsync();Assert.Equal(2,lines.Count);Assert.Equal(2,lines.Select(x=>x.StaffId).Distinct().Count());Assert.Equal("Daily",lines[0].PayBasis);Assert.Equal(0,lines[0].ProrationDenominator);Assert.Equal("Monthly",lines[1].PayBasis);Assert.Equal(31,lines[1].ProrationDenominator);Assert.Equal(200,lines.Sum(x=>x.GrossPay));Assert.Equal(190,lines.Sum(x=>x.NetPay));var run=await f.Db.HRPayrollRuns.SingleAsync();f.ActAs("approver-a");await Service(f).Approve(id,run.Revision,"Both contracts reviewed",true);run=await f.Db.HRPayrollRuns.SingleAsync();await Service(f).Post(id,run.Revision,End);var voucher=await f.Db.Vouchers.Include(x=>x.Lines).SingleAsync();Assert.Equal(206,voucher.Lines.Sum(x=>x.Debit));Assert.Equal(206,voucher.Lines.Sum(x=>x.Credit));await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Calculate(new(){Reference="DUPLICATE",PeriodStart=Start,PolicyId=setup.policy}));Assert.Equal(2,await f.Db.HRPayrollLines.CountAsync());
 }
 [Fact] public async Task ContractBasisDriftInvalidatesDraftAndRecalculationSnapshotsNewBasis()
 {
  await using var f=await SecurityTestScope.CreateAsync();var setup=await Setup(f);var id=await Service(f).Calculate(new(){Reference="DRIFT",PeriodStart=Start,PolicyId=setup.policy});var s=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==setup.monthly);await Service(f).SetRates(new(){EffectiveFrom=Start,StaffId=s.Id,PayBasis="Daily",DailyRate=50,MonthlySalary=3100,Revision=s.Revision,ReviewNote="Explicit reviewed contract correction before approval"});var r=await f.Db.HRPayrollRuns.SingleAsync();f.ActAs("approver-a");await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Approve(id,r.Revision,"Stale contract",true));f.ActAs();r=await f.Db.HRPayrollRuns.SingleAsync();await Service(f).Recalculate(id,r.Revision);var line=await f.Db.HRPayrollLines.SingleAsync(x=>x.StaffId==setup.monthly);Assert.Equal("Daily",line.PayBasis);Assert.Equal(50,line.GrossPay);
 }
 [Fact] public async Task NonMixedPolicyRejectsMismatchedStaffBasisWithoutSilentRateInference()
 {
  await using var f=await SecurityTestScope.CreateAsync();var setup=await Setup(f,"Monthly");await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Calculate(new(){Reference="WRONG",PeriodStart=Start,PolicyId=setup.policy}));Assert.Empty(await f.Db.HRPayrollRuns.ToListAsync());
 }
 [Fact] public async Task MixedPolicyRequiresIndependentExplicitReview()
 {
  await using var f=await SecurityTestScope.CreateAsync();var setup=await Setup(f,approve:false);await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Calculate(new(){Reference="UNREVIEWED",PeriodStart=Start,PolicyId=setup.policy}));Assert.Empty(await f.Db.HRPayrollLines.ToListAsync());
 }
 [Fact] public async Task RealMvcMetadataAllowsEmptyCreateTokensButKeepsEditAndBusinessInputsRequired()
 {
  await using var f=await SecurityTestScope.CreateAsync();var metadata=f.Services.GetRequiredService<IModelMetadataProvider>();Assert.False(metadata.GetMetadataForProperty(typeof(HRPayrollStaffInput),nameof(HRPayrollStaffInput.Revision)).IsRequired);Assert.False(metadata.GetMetadataForProperty(typeof(HRPayrollStaffInput),nameof(HRPayrollStaffInput.JobTitle)).IsRequired);Assert.False(metadata.GetMetadataForProperty(typeof(HRPayrollAttendanceInput),nameof(HRPayrollAttendanceInput.Revision)).IsRequired);Assert.True(metadata.GetMetadataForProperty(typeof(HRPayrollRateInput),nameof(HRPayrollRateInput.Revision)).IsRequired);Assert.True(metadata.GetMetadataForProperty(typeof(HRPayrollRateInput),nameof(HRPayrollRateInput.EffectiveFrom)).IsRequired);Assert.True(metadata.GetMetadataForProperty(typeof(HRPayrollLeaveInput),nameof(HRPayrollLeaveInput.Reason)).IsRequired);
  var context=new ActionContext(f.Http,new RouteData(),new ActionDescriptor(),new ModelStateDictionary());f.Services.GetRequiredService<IObjectModelValidator>().Validate(context,null,"",new HRPayrollStaffInput{Code="VALID",Name="Valid staff",StartDate=End,JobTitle=null,Revision=null});Assert.True(context.ModelState.IsValid);
 }
}
