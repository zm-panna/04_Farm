using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Livestock.Areas.Livestock.Controllers;
[Area("Livestock")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
