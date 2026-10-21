using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class OperationalCurrencyPrecisionTests
{
    private static IntegratedOperations Ops(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
    private static FarmService Farm(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
    private static async Task<StockItem> Seed(SecurityTestScope f,int minor)
    {
        foreach(var permission in new[]{"Inventory.Create","Purchase.Post","Purchase.Approve","Sales.Post","Accounts.Post","Livestock.Create","Livestock.Sell"}) await f.GrantAsync(permission);
        await CompanyBusinessPolicyTestData.SeedAsync(f,currency:minor==0?"JPY":minor==3?"KWD":"USD",minorUnits:minor);
        return await Farm(f).AddStock("CURR","Currency item",0,default,"KG");
    }
    [Theory][InlineData(0,1.5)][InlineData(2,1.001)]
    public async Task FractionalNewPurchaseInvoiceRejectedBeforeAnyStockOrJournal(int minor,decimal price)
    {
        await using var f=await SecurityTestScope.CreateAsync();var item=await Seed(f,minor);
        await Assert.ThrowsAsync<ArgumentException>(()=>Ops(f).Receive(item.Id,1,price,"INVALID",default));
        Assert.Empty(await f.Db.PurchaseReceipts.ToListAsync());Assert.Empty(await f.Db.Vouchers.ToListAsync());Assert.Empty(await f.Db.StockMovements.ToListAsync());
    }
    [Theory][InlineData(0)][InlineData(2)][InlineData(3)]
    public async Task SaleReturnRoundsCustomerMoneyButKeepsExactInternalCost(int minor)
    {
        await using var f=await SecurityTestScope.CreateAsync();var item=await Seed(f,minor);
        await Ops(f).Receive(item.Id,3,11,"OPEN",default);await Ops(f).Sell(item.Id,3,17,"SALE",default);
        var doc=await f.Db.FinanceDocuments.SingleAsync(x=>x.Kind=="Sale");
        var r1=await Ops(f).Return(doc.Id,1.001m,"R1",default);var r2=await Ops(f).Return(doc.Id,1.999m,"R2",default);
        Assert.Equal(51m,r1.Amount+r2.Amount);Assert.Equal(33m,r1.CostAmount+r2.CostAmount);
        Assert.Equal(CompanyBusinessPolicyService.RoundMoney(51m*1.001m/3m,minor),r1.Amount);
        Assert.Equal(11.011m,r1.CostAmount);
        Assert.All(await f.Db.Vouchers.Include(x=>x.Lines).ToListAsync(),v=>Assert.Equal(v.Lines.Sum(x=>x.Debit),v.Lines.Sum(x=>x.Credit)));
    }
    [Theory][InlineData(0,1.5)][InlineData(2,1.001)]
    public async Task InvalidSaleAndManualCashJournalAreRejectedWithoutWrites(int minor,decimal amount)
    {
        await using var f=await SecurityTestScope.CreateAsync();var item=await Seed(f,minor);await Ops(f).Receive(item.Id,2,10,"OPEN",default);
        await Assert.ThrowsAsync<ArgumentException>(()=>Ops(f).Sell(item.Id,1,amount,"BADSALE",default));
        await Assert.ThrowsAsync<ArgumentException>(()=>Farm(f).PostVoucher("BADCASH",[new(){AccountCode="1000",Debit=amount},new(){AccountCode="3900",Credit=amount}],default));
        Assert.Empty(await f.Db.ProductSales.ToListAsync());Assert.Single(await f.Db.Vouchers.ToListAsync());
    }
    [Fact] public async Task UnconfiguredCoreRetainsExplicitThreeDecimalContract()
    {
        await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.Create");await f.GrantAsync("Purchase.Post");var item=await Farm(f).AddStock("RAW","Unconfigured",0,default,"KG");await Ops(f).Receive(item.Id,1,1.001m,"RAW",default);Assert.Equal(1.001m,(await f.Db.FinanceDocuments.SingleAsync()).Amount);
    }
    [Fact] public async Task NoncashBookCorrectionRetainsInternalPrecision()
    {
        await using var f=await SecurityTestScope.CreateAsync();await Seed(f,2);
        var voucher=await Farm(f).PostVoucher("BOOK",[new(){AccountCode="5100",Debit=.001m},new(){AccountCode="3900",Credit=.001m}],default);
        Assert.Equal(.001m,voucher.Lines.Sum(x=>x.Debit));
    }
    [Fact] public void CurrencyDisplayNeverHidesFractionalHistoricalAmount()
    {
        var model=new OperationsWorkspaceModel{CurrencyCode="USD",CurrencyMinorUnits=2};Assert.Equal("0.01",model.MoneyStep);Assert.Equal("1.00",model.Money(1));Assert.Equal("1.001",model.Money(1.001m));
    }
}
