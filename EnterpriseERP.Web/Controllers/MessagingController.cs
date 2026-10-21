using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseERP.Web.Controllers;

public sealed record MessagingIndexModel(bool CanSend, bool CanReviewAutomation);

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MessagingController(MessagingService messaging, MessagingAccess access) : Controller
{
    [HttpGet("/Messaging")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var actor = await access.DemandAsync(User, "Messaging.View", ct);
        return View(new MessagingIndexModel(
            await access.EligibleAsync(actor.CompanyId, actor.BranchId, actor.UserId, "Messaging.Send", ct) is not null,
            await access.EligibleAsync(actor.CompanyId, actor.BranchId, actor.UserId, "Automation.Review", ct) is not null));
    }

    [HttpGet("/api/messaging/inbox")]
    public async Task<IActionResult> Inbox(long? beforeId, bool unreadOnly, CancellationToken ct) =>
        Ok(await messaging.InboxAsync(User, beforeId, unreadOnly, ct));

    [HttpGet("/api/messaging/sent")]
    public async Task<IActionResult> Sent(long? beforeId, CancellationToken ct) =>
        Ok(await messaging.SentAsync(User, beforeId, ct));

    [HttpGet("/api/messaging/messages/{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        Ok(await messaging.DetailAsync(User, id, ct));

    [HttpGet("/api/messaging/recipients")]
    public async Task<IActionResult> Recipients(string? q, CancellationToken ct) =>
        Ok(await messaging.RecipientsAsync(User, q, ct));

    [HttpPost("/api/messaging/send"), ValidateAntiForgeryToken, RequestSizeLimit(40000)]
    public async Task<IActionResult> Send([FromBody] MessagingSendRequest? request, CancellationToken ct)
    {
        if (!ModelState.IsValid || request is null) return BadRequest(new { detail = "A valid internal message is required." });
        try { return Ok(await messaging.SendAsync(User, request, ct)); }
        catch (MessagingFloodException ex)
        {
            Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return StatusCode(429, new { detail = ex.Message, retryAfterSeconds = ex.RetryAfterSeconds });
        }
    }

    [HttpPost("/api/messaging/notifications/{id:long}/read"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(long id, CancellationToken ct)
    { await messaging.ReadAsync(User, id, ct); return NoContent(); }

    [HttpGet("/api/messaging/automation")]
    public async Task<IActionResult> Automation(long? beforeId, bool unreadOnly, CancellationToken ct) =>
        Ok(await messaging.AutomationAsync(User, beforeId, unreadOnly, ct));

    [HttpGet("/api/messaging/automation/{id:long}")]
    public async Task<IActionResult> AutomationDetail(long id, CancellationToken ct) =>
        Ok(await messaging.AutomationDetailAsync(User, id, ct));

    [HttpPost("/api/messaging/automation/{id:long}/read"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAutomation(long id, CancellationToken ct)
    { await messaging.ReadAutomationAsync(User, id, ct); return NoContent(); }
}
