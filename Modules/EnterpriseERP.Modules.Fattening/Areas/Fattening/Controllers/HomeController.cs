using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Fattening.Areas.Fattening.Controllers;
[Area("Fattening")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
