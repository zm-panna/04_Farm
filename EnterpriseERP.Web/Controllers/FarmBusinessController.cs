using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;

[Authorize, Route("Fattening")]
public sealed class FatteningController(FarmBusinessService service) : Controller
{
    [HttpGet(""), HttpGet("Home"), HttpGet("Home/Index")]
    public async Task<IActionResult> Index(CancellationToken ct = default) => View(await service.Fattening(ct));

    [HttpGet("weights")]
    public async Task<IActionResult> Weights(int page = 1, string? search = null, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return View(await service.WeightHistory(page, search, ct));
    }

    [HttpPost("plan"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Plan([Bind(Prefix = "Plan")] FatteningPlanInput input, CancellationToken ct = default)
    {
        if (ModelState.IsValid)
        {
            try { await service.StartPlan(input, ct); TempData["Success"] = "Fattening plan and starting weight saved."; return RedirectToAction(nameof(Index)); }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "The plan could not be saved. Refresh and check for a duplicate reference or animal."); }
        }
        var page = await service.Fattening(ct); page.Plan = input;
        return View("Index", page);
    }
    [HttpPost("weight"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Weight([Bind(Prefix = "Weight")] WeightEntryInput input, CancellationToken ct = default)
    {
        if (ModelState.IsValid)
        {
            try { await service.RecordWeight(input, ct); TempData["Success"] = "Weight recorded."; return RedirectToAction(nameof(Index)); }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "The weight could not be saved. Refresh and check for a duplicate reference or weighing date."); }
        }
        var page = await service.Fattening(ct); page.Weight = input;
        return View("Index", page);
    }
}

[Authorize, Route("Expenses")]
public sealed class ExpensesController(FarmBusinessService service) : Controller
{
    [HttpGet(""), HttpGet("Home"), HttpGet("Home/Index")]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        try { return View(await service.Expenses(from, to, ct)); }
        catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); return View(await service.Expenses(ct: ct)); }
    }
    [HttpPost("record"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Record([Bind(Prefix = "Expense")] ExpenseInput input, CancellationToken ct = default)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await service.RecordExpense(input, ct); TempData["Success"] = "Expense and balanced cash voucher posted.";
                return RedirectToAction(nameof(Index), new { from = input.ExpenseDate.ToString("yyyy-MM-dd"), to = input.ExpenseDate.ToString("yyyy-MM-dd") });
            }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "The expense could not be posted. Refresh and check for a duplicate reference."); }
        }
        var page = await service.Expenses(ct: ct); page.Expense = input;
        return View("Index", page);
    }
}

[Authorize, Route("breeding")]
public sealed class BreedingController(FarmBusinessService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct = default) => View(await service.Breeding(ct));
    [HttpPost("record"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Record([Bind(Prefix = "Breeding")] BreedingInput input, CancellationToken ct = default)
    {
        if (ModelState.IsValid)
        {
            try { await service.StartBreeding(input, ct); TempData["Success"] = "Breeding service recorded."; return RedirectToAction(nameof(Index)); }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "The cycle could not be saved. Refresh and check for an existing open cycle or reference."); }
        }
        var page = await service.Breeding(ct); page.Breeding = input;
        return View("Index", page);
    }
    [HttpPost("outcome"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Outcome([Bind(Prefix = "OutcomeEntry")] BreedingOutcomeInput request, CancellationToken ct = default)
    {
        if (ModelState.IsValid)
        {
            try { await service.RecordOutcome(request, ct); TempData["Success"] = "Outcome recorded in the cycle history."; return RedirectToAction(nameof(Index)); }
            catch (ArgumentException ex) { ModelState.AddModelError("", ex.Message); }
            catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "The outcome could not be saved. Refresh and check the current cycle status and reference."); }
        }
        var page = await service.Breeding(ct); page.OutcomeEntry = request;
        return View("Index", page);
    }
}
