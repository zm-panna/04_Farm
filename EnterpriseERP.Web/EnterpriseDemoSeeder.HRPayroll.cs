using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public static partial class EnterpriseDemoSeeder
{
 internal static async Task SeedHRPayroll(AppDbContext db,Company company,Branch branch,AppUser maker,AppUser reviewer,CompanyBusinessPolicy policy,TaxPolicy tax,int number,DateTimeOffset now,DateOnly today,CancellationToken ct)
 {
  if(db.Database.CurrentTransaction==null||company.Slug?.StartsWith("demo-farm-",StringComparison.Ordinal)!=true||maker.Id==reviewer.Id||branch.CompanyId!=company.Id||policy.CompanyId!=company.Id||policy.Status!="Approved")
   throw new InvalidOperationException("HR payroll fixtures require the guarded synthetic demo transaction and separate fictional maker/reviewer identities.");
  var start=new DateOnly(today.Year,today.Month,1).AddMonths(-1);var end=start.AddMonths(1).AddDays(-1);
  if(policy.EffectiveFrom>start||(policy.EffectiveTo.HasValue&&policy.EffectiveTo.Value<end))throw new InvalidOperationException("Demo company policy must cover the previous fully ended payroll month.");
  decimal money(decimal value)=>CompanyBusinessPolicyService.RoundMoney(value,policy.CurrencyMinorUnits);
  var p=new HRPayrollPolicy{CompanyId=company.Id,BranchId=branch.Id,CompanyBusinessPolicyId=policy.Id,Name=$"DEMO FICTIONAL daily payroll {number}",Currency=policy.CurrencyCode,CurrencyDecimals=policy.CurrencyMinorUnits,CountryCode=policy.CountryCode,RegionCode=policy.RegionCode,TimeZoneId=policy.TimeZoneId,EffectiveFrom=start,EffectiveThrough=end,DeductionPercent=5,EmployerContributionPercent=3,JurisdictionAndBasis="DEMO FICTIONAL test percentages and daily-rated contract only. Not statutory guidance, actual employee compensation, or real approval.",ExpenseAccount="6100",PayableAccount="2300",DeductionAccount="2310",EmployerExpenseAccount="6110",EmployerPayableAccount="2320",CreatedBy=maker.Id,Reviewed=true,ReviewedBy=reviewer.Id,ReviewNote="DEMO FICTIONAL seeded review scenario, not real approval."};
  var link=maker.EmailConfirmed&&await db.UserCompanyAccess.AnyAsync(x=>x.CompanyId==company.Id&&x.UserId==maker.Id,ct)&&await db.UserBranchAccess.AnyAsync(x=>x.CompanyId==company.Id&&x.BranchId==branch.Id&&x.UserId==maker.Id,ct)?maker.Id:null;
  var staff=new HRPayrollStaff{CompanyId=company.Id,BranchId=branch.Id,Code=$"DEMO-HR-{number}",Name=$"DEMO FICTIONAL Employee {number}",JobTitle="DEMO FICTIONAL farm operator",StartDate=end.AddDays(-1),Active=true,IdentityUserId=link,DailyRate=money(100.125m),OvertimeHourlyRate=money(20.125m)};
  db.HRPayrollPolicies.Add(p);db.HRPayrollStaff.Add(staff);await db.SaveChangesAsync(ct);
  var compensation=new HRPayrollCompensation{CompanyId=company.Id,BranchId=branch.Id,StaffId=staff.Id,Version=1,EffectiveFrom=staff.StartDate,PayBasis="Daily",DailyRate=staff.DailyRate,MonthlySalary=0,OvertimeHourlyRate=staff.OvertimeHourlyRate,ReviewedBy=maker.Id,ReviewedUtc=now,ReviewNote="DEMO FICTIONAL effective-dated contract; not actual compensation or real review."};db.HRPayrollCompensations.Add(compensation);await db.SaveChangesAsync(ct);
  var attendance=new HRPayrollAttendance{CompanyId=company.Id,BranchId=branch.Id,StaffId=staff.Id,Date=end,PaidDays=1,OvertimeHours=2,RecordedBy=maker.Id,Note="DEMO FICTIONAL reviewed attendance and overtime scenario."};
  var leave=new HRPayrollLeave{CompanyId=company.Id,BranchId=branch.Id,StaffId=staff.Id,From=end.AddDays(-1),Through=end.AddDays(-1),Paid=true,Reason="DEMO FICTIONAL paid leave request.",Status="Approved",RequestedBy=maker.Id,ReviewedBy=reviewer.Id,ReviewNote="DEMO FICTIONAL independent approval scenario."};
  db.HRPayrollAttendance.Add(attendance);db.HRPayrollLeaves.Add(leave);await db.SaveChangesAsync(ct);
  var basePay=money(2*staff.DailyRate);var overtime=money(2*staff.OvertimeHourlyRate);var gross=basePay+overtime;var deduction=money(gross*p.DeductionPercent/100m);var employer=money(gross*p.EmployerContributionPercent/100m);
  var reference=$"DEMO-PAY-{number}";
  var voucher=BusinessInput.Voucher(company.Id,branch.Id,"PAYROLL-"+reference,(p.ExpenseAccount,gross,0),(p.PayableAccount,0,gross-deduction),(p.DeductionAccount,0,deduction),(p.EmployerExpenseAccount,employer,0),(p.EmployerPayableAccount,0,employer));voucher.Date=new DateTimeOffset(end.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);db.Vouchers.Add(voucher);await db.SaveChangesAsync(ct);
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{Policy=p,Staff=new[]{staff},Compensation=new[]{compensation},Attendance=new[]{attendance},Leaves=new[]{leave}}))));
  var run=new HRPayrollRun{CompanyId=company.Id,BranchId=branch.Id,PolicyId=p.Id,Reference=reference,PeriodStart=start,PeriodEnd=end,Currency=p.Currency,CurrencyDecimals=p.CurrencyDecimals,Status="Posted",SourceHash=hash,CreatedBy=maker.Id,ApprovedBy=reviewer.Id,ApprovedUtc=now,ReviewNote="DEMO FICTIONAL seeded reviewed payroll. No actual employee or payment.",PostedBy=reviewer.Id,PostedUtc=now,PostingDate=end,VoucherId=voucher.Id,Lines=new(){new(){CompensationId=compensation.Id,CompensationVersion=compensation.Version,CompensationEffectiveFrom=compensation.EffectiveFrom,CompanyId=company.Id,BranchId=branch.Id,StaffId=staff.Id,StaffCode=staff.Code,StaffName=staff.Name,DailyRate=staff.DailyRate,OvertimeHourlyRate=staff.OvertimeHourlyRate,PaidUnitsUsed=2,AttendanceDays=1,PaidLeaveDays=1,OvertimeHours=2,BasePay=basePay,OvertimePay=overtime,GrossPay=gross,Deductions=deduction,NetPay=gross-deduction,EmployerContribution=employer}}};
  db.HRPayrollRuns.Add(run);db.Audits.Add(new(){CompanyId=company.Id,ActorId=reviewer.Id,Action="Demo.HRPayroll.Seed",Reference=reference+" DEMO FICTIONAL; no real approval or payment",CreatedUtc=now});await db.SaveChangesAsync(ct);
 }
}
