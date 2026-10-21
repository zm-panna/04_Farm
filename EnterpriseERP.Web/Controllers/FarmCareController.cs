using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Web.Controllers;
[Authorize,ApiController,Route("api/farm-care")]
public sealed class FarmCareController(FarmHealthFeedService farm):ControllerBase
{
 [HttpGet("health/{animalId:int}")] public Task<List<AnimalHealthRecord>> Health(int animalId,CancellationToken ct)=>farm.Health(animalId,ct);
 [HttpPost("health")][ValidateAntiForgeryToken] public Task<AnimalHealthRecord> RecordHealth([FromForm]int animalId,[FromForm]string reference,[FromForm]string eventType,[FromForm]DateOnly eventDate,[FromForm]DateOnly? nextDue,[FromForm]string details,CancellationToken ct)=>farm.RecordHealth(animalId,reference,eventType,eventDate,nextDue,details,ct);
 [HttpGet("feed/{animalId:int}")] public Task<List<AnimalFeedRecord>> Feed(int animalId,CancellationToken ct)=>farm.FeedHistory(animalId,ct);
 [HttpPost("feed")][ValidateAntiForgeryToken] public Task<AnimalFeedRecord> Consume([FromForm]int animalId,[FromForm]int stockItemId,[FromForm]decimal quantity,[FromForm]string reference,[FromForm]DateOnly date,CancellationToken ct)=>farm.ConsumeFeed(animalId,stockItemId,quantity,reference,date,ct);
}
