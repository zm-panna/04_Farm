using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
namespace EnterpriseERP.Web;
/// <summary>Legacy module project markers are not enabled product workflows.</summary>
public sealed class SupportedControllersFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.Where(x => x.Namespace?.StartsWith("EnterpriseERP.Modules.", StringComparison.Ordinal) == true).ToArray())
            feature.Controllers.Remove(controller);
    }
}
