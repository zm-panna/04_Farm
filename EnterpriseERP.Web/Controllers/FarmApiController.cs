using EnterpriseERP.Domain;using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;
namespace EnterpriseERP.Web.Controllers;
[ApiController][Route("api/farm")][Authorize]
public sealed class FarmApiController(FarmService farm):ControllerBase
{
 [HttpGet("animals")] public Task<List<Animal>> Animals(CancellationToken ct)=>farm.Animals(ct);
 [HttpPost("animals")][ValidateAntiForgeryToken] public Task<Animal> AddAnimal([FromForm]string tag,[FromForm]string breed,[FromForm]decimal cost,CancellationToken ct)=>farm.AddAnimal(tag,breed,cost,ct);
 [HttpPost("stock")][ValidateAntiForgeryToken] public Task<StockItem> AddStock([FromForm]string sku,[FromForm]string name,[FromForm]decimal cost,[FromForm]string unitOfMeasure,CancellationToken ct)=>farm.AddStock(sku,name,cost,ct,unitOfMeasure);
 [HttpPost("stock/{id:int}/move")][ValidateAntiForgeryToken] public Task<StockItem> MoveStock(int id,[FromForm]decimal delta,[FromForm]string reference,CancellationToken ct)=>farm.MoveStock(id,delta,reference,ct);
 [HttpPost("milk")][ValidateAntiForgeryToken] public Task<MilkYield> Milk([FromForm]int animalId,[FromForm]DateOnly date,[FromForm]string session,[FromForm]decimal litres,CancellationToken ct)=>farm.RecordMilk(animalId,date,session,litres,ct);
 [HttpPost("animals/{id:int}/sale")][ValidateAntiForgeryToken] public Task<AnimalSale> Sale(int id,[FromForm]decimal amount,[FromForm]string reference,CancellationToken ct)=>farm.SellAnimal(id,amount,reference,ct);
}
