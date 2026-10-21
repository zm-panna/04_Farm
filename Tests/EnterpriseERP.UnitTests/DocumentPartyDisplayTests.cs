using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class DocumentPartyDisplayTests
{
 [Fact] public async Task DisplayFallbackIsDistinctFromARealPartyNamedUnassigned()
 {
  await using var f=await SecurityTestScope.CreateAsync();foreach(var permission in new[]{"Inventory.Create","Purchase.Post","Accounts.View"}) await f.GrantAsync(permission);
  var farm=new FarmService(f.Db,f.Tenant,f.Permissions);var operations=new IntegratedOperations(f.Db,f.Tenant,f.Permissions);
  var item=await farm.AddStock("DISPLAY-ITEM","Display item",0,default,"KG");
  var party=new TradingParty{CompanyId=1,BranchId=11,Kind="Supplier",Code="DISPLAY-PARTY",Name="Unassigned",Active=true};
  f.Db.TradingParties.Add(party);await f.Db.SaveChangesAsync();
  await operations.Receive(item.Id,1,1,"without-party",default);
  await operations.Receive(item.Id,1,1,"real-party",default,party.Id);
  var documents=await new FinanceService(f.Db,f.Tenant,f.Permissions).Documents(default);
  var fallback=Assert.Single(documents,x=>x.Reference=="PUR-without-party");Assert.Equal("Unassigned",fallback.Party);Assert.False(fallback.PartyAssigned);
  var real=Assert.Single(documents,x=>x.Reference=="PUR-real-party");Assert.Equal("Unassigned",real.Party);Assert.True(real.PartyAssigned);
 }
}
