using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Web.Controllers;
[ApiController,Authorize,Route("api/support")]
public sealed class SupportApiController(SupportService support):ControllerBase
{
 [HttpGet("tickets")]public async Task<IActionResult> List([FromQuery]string? status,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await support.List(status,page,ct));
 [HttpPost("tickets"),ValidateAntiForgeryToken]public async Task<IActionResult> Create([FromBody]CreateTicketRequest request,CancellationToken ct)=>Ok(new{id=await support.Create(request,ct)});
 [HttpPut("tickets/{id:long}"),ValidateAntiForgeryToken]public async Task<IActionResult> Update(long id,[FromBody]UpdateTicketRequest request,CancellationToken ct){await support.Update(id,request,ct);return NoContent();}
 [HttpGet("tickets/{id:long}/history")]public async Task<IActionResult> History(long id,CancellationToken ct)=>Ok(await support.History(id,ct));
}
