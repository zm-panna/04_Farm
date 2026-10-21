using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class OpeningBalanceWorkflowTests
{
 private static DateOnly Today=>DateOnly.FromDateTime(DateTime.UtcNow);
 private static OpeningBalanceService Service(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
 private static OpeningBalanceInput CashOpening(string reference="OPEN-1")=>new()
 {
  Reference=reference,OpeningDate=Today,ReviewNote="Reviewed source statement and chart mapping",ConfirmReviewed=true,
  Ledger=new(){new(){AccountCode="1000",Debit=100},new(){AccountCode="3000",Credit=100}}
 };
 private static async Task SeedMasters(SecurityTestScope f)
 {
  f.Db.StockItems.AddRange(new StockItem{Id=1,CompanyId=1,BranchId=11,SKU="OPEN-ITEM",Name="Feed"},new StockItem{Id=2,CompanyId=1,BranchId=12,SKU="OTHER-BRANCH",Name="Other"},new StockItem{Id=3,CompanyId=2,BranchId=21,SKU="OTHER-TENANT",Name="Other"});
  f.Db.Animals.AddRange(new Animal{Id=1,CompanyId=1,BranchId=11,Tag="OPEN-ANIMAL",Breed="Cattle"},new Animal{Id=2,CompanyId=1,BranchId=12,Tag="OTHER-BRANCH"},new Animal{Id=3,CompanyId=2,BranchId=21,Tag="OTHER-TENANT"});
  await f.Db.SaveChangesAsync();
 }
 [Fact] public async Task BalancedOpeningPostsOnceWithTraceableVoucherAndAudit()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");
  var opening=await Service(f).Post(CashOpening(" OPEN-1 "));
  Assert.True(opening.VoucherId>0);Assert.Equal("OPEN-1",opening.Reference);
  var voucher=await f.Db.Vouchers.Include(x=>x.Lines).SingleAsync();Assert.True(voucher.Posted);Assert.Equal(100,voucher.Lines.Sum(x=>x.Debit));Assert.Equal(100,voucher.Lines.Sum(x=>x.Credit));
  Assert.Single(await f.Db.Audits.Where(x=>x.Action=="Accounts.Opening.Post").ToListAsync());
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(CashOpening("SECOND")));Assert.Single(await f.Db.OpeningBalances.ToListAsync());
 }
 [Fact] public async Task ItemizedValuesMatchStockAnimalAndLedgerWithoutInventingPayables()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");await SeedMasters(f);
  var input=CashOpening();input.Ledger=new(){new(){AccountCode="1000",Debit=50},new(){AccountCode="3000",Credit=251}};
  input.Stocks.Add(new(){StockItemId=1,Quantity=3,Value=1});input.Animals.Add(new(){AnimalId=1,Value=200});
  var opening=await Service(f).Post(input);
  Assert.Single(opening.Stocks);Assert.Single(opening.Animals);Assert.Empty(await f.Db.FinanceDocuments.ToListAsync());
  var stock=await f.Db.StockItems.SingleAsync(x=>x.Id==1);Assert.Equal(3,stock.QuantityOnHand);Assert.Equal(.333m,stock.UnitCost);
  Assert.Equal(1,(await f.Db.InventoryCostBalances.SingleAsync()).Value);Assert.Equal(3,(await f.Db.StockMovements.SingleAsync()).QuantityDelta);
  Assert.Equal(200,(await f.Db.Animals.SingleAsync(x=>x.Id==1)).PurchaseCost);
  Assert.Equal(1,opening.Voucher.Lines.Single(x=>x.AccountCode=="1300").Debit);Assert.Equal(200,opening.Voucher.Lines.Single(x=>x.AccountCode=="1400").Debit);
  // Exact final depletion and a sale now use the approved opening basis, not fabricated acquisition invoices.
  await new FarmHealthFeedService(f.Db,f.Tenant,f.Permissions).ConsumeFeed(1,1,3,"FEED-OPEN",Today,default);
  Assert.Equal(1,(await f.Db.AnimalFeedRecords.SingleAsync()).CostAmount);Assert.Equal(0,(await f.Db.InventoryCostBalances.SingleAsync()).Value);
  await new FarmService(f.Db,f.Tenant,f.Permissions).SellAnimal(1,300,"SALE-OPEN",default);
  Assert.True((await f.Db.Animals.SingleAsync(x=>x.Id==1)).Sold);
  Assert.DoesNotContain(await f.Db.FinanceDocuments.ToListAsync(),x=>x.Kind=="AnimalPurchase");
 }
 [Theory][InlineData("Accounts.Opening")][InlineData("Permissions.Manage")]
 public async Task OpeningRequiresBothExplicitOperationAndTenantAdministration(string onlyGrant)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync(onlyGrant);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Post(CashOpening()));Assert.Empty(await f.Db.Vouchers.ToListAsync());
 }
 [Fact] public async Task UnreviewedOrUnbalancedOpeningWritesNothing()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");var input=CashOpening();input.ConfirmReviewed=false;
  await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Post(input));input.ConfirmReviewed=true;input.Ledger[1].Credit=99;
  await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Post(input));Assert.Empty(await f.Db.OpeningBalances.ToListAsync());Assert.Empty(await f.Db.Vouchers.ToListAsync());
 }
 [Theory][InlineData("1100")][InlineData("1300")][InlineData("1400")][InlineData("2100")][InlineData("4100")][InlineData("6900")]
 public async Task DirectControlOrProfitLossAccountsAreRejected(string account)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");var input=CashOpening();input.Ledger[0].AccountCode=account;
  await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Post(input));Assert.Empty(await f.Db.Vouchers.ToListAsync());
 }
 [Theory][InlineData(2)][InlineData(3)]
 public async Task OpeningCannotValueAnotherBranchOrCompanyStock(int id)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");await SeedMasters(f);var input=CashOpening();input.Ledger=new(){new(){AccountCode="3000",Credit=100}};input.Stocks.Add(new(){StockItemId=id,Quantity=10,Value=100});
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(input));Assert.Empty(await f.Db.OpeningBalances.ToListAsync());Assert.Empty(await f.Db.StockMovements.ToListAsync());Assert.Equal(0,(await f.Db.StockItems.AsNoTracking().SingleAsync(x=>x.Id==id)).QuantityOnHand);
 }
 [Theory][InlineData(2)][InlineData(3)]
 public async Task OpeningCannotValueAnotherBranchOrCompanyAnimal(int id)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");await SeedMasters(f);var input=CashOpening();input.Ledger=new(){new(){AccountCode="3000",Credit=100}};input.Animals.Add(new(){AnimalId=id,Value=100});
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(input));Assert.Empty(await f.Db.OpeningBalances.ToListAsync());Assert.Equal(0,(await f.Db.Animals.AsNoTracking().SingleAsync(x=>x.Id==id)).PurchaseCost);
 }
 [Fact] public async Task PreviouslyNonzeroMasterValuesRequireSeparateMigration()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");await SeedMasters(f);
  (await f.Db.StockItems.SingleAsync(x=>x.Id==1)).QuantityOnHand=5;await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(CashOpening()));Assert.Empty(await f.Db.Vouchers.ToListAsync());
 }
 [Fact] public async Task ExistingFinancialActivityPreventsLateOpening()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");
  f.Db.Vouchers.Add(BusinessInput.Voucher(1,11,"EXISTING",("1000",1,0),("3000",0,1)));await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(CashOpening()));Assert.Empty(await f.Db.OpeningBalances.ToListAsync());
 }
 [Fact] public async Task ClosedDateCannotReceiveOpening()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");
  f.Db.AccountingPeriodCloses.Add(new(){CompanyId=1,BranchId=11,ClosedThrough=Today.AddDays(-1),ClosedBy="operator-a",Reason="Closed"});await f.Db.SaveChangesAsync();
  var input=CashOpening();input.OpeningDate=Today.AddDays(-2);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Post(input));Assert.Empty(await f.Db.OpeningBalances.ToListAsync());
 }
 [Fact] public async Task OpeningFailureRollsBackAllValuationsAndBalances()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");await SeedMasters(f);
  await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectOpeningAudit BEFORE INSERT ON Audits WHEN NEW.Action='Accounts.Opening.Post' BEGIN SELECT RAISE(ABORT,'simulated audit failure'); END;");
  var input=CashOpening();input.Ledger=new(){new(){AccountCode="3000",Credit=300}};input.Stocks.Add(new(){StockItemId=1,Quantity=10,Value=100});input.Animals.Add(new(){AnimalId=1,Value=200});
  await Assert.ThrowsAsync<DbUpdateException>(()=>Service(f).Post(input));
  Assert.Empty(await f.Db.OpeningBalances.ToListAsync());Assert.Empty(await f.Db.OpeningStockValues.ToListAsync());Assert.Empty(await f.Db.OpeningAnimalValues.ToListAsync());Assert.Empty(await f.Db.InventoryCostBalances.ToListAsync());Assert.Empty(await f.Db.StockMovements.ToListAsync());Assert.Empty(await f.Db.Vouchers.ToListAsync());
  Assert.Equal(0,(await f.Db.StockItems.AsNoTracking().SingleAsync(x=>x.Id==1)).QuantityOnHand);Assert.Equal(0,(await f.Db.Animals.AsNoTracking().SingleAsync(x=>x.Id==1)).PurchaseCost);
 }
 [Fact] public async Task OpeningRowsCannotBeEditedAfterPosting()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");var opening=await Service(f).Post(CashOpening());opening.ReviewNote="Rewritten";
  await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());
 }
}
