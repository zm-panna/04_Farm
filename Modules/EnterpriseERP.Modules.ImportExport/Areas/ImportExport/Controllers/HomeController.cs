using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.ImportExport.Areas.ImportExport.Controllers;
[Area("ImportExport")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
