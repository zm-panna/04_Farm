using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Ecommerce.Areas.Ecommerce.Controllers;
[Area("Ecommerce")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
