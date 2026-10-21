using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Reporting.Areas.Reporting.Controllers;
[Area("Reporting")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
