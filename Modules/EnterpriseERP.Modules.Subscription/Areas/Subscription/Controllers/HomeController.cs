using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Subscription.Areas.Subscription.Controllers;
[Area("Subscription")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
