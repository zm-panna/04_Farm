using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web.Controllers;
[Authorize,Route("Accounts/opening")]
public sealed class OpeningBalanceController(OpeningBalanceService service):Controller
{
 [HttpGet("")]public async Task<IActionResult> Index(CancellationToken ct=default)=>View(await service.Page(ct));
 [HttpPost("post"),ValidateAntiForgeryToken]
 public async Task<IActionResult> Post([Bind(Prefix="Opening")]OpeningBalanceInput input,CancellationToken ct=default)
 {
  if(ModelState.IsValid)
  {
   try{await service.Post(input,ct);TempData["Success"]="Opening balances, itemized values and balanced voucher posted.";return RedirectToAction(nameof(Index));}
   catch(ArgumentException ex){ModelState.AddModelError("",ex.Message);}
   catch(InvalidOperationException ex){ModelState.AddModelError("",ex.Message);}
   catch(DbUpdateException){ModelState.AddModelError("","Opening balances could not be posted. Reload to check for another opening or changed source data.");}
  }
  var page=await service.Page(ct);
  input.Ledger=(input.Ledger??new()).Where(x=>x!=null).Take(6).ToList();
  input.Stocks=(input.Stocks??new()).Where(x=>x!=null).Take(100).ToList();
  input.Animals=(input.Animals??new()).Where(x=>x!=null).Take(100).ToList();
  if(input.Ledger.Count==0)input.Ledger=page.Opening.Ledger;
  if(input.Stocks.Count==0)input.Stocks=page.Opening.Stocks;
  if(input.Animals.Count==0)input.Animals=page.Opening.Animals;
  page.Opening=input;return View("Index",page);
 }
}
