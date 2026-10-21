using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Expenses.Areas.Expenses.Controllers;
[Area("Expenses")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
