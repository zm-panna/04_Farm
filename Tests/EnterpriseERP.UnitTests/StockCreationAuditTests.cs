using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class StockCreationAuditTests
{
 private static FarmService Farm(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
 [Fact] public async Task ExplicitStockUnitIsNormalizedAndAuditedWithCreate()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.Create");
  var item=await Farm(f).AddStock(" feed ","Feed product",1.001m,default," kg ");
  Assert.Equal("FEED",item.SKU);Assert.Equal("KG",item.UnitOfMeasure);Assert.Equal(0,item.QuantityOnHand);
  var audits=await f.Db.Audits.ToListAsync();Assert.Equal(2,audits.Count);Assert.All(audits,a=>{Assert.Equal(1,a.CompanyId);Assert.Equal("operator-a",a.ActorId);});
  Assert.Contains(audits,a=>a.Action=="Product.Create"&&a.Reference=="FEED");Assert.Contains(audits,a=>a.Action=="Product.UnitAssigned"&&a.Reference.Contains("KG"));
 }
 [Theory][InlineData(null)][InlineData("")][InlineData("kg/l")]
 public async Task MissingOrInvalidUnitCannotCreateStockOrAudit(string? unit)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.Create");
  await Assert.ThrowsAsync<ArgumentException>(()=>Farm(f).AddStock("FEED","Feed product",0,default,unit));
  Assert.Empty(await f.Db.StockItems.ToListAsync());Assert.Empty(await f.Db.Audits.ToListAsync());
 }
 [Theory][InlineData("A","Valid name")][InlineData("VALID","N")][InlineData("A\tB","Valid name")][InlineData("VALID","A\tB")]
 public async Task StockServiceUsesTheSameTextRulesAsMasterUi(string sku,string name)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.Create");
  await Assert.ThrowsAsync<ArgumentException>(()=>Farm(f).AddStock(sku,name,0,default,"KG"));Assert.Empty(await f.Db.StockItems.ToListAsync());
 }
 [Fact] public async Task StockApiLimitsDoNotCreateValuesThatMasterUiCannotEdit()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.Create");
  await Assert.ThrowsAsync<ArgumentException>(()=>Farm(f).AddStock(new string('S',61),"Valid name",0,default,"KG"));
  await Assert.ThrowsAsync<ArgumentException>(()=>Farm(f).AddStock("VALID",new string('N',161),0,default,"KG"));
  var item=await Farm(f).AddStock(new string('S',60),new string('N',160),0,default,"KG");Assert.Equal(60,item.SKU.Length);Assert.Equal(160,item.Name.Length);
 }
 [Fact] public async Task DuplicateStockCannotCreateExtraAudits()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.Create");
  await Farm(f).AddStock("FEED","Feed product",0,default,"KG");
  await Assert.ThrowsAsync<DbUpdateException>(()=>Farm(f).AddStock("FEED","Another product",0,default,"KG"));
  f.Db.ChangeTracker.Clear();Assert.Equal(1,await f.Db.StockItems.CountAsync());Assert.Equal(2,await f.Db.Audits.CountAsync());
 }
 [Fact] public async Task StockStoreFailureRollsBackBothAuditEntries()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.Create");
  await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_synthetic_stock BEFORE INSERT ON StockItems WHEN NEW.SKU='FAIL-STOCK' BEGIN SELECT RAISE(ABORT, 'synthetic fixture rejection'); END;");
  await Assert.ThrowsAsync<DbUpdateException>(()=>Farm(f).AddStock("FAIL-STOCK","Synthetic failure",0,default,"KG"));
  f.Db.ChangeTracker.Clear();Assert.Empty(await f.Db.StockItems.ToListAsync());Assert.Empty(await f.Db.Audits.ToListAsync());
 }
 [Fact] public async Task DeniedStockCreationCannotCreateMetadataOrAudits()
 {
  await using var f=await SecurityTestScope.CreateAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Farm(f).AddStock("DENIED","Denied product",0,default,"KG"));
  Assert.Empty(await f.Db.StockItems.ToListAsync());Assert.Empty(await f.Db.Audits.ToListAsync());
 }
}
