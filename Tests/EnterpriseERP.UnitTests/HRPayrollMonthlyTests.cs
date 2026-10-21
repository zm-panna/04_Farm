using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class HRPayrollMonthlyTests
{
 private static HRPayrollService Service(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
 private static async Task<(int staff,int policy)> Setup(SecurityTestScope f,DateOnly month,string mode="ActualCalendarDays",decimal fixedUnits=0,bool cap=false,int? employedFromDay=null)
 {
  await CompanyBusinessPolicyTestData.SeedAsync(f);await f.GrantAsync("*");await f.GrantAsync("*","approver-a");var end=month.AddMonths(1).AddDays(-1);
  var staff=await Service(f).SaveStaff(new(){Code="MONTHLY",Name="Monthly contract employee",StartDate=employedFromDay.HasValue?new(month.Year,month.Month,employedFromDay.Value):month});var s=await f.Db.HRPayrollStaff.SingleAsync();await Service(f).SetRates(new(){EffectiveFrom=month,StaffId=staff,DailyRate=0,PayBasis="Monthly",MonthlySalary=3100,OvertimeHourlyRate=20,Revision=s.Revision,ReviewNote="Explicit monthly test contract"});
  var policy=await Service(f).CreatePolicy(new(){Name="Explicit monthly policy",PayBasis="Monthly",MonthlyProrationMode=mode,MonthlyFixedUnits=fixedUnits,CapMonthlyPaidUnits=cap,Currency="BDT",CurrencyDecimals=2,EffectiveFrom=month,EffectiveThrough=end,DeductionPercent=5,EmployerContributionPercent=3,JurisdictionAndBasis="Synthetic monthly proration chosen for test only; no statutory formula inferred.",ExpenseAccount="6100",PayableAccount="2300",DeductionAccount="2310",EmployerExpenseAccount="6110",EmployerPayableAccount="2320"});var p=await f.Db.HRPayrollPolicies.SingleAsync();f.ActAs("approver-a");await Service(f).ReviewPolicy(policy,p.Revision,"Monthly denominator and cap independently checked",true);f.ActAs();return(staff,policy);
 }
 private static async Task Attend(SecurityTestScope f,int staff,DateOnly from,DateOnly through,DateOnly? unpaid=null)
 {for(var date=from;date<=through;date=date.AddDays(1))f.Db.HRPayrollAttendance.Add(new(){CompanyId=1,BranchId=11,StaffId=staff,Date=date,PaidDays=date==unpaid?0:1,Note="Explicit synthetic paid calendar day, including contracted paid rest days",RecordedBy="operator-a"});await f.Db.SaveChangesAsync();}
 [Theory][InlineData(2025,2,28)][InlineData(2024,2,29)][InlineData(2025,4,30)][InlineData(2025,1,31)]
 public async Task ActualCalendarPolicyPaysExactMonthlySalaryAcrossMonthLengths(int year,int month,int days)
 {
  await using var f=await SecurityTestScope.CreateAsync();var start=new DateOnly(year,month,1);var setup=await Setup(f,start);await Attend(f,setup.staff,start,start.AddMonths(1).AddDays(-1));await Service(f).Calculate(new(){Reference="MONTH",PeriodStart=start,PolicyId=setup.policy});var line=await f.Db.HRPayrollLines.SingleAsync();Assert.Equal("Monthly",line.PayBasis);Assert.Equal("ActualCalendarDays",line.ProrationMode);Assert.Equal(days,line.ProrationDenominator);Assert.Equal(days,line.PaidUnitsUsed);Assert.Equal(3100,line.MonthlySalary);Assert.Equal(3100,line.BasePay);Assert.Equal(0,line.DailyRate);Assert.Equal(2945,line.NetPay);
 }
 [Theory][InlineData(false,3203.33)][InlineData(true,3100)]
 public async Task FixedUnitsAndExplicitCapAreAppliedExactlyAsReviewed(bool cap,decimal expected)
 {
  await using var f=await SecurityTestScope.CreateAsync();var start=new DateOnly(2025,1,1);var setup=await Setup(f,start,"FixedPaidUnits",30,cap);await Attend(f,setup.staff,start,new(2025,1,31));await Service(f).Calculate(new(){Reference="FIXED",PeriodStart=start,PolicyId=setup.policy});var l=await f.Db.HRPayrollLines.SingleAsync();Assert.Equal(30,l.ProrationDenominator);Assert.Equal(cap?30:31,l.PaidUnitsUsed);Assert.Equal(expected,l.BasePay);
 }
 [Fact] public async Task PartialEmploymentPaidAndUnpaidLeaveAndOvertimeFollowExplicitMonthlyFormula()
 {
  await using var f=await SecurityTestScope.CreateAsync();var start=new DateOnly(2025,1,1);var setup=await Setup(f,start,employedFromDay:16);await Attend(f,setup.staff,new(2025,1,18),new(2025,1,31));await Service(f).RequestLeave(new(){StaffId=setup.staff,From=new(2025,1,16),Through=new(2025,1,16),Paid=true,Reason="Explicit paid day"},false);await Service(f).RequestLeave(new(){StaffId=setup.staff,From=new(2025,1,17),Through=new(2025,1,17),Paid=false,Reason="Explicit unpaid day"},false);f.ActAs("approver-a");foreach(var leave in await f.Db.HRPayrollLeaves.ToListAsync())await Service(f).ReviewLeave(leave.Id,leave.Revision,true,"Reviewed treatment");f.ActAs();var attendance=await f.Db.HRPayrollAttendance.FirstAsync();await Service(f).SaveAttendance(new(){Id=attendance.Id,StaffId=setup.staff,Date=attendance.Date,PaidDays=1,OvertimeHours=2,Note="Reviewed overtime",Revision=attendance.Revision});
  await Service(f).Calculate(new(){Reference="PARTIAL",PeriodStart=start,PolicyId=setup.policy});var l=await f.Db.HRPayrollLines.SingleAsync();Assert.Equal(14,l.AttendanceDays);Assert.Equal(1,l.PaidLeaveDays);Assert.Equal(15,l.PaidUnitsUsed);Assert.Equal(31,l.ProrationDenominator);Assert.Equal(1500,l.BasePay);Assert.Equal(40,l.OvertimePay);Assert.Equal(1540,l.GrossPay);
 }
 [Fact] public async Task MonthlyRateDriftRequiresRecalculationAndApprovedFormulaCannotBeChanged()
 {
  await using var f=await SecurityTestScope.CreateAsync();var start=new DateOnly(2025,2,1);var setup=await Setup(f,start);await Attend(f,setup.staff,start,new(2025,2,28));var id=await Service(f).Calculate(new(){Reference="DRIFT",PeriodStart=start,PolicyId=setup.policy});var s=await f.Db.HRPayrollStaff.SingleAsync();await Service(f).SetRates(new(){EffectiveFrom=start,StaffId=s.Id,PayBasis="Monthly",MonthlySalary=2800,OvertimeHourlyRate=20,Revision=s.Revision,ReviewNote="Revised explicit monthly amount"});f.ActAs("approver-a");var run=await f.Db.HRPayrollRuns.SingleAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Approve(id,run.Revision,"Old amount cannot be reviewed",true));f.ActAs();run=await f.Db.HRPayrollRuns.SingleAsync();await Service(f).Recalculate(id,run.Revision);Assert.Equal(2800,(await f.Db.HRPayrollLines.SingleAsync()).BasePay);
  var p=await f.Db.HRPayrollPolicies.SingleAsync();p.MonthlyFixedUnits=30;p.MonthlyProrationMode="FixedPaidUnits";await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());
 }
 [Theory][InlineData(0)][InlineData(-1)][InlineData(366.1)]
 public async Task FixedMonthlyDenominatorMustBeExplicitPositiveAndBounded(decimal units)
 {
  await using var f=await SecurityTestScope.CreateAsync();await Assert.ThrowsAsync<ArgumentException>(()=>Setup(f,new(2025,1,1),"FixedPaidUnits",units));Assert.Empty(await f.Db.HRPayrollPolicies.ToListAsync());
 }
 [Fact] public async Task DraftCanAdoptCorrectedReviewedPolicyWithoutRewritingOldPolicy()
 {
  await using var f=await SecurityTestScope.CreateAsync();var start=new DateOnly(2025,1,1);var setup=await Setup(f,start);await Attend(f,setup.staff,start,new(2025,1,31));var id=await Service(f).Calculate(new(){Reference="REPLACE",PeriodStart=start,PolicyId=setup.policy});
  var replacement=await Service(f).CreatePolicy(new(){Name="Corrected explicit monthly basis",PayBasis="Monthly",MonthlyProrationMode="FixedPaidUnits",MonthlyFixedUnits=30,CapMonthlyPaidUnits=false,Currency="BDT",CurrencyDecimals=2,EffectiveFrom=start,EffectiveThrough=new(2025,1,31),DeductionPercent=0,EmployerContributionPercent=0,JurisdictionAndBasis="Synthetic correction only, independently reviewed fixed 30-unit uncapped formula.",ExpenseAccount="6100",PayableAccount="2300",DeductionAccount="2310",EmployerExpenseAccount="6110",EmployerPayableAccount="2320"});
  var replacementRow=await f.Db.HRPayrollPolicies.SingleAsync(x=>x.Id==replacement);f.ActAs("approver-a");await Service(f).ReviewPolicy(replacement,replacementRow.Revision,"Explicit correction independently reviewed",true);f.ActAs();var run=await f.Db.HRPayrollRuns.SingleAsync();await Service(f).RecalculateWithPolicy(id,run.Revision,replacement);Assert.Equal(replacement,(await f.Db.HRPayrollRuns.SingleAsync()).PolicyId);Assert.Equal(3203.33m,(await f.Db.HRPayrollLines.SingleAsync()).BasePay);Assert.Equal("ActualCalendarDays",(await f.Db.HRPayrollPolicies.SingleAsync(x=>x.Id==setup.policy)).MonthlyProrationMode);
 }

}
