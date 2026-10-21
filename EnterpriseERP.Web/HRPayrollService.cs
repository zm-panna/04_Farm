using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

// HR-facing contracts deliberately do not contain compensation or payroll totals.
public sealed class HRPayrollStaffInput
{
 public int Id {get;set;}
 [Required,StringLength(40)] public string Code {get;set;}="";
 [Required,StringLength(160)] public string Name {get;set;}="";
 [StringLength(200)] public string? JobTitle {get;set;}
 public DateOnly StartDate {get;set;}=DateOnly.FromDateTime(DateTime.UtcNow);
 public DateOnly? EndDate {get;set;}
 public bool Active {get;set;}=true;
 public string? Revision {get;set;}
 public bool HasIdentity {get;set;}
}
public sealed class HRPayrollRateInput
{
 [Required] public DateOnly? EffectiveFrom {get;set;}
 public List<HRPayrollCompensation> History {get;set;}=new();
 [Required] public string PayBasis {get;set;}="Daily";
 public string Currency {get;set;}="";
 public int StaffId {get;set;}
 public string Name {get;set;}="";
 [Range(typeof(decimal),"0","999999999999.999")] public decimal DailyRate {get;set;}
 [Range(typeof(decimal),"0","999999999999.999")] public decimal MonthlySalary {get;set;}
 [Range(typeof(decimal),"0","999999999999.999")] public decimal OvertimeHourlyRate {get;set;}
 [Required] public string Revision {get;set;}="";
 [Required,StringLength(1000)] public string ReviewNote {get;set;}="";
}
public sealed class HRPayrollAttendanceInput
{
 public long Id {get;set;}
 public int StaffId {get;set;}
 public DateOnly Date {get;set;}=DateOnly.FromDateTime(DateTime.UtcNow);
 public decimal PaidDays {get;set;}=1;
 public decimal OvertimeHours {get;set;}
 [Required,StringLength(1000)] public string Note {get;set;}="";
 public string? Revision {get;set;}
}
public sealed class HRPayrollLeaveInput
{
 public int StaffId {get;set;}
 public DateOnly From {get;set;}=DateOnly.FromDateTime(DateTime.UtcNow);
 public DateOnly Through {get;set;}=DateOnly.FromDateTime(DateTime.UtcNow);
 public bool Paid {get;set;}
 [Required,StringLength(1000)] public string Reason {get;set;}="";
}
public sealed class HRPayrollPolicyInput
{
 [Required] public string PayBasis {get;set;}="Daily";
 public string MonthlyProrationMode {get;set;}="ActualCalendarDays";
 public decimal MonthlyFixedUnits {get;set;}
 public bool CapMonthlyPaidUnits {get;set;}
 [Required,StringLength(200)] public string Name {get;set;}="";
 [Required,StringLength(3,MinimumLength=3)] public string Currency {get;set;}="";
 [Range(0,3)] public int CurrencyDecimals {get;set;}=2;
 public DateOnly EffectiveFrom {get;set;}=new(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1);
 public DateOnly EffectiveThrough {get;set;}=new(DateTime.UtcNow.Year,12,31);
 [Range(typeof(decimal),"0","100")] public decimal DeductionPercent {get;set;}
 [Range(typeof(decimal),"0","100")] public decimal EmployerContributionPercent {get;set;}
 [Required,StringLength(2000)] public string JurisdictionAndBasis {get;set;}="";
 [Required,StringLength(4)] public string ExpenseAccount {get;set;}="";
 [Required,StringLength(4)] public string PayableAccount {get;set;}="";
 [Required,StringLength(4)] public string DeductionAccount {get;set;}="";
 [Required,StringLength(4)] public string EmployerExpenseAccount {get;set;}="";
 [Required,StringLength(4)] public string EmployerPayableAccount {get;set;}="";
}
public sealed class HRPayrollRunInput
{
 [Required,StringLength(60)] public string Reference {get;set;}="";
 public int PolicyId {get;set;}
 public DateOnly PeriodStart {get;set;}=new DateOnly(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1).AddMonths(-1);
}
public sealed class HRPayrollPage<T>
{
 public List<T> Items {get;set;}=new();
 public int Page {get;set;}=1;
 public int Total {get;set;}
 public int Pages=>Math.Max(1,(Total+19)/20);
 public string Search {get;set;}="";
 public string Status {get;set;}="";
 public int StaffId {get;set;}
 public List<HRPayrollStaffInput> Staff {get;set;}=new();
}
public sealed record HRPayrollPayslip(HRPayrollRun Run,HRPayrollLine Line,HRPayrollPolicy Policy);
public sealed record HRPayrollOwnPage(HRPayrollStaffInput Staff,List<HRPayrollLeave> Leaves,List<HRPayrollPayslip> Payslips,int Page,int Total);
public sealed record HRPayrollIdentityOption(string Id,string Email);

public sealed class HRPayrollService(AppDbContext db,TenantContext tenant,PermissionService permission)
{
 private async Task<DateOnly> BusinessToday(int company,CancellationToken ct)
 {var now=DateTimeOffset.UtcNow;return CompanyBusinessPolicyService.BusinessDate(await CompanyBusinessPolicyService.Current(db,company,ct,atUtc:now),now);}
 public static DateOnly CompletedMonthEnd(DateOnly start,DateOnly businessToday)
 {
  if(start.Year<2000||start.Day!=1)throw new ArgumentException("Select the first day of a payroll month.");
  var end=start.AddMonths(1).AddDays(-1);
  if(end>=businessToday)throw new ArgumentException("Calculate only a fully ended payroll month.");
  return end;
 }
 private async Task<(int company,int branch,string user)> Auth(string operation,CancellationToken ct)
 {await permission.Demand(operation,ct);return await tenant.GetAsync(ct);}
 private async Task<T> Write<T>(Func<Task<T>> action,CancellationToken ct)
 {
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  try {var result=await action();await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return result;}
  catch {await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw;}
 }
 private void Audit((int company,int branch,string user) t,string action,string reference)=>db.Audits.Add(new(){CompanyId=t.company,ActorId=t.user,Action=action,Reference=$"Branch {t.branch}: {reference[..Math.Min(175,reference.Length)]}"});
 private void Version<T>(T entity,string? expected) where T:class
 {
  if(string.IsNullOrWhiteSpace(expected)||!string.Equals(db.Entry(entity).Property<string>("Revision").CurrentValue,expected,StringComparison.Ordinal))
   throw new DbUpdateConcurrencyException("This record changed. Reload it and review the current values before trying again.");
  db.Entry(entity).Property<string>("Revision").OriginalValue=expected;
  db.Entry(entity).Property<string>("Revision").CurrentValue=Guid.NewGuid().ToString("N");
 }
 private static HRPayrollStaffInput Safe(HRPayrollStaff s)=>new(){Id=s.Id,Code=s.Code,Name=s.Name,JobTitle=s.JobTitle,StartDate=s.StartDate,EndDate=s.EndDate,Active=s.Active,Revision=s.Revision,HasIdentity=s.IdentityUserId!=null};
 private IQueryable<HRPayrollStaff> StaffQuery((int company,int branch,string user)t)=>db.HRPayrollStaff.Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);
 private async Task<HRPayrollStaff> Staff((int company,int branch,string user)t,int id,CancellationToken ct)=>await StaffQuery(t).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new InvalidOperationException("Staff member not found in the selected branch.");
 private async Task Unlocked((int company,int branch,string user)t,DateOnly from,DateOnly through,CancellationToken ct)
 {
  if(await db.HRPayrollRuns.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status!="Draft"&&x.PeriodStart<=through&&x.PeriodEnd>=from,ct))
   throw new InvalidOperationException("This date overlaps approved or posted payroll. Its source attendance and leave are locked.");
 }
 private static void Dates(DateOnly from,DateOnly through)
 {if(from.Year<2000||through<from||through.DayNumber-from.DayNumber>366)throw new ArgumentException("Use a valid date range of at most 367 days, from year 2000 onward.");}
 private static void Employment(HRPayrollStaff s,DateOnly from,DateOnly through)
 {if(from<s.StartDate||(s.EndDate.HasValue&&through>s.EndDate.Value))throw new ArgumentException("Dates must be inside the staff member's employment dates.");}
 public async Task<HRPayrollPage<HRPayrollStaffInput>> BrowseStaff(string? search,string? status,int page=1,CancellationToken ct=default)
 {
  var t=await Auth("HR.View",ct);search=BusinessInput.Text(search,"Search",160,false);status=status is "Active" or "Inactive"?status:"";
  var query=StaffQuery(t).AsNoTracking();if(search.Length>0)query=query.Where(x=>x.Code.Contains(search)||x.Name.Contains(search)||x.JobTitle.Contains(search));if(status!="")query=query.Where(x=>x.Active==(status=="Active"));
  var count=await query.CountAsync(ct);page=Math.Clamp(page,1,Math.Max(1,(count+19)/20));
  return new(){Items=(await query.OrderBy(x=>x.Code).Skip((page-1)*20).Take(20).ToListAsync(ct)).Select(Safe).ToList(),Page=page,Total=count,Search=search,Status=status};
 }
 public async Task<HRPayrollStaffInput> StaffForm(int id,CancellationToken ct=default)
 {var t=await Auth("HR.Manage",ct);return id==0?new():Safe(await Staff(t,id,ct));}
 public async Task<int> SaveStaff(HRPayrollStaffInput input,CancellationToken ct=default)
 {
  var t=await Auth("HR.Manage",ct);var today=await BusinessToday(t.company,ct);var code=BusinessInput.Text(input.Code,"Staff code",40);var name=BusinessInput.Text(input.Name,"Staff name",160);var title=BusinessInput.Text(input.JobTitle,"Job title",200,false);
  if(input.StartDate.Year<2000||input.StartDate>today||(input.EndDate.HasValue&&(input.EndDate<input.StartDate||input.EndDate>today))||input.Active==input.EndDate.HasValue)throw new ArgumentException("Active staff require no end date; deactivation requires a valid end date on or before today.");
  return await Write(async()=>{
   if(await StaffQuery(t).AnyAsync(x=>x.Code==code&&x.Id!=input.Id,ct))throw new InvalidOperationException("This staff code already exists.");
   HRPayrollStaff s;
   if(input.Id==0){await Unlocked(t,input.StartDate,input.EndDate??today,ct);s=new(){CompanyId=t.company,BranchId=t.branch};db.HRPayrollStaff.Add(s);}
   else
   {
    s=await Staff(t,input.Id,ct);Version(s,input.Revision);
    if(s.StartDate!=input.StartDate||s.EndDate!=input.EndDate)
    {
     if(s.StartDate!=input.StartDate)await Unlocked(t,s.StartDate<input.StartDate?s.StartDate:input.StartDate,(s.StartDate>input.StartDate?s.StartDate:input.StartDate).AddDays(-1),ct);
     if(s.EndDate!=input.EndDate){var a=s.EndDate??today;var b=input.EndDate??today;var first=(a<b?a:b).AddDays(1);var last=a>b?a:b;if(first<=last)await Unlocked(t,first,last,ct);}
     if(await db.HRPayrollAttendance.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id&&(x.Date<input.StartDate||(input.EndDate.HasValue&&x.Date>input.EndDate.Value)),ct)||await db.HRPayrollLeaves.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id&&x.Status!="Rejected"&&(x.From<input.StartDate||(input.EndDate.HasValue&&x.Through>input.EndDate.Value)),ct))throw new InvalidOperationException("Employment dates cannot exclude recorded attendance or pending/approved leave.");
    }
   }
   s.Code=code;s.Name=name;s.JobTitle=title;s.StartDate=input.StartDate;s.EndDate=input.EndDate;s.Active=input.Active;Audit(t,input.Id==0?"HR.Staff.Create":"HR.Staff.Update",code);await db.SaveChangesAsync(ct);return s.Id;
  },ct);
 }
 public async Task<HRPayrollRateInput> Rates(int id,CancellationToken ct=default)
 {var t=await Auth("Payroll.Rates",ct);var s=await Staff(t,id,ct);var company=await CompanyBusinessPolicyService.Current(db,t.company,ct);var history=await db.HRPayrollCompensations.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==id).OrderByDescending(x=>x.EffectiveFrom).ThenByDescending(x=>x.Version).ToListAsync(ct);var latest=history.FirstOrDefault();return new(){History=history,EffectiveFrom=latest?.EffectiveFrom??s.StartDate,PayBasis=latest?.PayBasis??s.PayBasis,Currency=company.CurrencyCode,StaffId=id,Name=s.Name,DailyRate=latest?.DailyRate??s.DailyRate,MonthlySalary=latest?.MonthlySalary??s.MonthlySalary,OvertimeHourlyRate=latest?.OvertimeHourlyRate??s.OvertimeHourlyRate,Revision=s.Revision};}
 public async Task SetRates(HRPayrollRateInput input,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.Rates",ct);BusinessInput.Decimal(input.DailyRate,"Daily rate",maximum:999999999999.999m);BusinessInput.Decimal(input.MonthlySalary,"Monthly salary",maximum:999999999999.999m);if(input.PayBasis is not ("Daily" or "Monthly")||(input.PayBasis=="Daily"&&input.DailyRate<=0)||(input.PayBasis=="Monthly"&&input.MonthlySalary<=0))throw new ArgumentException("Choose an explicit staff Daily or Monthly pay basis and a positive contractual amount for that basis.");var company=await CompanyBusinessPolicyService.Current(db,t.company,ct);CompanyBusinessPolicyService.RequireMoney(input.MonthlySalary,company.CurrencyMinorUnits);BusinessInput.Decimal(input.OvertimeHourlyRate,"Overtime rate",maximum:999999999999.999m);var note=BusinessInput.Text(input.ReviewNote,"Rate review note",1000);
  await Write(async()=>{var s=await Staff(t,input.StaffId,ct);Version(s,input.Revision);
   var effective=input.EffectiveFrom??throw new ArgumentException("Choose an explicit compensation effective date.");
   if(effective.Year<2000||effective>CompanyBusinessPolicyService.BusinessDate(company,DateTimeOffset.UtcNow)||(effective.Day!=1&&effective!=s.StartDate))throw new ArgumentException("Compensation must take effect on a month boundary or the hire date, on or before the current business date. Mid-period rate changes require a separately reviewed extension.");
   var latest=await db.HRPayrollCompensations.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id).OrderByDescending(x=>x.EffectiveFrom).ThenByDescending(x=>x.Version).FirstOrDefaultAsync(ct);
   if(latest!=null&&effective==latest.EffectiveFrom&&latest.PayBasis==input.PayBasis&&latest.DailyRate==input.DailyRate&&latest.MonthlySalary==input.MonthlySalary&&latest.OvertimeHourlyRate==input.OvertimeHourlyRate)throw new InvalidOperationException("Identical compensation terms already exist for this effective date; no duplicate version was created.");
   if(latest!=null&&effective<latest.EffectiveFrom)throw new InvalidOperationException("New compensation versions cannot predate the latest configured version; use a separately reviewed historical correction.");
   if(await db.HRPayrollRuns.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status!="Draft"&&x.PeriodEnd>=effective&&x.Lines.Any(l=>l.StaffId==s.Id),ct))throw new InvalidOperationException("Compensation cannot rewrite an approved or posted payroll period. Choose a later effective month.");
   var version=(await db.HRPayrollCompensations.Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id).Select(x=>(int?)x.Version).MaxAsync(ct)??0)+1;
   db.HRPayrollCompensations.Add(new(){CompanyId=t.company,BranchId=t.branch,StaffId=s.Id,Version=version,EffectiveFrom=effective,PayBasis=input.PayBasis,DailyRate=input.DailyRate,MonthlySalary=input.MonthlySalary,OvertimeHourlyRate=input.OvertimeHourlyRate,ReviewNote=note,ReviewedBy=t.user});
   s.PayBasis=input.PayBasis;s.DailyRate=input.DailyRate;s.MonthlySalary=input.MonthlySalary;s.OvertimeHourlyRate=input.OvertimeHourlyRate;Audit(t,"Payroll.Rates.Update",s.Code+"; "+note[..Math.Min(100,note.Length)]);return true;},ct);
 }
 public async Task<(HRPayrollStaffInput staff,List<HRPayrollIdentityOption> users)> IdentityForm(int id,CancellationToken ct=default)
 {
  var t=await Auth("HR.IdentityLink",ct);var s=await Staff(t,id,ct);
  var users=await db.Users.AsNoTracking().Where(u=>u.EmailConfirmed&&db.UserCompanyAccess.Any(c=>c.CompanyId==t.company&&c.UserId==u.Id)&&db.UserBranchAccess.Any(b=>b.CompanyId==t.company&&b.BranchId==t.branch&&b.UserId==u.Id)&&!db.HRPayrollStaff.Any(e=>e.CompanyId==t.company&&e.IdentityUserId==u.Id)).OrderBy(x=>x.Email).Select(x=>new HRPayrollIdentityOption(x.Id,x.Email??x.UserName??x.Id)).Take(200).ToListAsync(ct);
  return (Safe(s),users);
 }
 public async Task LinkIdentity(int id,string userId,string revision,bool confirm,CancellationToken ct=default)
 {
  var t=await Auth("HR.IdentityLink",ct);userId=BusinessInput.Text(userId,"Existing account ID",128);if(!confirm)throw new ArgumentException("Confirm that this exact verified account belongs to this staff member; it grants access to their payslips.");
  await Write(async()=>{var s=await Staff(t,id,ct);Version(s,revision);if(s.IdentityUserId!=null)throw new InvalidOperationException("An identity is already linked. Reassignment requires a separately reviewed account-recovery procedure.");
   if(!await db.Users.AnyAsync(x=>x.Id==userId&&x.EmailConfirmed,ct)||!await db.UserCompanyAccess.AnyAsync(x=>x.UserId==userId&&x.CompanyId==t.company,ct)||!await db.UserBranchAccess.AnyAsync(x=>x.UserId==userId&&x.CompanyId==t.company&&x.BranchId==t.branch,ct)||await db.HRPayrollStaff.AnyAsync(x=>x.CompanyId==t.company&&x.IdentityUserId==userId,ct))throw new ArgumentException("Select an unlinked, email-verified existing account with company and branch access.");
   s.IdentityUserId=userId;Audit(t,"HR.Identity.Link",s.Code+"; account "+userId);return true;
  },ct);
 }
 public async Task<HRPayrollPage<HRPayrollAttendance>> Attendance(int staffId,int page=1,CancellationToken ct=default)
 {
  var t=await Auth("HR.Attendance",ct);var q=db.HRPayrollAttendance.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);if(staffId>0){await Staff(t,staffId,ct);q=q.Where(x=>x.StaffId==staffId);}var total=await q.CountAsync(ct);page=Math.Clamp(page,1,Math.Max(1,(total+19)/20));
  return new(){Items=await q.OrderByDescending(x=>x.Date).ThenBy(x=>x.Id).Skip((page-1)*20).Take(20).ToListAsync(ct),Page=page,Total=total,StaffId=staffId,Staff=(await StaffQuery(t).AsNoTracking().OrderBy(x=>x.Code).Take(500).ToListAsync(ct)).Select(Safe).ToList()};
 }
 public async Task SaveAttendance(HRPayrollAttendanceInput input,CancellationToken ct=default)
 {
  var t=await Auth("HR.Attendance",ct);var today=await BusinessToday(t.company,ct);var note=BusinessInput.Text(input.Note,"Attendance note",1000);
  if(input.Date.Year<2000||input.Date>today||input.PaidDays is not (0m or .5m or 1m))throw new ArgumentException("Use a recorded date and paid days of 0, 0.5 or 1; record contracted paid rest days as 1 and unpaid days as 0.");BusinessInput.Decimal(input.OvertimeHours,"Overtime hours",maximum:24);
  await Write(async()=>{var s=await Staff(t,input.StaffId,ct);Employment(s,input.Date,input.Date);await Unlocked(t,input.Date,input.Date,ct);
   if(await db.HRPayrollLeaves.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id&&x.Status=="Approved"&&x.From<=input.Date&&x.Through>=input.Date,ct))throw new InvalidOperationException("Approved leave already covers this day.");
   HRPayrollAttendance a;
   if(input.Id==0){if(await db.HRPayrollAttendance.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id&&x.Date==input.Date,ct))throw new InvalidOperationException("Attendance already exists; edit it using its current version.");a=new(){CompanyId=t.company,BranchId=t.branch,StaffId=s.Id,Date=input.Date};db.HRPayrollAttendance.Add(a);}
   else{a=await db.HRPayrollAttendance.SingleOrDefaultAsync(x=>x.Id==input.Id&&x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id&&x.Date==input.Date,ct)??throw new InvalidOperationException("Attendance not found.");Version(a,input.Revision);}
   a.PaidDays=input.PaidDays;a.OvertimeHours=input.OvertimeHours;a.Note=note;a.RecordedBy=t.user;Audit(t,"HR.Attendance.Save",s.Code+" "+input.Date);return true;
  },ct);
 }
 public async Task<HRPayrollPage<HRPayrollLeave>> Leaves(string? status,int staffId,int page=1,CancellationToken ct=default)
 {
  var t=await Auth("HR.View",ct);status=status is "Pending" or "Approved" or "Rejected"?status:"";var q=db.HRPayrollLeaves.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);if(status!="")q=q.Where(x=>x.Status==status);if(staffId>0){await Staff(t,staffId,ct);q=q.Where(x=>x.StaffId==staffId);}var total=await q.CountAsync(ct);page=Math.Clamp(page,1,Math.Max(1,(total+19)/20));
  return new(){Items=await q.OrderByDescending(x=>x.Id).Skip((page-1)*20).Take(20).ToListAsync(ct),Page=page,Total=total,Status=status,StaffId=staffId,Staff=(await StaffQuery(t).AsNoTracking().OrderBy(x=>x.Code).Take(500).ToListAsync(ct)).Select(Safe).ToList()};
 }
 public async Task RequestLeave(HRPayrollLeaveInput input,bool ownOnly,CancellationToken ct=default)
 {
  var t=await Auth(ownOnly?"Employee.Self":"HR.Manage",ct);Dates(input.From,input.Through);var reason=BusinessInput.Text(input.Reason,"Leave reason",1000);
  await Write(async()=>{var s=ownOnly?await OwnStaff(t,ct):await Staff(t,input.StaffId,ct);Employment(s,input.From,input.Through);await Unlocked(t,input.From,input.Through,ct);
   if(await db.HRPayrollLeaves.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id&&x.Status!="Rejected"&&x.From<=input.Through&&x.Through>=input.From,ct))throw new InvalidOperationException("A pending or approved leave already overlaps these dates.");
   db.HRPayrollLeaves.Add(new(){CompanyId=t.company,BranchId=t.branch,StaffId=s.Id,From=input.From,Through=input.Through,Paid=input.Paid,Reason=reason,RequestedBy=t.user});Audit(t,"HR.Leave.Request",s.Code+" "+input.From+" to "+input.Through);return true;
  },ct);
 }
 public async Task ReviewLeave(long id,string revision,bool approve,string note,CancellationToken ct=default)
 {
  var t=await Auth("HR.Leave.Approve",ct);note=BusinessInput.Text(note,"Leave review note",1000);
  await Write(async()=>{var l=await db.HRPayrollLeaves.SingleOrDefaultAsync(x=>x.Id==id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct)??throw new InvalidOperationException("Leave request not found.");Version(l,revision);var s=await Staff(t,l.StaffId,ct);
   if(l.Status!="Pending")throw new InvalidOperationException("Leave has already been reviewed.");if(l.RequestedBy==t.user||s.IdentityUserId==t.user)throw new UnauthorizedAccessException("Another authorized person must review this leave request.");await Unlocked(t,l.From,l.Through,ct);
   if(approve&&await db.HRPayrollAttendance.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==s.Id&&x.Date>=l.From&&x.Date<=l.Through,ct))throw new InvalidOperationException("Attendance overlaps this leave. Reject the request or resolve attendance before approval.");
   l.Status=approve?"Approved":"Rejected";l.ReviewedBy=t.user;l.ReviewNote=note;Audit(t,"HR.Leave."+l.Status,l.Id.ToString(CultureInfo.InvariantCulture));return true;
  },ct);
 }
 public async Task<List<HRPayrollPolicy>> ReviewedPolicies(CancellationToken ct=default)
 {var t=await Auth("Payroll.Calculate",ct);return await db.HRPayrollPolicies.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Reviewed).OrderByDescending(x=>x.Id).Take(200).ToListAsync(ct);}
 public async Task<HRPayrollPage<HRPayrollPolicy>> Policies(int page=1,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.View",ct);var q=db.HRPayrollPolicies.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);var count=await q.CountAsync(ct);page=Math.Clamp(page,1,Math.Max(1,(count+19)/20));return new(){Items=await q.OrderByDescending(x=>x.Id).Skip((page-1)*20).Take(20).ToListAsync(ct),Page=page,Total=count};
 }
 private static string Account(string value,char category)
 {value=BusinessInput.Text(value,"Account",4);if(value.Length!=4||!value.All(char.IsAsciiDigit)||value[0]!=category||value=="2100")throw new ArgumentException($"Choose a four-digit {category}xxx account for the selected expense/liability mapping.");return value;}
 public async Task<int> CreatePolicy(HRPayrollPolicyInput input,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.Policy",ct);var name=BusinessInput.Text(input.Name,"Policy name",200);var currency=BusinessInput.Text(input.Currency,"Currency",3).ToUpperInvariant();var basis=BusinessInput.Text(input.JurisdictionAndBasis,"Jurisdiction and reviewed calculation basis",2000);
  if(currency.Length!=3||!currency.All(char.IsAsciiLetter)||input.CurrencyDecimals is <0 or >3)throw new ArgumentException("Enter the approved three-letter currency and 0–3 decimal places.");Dates(input.EffectiveFrom,input.EffectiveThrough);BusinessInput.Decimal(input.DeductionPercent,"Employee deduction percent",maximum:100);BusinessInput.Decimal(input.EmployerContributionPercent,"Employer contribution percent",maximum:100);
  if(input.PayBasis is not ("Daily" or "Monthly" or "Mixed"))throw new ArgumentException("Choose an explicit Daily, Monthly or Mixed payroll policy basis.");
  var mode="NotApplicable";decimal fixedUnits=0;var cap=false;
  if(input.PayBasis is "Monthly" or "Mixed")
  {
   if(input.MonthlyProrationMode is not ("ActualCalendarDays" or "FixedPaidUnits"))throw new ArgumentException("Choose the actual calendar-day or explicitly fixed-unit monthly denominator.");
   mode=input.MonthlyProrationMode;cap=input.CapMonthlyPaidUnits;
   if(mode=="FixedPaidUnits")fixedUnits=BusinessInput.Decimal(input.MonthlyFixedUnits,"Fixed monthly paid units",true,maximum:366);
   else if(input.MonthlyFixedUnits!=0)throw new ArgumentException("Actual-calendar proration must have zero fixed units; its denominator comes from the payroll month.");
  }
  var expense=Account(input.ExpenseAccount,'6');var payable=Account(input.PayableAccount,'2');var deduction=Account(input.DeductionAccount,'2');var employerExpense=Account(input.EmployerExpenseAccount,'6');var employerPayable=Account(input.EmployerPayableAccount,'2');
  if(new[]{payable,deduction,employerPayable}.Distinct().Count()!=3)throw new ArgumentException("Use distinct employee-payable, deduction-payable and employer-contribution-payable accounts.");
  return await Write(async()=>{var business=await CompanyBusinessPolicyService.Require(db,t.company,input.EffectiveFrom,ct);var throughPolicy=await CompanyBusinessPolicyService.Require(db,t.company,input.EffectiveThrough,ct);if(business.Id!=throughPolicy.Id||business.CurrencyCode!=currency||business.CurrencyMinorUnits!=input.CurrencyDecimals)throw new InvalidOperationException("The entire payroll policy period, currency and decimal places must match one approved company business policy.");var p=new HRPayrollPolicy{PayBasis=input.PayBasis,MonthlyProrationMode=mode,MonthlyFixedUnits=fixedUnits,CapMonthlyPaidUnits=cap,CompanyBusinessPolicyId=business.Id,CountryCode=business.CountryCode,RegionCode=business.RegionCode,TimeZoneId=business.TimeZoneId,CompanyId=t.company,BranchId=t.branch,Name=name,Currency=currency,CurrencyDecimals=input.CurrencyDecimals,EffectiveFrom=input.EffectiveFrom,EffectiveThrough=input.EffectiveThrough,DeductionPercent=input.DeductionPercent,EmployerContributionPercent=input.EmployerContributionPercent,JurisdictionAndBasis=basis,ExpenseAccount=expense,PayableAccount=payable,DeductionAccount=deduction,EmployerExpenseAccount=employerExpense,EmployerPayableAccount=employerPayable,CreatedBy=t.user};db.HRPayrollPolicies.Add(p);Audit(t,"Payroll.Policy.Create",name);await db.SaveChangesAsync(ct);return p.Id;},ct);
 }
 public async Task ReviewPolicy(int id,string revision,string note,bool confirm,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.Policy.Approve",ct);note=BusinessInput.Text(note,"Policy review note",1000);if(!confirm)throw new ArgumentException("Confirm review of currency, date range, daily/monthly basis, monthly denominator and cap, jurisdiction, rates and account mappings.");
  await Write(async()=>{var p=await db.HRPayrollPolicies.SingleOrDefaultAsync(x=>x.Id==id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct)??throw new InvalidOperationException("Policy not found.");Version(p,revision);if(p.Reviewed)throw new InvalidOperationException("Policy has already been reviewed.");if(p.CreatedBy==t.user)throw new UnauthorizedAccessException("A different authorized user must review this policy.");p.Reviewed=true;p.ReviewedBy=t.user;p.ReviewNote=note;Audit(t,"Payroll.Policy.Review",p.Id.ToString(CultureInfo.InvariantCulture));return true;},ct);
 }
 private sealed record Source(HRPayrollPolicy Policy,List<HRPayrollStaff> Staff,List<HRPayrollCompensation> Compensation,List<HRPayrollAttendance> Attendance,List<HRPayrollLeave> Leaves,string Hash);
 private async Task<Source> Sources((int company,int branch,string user)t,int policyId,DateOnly start,DateOnly end,CancellationToken ct)
 {
  if(CompletedMonthEnd(start,await BusinessToday(t.company,ct))!=end)throw new InvalidOperationException("The payroll period must be one fully ended calendar month.");
  var p=await db.HRPayrollPolicies.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==policyId&&x.CompanyId==t.company&&x.BranchId==t.branch&&x.Reviewed,ct)??throw new InvalidOperationException("Choose an independently reviewed payroll policy.");if(p.EffectiveFrom>start||p.EffectiveThrough<end)throw new InvalidOperationException("The reviewed policy must cover the entire payroll month.");
  if(p.PayBasis is not ("Daily" or "Monthly" or "Mixed")||((p.PayBasis is "Monthly" or "Mixed")&&(p.MonthlyProrationMode is not ("ActualCalendarDays" or "FixedPaidUnits")||(p.MonthlyProrationMode=="FixedPaidUnits"&&p.MonthlyFixedUnits<=0))))throw new InvalidOperationException("The reviewed payroll formula is invalid; create and independently review a corrected policy.");
  var business=await CompanyBusinessPolicyService.Require(db,t.company,start,ct);var endBusiness=await CompanyBusinessPolicyService.Require(db,t.company,end,ct);if(p.CompanyBusinessPolicyId!=business.Id||business.Id!=endBusiness.Id||p.Currency!=business.CurrencyCode||p.CurrencyDecimals!=business.CurrencyMinorUnits)throw new InvalidOperationException("Payroll policy no longer matches the company policy effective for this month.");
  var staff=await StaffQuery(t).AsNoTracking().Where(x=>x.StartDate<=end&&(!x.EndDate.HasValue||x.EndDate>=start)).OrderBy(x=>x.Id).ToListAsync(ct);
  if(staff.Count==0||staff.Count>5000)throw new InvalidOperationException("A payroll run requires 1–5000 staff in this branch.");
  var configured=await db.HRPayrollCompensations.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.EffectiveFrom<=end).ToListAsync(ct);var compensation=new List<HRPayrollCompensation>();
  foreach(var s in staff)
  {
   var first=s.StartDate>start?s.StartDate:start;var last=s.EndDate.HasValue&&s.EndDate.Value<end?s.EndDate.Value:end;
   var rate=configured.Where(x=>x.StaffId==s.Id&&x.EffectiveFrom<=first).OrderByDescending(x=>x.EffectiveFrom).ThenByDescending(x=>x.Version).FirstOrDefault()??throw new InvalidOperationException($"Staff {s.Code} has no reviewed compensation version effective for this month.");
   if(configured.Any(x=>x.StaffId==s.Id&&x.EffectiveFrom>first&&x.EffectiveFrom<=last))throw new InvalidOperationException($"Staff {s.Code} has an unresolved mid-period compensation change. Review the dates; no proration rule is inferred.");
   s.PayBasis=rate.PayBasis;s.DailyRate=rate.DailyRate;s.MonthlySalary=rate.MonthlySalary;s.OvertimeHourlyRate=rate.OvertimeHourlyRate;compensation.Add(rate);
  }
  var attendance=await db.HRPayrollAttendance.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Date>=start&&x.Date<=end).OrderBy(x=>x.Id).ToListAsync(ct);
  var leaves=await db.HRPayrollLeaves.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.From<=end&&x.Through>=start).OrderBy(x=>x.Id).ToListAsync(ct);
  if(leaves.Any(x=>x.Status=="Pending"))throw new InvalidOperationException("Review pending leave in this month before calculating payroll.");
  var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{Policy=p,Staff=staff,Compensation=compensation,Attendance=attendance,Leaves=leaves}))));return new(p,staff,compensation,attendance,leaves,hash);
 }
 private static decimal Money(decimal value,int decimals)=>BusinessInput.Amount(decimal.Round(value,decimals,MidpointRounding.AwayFromZero),"Calculated payroll amount",false);
 private static List<HRPayrollLine> CalculateLines(Source source,int company,int branch,DateOnly start,DateOnly end)
 {
  var result=new List<HRPayrollLine>();
  foreach(var s in source.Staff)
  {
   if(s.PayBasis is not ("Daily" or "Monthly")||(source.Policy.PayBasis!="Mixed"&&s.PayBasis!=source.Policy.PayBasis))throw new InvalidOperationException($"Staff {s.Code} pay basis does not match this policy. Explicitly review their contract basis or select an independently reviewed Mixed policy.");
   var basis=s.PayBasis;var compensation=source.Compensation.Single(x=>x.StaffId==s.Id);
   if((basis=="Daily"&&s.DailyRate<=0)||(basis=="Monthly"&&s.MonthlySalary<=0))throw new InvalidOperationException($"Set and review {basis} pay rates for staff {s.Code} before calculation.");
   var from=s.StartDate>start?s.StartDate:start;var through=s.EndDate.HasValue&&s.EndDate.Value<end?s.EndDate.Value:end;var attendance=source.Attendance.Where(x=>x.StaffId==s.Id).ToList();var leaves=source.Leaves.Where(x=>x.StaffId==s.Id&&x.Status=="Approved").ToList();decimal paidLeave=0;
   for(var d=from;d<=through;d=d.AddDays(1))
   {
    var a=attendance.Count(x=>x.Date==d);var l=leaves.Where(x=>x.From<=d&&x.Through>=d).ToList();if(a+l.Count!=1)throw new InvalidOperationException($"Staff {s.Code}: {d:yyyy-MM-dd} needs exactly one attendance entry or approved leave. Record each paid rest day as one paid day and each unpaid day as zero, according to the reviewed contract.");if(l.Count==1&&l[0].Paid)paidLeave++;
   }
   var paid=attendance.Sum(x=>x.PaidDays);var overtime=attendance.Sum(x=>x.OvertimeHours);if(overtime>0&&s.OvertimeHourlyRate<=0)throw new InvalidOperationException($"Staff {s.Code} has overtime hours but no positive reviewed overtime rate.");var decimals=source.Policy.CurrencyDecimals;
   try
   {
    var denominator=basis=="Monthly"?(source.Policy.MonthlyProrationMode=="ActualCalendarDays"?DateTime.DaysInMonth(start.Year,start.Month):source.Policy.MonthlyFixedUnits):0m;
    var paidUnits=basis=="Monthly"&&source.Policy.CapMonthlyPaidUnits?Math.Min(paid+paidLeave,denominator):paid+paidLeave;
    var basePay=Money(basis=="Monthly"?checked(s.MonthlySalary*paidUnits/denominator):checked(paidUnits*s.DailyRate),decimals);var overtimePay=Money(checked(overtime*s.OvertimeHourlyRate),decimals);var gross=Money(checked(basePay+overtimePay),decimals);var deduct=Money(checked(gross*source.Policy.DeductionPercent/100m),decimals);var employer=Money(checked(gross*source.Policy.EmployerContributionPercent/100m),decimals);
    result.Add(new(){CompensationId=compensation.Id,CompensationVersion=compensation.Version,CompensationEffectiveFrom=compensation.EffectiveFrom,PayBasis=basis,ProrationMode=basis=="Monthly"?source.Policy.MonthlyProrationMode:"NotApplicable",ProrationDenominator=denominator,PaidUnitsUsed=paidUnits,CompanyId=company,BranchId=branch,StaffId=s.Id,StaffCode=s.Code,StaffName=s.Name,DailyRate=s.DailyRate,MonthlySalary=s.MonthlySalary,OvertimeHourlyRate=s.OvertimeHourlyRate,AttendanceDays=paid,PaidLeaveDays=paidLeave,OvertimeHours=overtime,BasePay=basePay,OvertimePay=overtimePay,GrossPay=gross,Deductions=deduct,NetPay=gross-deduct,EmployerContribution=employer});
   }
   catch(OverflowException){throw new ArgumentException("Calculated payroll exceeds supported amounts.");}
  }
  BusinessInput.Amount(result.Sum(x=>x.GrossPay+x.EmployerContribution),"Payroll expense total");return result;
 }
 public async Task<long> Calculate(HRPayrollRunInput input,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.Calculate",ct);var today=await BusinessToday(t.company,ct);var reference=BusinessInput.Reference(input.Reference,60);var start=input.PeriodStart;var end=CompletedMonthEnd(start,today);
  return await Write(async()=>{if(await db.HRPayrollRuns.AnyAsync(x=>x.CompanyId==t.company&&(x.Reference==reference||(x.BranchId==t.branch&&x.PeriodStart==start)),ct))throw new InvalidOperationException("A payroll run already exists for this month or reference; recalculate its draft instead.");var source=await Sources(t,input.PolicyId,start,end,ct);var run=new HRPayrollRun{CompanyId=t.company,BranchId=t.branch,PolicyId=source.Policy.Id,Reference=reference,PeriodStart=start,PeriodEnd=end,Currency=source.Policy.Currency,CurrencyDecimals=source.Policy.CurrencyDecimals,CreatedBy=t.user,SourceHash=source.Hash,Lines=CalculateLines(source,t.company,t.branch,start,end)};db.HRPayrollRuns.Add(run);Audit(t,"Payroll.Calculate",reference);await db.SaveChangesAsync(ct);return run.Id;},ct);
 }
 private async Task<HRPayrollRun> Run((int company,int branch,string user)t,long id,CancellationToken ct)=>await db.HRPayrollRuns.Include(x=>x.Lines).SingleOrDefaultAsync(x=>x.Id==id&&x.CompanyId==t.company&&x.BranchId==t.branch,ct)??throw new InvalidOperationException("Payroll run not found in this branch.");
 public Task Recalculate(long id,string revision,CancellationToken ct=default)=>RecalculateWithPolicy(id,revision,null,ct);
 public async Task RecalculateWithPolicy(long id,string revision,int? replacementPolicyId,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.Calculate",ct);await Write(async()=>{var run=await Run(t,id,ct);Version(run,revision);if(run.Status!="Draft")throw new InvalidOperationException("Only draft payroll can be recalculated.");var source=await Sources(t,replacementPolicyId??run.PolicyId,run.PeriodStart,run.PeriodEnd,ct);var lines=CalculateLines(source,t.company,t.branch,run.PeriodStart,run.PeriodEnd);db.HRPayrollLines.RemoveRange(run.Lines);await db.SaveChangesAsync(ct);run.Lines=lines;run.PolicyId=source.Policy.Id;run.Currency=source.Policy.Currency;run.CurrencyDecimals=source.Policy.CurrencyDecimals;run.SourceHash=source.Hash;run.CreatedBy=t.user;Audit(t,"Payroll.Recalculate",run.Reference);return true;},ct);
 }
 public async Task Approve(long id,string revision,string note,bool confirm,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.Approve",ct);note=BusinessInput.Text(note,"Payroll review note",1000);if(!confirm)throw new ArgumentException("Confirm that each payslip, attendance, leave and policy was reviewed.");
  await Write(async()=>{var run=await Run(t,id,ct);Version(run,revision);if(run.Status!="Draft")throw new InvalidOperationException("Only draft payroll can be approved.");if(run.CreatedBy==t.user)throw new UnauthorizedAccessException("A different authorized user must approve payroll.");var source=await Sources(t,run.PolicyId,run.PeriodStart,run.PeriodEnd,ct);if(source.Hash!=run.SourceHash)throw new InvalidOperationException("Payroll source data changed. Recalculate the draft and review the new amounts.");run.Status="Approved";run.ApprovedBy=t.user;run.ApprovedUtc=DateTimeOffset.UtcNow;run.ReviewNote=note;Audit(t,"Payroll.Approve",run.Reference);return true;},ct);
 }
 public async Task Post(long id,string revision,DateOnly postingDate,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.Post",ct);var today=await BusinessToday(t.company,ct);if(postingDate.Year<2000||postingDate>today)throw new ArgumentException("Use a posting date on or before today.");
  await Write(async()=>{var run=await Run(t,id,ct);if(string.IsNullOrWhiteSpace(revision)||run.Revision!=revision)throw new DbUpdateConcurrencyException("This payroll changed. Reload before posting.");if(run.Status!="Approved"||run.VoucherId!=null)throw new InvalidOperationException("Only approved, unposted payroll can be posted once.");if(postingDate<run.PeriodEnd)throw new ArgumentException("Posting date must be on or after the payroll period end.");var p=await db.HRPayrollPolicies.AsNoTracking().SingleAsync(x=>x.Id==run.PolicyId&&x.CompanyId==t.company&&x.BranchId==t.branch&&x.Reviewed,ct);
   var business=await CompanyBusinessPolicyService.Require(db,t.company,postingDate,ct);if(business.CurrencyCode!=run.Currency||business.CurrencyMinorUnits!=run.CurrencyDecimals)throw new InvalidOperationException("Posting currency differs from the approved payroll snapshot.");
   var gross=run.Lines.Sum(x=>x.GrossPay);var net=run.Lines.Sum(x=>x.NetPay);var deductions=run.Lines.Sum(x=>x.Deductions);var employer=run.Lines.Sum(x=>x.EmployerContribution);
   var voucher=BusinessInput.Voucher(t.company,t.branch,"PAYROLL-"+run.Reference,(p.ExpenseAccount,gross,0),(p.PayableAccount,0,net),(p.DeductionAccount,0,deductions),(p.EmployerExpenseAccount,employer,0),(p.EmployerPayableAccount,0,employer));voucher.Date=new DateTimeOffset(postingDate.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);db.Vouchers.Add(voucher);await db.SaveChangesAsync(ct);
   Version(run,revision);run.Status="Posted";run.PostedBy=t.user;run.PostedUtc=DateTimeOffset.UtcNow;run.PostingDate=postingDate;run.VoucherId=voucher.Id;Audit(t,"Payroll.Post",run.Reference);return true;
  },ct);
 }
 public async Task<HRPayrollPage<HRPayrollRun>> Register(string? search,string? status,int page=1,CancellationToken ct=default)
 {
  var t=await Auth("Payroll.View",ct);search=BusinessInput.Text(search,"Search",60,false);status=status is "Draft" or "Approved" or "Posted"?status:"";var q=db.HRPayrollRuns.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch);if(search.Length>0)q=q.Where(x=>x.Reference.Contains(search));if(status!="")q=q.Where(x=>x.Status==status);var total=await q.CountAsync(ct);page=Math.Clamp(page,1,Math.Max(1,(total+19)/20));return new(){Items=await q.Include(x=>x.Lines).OrderByDescending(x=>x.PeriodStart).Skip((page-1)*20).Take(20).ToListAsync(ct),Total=total,Page=page,Search=search,Status=status};
 }
 public async Task<(HRPayrollRun run,HRPayrollPolicy policy)> Details(long id,CancellationToken ct=default)
 {var t=await Auth("Payroll.View",ct);var run=await Run(t,id,ct);return(run,await db.HRPayrollPolicies.AsNoTracking().SingleAsync(x=>x.Id==run.PolicyId&&x.CompanyId==t.company&&x.BranchId==t.branch,ct));}
 private async Task<HRPayrollStaff> OwnStaff((int company,int branch,string user)t,CancellationToken ct)=>await StaffQuery(t).AsNoTracking().SingleOrDefaultAsync(x=>x.IdentityUserId==t.user,ct)??throw new UnauthorizedAccessException("No verified staff account is linked in this branch. Ask an authorized HR identity administrator.");
 public async Task<HRPayrollOwnPage> Own(int page=1,CancellationToken ct=default)
 {
  var t=await Auth("Employee.Self",ct);var staff=await OwnStaff(t,ct);var q=db.HRPayrollLines.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==staff.Id&&db.HRPayrollRuns.Any(r=>r.Id==x.RunId&&r.CompanyId==t.company&&r.BranchId==t.branch&&r.Status=="Posted"));var count=await q.CountAsync(ct);var leaveCount=await db.HRPayrollLeaves.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==staff.Id,ct);page=Math.Clamp(page,1,Math.Max(1,(Math.Max(count,leaveCount)+19)/20));var slips=new List<HRPayrollPayslip>();foreach(var line in await q.OrderByDescending(x=>x.RunId).Skip((page-1)*20).Take(20).ToListAsync(ct)){var r=await db.HRPayrollRuns.AsNoTracking().SingleAsync(x=>x.Id==line.RunId,ct);var p=await db.HRPayrollPolicies.AsNoTracking().SingleAsync(x=>x.Id==r.PolicyId,ct);slips.Add(new(r,line,p));}
  var leaves=await db.HRPayrollLeaves.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.StaffId==staff.Id).OrderByDescending(x=>x.Id).Skip((page-1)*20).Take(20).ToListAsync(ct);return new(Safe(staff),leaves,slips,page,Math.Max(count,leaveCount));
 }
 public async Task<HRPayrollPayslip> Payslip(long lineId,bool ownOnly,CancellationToken ct=default)
 {
  var t=await Auth(ownOnly?"Employee.Self":"Payroll.View",ct);var staffId=ownOnly?(await OwnStaff(t,ct)).Id:0;var line=await db.HRPayrollLines.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==lineId&&x.CompanyId==t.company&&x.BranchId==t.branch&&(!ownOnly||x.StaffId==staffId),ct)??throw new InvalidOperationException("Payslip not found.");var run=await db.HRPayrollRuns.AsNoTracking().SingleAsync(x=>x.Id==line.RunId&&x.CompanyId==t.company&&x.BranchId==t.branch,ct);if(run.Status!="Posted")throw new InvalidOperationException("Payslips become available after accrual posting.");return new(run,line,await db.HRPayrollPolicies.AsNoTracking().SingleAsync(x=>x.Id==run.PolicyId&&x.CompanyId==t.company&&x.BranchId==t.branch,ct));
 }
}
