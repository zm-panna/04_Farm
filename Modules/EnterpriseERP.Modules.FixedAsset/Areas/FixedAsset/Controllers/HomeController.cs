using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.FixedAsset.Areas.FixedAsset.Controllers;
[Area("FixedAsset")][Authorize]
public sealed class HomeController : Controller { public IActionResult Index() => View(); }
