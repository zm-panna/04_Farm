using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Modules.Administration.Areas.Administration.Controllers;
// Compatibility entry for the old module area; the live implementation is in the host web app.
[Area("Administration"), Authorize]
public sealed class HomeController : Controller
{
    public IActionResult Index() => Redirect("/Administration");
}
