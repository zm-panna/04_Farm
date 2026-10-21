using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Dairy.Areas.Dairy.Controllers;
[Area("Dairy")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
