using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Payroll.Areas.Payroll.Controllers;
[Area("Payroll")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
