using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Web.Controllers;
[Authorize,ApiController,Route("api/operations")]
public sealed class IntegratedController(IntegratedOperations operations,ReportService reports,SubscriptionService subscriptions,AutomationService automation,AiToolService ai):ControllerBase
{
 [HttpPost("purchase")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Purchase([FromForm]int itemId,[FromForm]decimal quantity,[FromForm]decimal price,[FromForm]string reference,CancellationToken ct)=>Ok(await operations.Receive(itemId,quantity,price,reference,ct));
 [HttpPost("sale")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Sale([FromForm]int itemId,[FromForm]decimal quantity,[FromForm]decimal price,[FromForm]string reference,CancellationToken ct)=>Ok(await operations.Sell(itemId,quantity,price,reference,ct));
 [HttpGet("summary")]public async Task<IActionResult> Summary(CancellationToken ct)=>Ok(await reports.Summary(ct));
 [HttpGet("summary.csv")]public async Task<IActionResult> Csv(CancellationToken ct)=>File(await reports.Csv(ct,ReportLanguage.Resolve(Request.Query["lang"],Request.Cookies["farmsphere-language"])),"text/csv","farm-summary.csv");
 [HttpGet("subscription")]public async Task<IActionResult> Subscription(CancellationToken ct)=>Ok(await subscriptions.State(DateOnly.FromDateTime(DateTime.UtcNow),ct));
 [HttpPost("subscription/payment")][ValidateAntiForgeryToken]
 public async Task<IActionResult> Payment([FromForm]long invoiceId,[FromForm]string reference,[FromForm]decimal amount,CancellationToken ct){await subscriptions.RecordPayment(invoiceId,reference,amount,ct);return Ok(new{saved=true});}
 [HttpGet("automation/{id:long}/dry-run")]public async Task<IActionResult> Preview(long id,CancellationToken ct)=>Ok(await automation.DryRun(id,ct));
 [HttpGet("ai/summary")]public async Task<IActionResult> Ai(CancellationToken ct)=>Content(await ai.Execute("farm.summary",ct),"application/json");
}
