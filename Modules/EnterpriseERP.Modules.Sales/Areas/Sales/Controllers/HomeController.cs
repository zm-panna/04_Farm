using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Sales.Areas.Sales.Controllers;
[Area("Sales")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
