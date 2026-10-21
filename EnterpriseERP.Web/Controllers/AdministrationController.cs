using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;

[Authorize, Route("Administration")]
public sealed class AdministrationController(AdministrationService service) : Controller
{
    [HttpGet(""), HttpGet("Home"), HttpGet("Home/Index")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await service.Page(ct));

    [HttpPost("staff/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaff([Bind(Prefix = "CreateStaff")] StaffCreateInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.CreateStaff(input, ct);
                TempData["Success"] = "Staff account created with the selected branch and operational permissions. Share the initial password through your approved secure channel; ask the staff member to change it after login. No email was sent.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException) { Error(ex); }
        }
        // Never redisplay submitted credentials, including the MVC model-state attempted value.
        input.Password = ""; ModelState.Remove("CreateStaff.Password");
        var page = await service.Page(ct); page.CreateStaff = input;
        return View("Index", page);
    }

    [HttpPost("staff/verify-email-ownership"), ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmailOwnership([Bind(Prefix = "VerifyEmail")] StaffEmailVerificationInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.VerifyStaffEmailOwnership(input, ct);
                TempData["Success"] = "Out-of-band email ownership review recorded. The account is eligible for separately confirmed staff/customer linking. This application sent no email.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException) { Error(ex); }
        }
        input.CurrentPassword = "";
        ModelState.Remove("VerifyEmail.CurrentPassword");
        return View("Index", await service.Page(ct));
    }

    [HttpPost("staff/lookup"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Lookup([Bind(Prefix = "Lookup")] StaffLookupInput input, CancellationToken ct)
    {
        var page = await service.Page(ct); page.Lookup = input;
        if (ModelState.IsValid)
        {
            try { page.Candidate = await service.LookupExisting(input, ct); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException) { Error(ex); }
        }
        return View("Index", page);
    }

    [HttpPost("staff/attach"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Attach([Bind(Prefix = "Attach")] StaffAttachInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.AttachExisting(input, ct);
                TempData["Success"] = "The confirmed account has access to the selected company and branch. Existing password, roles and permissions were preserved. Assign operational permissions separately if needed.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException) { Error(ex); }
        }
        return View("Index", await service.Page(ct));
    }

    [HttpPost("staff/revoke"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke([Bind(Prefix = "Revoke")] StaffRevokeInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var changed = await service.RevokeStaffAccess(input, ct);
                TempData["Success"] = changed
                    ? input.RemoveCompanyAccess ? "Company access and its branch/operational grants revoked. Other companies, the Identity account and business history were preserved."
                        : "Selected branch access revoked. Existing selection claims can no longer access that branch; other access and history were preserved."
                    : "That branch access had already been revoked.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException) { Error(ex); }
        }
        return View("Index", await service.Page(ct));
    }

    [HttpPost("company/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCompany([Bind(Prefix = "NewCompany")] CompanyCreateInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.CreateCompany(input, ct);
                TempData["Success"] = "Company, farm and branch created. You have administrative scope there. Select the new company and branch to continue.";
                return RedirectToAction("Index", "Context");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException) { Error(ex); }
        }
        var page = await service.Page(ct); page.NewCompany = input;
        return View("Index", page);
    }

    [HttpPost("branch/create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBranch([Bind(Prefix = "NewBranch")] BranchCreateInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.CreateBranch(input, ct);
                TempData["Success"] = "Branch created in the selected company. Your branch access was added; select it from Company & Branch when needed.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException) { Error(ex); }
        }
        var page = await service.Page(ct); page.NewBranch = input;
        return View("Index", page);
    }

    private void Error(Exception ex) => ModelState.AddModelError("", ex is DbUpdateException
        ? "The operation could not be saved. Check for a duplicate account/name or changed access, then refresh." : ex.Message);
}
