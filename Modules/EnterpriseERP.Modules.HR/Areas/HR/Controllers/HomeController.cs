using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.HR.Areas.HR.Controllers;
[Area("HR")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
