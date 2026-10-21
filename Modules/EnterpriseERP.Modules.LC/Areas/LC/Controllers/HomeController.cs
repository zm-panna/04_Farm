using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.LC.Areas.LC.Controllers;
[Area("LC")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
