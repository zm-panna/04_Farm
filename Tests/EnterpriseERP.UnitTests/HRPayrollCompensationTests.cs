using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class HRPayrollCompensationTests
{
 private static readonly DateOnly Start=new(2025,1,1),End=new(2025,1,31);
 private static HRPayrollService Service(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
 private static async Task<int> Setup(SecurityTestScope f,int minor=2)
 {await CompanyBusinessPolicyTestData.SeedAsync(f,minorUnits:minor);await f.GrantAsync("*");await f.GrantAsync("*","approver-a");return await Service(f).SaveStaff(new(){Code="HISTORY",Name="Compensation history employee",StartDate=End});}
 private static async Task Rate(SecurityTestScope f,int id,DateOnly date,decimal daily,string basis="Daily",decimal salary=0)
 {var s=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==id);await Service(f).SetRates(new(){StaffId=id,EffectiveFrom=date,PayBasis=basis,DailyRate=daily,MonthlySalary=salary,Revision=s.Revision,ReviewNote="Explicit synthetic effective-dated contract"});}
 private static async Task<int> Policy(SecurityTestScope f,string basis="Daily",int minor=2)
 {var id=await Service(f).CreatePolicy(new(){Name="History policy",PayBasis=basis,MonthlyProrationMode="ActualCalendarDays",Currency="BDT",CurrencyDecimals=minor,EffectiveFrom=Start,EffectiveThrough=End,JurisdictionAndBasis="Synthetic test only, reviewed effective contract and no statutory percentages.",ExpenseAccount="6100",PayableAccount="2300",DeductionAccount="2310",EmployerExpenseAccount="6110",EmployerPayableAccount="2320"});var p=await f.Db.HRPayrollPolicies.SingleAsync(x=>x.Id==id);f.ActAs("approver-a");await Service(f).ReviewPolicy(id,p.Revision,"Independently reviewed",true);f.ActAs();return id;}
 [Fact] public async Task PastPayrollUsesItsImmutableEffectiveVersionNotCurrentConvenienceRate()
 {
  await using var f=await SecurityTestScope.CreateAsync();var staff=await Setup(f);await Rate(f,staff,Start,100);await Rate(f,staff,new(2025,2,1),200);var p=await Policy(f);await Service(f).SaveAttendance(new(){StaffId=staff,Date=End,PaidDays=1,Note="Single employed day"});var id=await Service(f).Calculate(new(){Reference="HISTORICAL",PeriodStart=Start,PolicyId=p});var line=await f.Db.HRPayrollLines.SingleAsync();Assert.Equal(100,line.DailyRate);Assert.Equal(100,line.GrossPay);Assert.Equal(1,line.CompensationVersion);Assert.Equal(Start,line.CompensationEffectiveFrom);Assert.Equal(200,(await f.Db.HRPayrollStaff.SingleAsync()).DailyRate);
  var run=await f.Db.HRPayrollRuns.SingleAsync();f.ActAs("approver-a");await Service(f).Approve(id,run.Revision,"Historical version explicitly checked",true);run=await f.Db.HRPayrollRuns.SingleAsync();await Service(f).Post(id,run.Revision,End);f.ActAs();await Rate(f,staff,new(2025,3,1),0,"Monthly",3100);Assert.Equal("Daily",(await f.Db.HRPayrollLines.SingleAsync()).PayBasis);Assert.Equal(100,(await f.Db.HRPayrollLines.SingleAsync()).GrossPay);
 }
 [Fact] public async Task CurrentStaffRateCannotServeAsFallbackWithoutEffectiveHistory()
 {
  await using var f=await SecurityTestScope.CreateAsync();var staff=await Setup(f);await Rate(f,staff,new(2025,2,1),999);await Service(f).SaveAttendance(new(){StaffId=staff,Date=End,PaidDays=1,Note="Historical day"});var p=await Policy(f);await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Calculate(new(){Reference="NO-HISTORY",PeriodStart=Start,PolicyId=p}));Assert.Empty(await f.Db.HRPayrollRuns.ToListAsync());
 }
 [Fact] public async Task ApprovedPeriodForbidsBackdatedCompensationAndHistoryCannotBeEdited()
 {
  await using var f=await SecurityTestScope.CreateAsync();var staff=await Setup(f);await Rate(f,staff,Start,100);await Service(f).SaveAttendance(new(){StaffId=staff,Date=End,PaidDays=1,Note="Reviewed day"});var p=await Policy(f);var id=await Service(f).Calculate(new(){Reference="LOCKED",PeriodStart=Start,PolicyId=p});var run=await f.Db.HRPayrollRuns.SingleAsync();f.ActAs("approver-a");await Service(f).Approve(id,run.Revision,"Independently reviewed",true);f.ActAs();await Assert.ThrowsAsync<InvalidOperationException>(()=>Rate(f,staff,Start,200));Assert.Single(await f.Db.HRPayrollCompensations.ToListAsync());var rate=await f.Db.HRPayrollCompensations.SingleAsync();rate.DailyRate=999;await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());
 }
 [Fact] public async Task NonBoundaryEffectiveDatesAndUnspecifiedDatesAreRejected()
 {
  await using var f=await SecurityTestScope.CreateAsync();var staff=await Setup(f);await Assert.ThrowsAsync<ArgumentException>(()=>Rate(f,staff,new(2025,1,15),100));var s=await f.Db.HRPayrollStaff.SingleAsync();await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).SetRates(new(){StaffId=staff,DailyRate=100,Revision=s.Revision,ReviewNote="No effective date"}));Assert.Empty(await f.Db.HRPayrollCompensations.ToListAsync());
 }
 [Theory][InlineData(0)][InlineData(3)]
 public async Task EffectiveMonthlySalaryAndSnapshotsHonorConfiguredCurrencyPrecision(int minor)
 {
  await using var f=await SecurityTestScope.CreateAsync();var staff=await Setup(f,minor);var salary=minor==0?100m:100.001m;await Rate(f,staff,Start,0,"Monthly",salary);await Service(f).SaveAttendance(new(){StaffId=staff,Date=End,PaidDays=1,Note="One paid day"});var p=await Policy(f,"Monthly",minor);await Service(f).Calculate(new(){Reference="PRECISION",PeriodStart=Start,PolicyId=p});var line=await f.Db.HRPayrollLines.SingleAsync();Assert.Equal(salary,line.MonthlySalary);Assert.Equal(decimal.Round(salary/31,minor,MidpointRounding.AwayFromZero),line.BasePay);Assert.Equal(line.BasePay,decimal.Round(line.BasePay,minor));
 }
 [Fact] public async Task DuplicateEffectiveTermsAreRejectedButReviewedDraftCorrectionsAppendHistory()
 {
  await using var f=await SecurityTestScope.CreateAsync();var staff=await Setup(f);await Rate(f,staff,Start,100);await Assert.ThrowsAsync<InvalidOperationException>(()=>Rate(f,staff,Start,100));await Rate(f,staff,Start,110);var history=await f.Db.HRPayrollCompensations.OrderBy(x=>x.Version).ToListAsync();Assert.Equal(2,history.Count);Assert.Equal(100,history[0].DailyRate);Assert.Equal(110,history[1].DailyRate);Assert.Equal(2,history[1].Version);
 }

}
