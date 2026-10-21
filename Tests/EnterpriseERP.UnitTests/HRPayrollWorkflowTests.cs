using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class HRPayrollWorkflowTests
{
 private static DateOnly Start=>new DateOnly(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1).AddMonths(-1);
 private static DateOnly End=>Start.AddMonths(1).AddDays(-1);
 private static HRPayrollService Service(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
 private static async Task Setup(SecurityTestScope f,int minorUnits=2)
 {await CompanyBusinessPolicyTestData.SeedAsync(f,minorUnits:minorUnits);await f.GrantAsync("*");await f.GrantAsync("*","approver-a");}
 private static HRPayrollPolicyInput PolicyInput(int decimals=2)=>new(){Name="Synthetic daily-rated policy",Currency="BDT",CurrencyDecimals=decimals,EffectiveFrom=Start,EffectiveThrough=End,DeductionPercent=5,EmployerContributionPercent=3,JurisdictionAndBasis="Synthetic test only; explicit daily-rated contract and reviewed test deductions, not statutory guidance.",ExpenseAccount="6100",PayableAccount="2300",DeductionAccount="2310",EmployerExpenseAccount="6110",EmployerPayableAccount="2320"};
 private static async Task<int> Policy(SecurityTestScope f,int decimals=2)
 {
  var id=await Service(f).CreatePolicy(PolicyInput(decimals));var p=await f.Db.HRPayrollPolicies.SingleAsync(x=>x.Id==id);f.ActAs("approver-a");await Service(f).ReviewPolicy(id,p.Revision,"Checked test currency, dates, rates and accounts",true);f.ActAs();return id;
 }
 private static async Task<int> Staff(SecurityTestScope f,string code="STAFF-1",DateOnly? start=null)
 {
  var id=await Service(f).SaveStaff(new(){Code=code,Name="Employee "+code,JobTitle="Farm operator",StartDate=start??End.AddDays(-1)});var s=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==id);await Service(f).SetRates(new(){EffectiveFrom=start??End.AddDays(-1),StaffId=id,DailyRate=100,OvertimeHourlyRate=20,Revision=s.Revision,ReviewNote="Synthetic approved contractual rates"});return id;
 }
 private static async Task Attendance(SecurityTestScope f,int id)
 {await Service(f).SaveAttendance(new(){StaffId=id,Date=End.AddDays(-1),PaidDays=1,Note="Reviewed full day"});await Service(f).SaveAttendance(new(){StaffId=id,Date=End,PaidDays=.5m,OvertimeHours=2,Note="Reviewed half day plus overtime"});}
 private static async Task<long> Draft(SecurityTestScope f)
 {var policy=await Policy(f);var staff=await Staff(f);await Attendance(f,staff);return await Service(f).Calculate(new(){Reference="PAY-1",PeriodStart=Start,PolicyId=policy});}
 private static async Task<HRPayrollRun> Approve(SecurityTestScope f,long id)
 {var r=await f.Db.HRPayrollRuns.SingleAsync(x=>x.Id==id);f.ActAs("approver-a");await Service(f).Approve(id,r.Revision,"Reviewed all lines and source records",true);return await f.Db.HRPayrollRuns.Include(x=>x.Lines).SingleAsync(x=>x.Id==id);}
 [Fact] public async Task FullWorkflowProducesExactReviewedBalancedAccrualAndAudit()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var draft=await f.Db.HRPayrollRuns.Include(x=>x.Lines).SingleAsync();var line=Assert.Single(draft.Lines);
  Assert.Equal(150m,line.BasePay);Assert.Equal(40m,line.OvertimePay);Assert.Equal(190m,line.GrossPay);Assert.Equal(9.50m,line.Deductions);Assert.Equal(180.50m,line.NetPay);Assert.Equal(5.70m,line.EmployerContribution);Assert.Empty(await f.Db.Vouchers.ToListAsync());
  var approved=await Approve(f,id);await Service(f).Post(id,approved.Revision,End);var voucher=await f.Db.Vouchers.Include(x=>x.Lines).SingleAsync();Assert.Equal(195.70m,voucher.Lines.Sum(x=>x.Debit));Assert.Equal(voucher.Lines.Sum(x=>x.Debit),voucher.Lines.Sum(x=>x.Credit));Assert.Equal(180.50m,voucher.Lines.Single(x=>x.AccountCode=="2300").Credit);Assert.Equal("Posted",(await f.Db.HRPayrollRuns.SingleAsync()).Status);Assert.Empty(await f.Db.FinancePayments.ToListAsync());Assert.Single(await f.Db.Audits.Where(x=>x.Action=="Payroll.Post").ToListAsync());
 }
 [Fact] public async Task IndependentPolicyAndPayrollReviewAndExplicitConfirmationRequired()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var policy=await Service(f).CreatePolicy(PolicyInput());var p=await f.Db.HRPayrollPolicies.SingleAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).ReviewPolicy(policy,p.Revision,"Self review",true));f.ActAs("approver-a");p=await f.Db.HRPayrollPolicies.SingleAsync();await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).ReviewPolicy(policy,p.Revision,"No confirmation",false));await Service(f).ReviewPolicy(policy,p.Revision,"Independent policy review",true);
  f.ActAs();var staff=await Staff(f);await Attendance(f,staff);var id=await Service(f).Calculate(new(){Reference="P",PeriodStart=Start,PolicyId=policy});var r=await f.Db.HRPayrollRuns.SingleAsync();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Approve(id,r.Revision,"Self review",true));f.ActAs("approver-a");r=await f.Db.HRPayrollRuns.SingleAsync();await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Approve(id,r.Revision,"Unconfirmed review",false));
 }
 [Fact] public async Task EmployeeCannotEscalateThroughPayrollOrIdentityOperations()
 {
  await using var f=await SecurityTestScope.CreateAsync();await CompanyBusinessPolicyTestData.SeedAsync(f);await f.GrantAsync("Employee.Self");
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).BrowseStaff(null,null));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).CreatePolicy(PolicyInput()));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Calculate(new(){Reference="P",PolicyId=1,PeriodStart=Start}));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).LinkIdentity(1,"operator-a","token",true));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Post(1,"token",End));
 }
 [Fact] public async Task GeneralHrContractsAndPagesNeverReturnCompensation()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);await Staff(f);f.Db.UserPermissions.RemoveRange(await f.Db.UserPermissions.Where(x=>x.UserId=="operator-a").ToListAsync());await f.Db.SaveChangesAsync();await f.GrantAsync("HR.View");await f.GrantAsync("HR.Manage");
  Assert.Single((await Service(f).BrowseStaff(null,null)).Items);Assert.DoesNotContain(typeof(HRPayrollStaffInput).GetProperties(),p=>p.Name.Contains("Rate")||p.Name.Contains("Pay"));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Rates(1));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Register(null,null));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Payslip(1,false));
 }
 [Fact] public async Task IdentityLinkRequiresExactVerifiedMemberAndProtectedPermission()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Staff(f);var s=await f.Db.HRPayrollStaff.SingleAsync();await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).LinkIdentity(id,"operator-a",s.Revision,true));s=await f.Db.HRPayrollStaff.SingleAsync();await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).LinkIdentity(id,"operator-b",s.Revision,true));
  var user=await f.Db.Users.SingleAsync(x=>x.Id=="operator-a");user.EmailConfirmed=true;await f.Db.SaveChangesAsync();s=await f.Db.HRPayrollStaff.SingleAsync();await Service(f).LinkIdentity(id,user.Id,s.Revision,true);s=await f.Db.HRPayrollStaff.SingleAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).LinkIdentity(id,"approver-a",s.Revision,true));
 }
 [Fact] public async Task IdentityOwnershipIsUniqueAndOwnSlipIdTamperingIsBlocked()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var first=await Staff(f,"ONE");var second=await Staff(f,"TWO");await Attendance(f,first);await Attendance(f,second);var user=await f.Db.Users.SingleAsync(x=>x.Id=="operator-a");user.EmailConfirmed=true;await f.Db.SaveChangesAsync();var s=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==first);await Service(f).LinkIdentity(first,user.Id,s.Revision,true);s=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==second);await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).LinkIdentity(second,user.Id,s.Revision,true));
  var policy=await Policy(f);var run=await Service(f).Calculate(new(){Reference="BOTH",PeriodStart=Start,PolicyId=policy});var r=await Approve(f,run);await Service(f).Post(run,r.Revision,End);f.ActAs();var page=await Service(f).Own();Assert.Single(page.Payslips);Assert.Equal(first,page.Payslips[0].Line.StaffId);var other=await f.Db.HRPayrollLines.SingleAsync(x=>x.StaffId==second);await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Payslip(other.Id,true));
 }
 [Fact] public async Task OwnLeaveIgnoresForgedStaffSelectionAndCannotApproveItself()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var one=await Staff(f,"ONE");var two=await Staff(f,"TWO");var user=await f.Db.Users.SingleAsync(x=>x.Id=="operator-a");user.EmailConfirmed=true;await f.Db.SaveChangesAsync();var s=await f.Db.HRPayrollStaff.SingleAsync(x=>x.Id==one);await Service(f).LinkIdentity(one,user.Id,s.Revision,true);
  await Service(f).RequestLeave(new(){StaffId=two,From=End,Through=End,Paid=true,Reason="Planned leave"},true);var leave=await f.Db.HRPayrollLeaves.SingleAsync();Assert.Equal(one,leave.StaffId);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).ReviewLeave(leave.Id,leave.Revision,true,"Self approval"));
 }
 [Fact] public async Task PaidLeaveAndOvertimeAreDeterministicAndPendingLeaveBlocksCalculation()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var policy=await Policy(f);var staff=await Staff(f);await Service(f).SaveAttendance(new(){StaffId=staff,Date=End.AddDays(-1),PaidDays=.5m,OvertimeHours=1,Note="Half day"});await Service(f).RequestLeave(new(){StaffId=staff,From=End,Through=End,Paid=true,Reason="Reviewed leave request"},false);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Calculate(new(){Reference="LEAVE",PeriodStart=Start,PolicyId=policy}));var leave=await f.Db.HRPayrollLeaves.SingleAsync();f.ActAs("approver-a");await Service(f).ReviewLeave(leave.Id,leave.Revision,true,"Approved paid day");f.ActAs();await Service(f).Calculate(new(){Reference="LEAVE",PeriodStart=Start,PolicyId=policy});var line=await f.Db.HRPayrollLines.SingleAsync();Assert.Equal(1,line.PaidLeaveDays);Assert.Equal(170,line.GrossPay);
 }
 [Fact] public async Task MissingDayNeverSilentlyReducesSalary()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var p=await Policy(f);var staff=await Staff(f);await Service(f).SaveAttendance(new(){StaffId=staff,Date=End,PaidDays=1,Note="One date only"});await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Calculate(new(){Reference="MISSING",PolicyId=p,PeriodStart=Start}));Assert.Empty(await f.Db.HRPayrollRuns.ToListAsync());
 }
 [Fact] public async Task AttendanceAndLeaveCannotOverlapOrBeDuplicated()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var staff=await Staff(f);await Attendance(f,staff);await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).SaveAttendance(new(){StaffId=staff,Date=End,PaidDays=1,Note="Duplicate"}));await Service(f).RequestLeave(new(){StaffId=staff,From=End,Through=End,Paid=true,Reason="Overlapping request"},false);await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).RequestLeave(new(){StaffId=staff,From=End,Through=End,Reason="Duplicate request"},false));var l=await f.Db.HRPayrollLeaves.SingleAsync();f.ActAs("approver-a");await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).ReviewLeave(l.Id,l.Revision,true,"Overlapping approval"));
 }
 [Fact] public async Task ChangedSourceRequiresRecalculationAndOldVersionCannotOverwrite()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var a=await f.Db.HRPayrollAttendance.FirstAsync();var token=a.Revision;await Service(f).SaveAttendance(new(){Id=a.Id,StaffId=a.StaffId,Date=a.Date,PaidDays=.5m,Note="Correction",Revision=token});await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>Service(f).SaveAttendance(new(){Id=a.Id,StaffId=a.StaffId,Date=a.Date,PaidDays=1,Note="Stale edit",Revision=token}));var r=await f.Db.HRPayrollRuns.SingleAsync();f.ActAs("approver-a");await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Approve(id,r.Revision,"Attempt stale-source approval",true));f.ActAs();r=await f.Db.HRPayrollRuns.SingleAsync();var runToken=r.Revision;await Service(f).Recalculate(id,runToken);Assert.Equal(140,(await f.Db.HRPayrollLines.SingleAsync()).GrossPay);await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>Service(f).Recalculate(id,runToken));
 }
 [Fact] public async Task ClosedPeriodBlocksPostingWithoutAnyPartialAccrual()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var r=await Approve(f,id);f.Db.AccountingPeriodCloses.Add(new(){CompanyId=1,BranchId=11,ClosedThrough=End,ClosedBy="approver-a",Reason="Closed month"});await f.Db.SaveChangesAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(id,r.Revision,End));Assert.Empty(await f.Db.Vouchers.ToListAsync());Assert.Equal("Approved",(await f.Db.HRPayrollRuns.SingleAsync()).Status);Assert.Null((await f.Db.HRPayrollRuns.SingleAsync()).VoucherId);
 }
 [Fact] public async Task AuditFailureRollsBackVoucherRunStateAndTracker()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var r=await Approve(f,id);await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectPayrollAudit BEFORE INSERT ON Audits WHEN NEW.Action='Payroll.Post' BEGIN SELECT RAISE(ABORT,'simulated audit failure'); END;");await Assert.ThrowsAsync<DbUpdateException>(()=>Service(f).Post(id,r.Revision,End));Assert.Empty(f.Db.ChangeTracker.Entries());Assert.Empty(await f.Db.Vouchers.ToListAsync());Assert.Equal("Approved",(await f.Db.HRPayrollRuns.SingleAsync()).Status);Assert.Null((await f.Db.HRPayrollRuns.SingleAsync()).VoucherId);
 }
 [Fact] public async Task RepeatedCalculateApproveAndPostCannotDuplicateEffects()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var r=await f.Db.HRPayrollRuns.SingleAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Calculate(new(){Reference="OTHER",PeriodStart=Start,PolicyId=r.PolicyId}));r=await Approve(f,id);var token=r.Revision;await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Approve(id,token,"Repeated approval",true));r=await f.Db.HRPayrollRuns.SingleAsync();await Service(f).Post(id,r.Revision,End);r=await f.Db.HRPayrollRuns.SingleAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(id,r.Revision,End));Assert.Single(await f.Db.Vouchers.ToListAsync());Assert.Single(await f.Db.HRPayrollLines.ToListAsync());
 }
 [Fact] public async Task ApprovedPeriodLocksSourceAndSnapshotsRemainImmutable()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var r=await Approve(f,id);var a=await f.Db.HRPayrollAttendance.FirstAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).SaveAttendance(new(){Id=a.Id,StaffId=a.StaffId,Date=a.Date,PaidDays=0,Note="Forbidden change",Revision=a.Revision}));r=await f.Db.HRPayrollRuns.Include(x=>x.Lines).SingleAsync();r.Lines[0].NetPay=1;await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());f.Db.ChangeTracker.Clear();var p=await f.Db.HRPayrollPolicies.SingleAsync();p.DeductionPercent=99;await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());
 }
 [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
 public async Task ApprovedCurrencyPrecisionIsHonoredAtEveryAmount(int decimals)
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f,decimals);var p=await Policy(f,decimals);var staff=await Staff(f);var s=await f.Db.HRPayrollStaff.SingleAsync();await Service(f).SetRates(new(){EffectiveFrom=End.AddDays(-1),StaffId=staff,DailyRate=10.555m,OvertimeHourlyRate=1.111m,Revision=s.Revision,ReviewNote="Fractional test rate"});await Attendance(f,staff);await Service(f).Calculate(new(){Reference="ROUND",PolicyId=p,PeriodStart=Start});var l=await f.Db.HRPayrollLines.SingleAsync();foreach(var amount in new[]{l.BasePay,l.OvertimePay,l.GrossPay,l.Deductions,l.NetPay,l.EmployerContribution})Assert.Equal(amount,decimal.Round(amount,decimals));Assert.Equal(l.GrossPay-l.Deductions,l.NetPay);
 }
 [Fact] public async Task MismatchedUnreviewedExpiredOrAbsentBusinessPolicyBlocksPayroll()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).CreatePolicy(PolicyInput()));await CompanyBusinessPolicyTestData.SeedAsync(f);var input=PolicyInput();input.Currency="USD";await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).CreatePolicy(input));input=PolicyInput();input.CurrencyDecimals=3;await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).CreatePolicy(input));var id=await Service(f).CreatePolicy(PolicyInput());await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Calculate(new(){Reference="UNREVIEWED",PolicyId=id,PeriodStart=Start}));
 }
 [Theory][InlineData(-1)][InlineData(1.1)][InlineData(0.25)]
 public async Task InvalidAttendanceFractionsAreRejected(decimal paidDays)
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var staff=await Staff(f);await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).SaveAttendance(new(){StaffId=staff,Date=End,PaidDays=paidDays,Note="Invalid"}));Assert.Empty(await f.Db.HRPayrollAttendance.ToListAsync());
 }
 [Fact] public async Task BranchAndCompanyIsolationAppliesToReadEditReviewAndPosting()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var staff=await f.Db.HRPayrollStaff.SingleAsync();var r=await f.Db.HRPayrollRuns.SingleAsync();await CompanyBusinessPolicyTestData.SeedAsync(f,2);await f.GrantAsync("*","operator-b",2);f.ActAs("operator-b",2,21);Assert.Empty((await Service(f).BrowseStaff(null,null)).Items);Assert.Empty((await Service(f).Register(null,null)).Items);await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Rates(staff.Id));await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Details(id));await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Approve(id,r.Revision,"Other company",true));
  f.ActAs();f.Db.UserBranchAccess.Add(new(){UserId="operator-a",CompanyId=1,BranchId=12});await f.Db.SaveChangesAsync();f.ActAs("operator-a",1,12);Assert.Empty((await Service(f).Register(null,null)).Items);await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).StaffForm(staff.Id));await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(id,r.Revision,End));
 }
 [Fact] public async Task DirectRelationalForeignKeyPreventsCrossBranchStaffReference()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var staff=await Staff(f);f.Db.HRPayrollAttendance.Add(new(){CompanyId=1,BranchId=12,StaffId=staff,Date=End,PaidDays=1});await Assert.ThrowsAsync<DbUpdateException>(()=>f.Db.SaveChangesAsync());
 }
 [Fact] public async Task StaffCanDeactivateAfterPriorPostedMonthWithoutDeletingHistory()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var r=await Approve(f,id);await Service(f).Post(id,r.Revision,End);f.ActAs();var s=await Service(f).StaffForm((await f.Db.HRPayrollStaff.SingleAsync()).Id);s.Active=false;s.EndDate=DateOnly.FromDateTime(DateTime.UtcNow);await Service(f).SaveStaff(s);Assert.False((await f.Db.HRPayrollStaff.SingleAsync()).Active);Assert.Single(await f.Db.HRPayrollLines.ToListAsync());Assert.Equal(2,await f.Db.HRPayrollAttendance.CountAsync());
 }
 [Fact] public async Task PaginationSearchAndStatusDoNotLeakOtherBranchesOrSalary()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);for(var n=0;n<23;n++)await Service(f).SaveStaff(new(){Code=$"S{n:00}",Name="Farm worker",StartDate=End});var page=await Service(f).BrowseStaff("Farm","Active",2);Assert.Equal(23,page.Total);Assert.Equal(3,page.Items.Count);Assert.Equal(2,page.Page);Assert.Equal("S20",page.Items[0].Code);var clamped=await Service(f).BrowseStaff("S00","Active",999);Assert.Single(clamped.Items);Assert.Equal(1,clamped.Page);
 }
 [Fact] public async Task OriginalTokensProtectStaffRatesPolicyAndLeaveTransitions()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var staff=await Staff(f);var input=await Service(f).StaffForm(staff);var old=input.Revision;input.JobTitle="Changed role";await Service(f).SaveStaff(input);input.Revision=old;input.JobTitle="Stale overwrite";await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>Service(f).SaveStaff(input));
  await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>Service(f).SetRates(new(){EffectiveFrom=End.AddDays(-1),StaffId=staff,DailyRate=999,Revision=old!,ReviewNote="Stale rate"}));
  var p=await Service(f).CreatePolicy(PolicyInput());var policy=await f.Db.HRPayrollPolicies.SingleAsync();var token=policy.Revision;f.ActAs("approver-a");await Service(f).ReviewPolicy(p,token,"Independent review",true);await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>Service(f).ReviewPolicy(p,token,"Repeated stale review",true));f.ActAs();
  await Service(f).RequestLeave(new(){StaffId=staff,From=End,Through=End,Reason="Test leave"},false);var leave=await f.Db.HRPayrollLeaves.SingleAsync();var leaveToken=leave.Revision;f.ActAs("approver-a");await Service(f).ReviewLeave(leave.Id,leaveToken,false,"Rejected test leave");await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>Service(f).ReviewLeave(leave.Id,leaveToken,true,"Stale approval"));
 }
 [Fact] public async Task ApprovePermissionDoesNotAuthorizeAccrualPosting()
 {
  await using var f=await SecurityTestScope.CreateAsync();await Setup(f);var id=await Draft(f);var r=await Approve(f,id);f.Db.UserPermissions.RemoveRange(await f.Db.UserPermissions.Where(x=>x.UserId=="approver-a").ToListAsync());await f.Db.SaveChangesAsync();await f.GrantAsync("Payroll.Approve","approver-a");await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Post(id,r.Revision,End));Assert.Empty(await f.Db.Vouchers.ToListAsync());
 }

}
