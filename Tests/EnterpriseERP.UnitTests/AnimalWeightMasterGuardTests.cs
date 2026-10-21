using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class AnimalWeightMasterGuardTests
{
 [Fact] public async Task MasterEditCannotOverrideWeightOrBypassFatteningPermission()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Livestock.Edit");
  var animal=new Animal{Id=1,CompanyId=1,BranchId=11,Tag="WEIGHT-1",Breed="Cattle",CurrentWeightKg=130};f.Db.Animals.Add(animal);
  f.Db.AnimalWeightRecords.Add(new(){CompanyId=1,BranchId=11,AnimalId=1,Reference="WEIGH-1",WeighedOn=DateOnly.FromDateTime(DateTime.UtcNow),WeightKg=130,RecordedBy="operator-a"});await f.Db.SaveChangesAsync();
  var controller=new AnimalRegistryController(f.Db,f.Tenant,f.Permissions,new(f.Db,f.Tenant,f.Permissions));
  var result=await controller.Save(new(){Id=1,Tag="WEIGHT-CHANGED",Breed="Cattle",CurrentWeightKg=140,RowVersion=animal.RowVersion.ToArray()});
  Assert.IsType<ViewResult>(result);Assert.False(controller.ModelState.IsValid);
  var saved=await f.Db.Animals.AsNoTracking().SingleAsync();Assert.Equal(130,saved.CurrentWeightKg);Assert.Equal("WEIGHT-1",saved.Tag);
  Assert.Equal(130,(await f.Db.AnimalWeightRecords.SingleAsync()).WeightKg);Assert.Empty(await f.Db.Audits.ToListAsync());
 }
 [Fact] public async Task MasterMetadataEditPreservesExistingWeightWithoutFatteningPermission()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Livestock.Edit");
  var animal=new Animal{Id=1,CompanyId=1,BranchId=11,Tag="WEIGHT-1",Breed="Cattle",CurrentWeightKg=130};f.Db.Animals.Add(animal);await f.Db.SaveChangesAsync();
  var controller=new AnimalRegistryController(f.Db,f.Tenant,f.Permissions,new(f.Db,f.Tenant,f.Permissions));
  Assert.IsType<RedirectToActionResult>(await controller.Save(new(){Id=1,Tag="WEIGHT-2",Breed="Improved",CurrentWeightKg=130,RowVersion=animal.RowVersion.ToArray()}));
  Assert.Equal(130,(await f.Db.Animals.AsNoTracking().SingleAsync()).CurrentWeightKg);
  Assert.Equal("WEIGHT-2",(await f.Db.Animals.AsNoTracking().SingleAsync()).Tag);
 }
 [Fact] public async Task MasterEditCannotClearAnExistingWeight()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Livestock.Edit");
  var animal=new Animal{Id=1,CompanyId=1,BranchId=11,Tag="WEIGHT-1",Breed="Cattle",CurrentWeightKg=130};f.Db.Animals.Add(animal);await f.Db.SaveChangesAsync();
  var controller=new AnimalRegistryController(f.Db,f.Tenant,f.Permissions,new(f.Db,f.Tenant,f.Permissions));
  Assert.IsType<ViewResult>(await controller.Save(new(){Id=1,Tag="WEIGHT-1",Breed="Cattle",CurrentWeightKg=null,RowVersion=animal.RowVersion.ToArray()}));
  Assert.Equal(130,(await f.Db.Animals.AsNoTracking().SingleAsync()).CurrentWeightKg);
 }
}
