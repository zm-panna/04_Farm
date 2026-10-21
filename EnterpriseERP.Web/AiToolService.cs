using System.Text.Json;
namespace EnterpriseERP.Web;
/// <summary>Allowlisted read-only tool dispatcher. The model never receives SQL or database credentials.</summary>
public sealed class AiToolService(ReportService reports, PermissionService permissions, EnterpriseAiService? enterprise = null)
{
 public async Task<string> Execute(string tool, CancellationToken ct)
 {
  await permissions.Demand("AI.Read", ct);
  if (tool.StartsWith("enterprise.", StringComparison.Ordinal) && enterprise is not null)
   return await enterprise.Serialize(tool["enterprise.".Length..],ct);
  if (tool != "farm.summary") throw new UnauthorizedAccessException("Unknown AI tool");
  var data = await reports.Summary(ct); // Reports.View and tenant scope are independently enforced.
  return JsonSerializer.Serialize(new { tool = "farm.summary", generatedUtc = DateTimeOffset.UtcNow, data });
 }
}
