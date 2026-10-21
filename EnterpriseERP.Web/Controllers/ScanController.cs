using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using ZXing;

namespace EnterpriseERP.Web.Controllers;
public sealed record ScanWorkspaceModel(bool CanManage, string[] Contexts, DateOnly BusinessDate);
public sealed class ScanResolveInput
{
    [Required, StringLength(20)] public string Kind { get; set; } = "";
    [Required, StringLength(80)] public string Value { get; set; } = "";
    public int? DestinationBranchId { get; set; }
}
public sealed class ScanConfirmation { public bool ConfirmReviewed { get; set; } }
public sealed class ScanRevocation { [Required, StringLength(300)] public string Reason { get; set; } = ""; }

[Authorize, EnableRateLimiting("scanning"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ScanController(ScanService service, AppDbContext db, TenantContext tenant, PermissionService permission) : Controller
{
    [HttpGet("/scan")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await permission.Demand("Scanning.Use", ct); var t = await tenant.GetAsync(ct);
        var contexts = new List<string>();
        foreach (var pair in new[] { ("Product", "Inventory.View"), ("Animal", "Livestock.View"), ("Asset", "FixedAssets.View"), ("PurchaseOrder", "Purchase.View"), ("Document", "Accounts.View") })
        { try { await permission.Demand(pair.Item2, ct); contexts.Add(pair.Item1); } catch (UnauthorizedAccessException) { } }
        var manage = false; try { await permission.Demand("Scanning.ManageLabels", ct); manage = true; } catch (UnauthorizedAccessException) { }
        return View(new ScanWorkspaceModel(manage, contexts.ToArray(), (await CompanyBusinessDate.Current(db, t.company, ct)).Date));
    }
    [HttpPost("/api/scan/resolve"), ValidateAntiForgeryToken, RequestSizeLimit(4000)]
    public async Task<IActionResult> Resolve([FromBody] ScanResolveInput? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input is null) return BadRequest(new { detail = "Choose a scan context and bounded label value." });
        return Ok(await service.Resolve(input.Kind, input.Value, input.DestinationBranchId, ct));
    }
    [HttpGet("/api/scan/branches")]
    public async Task<IActionResult> Branches(CancellationToken ct) => Ok(await service.Branches(ct));
    [HttpGet("/api/scan/options")]
    public async Task<IActionResult> Options(string kind, string? q, int page = 1, CancellationToken ct = default) => Ok(await service.Options(kind, q, page, ct));
    [HttpGet("/api/scan/labels")]
    public async Task<IActionResult> Labels(string kind, string? q, int page = 1, CancellationToken ct = default) => Ok(await service.Labels(kind, q, page, ct));
    [HttpPost("/api/scan/labels"), ValidateAntiForgeryToken, RequestSizeLimit(4000)]
    public async Task<IActionResult> CreateLabel([FromBody] ScanLabelInput? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input is null) return BadRequest(new { detail = "Choose a valid label mapping." });
        return Ok(await service.CreateLabel(input, ct));
    }
    [HttpPost("/api/scan/labels/{id:long}/revoke"), ValidateAntiForgeryToken, RequestSizeLimit(4000)]
    public async Task<IActionResult> Revoke(long id, [FromBody] ScanRevocation? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input is null) return BadRequest(new { detail = "Enter a bounded revocation reason." });
        await service.RevokeLabel(id, input.Reason, ct); return NoContent();
    }
    [HttpGet("/scan/labels/{id:long}/print")]
    public async Task<IActionResult> Print(long id, CancellationToken ct) => View(await service.PrintLabel(id, ct));
    [HttpGet("/api/scan/labels/{id:long}/image")]
    public async Task<IActionResult> Image(long id, string format, CancellationToken ct)
    {
        var label = await service.PrintLabel(id, ct);
        if (format is not ("qr" or "code128")) return BadRequest(new { detail = "Choose QR or Code128 for the printed label." });
        return Content(ScanLabelImage.Svg(label.Value, format), "image/svg+xml", Encoding.UTF8);
    }
    [HttpPost("/api/scan/drafts"), ValidateAntiForgeryToken, RequestSizeLimit(16000)]
    public async Task<IActionResult> Draft([FromBody] ScanDraftInput? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input is null) return BadRequest(new { detail = "Complete the scan draft fields with valid values." });
        return Ok(await service.Draft(input, ct));
    }
    [HttpGet("/api/scan/drafts")]
    public async Task<IActionResult> History(int page = 1, string? q = null, string? status = null, CancellationToken ct = default) => Ok(await service.History(page, ct, q, status));
    [HttpGet("/api/scan/drafts/{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) => Ok(await service.Detail(id, ct));
    [HttpPost("/api/scan/drafts/{id:long}/confirm"), ValidateAntiForgeryToken, RequestSizeLimit(2000)]
    public async Task<IActionResult> Confirm(long id, [FromBody] ScanConfirmation? input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input is null) return BadRequest(new { detail = "Review the draft and explicitly confirm the operation." });
        return Ok(await service.Confirm(id, input.ConfirmReviewed, ct));
    }
    [HttpPost("/api/scan/drafts/{id:long}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) { await service.Cancel(id, ct); return NoContent(); }
}

public static class ScanLabelImage
{
    public static string Svg(string value, string format)
    {
        if (value.Length is < 1 or > 80 || value.Any(c => c is < '!' or > '~') || format is not ("qr" or "code128")) throw new ArgumentException("Invalid printable identifier.");
        var matrix = new MultiFormatWriter().encode(value, format == "qr" ? BarcodeFormat.QR_CODE : BarcodeFormat.CODE_128,
            format == "qr" ? 256 : 720, format == "qr" ? 256 : 140, new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = format == "qr" ? 4 : 20 });
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {matrix.Width} {matrix.Height}\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/><path fill=\"black\" d=\"");
        for (var y = 0; y < matrix.Height;)
        {
            var height = 1;
            while (y + height < matrix.Height && Enumerable.Range(0, matrix.Width).All(x => matrix[x, y] == matrix[x, y + height])) height++;
            for (var x = 0; x < matrix.Width;)
            {
                if (!matrix[x, y]) { x++; continue; }
                var start = x; while (x < matrix.Width && matrix[x, y]) x++;
                svg.Append($"M{start} {y}h{x - start}v{height}h{start - x}z");
            }
            y += height;
        }
        return svg.Append("\"/></svg>").ToString();
    }
}
