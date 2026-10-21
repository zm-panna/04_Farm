using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Hotel.Areas.Hotel.Controllers;
[Area("Hotel")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
