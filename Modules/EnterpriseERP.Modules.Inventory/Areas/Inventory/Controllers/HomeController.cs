using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Inventory.Areas.Inventory.Controllers;
[Area("Inventory")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
