using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Accounts.Areas.Accounts.Controllers;
[Area("Accounts")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
