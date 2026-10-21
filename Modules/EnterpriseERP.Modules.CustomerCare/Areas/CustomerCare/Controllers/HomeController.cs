using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.CustomerCare.Areas.CustomerCare.Controllers;
[Area("CustomerCare")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
