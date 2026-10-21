using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
public sealed class HRPayrollAttendancePage
{
 public HRPayrollPage<HRPayrollAttendance> Browse {get;set;}=new();
 public HRPayrollAttendanceInput Entry {get;set;}=new();
}
public sealed class HRPayrollLeavePage
{
 public HRPayrollPage<HRPayrollLeave> Browse {get;set;}=new();
 public HRPayrollLeaveInput Entry {get;set;}=new();
}
public sealed class HRPayrollPolicyPage
{
 public HRPayrollPage<HRPayrollPolicy> Browse {get;set;}=new();
 public HRPayrollPolicyInput Entry {get;set;}=new(){PayBasis="",MonthlyProrationMode="",CurrencyDecimals=-1,DeductionPercent=-1,EmployerContributionPercent=-1};
}
public sealed class HRPayrollCalculatePage
{
 public HRPayrollRunInput Entry {get;set;}=new();
 public List<HRPayrollPolicy> Policies {get;set;}=new();
}
public sealed class HRPayrollIdentityPage
{
 public HRPayrollStaffInput Staff {get;set;}=new();
 public List<HRPayrollIdentityOption> Users {get;set;}=new();
 public string UserId {get;set;}="";
 public bool Confirm {get;set;}
}
public sealed record HRPayrollDetailsPage(HRPayrollRun Run,HRPayrollPolicy Policy);
[Authorize,Route("HRPayroll")]
public sealed class HRPayrollController(HRPayrollService service):Controller
{
 [HttpGet("")]public async Task<IActionResult> Index(string? search,string? status,int page=1,CancellationToken ct=default)=>View(await service.BrowseStaff(search,status,page,ct));
 [HttpGet("staff/{id:int?}")]public async Task<IActionResult> Staff(int id=0,CancellationToken ct=default)=>View(await service.StaffForm(id,ct));
 [HttpPost("staff"),ValidateAntiForgeryToken]
 public async Task<IActionResult> SaveStaff(HRPayrollStaffInput input,CancellationToken ct)
 {if(ModelState.IsValid&&await Try(async()=>{await service.SaveStaff(input,ct);}))return Success("Staff record saved.",nameof(Index));return View("Staff",input);}
 [HttpGet("rates/{id:int}")]public async Task<IActionResult> Rates(int id,CancellationToken ct)=>View(await service.Rates(id,ct));
 [HttpPost("rates"),ValidateAntiForgeryToken]
 public async Task<IActionResult> SaveRates(HRPayrollRateInput input,CancellationToken ct)
 {if(ModelState.IsValid&&await Try(()=>service.SetRates(input,ct)))return Success("Confidential pay rates saved. Recalculate any existing draft payroll.",nameof(Index));return View("Rates",input);}
 [HttpGet("identity/{id:int}")]
 public async Task<IActionResult> Identity(int id,CancellationToken ct){var p=await service.IdentityForm(id,ct);return View(new HRPayrollIdentityPage{Staff=p.staff,Users=p.users});}
 [HttpPost("identity/{id:int}"),ValidateAntiForgeryToken]
 public async Task<IActionResult> LinkIdentity(int id,string userId,string revision,bool confirm,CancellationToken ct)
 {if(ModelState.IsValid&&await Try(()=>service.LinkIdentity(id,userId,revision,confirm,ct)))return Success("Verified account linked to this staff member.",nameof(Index));var p=await service.IdentityForm(id,ct);return View("Identity",new HRPayrollIdentityPage{Staff=p.staff,Users=p.users,UserId=userId,Confirm=confirm});}
 [HttpGet("attendance")]
 public async Task<IActionResult> Attendance(int staffId=0,int page=1,CancellationToken ct=default)=>View(new HRPayrollAttendancePage{Browse=await service.Attendance(staffId,page,ct),Entry=new(){StaffId=staffId}});
 [HttpPost("attendance"),ValidateAntiForgeryToken]
 public async Task<IActionResult> SaveAttendance([Bind(Prefix="Entry")]HRPayrollAttendanceInput input,CancellationToken ct)
 {if(ModelState.IsValid&&await Try(()=>service.SaveAttendance(input,ct)))return Success("Attendance saved.",nameof(Attendance),new{staffId=input.StaffId});return View("Attendance",new HRPayrollAttendancePage{Browse=await service.Attendance(input.StaffId,1,ct),Entry=input});}
 [HttpGet("leave")]
 public async Task<IActionResult> Leave(string? status,int staffId=0,int page=1,CancellationToken ct=default)=>View(new HRPayrollLeavePage{Browse=await service.Leaves(status,staffId,page,ct),Entry=new(){StaffId=staffId}});
 [HttpPost("leave"),ValidateAntiForgeryToken]
 public async Task<IActionResult> RequestLeave([Bind(Prefix="Entry")]HRPayrollLeaveInput input,CancellationToken ct)
 {if(ModelState.IsValid&&await Try(()=>service.RequestLeave(input,false,ct)))return Success("Leave request submitted for independent review.",nameof(Leave));return View("Leave",new HRPayrollLeavePage{Browse=await service.Leaves(null,input.StaffId,1,ct),Entry=input});}
 [HttpPost("leave/{id:long}/review"),ValidateAntiForgeryToken]
 public async Task<IActionResult> ReviewLeave(long id,string revision,bool approve,string note,CancellationToken ct)
 {await Try(()=>service.ReviewLeave(id,revision,approve,note,ct));return ActionResultWithErrors(nameof(Leave),"Leave review saved.");}
 [HttpGet("policies")]
 public async Task<IActionResult> Policies(int page=1,CancellationToken ct=default)=>View(new HRPayrollPolicyPage{Browse=await service.Policies(page,ct)});
 [HttpPost("policies"),ValidateAntiForgeryToken]
 public async Task<IActionResult> CreatePolicy([Bind(Prefix="Entry")]HRPayrollPolicyInput input,CancellationToken ct)
 {
  // Non-nullable model properties retain CLR/default values when a form field is absent.
  // Require the submitted review choices themselves, including explicitly reviewed zero/false.
  foreach(var field in new[]{"PayBasis","MonthlyProrationMode","MonthlyFixedUnits","CapMonthlyPaidUnits","CurrencyDecimals","EffectiveFrom","EffectiveThrough","DeductionPercent","EmployerContributionPercent"})
  {
   var key="Entry."+field;
   if(!Request.HasFormContentType||!Request.Form.TryGetValue(key,out var values)||values.Count!=1||string.IsNullOrWhiteSpace(values[0]))
    ModelState.AddModelError(key,"Enter or select this policy value explicitly, including reviewed zero or No where applicable.");
  }
  if(ModelState.IsValid&&await Try(async()=>{await service.CreatePolicy(input,ct);}))return Success("Policy proposal saved. A different authorized reviewer must approve it.",nameof(Policies));
  return View("Policies",new HRPayrollPolicyPage{Browse=await service.Policies(1,ct),Entry=input});
 }
 [HttpPost("policies/{id:int}/review"),ValidateAntiForgeryToken]
 public async Task<IActionResult> ReviewPolicy(int id,string revision,string note,bool confirm,CancellationToken ct)
 {await Try(()=>service.ReviewPolicy(id,revision,note,confirm,ct));return ActionResultWithErrors(nameof(Policies),"Policy reviewed and locked.");}
 [HttpGet("register")]
 public async Task<IActionResult> Register(string? search,string? status,int page=1,CancellationToken ct=default)=>View(await service.Register(search,status,page,ct));
 [HttpGet("calculate")]
 public async Task<IActionResult> Calculate(CancellationToken ct)=>View(new HRPayrollCalculatePage{Policies=await service.ReviewedPolicies(ct)});
 [HttpPost("calculate"),ValidateAntiForgeryToken]
 public async Task<IActionResult> Calculate([Bind(Prefix="Entry")]HRPayrollRunInput input,CancellationToken ct)
 {long id=0;if(ModelState.IsValid&&await Try(async()=>{id=await service.Calculate(input,ct);}))return Success("Payroll draft calculated. Review every line before independent approval.",nameof(Details),new{id});return View(new HRPayrollCalculatePage{Entry=input,Policies=await service.ReviewedPolicies(ct)});}
 [HttpGet("register/{id:long}")]
 public async Task<IActionResult> Details(long id,CancellationToken ct){var p=await service.Details(id,ct);return View(new HRPayrollDetailsPage(p.run,p.policy));}
 [HttpPost("register/{id:long}/recalculate"),ValidateAntiForgeryToken]
 public async Task<IActionResult> Recalculate(long id,string revision,int? replacementPolicyId,CancellationToken ct)
 {await Try(()=>service.RecalculateWithPolicy(id,revision,replacementPolicyId,ct));return ActionResultWithErrors(nameof(Details),"Draft recalculated; review the changed values.",new{id});}
 [HttpPost("register/{id:long}/approve"),ValidateAntiForgeryToken]
 public async Task<IActionResult> Approve(long id,string revision,string note,bool confirm,CancellationToken ct)
 {await Try(()=>service.Approve(id,revision,note,confirm,ct));return ActionResultWithErrors(nameof(Details),"Payroll approved. Posting is a separate accrual action.",new{id});}
 [HttpPost("register/{id:long}/post"),ValidateAntiForgeryToken]
 public async Task<IActionResult> Post(long id,string revision,DateOnly postingDate,CancellationToken ct)
 {await Try(()=>service.Post(id,revision,postingDate,ct));return ActionResultWithErrors(nameof(Details),"Payroll accrual posted to the general ledger. No salary payment or money transfer was made.",new{id});}
 [HttpGet("me")]
 public async Task<IActionResult> Me(int page=1,CancellationToken ct=default)=>View(await service.Own(page,ct));
 [HttpPost("me/leave"),ValidateAntiForgeryToken]
 public async Task<IActionResult> OwnLeave(HRPayrollLeaveInput input,CancellationToken ct)
 {if(ModelState.IsValid)await Try(()=>service.RequestLeave(input,true,ct));return ActionResultWithErrors(nameof(Me),"Your leave request was submitted for independent review.");}
 [HttpGet("me/payslip/{id:long}")]
 public async Task<IActionResult> OwnPayslip(long id,CancellationToken ct)=>View("Payslip",await service.Payslip(id,true,ct));
 [HttpGet("payslip/{id:long}")]
 public async Task<IActionResult> Payslip(long id,CancellationToken ct)=>View(await service.Payslip(id,false,ct));
 private async Task<bool> Try(Func<Task> action)
 {
  if(!ModelState.IsValid)return false;
  try{await action();return true;}
  catch(DbUpdateConcurrencyException){ModelState.AddModelError("","This record changed. Reload and review the current version before trying again.");}
  catch(ArgumentException e){ModelState.AddModelError("",e.Message);}
  catch(InvalidOperationException e){ModelState.AddModelError("",e.Message);}
  catch(DbUpdateException){ModelState.AddModelError("","The change could not be saved. Reload to check for a duplicate or changed record; no partial change was committed.");}
  return false;
 }
 private IActionResult Success(string message,string action,object? route=null){TempData["Success"]=message;return RedirectToAction(action,route);}
 private IActionResult ActionResultWithErrors(string action,string success,object? route=null)
 {if(ModelState.IsValid)TempData["Success"]=success;else TempData["HRPayrollError"]=string.Join(" ",ModelState.Values.SelectMany(x=>x.Errors).Select(x=>x.ErrorMessage));return RedirectToAction(action,route);}
}
