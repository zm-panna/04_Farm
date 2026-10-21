using System.Globalization;
using System.Security.Claims;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class FinanceCurrencyPrecisionTests
{
    private static decimal D(string value)=>decimal.Parse(value,CultureInfo.InvariantCulture);
    private static FinanceService Finance(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
    private static IntegratedOperations Operations(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
    private static async Task<FinanceDocument> HistoricSale(SecurityTestScope f,decimal amount)
    {
        var stock=await new FarmService(f.Db,f.Tenant,f.Permissions).AddStock("HISTORIC","Synthetic stock",0,default,"KG");
        await Operations(f).Receive(stock.Id,3,1,"HISTORIC-PURCHASE",default); await Operations(f).Sell(stock.Id,1,amount,"HISTORIC-SALE",default);
        return await f.Db.FinanceDocuments.SingleAsync(x=>x.Kind=="Sale");
    }
    private static void Billing(SecurityTestScope f)=>((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim("permission","platform.billing"));

    [Theory]
    [InlineData("JPY",0,"2","0.1")]
    [InlineData("USD",2,"2.12","0.001")]
    [InlineData("KWD",3,"2.123","0.0001")]
    public async Task Configured_currency_enforces_document_payment_refund_and_signed_bank_precision(string currency,int minorUnits,string allowedText,string rejectedText)
    {
        await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var document=await HistoricSale(f,10m);
        await CompanyBusinessPolicyTestData.SeedAsync(f,currency:currency,minorUnits:minorUnits); var finance=Finance(f);var allowed=D(allowedText);var rejected=D(rejectedText);
        var before=await f.Db.Vouchers.CountAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>finance.RecordDocumentPayment(document.Id,"BAD-PAYMENT",rejected,default)); Assert.Equal(before,await f.Db.Vouchers.CountAsync());
        await finance.RecordDocumentPayment(document.Id,"GOOD-PAYMENT",allowed,default);
        await Operations(f).Return(document.Id,1,"FULL-HISTORIC-RETURN",default);
        Assert.Equal(-allowed,(await finance.Documents(default)).Single(x=>x.Id==document.Id).Outstanding);
        await Assert.ThrowsAsync<ArgumentException>(()=>finance.RecordDocumentPayment(document.Id,"BAD-REFUND",rejected,default));
        await finance.RecordDocumentPayment(document.Id,"GOOD-REFUND",allowed,default); Assert.Equal(0,(await finance.Documents(default)).Single(x=>x.Id==document.Id).Outstanding);
        Assert.Equal("CustomerRefund",(await f.Db.FinancePayments.SingleAsync(x=>x.Reference=="GOOD-REFUND")).PartyType);
        var today=DateOnly.FromDateTime(DateTime.UtcNow);
        await Assert.ThrowsAsync<ArgumentException>(()=>finance.RecordBankLine("BAD-POSITIVE",rejected,today,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>finance.RecordBankLine("BAD-NEGATIVE",-rejected,today,default));
        var incoming=await finance.RecordBankLine("VALID-POSITIVE",allowed,today,default);var outgoing=await finance.RecordBankLine("VALID-NEGATIVE",-allowed,today,default);
        Assert.Equal(allowed,incoming.Amount);Assert.Equal(-allowed,outgoing.Amount);Assert.Equal(2,await f.Db.BankStatementLines.CountAsync());
        Assert.All(await f.Db.Vouchers.Include(x=>x.Lines).ToListAsync(),x=>Assert.Equal(x.Lines.Sum(l=>l.Debit),x.Lines.Sum(l=>l.Credit)));
    }
    [Theory]
    [InlineData("JPY",0,"2","0.1")]
    [InlineData("USD",2,"2.12","0.001")]
    [InlineData("KWD",3,"2.123","0.0001")]
    public async Task Configured_currency_enforces_actual_subscription_receipt_precision(string currency,int minorUnits,string allowedText,string rejectedText)
    {
        await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Subscription.Pay");Billing(f);await CompanyBusinessPolicyTestData.SeedAsync(f,currency:currency,minorUnits:minorUnits);
        var invoice=new SubscriptionInvoice{CompanyId=1,Reference="PRECISION-INVOICE",DueDate=DateOnly.FromDateTime(DateTime.UtcNow),Amount=10};f.Db.SubscriptionInvoices.Add(invoice);await f.Db.SaveChangesAsync();
        var service=new SubscriptionService(f.Db,f.Tenant,f.Permissions,f.Accessor);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.RecordPayment(invoice.Id,"INVALID-RECEIPT",D(rejectedText),default));Assert.Empty(await f.Db.SubscriptionPayments.ToListAsync());
        await service.RecordPayment(invoice.Id,"VALID-RECEIPT",D(allowedText),default);Assert.Equal(D(allowedText),(await f.Db.SubscriptionInvoices.SingleAsync()).Paid);
    }
    [Theory]
    [InlineData("JPY",0,"10.1")]
    [InlineData("USD",2,"10.001")]
    public async Task Historical_unrepresentable_document_and_subscription_residuals_require_review_without_revaluing(string currency,int minorUnits,string historicText)
    {
        await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");Billing(f);var historic=D(historicText);var document=await HistoricSale(f,historic);
        var invoice=new SubscriptionInvoice{CompanyId=1,Reference="HISTORIC-INVOICE",DueDate=DateOnly.FromDateTime(DateTime.UtcNow),Amount=historic};f.Db.SubscriptionInvoices.Add(invoice);await f.Db.SaveChangesAsync();
        await CompanyBusinessPolicyTestData.SeedAsync(f,currency:currency,minorUnits:minorUnits);var vouchers=await f.Db.Vouchers.CountAsync();
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>Finance(f).RecordDocumentPayment(document.Id,"NO-ROUNDING",1m,default));Assert.Contains("reviewed correction",error.Message);
        var subscription=new SubscriptionService(f.Db,f.Tenant,f.Permissions,f.Accessor);error=await Assert.ThrowsAsync<InvalidOperationException>(()=>subscription.RecordPayment(invoice.Id,"NO-ROUNDING",1m,default));Assert.Contains("reviewed correction",error.Message);
        Assert.Equal(historic,(await f.Db.FinanceDocuments.SingleAsync(x=>x.Id==document.Id)).Amount);Assert.Equal(historic,(await f.Db.SubscriptionInvoices.SingleAsync()).Amount);Assert.Equal(0,(await f.Db.SubscriptionInvoices.SingleAsync()).Paid);
        Assert.Empty(await f.Db.FinancePayments.ToListAsync());Assert.Empty(await f.Db.SubscriptionPayments.ToListAsync());Assert.Equal(vouchers,await f.Db.Vouchers.CountAsync());
    }
    [Fact] public async Task Unconfigured_legacy_company_keeps_existing_three_decimal_payment_contract()
    {
        await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");Billing(f);var document=await HistoricSale(f,10.123m);var finance=Finance(f);
        await finance.RecordDocumentPayment(document.Id,"LEGACY-RECEIPT",.123m,default);await finance.RecordBankLine("LEGACY-WITHDRAWAL",-.123m,DateOnly.FromDateTime(DateTime.UtcNow),default);
        var invoice=new SubscriptionInvoice{CompanyId=1,Reference="LEGACY-INVOICE",DueDate=DateOnly.FromDateTime(DateTime.UtcNow),Amount=10.123m};f.Db.SubscriptionInvoices.Add(invoice);await f.Db.SaveChangesAsync();
        await new SubscriptionService(f.Db,f.Tenant,f.Permissions,f.Accessor).RecordPayment(invoice.Id,"LEGACY-SUB",.123m,default);
        Assert.Equal(.123m,(await f.Db.FinancePayments.SingleAsync()).Amount);Assert.Equal(.123m,(await f.Db.SubscriptionPayments.SingleAsync()).Amount);Assert.Empty(await f.Db.CompanyCurrencyLocks.ToListAsync());
    }
}
