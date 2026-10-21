using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Purchase.Areas.Purchase.Controllers;
[Area("Purchase")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
