using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class EcommerceWorkflowTests
{
    private static EcommerceService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static async Task<(EcommerceService service, EcommerceProduct product, StockItem stock)> Seed(SecurityTestScope f, decimal price = 10m, decimal taxRate = 15m, decimal stockQuantity = 10m)
    {
        await f.GrantAsync("*"); await f.GrantAsync("*", "approver-a"); await f.GrantAsync("*", "operator-b", 2);
        var tax = await CompanyBusinessPolicyTestData.SeedTaxAsync(f, rate: taxRate);
        foreach (var user in await f.Db.Users.Where(x => x.Id == "operator-a" || x.Id == "approver-a").ToListAsync()) user.EmailConfirmed = true;
        var party = new TradingParty { CompanyId = 1, BranchId = 11, Kind = "Customer", Code = "WEB-A", Name = "Customer A", Active = true };
        var other = new TradingParty { CompanyId = 1, BranchId = 11, Kind = "Customer", Code = "WEB-B", Name = "Customer B", Active = true };
        var stock = new StockItem { CompanyId = 1, BranchId = 11, SKU = "WEB-S", Name = "Milk", UnitOfMeasure = "L", QuantityOnHand = stockQuantity, UnitCost = 4m };
        f.Db.TradingParties.AddRange(party, other); f.Db.StockItems.Add(stock); await f.Db.SaveChangesAsync();
        f.Db.InventoryCostBalances.Add(new InventoryCostBalance { CompanyId = 1, BranchId = 11, StockItemId = stock.Id, Value = stockQuantity * 4m }); await f.Db.SaveChangesAsync();
        var service = Service(f);
        await service.LinkCustomer("operator-a", party.Id, true, default); await service.LinkCustomer("approver-a", other.Id, true, default);
        var product = await service.PublishProduct(null, stock.Id, "Fresh milk", "Branch collection", "Dairy", price, tax.Id, true, null, default);
        return (service, product, stock);
    }
    private static async Task<EcommerceOrder> Order(EcommerceService service, long product, decimal quantity = 2m)
    { await service.SetCartLine(product, quantity, null, default); var cart = (await service.Cart(default)).Cart!; return await service.Checkout(cart.RevisionToken, true, default); }
    private static async Task ApproveAndFulfill(EcommerceService service, EcommerceOrder order)
    { await service.Approve(order.Id, order.StateToken, "Physical stock and tax policy reviewed. Internal review note.", true, default); await service.Fulfill(order.Id, order.StateToken, true, default); }

    [Fact] public async Task Checkout_uses_server_tax_prices_reserves_and_is_idempotent()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, stock) = await Seed(f);
        await service.SetCartLine(product.Id, 2, null, default); var cart = (await service.Cart(default)).Cart!;
        Assert.Equal(23m, cart.Lines.Single().GrossAmount);
        await Assert.ThrowsAsync<ArgumentException>(() => service.Checkout(cart.RevisionToken, false, default));
        var order = await service.Checkout(cart.RevisionToken, true, default); var replay = await service.Checkout(cart.RevisionToken, true, default);
        Assert.Equal(order.Id, replay.Id); Assert.Equal(20m, order.NetAmount); Assert.Equal(3m, order.TaxAmount); Assert.Equal(23m, order.GrossAmount);
        Assert.Equal("PendingUnverified", order.PaymentStatus); Assert.Equal("Submitted", order.Status); Assert.Equal(2m, stock.ReservedQuantity); Assert.Equal(10m, stock.QuantityOnHand);
        Assert.Single(await f.Db.EcommerceOrders.ToListAsync()); Assert.Single(await f.Db.EcommerceStockReservations.ToListAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.FinancePayments.ToListAsync());
    }
    [Fact] public async Task Customer_cannot_read_cancel_or_checkout_another_customers_order_or_cart()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f); var order = await Order(service, product.Id);
        f.ActAs("approver-a");
        Assert.Empty((await service.Orders(false, 1, null, null, default)).Orders); Assert.Null((await service.Cart(default)).Cart);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.Order(order.Id, false, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.Cancel(order.Id, order.StateToken, "Forged cancellation", false, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Checkout(order.CheckoutToken, true, default));
    }
    [Fact] public async Task Tenant_and_branch_queries_hide_products_orders_and_profiles()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f); var order = await Order(service, product.Id);
        f.ActAs("operator-b", 2, 21); Assert.Empty((await service.Browse(1, null, null, default)).Products); Assert.Empty((await service.Orders(true, 1, null, null, default)).Orders);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.Order(order.Id, true, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Approve(order.Id, order.StateToken, "Cross tenant", true, default));
        f.ActAs(); f.Db.UserBranchAccess.Add(new UserBranchAccess { CompanyId = 1, BranchId = 12, UserId = "operator-a" }); await f.Db.SaveChangesAsync(); f.ActAs("operator-a", 1, 12);
        Assert.Empty((await service.Browse(1, null, null, default)).Products); await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetCartLine(product.Id, 1, null, default));
    }
    [Fact] public async Task Identity_link_requires_existing_verified_scoped_identity_and_cannot_reassign()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, _, _) = await Seed(f);
        var party = new TradingParty { CompanyId = 1, BranchId = 11, Kind = "Customer", Code = "THIRD", Name = "Third" }; f.Db.TradingParties.Add(party); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.LinkCustomer("operator-b", party.Id, true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LinkCustomer("not-an-identity@example.invalid", party.Id, true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.LinkCustomer("operator-a", party.Id, false, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LinkCustomer("operator-a", party.Id, true, default));
        Assert.Equal(2, await f.Db.EcommerceCustomers.CountAsync());
    }
    [Fact] public async Task Missing_company_or_tax_approval_blocks_publishing_and_unpublished_products_are_hidden()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); var service = Service(f);
        var stock = new StockItem { CompanyId = 1, BranchId = 11, SKU = "X", Name = "X" }; f.Db.StockItems.Add(stock); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishProduct(null, stock.Id, "X", null, null, 1m, 99, true, null, default));
        await CompanyBusinessPolicyTestData.SeedAsync(f);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishProduct(null, stock.Id, "X", null, null, 1m, 99, true, null, default));
        var tax = await CompanyBusinessPolicyTestData.SeedTaxAsync(f); var product = await service.PublishProduct(null, stock.Id, "X", null, null, 1m, tax.Id, false, null, default);
        Assert.Empty((await service.Browse(1, null, null, default)).Products); Assert.False(product.Published);
    }
    [Fact] public async Task Forged_cart_prices_and_stale_catalog_revision_cannot_be_checked_out()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f);
        await service.SetCartLine(product.Id, 2, null, default); var cart = (await service.Cart(default)).Cart!;
        var line = await f.Db.EcommerceCartLines.SingleAsync(); line.NetAmount = 1m; line.TaxAmount = 0m; line.GrossAmount = 1m; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Checkout(cart.RevisionToken, true, default));
        await service.SetCartLine(product.Id, 2, null, default); cart = (await service.Cart(default)).Cart!;
        await service.PublishProduct(product.Id, product.StockItemId, product.Title, product.Description, product.Category, 12m, product.TaxPolicyId, true, product.RevisionToken, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Checkout(cart.RevisionToken, true, default));
        Assert.Empty(await f.Db.EcommerceOrders.ToListAsync()); Assert.Equal(0m, (await f.Db.StockItems.SingleAsync()).ReservedQuantity);
        Assert.DoesNotContain(typeof(EcommerceController).GetMethod(nameof(EcommerceController.Checkout))!.GetParameters(), x => x.Name is "price" or "amount" or "paymentStatus" or "customerId");
    }
    [Fact] public async Task Stale_cart_edit_and_expired_checkout_are_rejected()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f);
        await service.SetCartLine(product.Id, 1m, null, default); var token = (await service.Cart(default)).Cart!.RevisionToken;
        await service.SetCartLine(product.Id, 2m, token, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetCartLine(product.Id, 0m, token, default));
        var cart = await f.Db.EcommerceCarts.SingleAsync(); cart.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(-1); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Checkout(cart.RevisionToken, true, default)); Assert.Empty(await f.Db.EcommerceOrders.ToListAsync());
    }
    [Fact] public async Task Reservation_protects_core_sales_and_cancellation_releases_exactly_once()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f); var order = await Order(service, product.Id, 9);
        var operations = new IntegratedOperations(f.Db, f.Tenant, f.Permissions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.Sell(product.StockItemId, 2m, 12m, "DIRECT-DENIED", default));
        f.Db.ChangeTracker.Clear(); var token = order.StateToken;
        await service.Cancel(order.Id, token, "Customer cancelled", false, default); await service.Cancel(order.Id, token, "Customer cancelled", false, default);
        Assert.Equal(0m, (await f.Db.StockItems.SingleAsync()).ReservedQuantity); Assert.Equal("Released", (await f.Db.EcommerceStockReservations.SingleAsync()).Status);
        await operations.Sell(product.StockItemId, 2m, 12m, "DIRECT-OK", default); Assert.Equal(8m, (await f.Db.StockItems.SingleAsync()).QuantityOnHand);
    }
    [Fact] public async Task Reserved_stock_blocks_feed_negative_adjustments_and_branch_transfers()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, stock) = await Seed(f); await Order(service, product.Id, 9m);
        var animal = new Animal { CompanyId=1, BranchId=11, Tag="WEB-FEED", Breed="Test" }; var destination = new StockItem { CompanyId=1, BranchId=12, SKU=stock.SKU, Name="Destination", UnitOfMeasure=stock.UnitOfMeasure };
        f.Db.Animals.Add(animal); f.Db.StockItems.Add(destination); f.Db.UserBranchAccess.Add(new UserBranchAccess { CompanyId=1, BranchId=12, UserId="operator-a" }); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FarmHealthFeedService(f.Db,f.Tenant,f.Permissions).ConsumeFeed(animal.Id,stock.Id,2m,"FEED-RESERVED",DateOnly.FromDateTime(DateTime.UtcNow),default));
        f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new FarmService(f.Db,f.Tenant,f.Permissions).MoveStock(stock.Id,-2m,"ADJUST-RESERVED",default));
        f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new StockTransferService(f.Db,f.Tenant,f.Permissions).Post(stock.Id,12,destination.Id,2m,"TRANSFER-RESERVED",null,default));
        var actual=await f.Db.StockItems.SingleAsync(x=>x.Id==stock.Id); Assert.Equal(10m,actual.QuantityOnHand); Assert.Equal(9m,actual.ReservedQuantity);
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.AnimalFeedRecords.ToListAsync()); Assert.Empty(await f.Db.StockTransfers.ToListAsync()); Assert.Empty(await f.Db.StockMovements.ToListAsync());
    }
    [Fact] public async Task Last_available_stock_cannot_be_reserved_twice_and_failure_leaves_no_partial_order()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f);
        await service.SetCartLine(product.Id, 7, null, default); var token = (await service.Cart(default)).Cart!.RevisionToken;
        f.ActAs("approver-a"); await Order(service, product.Id, 5); f.ActAs();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Checkout(token, true, default));
        Assert.Single(await f.Db.EcommerceOrders.ToListAsync()); Assert.Equal(5m, (await f.Db.StockItems.SingleAsync()).ReservedQuantity); Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    [Fact] public async Task Fulfillment_requires_review_confirmation_and_current_state_then_posts_balanced_tax_and_cost_once()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f); var order = await Order(service, product.Id); var oldToken = order.StateToken;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Fulfill(order.Id, order.StateToken, true, default));
        order = await f.Db.EcommerceOrders.SingleAsync(); await Assert.ThrowsAsync<ArgumentException>(() => service.Approve(order.Id, order.StateToken, "Review", false, default));
        await service.Approve(order.Id, order.StateToken, "Reviewed", true, default); var approved = await f.Db.EcommerceOrders.SingleAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Fulfill(order.Id, oldToken, true, default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.Fulfill(order.Id, approved.StateToken, false, default));
        await service.Fulfill(order.Id, approved.StateToken, true, default); await service.Fulfill(order.Id, approved.StateToken, true, default);
        var item = await f.Db.StockItems.SingleAsync(); Assert.Equal(8m, item.QuantityOnHand); Assert.Equal(0m, item.ReservedQuantity);
        var voucher = await f.Db.Vouchers.Include(x => x.Lines).SingleAsync(); Assert.Equal(voucher.Lines.Sum(x => x.Debit), voucher.Lines.Sum(x => x.Credit));
        Assert.Equal(23m, voucher.Lines.Single(x => x.AccountCode == "1100").Debit); Assert.Equal(20m, voucher.Lines.Single(x => x.AccountCode == "4100").Credit); Assert.Equal(3m, voucher.Lines.Single(x => x.AccountCode == "2200").Credit); Assert.Equal(8m, voucher.Lines.Single(x => x.AccountCode == "5100").Debit);
        Assert.Equal("Consumed", (await f.Db.EcommerceStockReservations.SingleAsync()).Status); Assert.Single(await f.Db.ProductSales.ToListAsync()); Assert.Single(await f.Db.FinanceDocuments.ToListAsync()); Assert.Equal("PendingUnverified", (await f.Db.EcommerceOrders.SingleAsync()).PaymentStatus);
    }
    [Fact] public async Task Fulfillment_late_database_failure_rolls_back_stock_GL_documents_and_reservation()
    {
        var failure = new EcommerceDocumentFailure(); await using var f = await SecurityTestScope.CreateAsync(failure); var (service, product, _) = await Seed(f); var order = await Order(service, product.Id);
        await service.Approve(order.Id, order.StateToken, "Reviewed", true, default); failure.Armed = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Fulfill(order.Id, order.StateToken, true, default)); failure.Armed = false;
        Assert.Equal(10m, (await f.Db.StockItems.SingleAsync()).QuantityOnHand); Assert.Equal(2m, (await f.Db.StockItems.SingleAsync()).ReservedQuantity); Assert.Equal("Approved", (await f.Db.EcommerceOrders.SingleAsync()).Status); Assert.Equal("Reserved", (await f.Db.EcommerceStockReservations.SingleAsync()).Status);
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.ProductSales.ToListAsync()); Assert.Empty(await f.Db.FinanceDocuments.ToListAsync()); Assert.Empty(await f.Db.StockMovements.ToListAsync()); Assert.Empty(await f.Db.EcommerceFulfillments.ToListAsync());
    }
    [Fact] public async Task Tax_aware_returns_reverse_exact_original_allocations_and_block_generic_return_bypass()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f, .01m, 33.333m); var order = await Order(service, product.Id, 3);
        await ApproveAndFulfill(service, order); var line = order.Lines.Single(); var fulfillment = await f.Db.EcommerceFulfillments.SingleAsync();
        var operations = new IntegratedOperations(f.Db, f.Tenant, f.Permissions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.Return(fulfillment.FinanceDocumentId, 1m, "BYPASS", default));
        var token = Guid.NewGuid().ToString("N"); var first = await service.Return(order.Id, line.Id, 1m, token, "Received", true, default); var replay = await service.Return(order.Id, line.Id, 1m, token, "Received", true, default); Assert.Equal(first.Id, replay.Id);
        await service.Return(order.Id, line.Id, 1m, Guid.NewGuid().ToString("N"), "Received", true, default); await service.Return(order.Id, line.Id, 1m, Guid.NewGuid().ToString("N"), "Received", true, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Return(order.Id, line.Id, 1m, Guid.NewGuid().ToString("N"), "Excess", true, default));
        var returns = await f.Db.EcommerceReturns.ToListAsync(); Assert.Equal(.03m, returns.Sum(x => x.NetAmount)); Assert.Equal(.01m, returns.Sum(x => x.TaxAmount)); Assert.Equal(12m, returns.Sum(x => x.CostAmount));
        Assert.Equal(10m, (await f.Db.StockItems.SingleAsync()).QuantityOnHand); Assert.Equal(40m, (await f.Db.InventoryCostBalances.SingleAsync()).Value);
        var ledger = await f.Db.VoucherLines.ToListAsync(); foreach(var account in ledger.GroupBy(x=>x.AccountCode)) Assert.Equal(account.Sum(x=>x.Debit), account.Sum(x=>x.Credit));
        Assert.Equal(.04m, (await f.Db.StockDocumentReturns.ToListAsync()).Sum(x => x.Amount));
    }
    [Fact] public async Task Stock_row_concurrency_rejects_a_stale_competing_reservation_writer()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service,product,stock)=await Seed(f);
        await using var competing=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.Db.Database.GetDbConnection()).Options);
        var stale=await competing.StockItems.SingleAsync(x=>x.Id==stock.Id);
        await Order(service,product.Id,8m); stale.ReservedQuantity=7m;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>competing.SaveChangesAsync());
        Assert.Equal(8m,(await f.Db.StockItems.AsNoTracking().SingleAsync()).ReservedQuantity);
        Assert.Equal(8m,await EcommerceStockReservations.ReservedQuantity(f.Db,1,11,stock.Id,default));
    }
    [Fact] public async Task Staff_order_permission_does_not_grant_financial_posting()
    {
        await using var f=await SecurityTestScope.CreateAsync(); var (service,product,_)=await Seed(f); var order=await Order(service,product.Id);
        f.Db.UserPermissions.RemoveRange(await f.Db.UserPermissions.Where(x=>x.UserId=="operator-a").ToListAsync()); await f.Db.SaveChangesAsync();
        await f.GrantAsync("Ecommerce.View"); await f.GrantAsync("Ecommerce.Fulfill");
        await service.Approve(order.Id,order.StateToken,"Reviewed without finance grant",true,default);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.Fulfill(order.Id,order.StateToken,true,default)); Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    [Fact] public async Task Money_rounds_once_at_currency_precision_and_journal_uses_local_accounting_date()
    {
        await using var f=await SecurityTestScope.CreateAsync(); var (service,product,_)=await Seed(f,.01m,10m);
        var order=await Order(service,product.Id,1.499m); Assert.Equal(.01m,order.NetAmount); Assert.Equal(0m,order.TaxAmount);
        await ApproveAndFulfill(service,order); var policy=await CompanyBusinessPolicyService.Current(f.Db,1);
        var date=CompanyBusinessPolicyService.BusinessDate(policy,DateTimeOffset.UtcNow); Assert.Equal(date,DateOnly.FromDateTime((await f.Db.Vouchers.SingleAsync()).Date.UtcDateTime));
    }
    [Theory]
    [InlineData("JPY", 0, "7", "4")]
    [InlineData("KWD", 3, "7.125", "3.634")]
    public async Task Explicit_zero_and_three_minor_unit_currencies_preserve_tax_returns_and_internal_cost(string currency, int minorUnits, string priceText, string expectedTaxText)
    {
        await using var f=await SecurityTestScope.CreateAsync();
        await CompanyBusinessPolicyTestData.SeedAsync(f,currency:currency,minorUnits:minorUnits);
        var price=decimal.Parse(priceText,System.Globalization.CultureInfo.InvariantCulture); var expectedTax=decimal.Parse(expectedTaxText,System.Globalization.CultureInfo.InvariantCulture);
        var (service,product,stock)=await Seed(f,price,17m);
        Assert.Equal(minorUnits,(await service.Browse(1,null,null,default)).CurrencyMinorUnits);
        Assert.Equal(minorUnits,(await service.Cart(default)).CurrencyMinorUnits);
        Assert.Equal(minorUnits,(await service.Manage(default)).CurrencyMinorUnits);
        stock.UnitCost=4.125m; (await f.Db.InventoryCostBalances.SingleAsync()).Value=41.250m; await f.Db.SaveChangesAsync();
        var order=await Order(service,product.Id,3m); Assert.Equal(currency,order.CurrencyCode); Assert.Equal(minorUnits,order.CurrencyMinorUnits); Assert.Equal(price*3m,order.NetAmount); Assert.Equal(expectedTax,order.TaxAmount);
        Assert.Equal(3m,stock.ReservedQuantity);
        await ApproveAndFulfill(service,order); var line=order.Lines.Single(); var fulfilled=await f.Db.EcommerceFulfillments.SingleAsync(); Assert.Equal(12.375m,fulfilled.CostAmount);
        Assert.Equal(0m,stock.ReservedQuantity); Assert.Equal(7m,stock.QuantityOnHand); Assert.Equal(28.875m,(await f.Db.InventoryCostBalances.SingleAsync()).Value);
        var quantities=new[]{1.001m,.999m,1m};
        foreach(var quantity in quantities) await service.Return(order.Id,line.Id,quantity,Guid.NewGuid().ToString("N"),"Synthetic currency precision return",true,default);
        var returns=await f.Db.EcommerceReturns.ToListAsync();
        Assert.Equal(order.NetAmount,returns.Sum(x=>x.NetAmount)); Assert.Equal(order.TaxAmount,returns.Sum(x=>x.TaxAmount)); Assert.Equal(12.375m,returns.Sum(x=>x.CostAmount));
        Assert.All(returns,x=>{Assert.Equal(x.NetAmount,decimal.Round(x.NetAmount,minorUnits));Assert.Equal(x.TaxAmount,decimal.Round(x.TaxAmount,minorUnits));});
        Assert.Contains(returns,x=>x.CostAmount==4.129m); Assert.Equal(10m,(await f.Db.StockItems.SingleAsync()).QuantityOnHand); Assert.Equal(41.250m,(await f.Db.InventoryCostBalances.SingleAsync()).Value);
        var ledger=await f.Db.VoucherLines.ToListAsync(); foreach(var account in ledger.GroupBy(x=>x.AccountCode)) Assert.Equal(account.Sum(x=>x.Debit),account.Sum(x=>x.Credit));
        Assert.All(await f.Db.Vouchers.Include(x=>x.Lines).ToListAsync(),v=>Assert.Equal(v.Lines.Sum(x=>x.Debit),v.Lines.Sum(x=>x.Credit)));
    }
    [Fact] public async Task Customer_payload_excludes_costs_and_internal_review_notes()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f); var order = await Order(service, product.Id); await ApproveAndFulfill(service, order);
        var customer = await service.Order(order.Id, false, default); Assert.Empty(customer.Fulfillments); Assert.DoesNotContain(customer.Order.Events, x=>x.Note.Contains("Internal review note"));
        var staff = await service.Order(order.Id, true, default); Assert.Single(staff.Fulfillments); Assert.Contains(staff.Order.Events,x=>x.Note.Contains("Internal review note"));
    }
    [Fact] public async Task Immutable_order_snapshots_and_cross_branch_foreign_keys_are_enforced()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f); var order = await Order(service, product.Id);
        order.Lines.Single().GrossAmount = 1m; await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear();
        var foreign = new EcommerceProduct { CompanyId=1, BranchId=12, StockItemId=product.StockItemId, Title="Forged", NetUnitPrice=1m, CurrencyCode=product.CurrencyCode, CompanyPolicyId=product.CompanyPolicyId, TaxPolicyId=product.TaxPolicyId };
        f.Db.EcommerceProducts.Add(foreign); await Assert.ThrowsAsync<DbUpdateException>(()=>f.Db.SaveChangesAsync());
    }
    [Fact] public async Task Browse_and_orders_are_filtered_and_server_paginated()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var (service, product, _) = await Seed(f);
        for(var n=0;n<22;n++) { var stock=new StockItem { CompanyId=1, BranchId=11, SKU="P"+n, Name="P"+n }; f.Db.StockItems.Add(stock); await f.Db.SaveChangesAsync(); await service.PublishProduct(null,stock.Id,"Product "+n,null,"Other",1m,product.TaxPolicyId,true,null,default); }
        var first=await service.Browse(1,null,null,default); var second=await service.Browse(2,null,null,default); Assert.Equal(23,first.Total); Assert.Equal(20,first.Products.Count); Assert.Equal(3,second.Products.Count); Assert.Empty(first.Products.Select(x=>x.Product.Id).Intersect(second.Products.Select(x=>x.Product.Id)));
        Assert.Single((await service.Browse(1,"Fresh","Dairy",default)).Products); Assert.Empty((await service.Browse(1,"Fresh","Other",default)).Products);
        Assert.Equal(1,(await service.Browse(-3,"Fresh",null,default)).Page);
    }
}
internal sealed class EcommerceDocumentFailure : SaveChangesInterceptor
{
    public bool Armed { get; set; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && eventData.Context!.ChangeTracker.Entries<FinanceDocument>().Any(x=>x.State==EntityState.Added)) throw new InvalidOperationException("Injected failure after voucher and sale insert.");
        return base.SavingChangesAsync(eventData,result,cancellationToken);
    }
}
