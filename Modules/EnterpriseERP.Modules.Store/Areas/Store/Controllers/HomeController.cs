using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Store.Areas.Store.Controllers;
[Area("Store")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
