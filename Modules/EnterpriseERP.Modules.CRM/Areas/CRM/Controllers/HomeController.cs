using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.CRM.Areas.CRM.Controllers;
[Area("CRM")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
