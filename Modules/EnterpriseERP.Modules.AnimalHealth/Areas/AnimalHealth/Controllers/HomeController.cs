using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.AnimalHealth.Areas.AnimalHealth.Controllers;
[Area("AnimalHealth")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
