using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

public static partial class EnterpriseDemoSeeder
{
    /// <summary>Fictional, opt-in extension only; parent owns target checks and the single enclosing transaction.</summary>
    internal static async Task SeedEcommerce(AppDbContext db, Company company, Branch branch, AppUser maker, AppUser reviewer,
        CompanyBusinessPolicy policy, TaxPolicy tax, int number, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction == null || company.Slug?.StartsWith("demo-farm-", StringComparison.Ordinal) != true || number is < 1 or > 20)
            throw new InvalidOperationException("Ecommerce demo extension requires the guarded, transactional fictional demo seed.");
        if (!maker.EmailConfirmed || policy.CompanyId != company.Id || tax.CompanyId != company.Id || policy.Status != "Approved" || tax.Status != "Approved")
            throw new InvalidOperationException("Synthetic verified identity and approved demonstration policies are required.");
        var item = await db.StockItems.Where(x => x.CompanyId == company.Id && x.BranchId == branch.Id).OrderBy(x => x.Id).FirstAsync(ct);
        var party = await db.TradingParties.Where(x => x.CompanyId == company.Id && x.BranchId == branch.Id && x.Kind == "Customer" && x.Active).OrderBy(x => x.Id).FirstAsync(ct);
        var customer = new EcommerceCustomer { CompanyId = company.Id, BranchId = branch.Id, UserId = maker.Id, TradingPartyId = party.Id, LinkedBy = reviewer.Id, LinkedUtc = now };
        var product = new EcommerceProduct { CompanyId = company.Id, BranchId = branch.Id, StockItemId = item.Id, CompanyPolicyId = policy.Id, TaxPolicyId = tax.Id,
            CurrencyCode = policy.CurrencyCode, Title = $"DEMO storefront feed {number}", Description = "Fictional demonstration product; no real sale, delivery or payment.", Category = "DEMO feed", NetUnitPrice = 70 + number, Published = true };
        db.EcommerceCustomers.Add(customer); db.EcommerceProducts.Add(product); await db.SaveChangesAsync(ct);
        var net = CompanyBusinessPolicyService.RoundMoney(product.NetUnitPrice * 2m, policy.CurrencyMinorUnits);
        var taxAmount = TaxPolicyService.Calculate(net, tax, policy.CurrencyMinorUnits);
        var order = new EcommerceOrder { CompanyId = company.Id, BranchId = branch.Id, EcommerceCustomerId = customer.Id, CompanyPolicyId = policy.Id,
            Reference = $"DEMO-WEB-{number}", CheckoutToken = Guid.NewGuid().ToString("N"), CustomerName = party.Name, Status = "Fulfilled",
            CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits, CountryCode = policy.CountryCode, RegionCode = policy.RegionCode,
            NetAmount = net, TaxAmount = taxAmount, GrossAmount = net + taxAmount, CreatedBy = maker.Id, CreatedUtc = now };
        var line = new EcommerceOrderLine { CompanyId = company.Id, BranchId = branch.Id, EcommerceProductId = product.Id, StockItemId = item.Id, TaxPolicyId = tax.Id,
            Title = product.Title, UnitOfMeasure = item.UnitOfMeasure, Quantity = 2m, NetUnitPrice = product.NetUnitPrice, NetAmount = net, TaxAmount = taxAmount,
            GrossAmount = net + taxAmount, TaxRatePercent = tax.RatePercent, TaxTreatment = tax.TaxTreatment, TaxLiabilityAccount = tax.TaxLiabilityAccount };
        order.Lines.Add(line); db.EcommerceOrders.Add(order); await db.SaveChangesAsync(ct);
        db.EcommerceStockReservations.Add(new EcommerceStockReservation { CompanyId = company.Id, BranchId = branch.Id, StockItemId = item.Id, EcommerceOrderId = order.Id, EcommerceOrderLineId = line.Id, Quantity = 2m, Status = "Consumed" });
        var initialUnitCost = item.UnitCost; var balance = await InventoryValuation.Load(db, item, ct); var cost = InventoryValuation.Remove(item, balance, 2m);
        var date = new DateTimeOffset(CompanyBusinessPolicyService.BusinessDate(policy, now).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var saleVoucher = BusinessInput.Voucher(company.Id, branch.Id, "SAL-" + order.Reference, ("1100", line.GrossAmount, 0), ("4100", 0, net), (tax.TaxLiabilityAccount, 0, taxAmount), ("5100", cost, 0), ("1300", 0, cost)); saleVoucher.Date = date;
        db.Vouchers.Add(saleVoucher); db.StockMovements.Add(new StockMovement { CompanyId = company.Id, BranchId = branch.Id, StockItemId = item.Id, QuantityDelta = -2m, Reference = saleVoucher.Reference, CreatedUtc = now }); await db.SaveChangesAsync(ct);
        var sale = new ProductSale { CompanyId = company.Id, BranchId = branch.Id, StockItemId = item.Id, Reference = order.Reference, Quantity = 2m, UnitPrice = product.NetUnitPrice, UnitCost = initialUnitCost, VoucherId = saleVoucher.Id, CreatedUtc = now };
        db.ProductSales.Add(sale); await db.SaveChangesAsync(ct);
        var finance = new FinanceDocument { CompanyId = company.Id, BranchId = branch.Id, Kind = "Sale", PartyId = party.Id, SourceId = sale.Id, Reference = saleVoucher.Reference, Amount = line.GrossAmount, CostAmount = cost, VoucherId = saleVoucher.Id, CreatedUtc = now };
        db.FinanceDocuments.Add(finance); await db.SaveChangesAsync(ct);
        db.EcommerceFulfillments.Add(new EcommerceFulfillment { CompanyId = company.Id, BranchId = branch.Id, EcommerceOrderLineId = line.Id, ProductSaleId = sale.Id, FinanceDocumentId = finance.Id, CostAmount = cost });
        var returnNet = CompanyBusinessPolicyService.RoundMoney(net / 2m, policy.CurrencyMinorUnits); var returnTax = CompanyBusinessPolicyService.RoundMoney(taxAmount / 2m, policy.CurrencyMinorUnits); var returnCost = BusinessInput.Round(cost / 2m);
        InventoryValuation.Add(item, balance, 1m, returnCost);
        var returnVoucher = BusinessInput.Voucher(company.Id, branch.Id, $"RET-DEMO-WEB-{number}", ("4100", returnNet, 0), (tax.TaxLiabilityAccount, returnTax, 0), ("1100", 0, returnNet + returnTax), ("1300", returnCost, 0), ("5100", 0, returnCost)); returnVoucher.Date = date;
        db.Vouchers.Add(returnVoucher); db.StockMovements.Add(new StockMovement { CompanyId = company.Id, BranchId = branch.Id, StockItemId = item.Id, QuantityDelta = 1m, Reference = returnVoucher.Reference, CreatedUtc = now }); await db.SaveChangesAsync(ct);
        var returned = new StockDocumentReturn { CompanyId = company.Id, BranchId = branch.Id, FinanceDocumentId = finance.Id, Reference = $"DEMO-WEB-RETURN-{number}", Quantity = 1m, Amount = returnNet + returnTax, CostAmount = returnCost, VoucherId = returnVoucher.Id, CreatedUtc = now };
        db.StockDocumentReturns.Add(returned); await db.SaveChangesAsync(ct);
        db.EcommerceReturns.Add(new EcommerceReturn { CompanyId = company.Id, BranchId = branch.Id, EcommerceOrderLineId = line.Id, StockDocumentReturnId = returned.Id, RequestToken = Guid.NewGuid().ToString("N"), Quantity = 1m, NetAmount = returnNet, TaxAmount = returnTax, CostAmount = returnCost, Reason = "Fictional partial return, no cash refund or payment." });
        foreach (var action in new[] { "Submitted", "Approved", "Fulfilled", "Returned" })
            db.EcommerceOrderEvents.Add(new EcommerceOrderEvent { CompanyId = company.Id, BranchId = branch.Id, EcommerceOrderId = order.Id, Action = action, ActorId = action == "Submitted" ? maker.Id : reviewer.Id, Note = $"DEMO: fictional {action.ToLowerInvariant()} event; no real payment, delivery or communication.", CreatedUtc = now });
        var cartNet = product.NetUnitPrice; var cartTax = TaxPolicyService.Calculate(cartNet, tax, policy.CurrencyMinorUnits);
        db.EcommerceCarts.Add(new EcommerceCart { CompanyId = company.Id, BranchId = branch.Id, EcommerceCustomerId = customer.Id, ExpiresUtc = now.AddMinutes(30), Lines = new()
            { new EcommerceCartLine { CompanyId = company.Id, BranchId = branch.Id, EcommerceProductId = product.Id, CompanyPolicyId = policy.Id, TaxPolicyId = tax.Id, ProductRevisionToken = product.RevisionToken, Title = product.Title, CurrencyCode = policy.CurrencyCode, Quantity = 1m, NetUnitPrice = product.NetUnitPrice, NetAmount = cartNet, TaxAmount = cartTax, GrossAmount = cartNet + cartTax } } });
        db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = reviewer.Id, Action = "Ecommerce.DemoFixture", Reference = order.Reference + ";fictional;pending-unverified-payment", CreatedUtc = now });
        await db.SaveChangesAsync(ct);
    }
}

