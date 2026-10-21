using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.FeedManagement.Areas.FeedManagement.Controllers;
[Area("FeedManagement")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
