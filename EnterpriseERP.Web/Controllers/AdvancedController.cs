using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Web.Controllers;
[ApiController,Authorize,Route("api/advanced")]
public sealed class AdvancedController(FinanceService finance,ReportExportService export,OllamaChatService chat,AutomationApprovalService approvals):ControllerBase
{
 [HttpGet("ledger")]public async Task<IActionResult> Ledger([FromQuery]string? account,[FromQuery]DateTimeOffset from,[FromQuery]DateTimeOffset to,CancellationToken ct)=>Ok(await finance.Ledger(account,from,to,ct));
 [HttpGet("trial-balance")]public async Task<IActionResult> TrialBalance([FromQuery]DateTimeOffset through,CancellationToken ct)=>Ok(await finance.TrialBalance(through,ct));
 [HttpGet("statement")]public async Task<IActionResult> Statement([FromQuery]DateTimeOffset through,CancellationToken ct)=>Ok(await finance.Statement(through,ct));
 [HttpGet("trial-balance.xlsx")]public async Task<IActionResult> Excel([FromQuery]DateTimeOffset through,CancellationToken ct)=>File(await export.TrialBalanceExcel(through,ct,ReportLanguage.Resolve(Request.Query["lang"],Request.Cookies["farmsphere-language"])),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet","trial-balance.xlsx");
 [HttpGet("farm-summary.pdf")]public async Task<IActionResult> Pdf(CancellationToken ct)=>File(await export.SummaryPdf(ct,ReportLanguage.Resolve(Request.Query["lang"],Request.Cookies["farmsphere-language"])),"application/pdf","farm-summary.pdf");
 [HttpPost("payment")]public async Task<IActionResult> Payment([FromBody]PaymentRequest r,CancellationToken ct)=>Ok(new{voucherId=await finance.RecordPayment(r.Reference,r.PartyType,r.Amount,ct,r.DocumentId)});
 [HttpPost("reconcile")]public async Task<IActionResult> Reconcile([FromBody]ReconcileRequest r,CancellationToken ct){await finance.Reconcile(r.StatementLineId,r.VoucherId,ct);return Ok();}
 [HttpPost("ai/chat")]public async Task<IActionResult> Chat([FromBody]ChatRequest r,CancellationToken ct)=>Ok(new{answer=await chat.Ask(r.Question,ct)});
 [HttpPost("automation/request")]public async Task<IActionResult> RequestApproval([FromBody]ApprovalRequest r,CancellationToken ct)=>Ok(new{id=await approvals.Request(r.RuleId,r.Payload,ct)});
 [HttpPost("automation/approve")]public async Task<IActionResult> Approve([FromBody]ApprovalDecision r,CancellationToken ct){await approvals.Approve(r.ApprovalId,r.Payload,ct);return Ok();}
}
public record PaymentRequest(string Reference,string PartyType,decimal Amount,long DocumentId);
public record ReconcileRequest(long StatementLineId,long VoucherId);
public record ChatRequest(string Question);
public record ApprovalRequest(long RuleId,string Payload);
public record ApprovalDecision(long ApprovalId,string Payload);
