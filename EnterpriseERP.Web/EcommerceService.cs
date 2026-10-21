using System.Data;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

public sealed record EcommerceBrowseRow(EcommerceProduct Product, string Unit, decimal Available);
public sealed record EcommerceBrowsePage(IReadOnlyList<EcommerceBrowseRow> Products, int Page, int Pages, int Total, string Search, string Category, bool Staff)
{
    public int? CurrencyMinorUnits { get; init; }
    public string Money(decimal amount) => EcommerceMoneyPresentation.Format(amount, CurrencyMinorUnits);
}
public sealed record EcommerceCartPage(EcommerceCart? Cart, string CustomerName)
{
    public int? CurrencyMinorUnits { get; init; }
    public string Money(decimal amount) => EcommerceMoneyPresentation.Format(amount, CurrencyMinorUnits);
}
public sealed record EcommerceOrdersPage(IReadOnlyList<EcommerceOrder> Orders, int Page, int Pages, int Total, string Search, string Status, bool Staff);
public sealed record EcommerceOrderPage(EcommerceOrder Order, bool Staff, IReadOnlyList<EcommerceFulfillment> Fulfillments, IReadOnlyList<EcommerceReturn> Returns)
{
    public string Money(decimal amount) => EcommerceMoneyPresentation.Format(amount, Order.CurrencyMinorUnits);
}
public sealed record EcommerceManagePage(IReadOnlyList<EcommerceProduct> Products, IReadOnlyList<StockItem> Stock, IReadOnlyList<TradingParty> Customers, IReadOnlyList<EcommerceIdentityOption> Identities, IReadOnlyList<EcommerceCustomer> Links, IReadOnlyList<TaxPolicy> TaxPolicies)
{
    public int? CurrencyMinorUnits { get; init; }
    public string MoneyStep => EcommerceMoneyPresentation.Step(CurrencyMinorUnits);
    public string Money(decimal amount) => EcommerceMoneyPresentation.Format(amount, CurrencyMinorUnits);
}
public sealed record EcommerceIdentityOption(string Id, string Email);

public static class EcommerceMoneyPresentation
{
    // Never hide an incompatible historical fraction while displaying a locked denomination.
    public static string Format(decimal amount, int? minorUnits) => amount.ToString("N" + (decimal.Round(amount, minorUnits ?? 3) == amount ? minorUnits ?? 3 : 3));
    public static string Step(int? minorUnits) => minorUnits switch { 0 => "1", 1 => "0.1", 2 => "0.01", _ => "0.001" };
}

/// <summary>The stock-row reservation balance is authoritative. Reservation rows retain immutable allocation evidence.</summary>
public static class EcommerceStockReservations
{
    public static async Task<decimal> ReservedQuantity(AppDbContext db, int companyId, int branchId, int stockItemId, CancellationToken ct)
        => (await db.EcommerceStockReservations.Where(x => x.CompanyId == companyId && x.BranchId == branchId && x.StockItemId == stockItemId && x.Status == "Reserved").Select(x => x.Quantity).ToListAsync(ct)).Sum();
    public static Task DemandAvailable(AppDbContext db, StockItem item, decimal quantity, CancellationToken ct, long? consumingOrderId = null)
    {
        BusinessInput.Decimal(quantity, "Requested quantity", positive: true);
        if (consumingOrderId.HasValue) throw new InvalidOperationException("Release the owning reservation atomically before consuming stock.");
        if (quantity > item.QuantityOnHand - item.ReservedQuantity) throw new InvalidOperationException("Insufficient unreserved stock.");
        return Task.CompletedTask;
    }
}

public sealed class EcommerceService(AppDbContext db, TenantContext tenant, PermissionService permissions)
{
    private const int PageSize = 20;
    private Task<int?> LockedCurrencyMinorUnits(int company, CancellationToken ct)
        => db.CompanyCurrencyLocks.AsNoTracking().Where(x => x.CompanyId == company).Select(x => (int?)x.CurrencyMinorUnits).SingleOrDefaultAsync(ct);
    private static string Token() => Guid.NewGuid().ToString("N");
    private static string ValidToken(string token) => Guid.TryParseExact(token, "N", out _) ? token : throw new ArgumentException("Invalid request token. Reload this page.");
    private static decimal Money(decimal value, int units) => BusinessInput.Amount(decimal.Round(value, units, MidpointRounding.AwayFromZero), "Money", false);
    private static decimal NetTotal(decimal quantity, decimal price, int units)
    { try { return BusinessInput.Amount(Money(checked(quantity * price), units), "Net amount"); } catch (OverflowException) { throw new ArgumentException("Line total exceeds the supported range."); } }
    private static DateTimeOffset AccountingDate(CompanyBusinessPolicy policy) => new(CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    private async Task<T> Atomic<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { var result = await work(); await tx.CommitAsync(ct); return result; }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    private async Task<EcommerceCustomer> OwnCustomer((int company, int branch, string user) t, CancellationToken ct)
    {
        var profile = await db.EcommerceCustomers.SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.UserId == t.user && x.Active, ct)
            ?? throw new InvalidOperationException("Ask authorized staff to link your verified account to your customer record in this branch.");
        if (!await db.Users.AnyAsync(x => x.Id == t.user && x.EmailConfirmed, ct)
            || !await db.TradingParties.AnyAsync(x => x.Id == profile.TradingPartyId && x.CompanyId == t.company && x.BranchId == t.branch && x.Active && x.Kind == "Customer", ct))
            throw new UnauthorizedAccessException("An active customer record and verified account are required.");
        return profile;
    }
    public async Task<bool> CanManage(CancellationToken ct)
    {
        var t = await tenant.GetAsync(ct);
        return await db.UserPermissions.AsNoTracking().AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == "Ecommerce.Manage" || x.Permission == "*"), ct);
    }
    public async Task<bool> CanFulfill(CancellationToken ct)
    {
        var t = await tenant.GetAsync(ct);
        return await db.UserPermissions.AsNoTracking().AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == "Ecommerce.Fulfill" || x.Permission == "*"), ct);
    }
    public async Task<EcommerceBrowsePage> Browse(int page, string? search, string? category, CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.View", ct); var t = await tenant.GetAsync(ct);
        search = BusinessInput.Text(search, "Search", 100, false); category = BusinessInput.Text(category, "Category", 100, false);
        var query = db.EcommerceProducts.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Published);
        if (search.Length > 0) query = query.Where(x => x.Title.Contains(search) || x.Description.Contains(search));
        if (category.Length > 0) query = query.Where(x => x.Category == category);
        var total = await query.CountAsync(ct); var pages = Math.Max(1, (int)Math.Ceiling(total / (decimal)PageSize)); page = Math.Clamp(page, 1, pages);
        var products = await query.OrderBy(x => x.Title).ThenBy(x => x.Id).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        var ids = products.Select(x => x.StockItemId).ToList();
        var stock = await db.StockItems.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        return new(products.Select(x => new EcommerceBrowseRow(x, stock[x.StockItemId].UnitOfMeasure, stock[x.StockItemId].QuantityOnHand - stock[x.StockItemId].ReservedQuantity)).ToList(), page, pages, total, search, category, await CanFulfill(ct)) { CurrencyMinorUnits = await LockedCurrencyMinorUnits(t.company, ct) };
    }
    public async Task<EcommerceManagePage> Manage(CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.Manage", ct); var t = await tenant.GetAsync(ct);
        var users = await (from access in db.UserBranchAccess.AsNoTracking() join user in db.Users.AsNoTracking() on access.UserId equals user.Id
                           where access.CompanyId == t.company && access.BranchId == t.branch && user.EmailConfirmed
                           orderby user.Email select new EcommerceIdentityOption(user.Id, user.Email ?? user.UserName ?? user.Id)).ToListAsync(ct);
        return new(await db.EcommerceProducts.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderBy(x => x.Title).ToListAsync(ct),
            await db.StockItems.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderBy(x => x.SKU).ToListAsync(ct),
            await db.TradingParties.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == "Customer" && x.Active).OrderBy(x => x.Name).ToListAsync(ct), users,
            await db.EcommerceCustomers.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).ToListAsync(ct),
            await db.Set<TaxPolicy>().AsNoTracking().Where(x => x.CompanyId == t.company).OrderByDescending(x => x.Id).ToListAsync(ct)) { CurrencyMinorUnits = await LockedCurrencyMinorUnits(t.company, ct) };
    }
    public async Task<EcommerceCustomer> LinkCustomer(string existingUserId, int partyId, bool confirmIdentityVerified, CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.Manage", ct); var t = await tenant.GetAsync(ct);
        if (!confirmIdentityVerified) throw new ArgumentException("Confirm the verified identity belongs to this customer before linking.");
        return await Atomic(async () =>
        {
            if (!await db.Users.AnyAsync(x => x.Id == existingUserId && x.EmailConfirmed, ct)
                || !await db.UserCompanyAccess.AnyAsync(x => x.CompanyId == t.company && x.UserId == existingUserId, ct)
                || !await db.UserBranchAccess.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.UserId == existingUserId, ct))
                throw new ArgumentException("Choose an existing verified identity already authorized for this branch.");
            if (!await db.TradingParties.AnyAsync(x => x.Id == partyId && x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == "Customer" && x.Active, ct))
                throw new ArgumentException("Choose an active customer in this branch.");
            if (await db.EcommerceCustomers.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && (x.UserId == existingUserId || x.TradingPartyId == partyId), ct))
                throw new InvalidOperationException("That account or customer is already linked. Identity links cannot be reassigned.");
            var profile = new EcommerceCustomer { CompanyId = t.company, BranchId = t.branch, UserId = existingUserId, TradingPartyId = partyId, LinkedBy = t.user };
            db.EcommerceCustomers.Add(profile); Audit(t, "Ecommerce.CustomerLinked", $"party:{partyId};user:{existingUserId}"); await db.SaveChangesAsync(ct); return profile;
        }, ct);
    }
    public async Task<EcommerceProduct> PublishProduct(long? id, int stockItemId, string title, string? description, string? category, decimal netUnitPrice, long taxPolicyId, bool published, string? revisionToken, CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.Manage", ct); var t = await tenant.GetAsync(ct);
        title = BusinessInput.Text(title, "Title", 160); description = BusinessInput.Text(description, "Description", 2000, false); category = BusinessInput.Text(category, "Category", 100, false); BusinessInput.Amount(netUnitPrice, "Net unit price");
        return await Atomic(async () =>
        {
            var policy = await CompanyBusinessPolicyService.Current(db, t.company, ct);
            await TaxPolicyService.Require(db, t.company, taxPolicyId, CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow), ct);
            if (Money(netUnitPrice, policy.CurrencyMinorUnits) != netUnitPrice) throw new ArgumentException("Price has more precision than the configured currency permits.");
            if (!await db.StockItems.AnyAsync(x => x.Id == stockItemId && x.CompanyId == t.company && x.BranchId == t.branch, ct)) throw new ArgumentException("Choose stock in this branch.");
            var product = id.HasValue ? await db.EcommerceProducts.SingleAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct) : new EcommerceProduct { CompanyId = t.company, BranchId = t.branch, StockItemId = stockItemId };
            if (id.HasValue && (product.RevisionToken != revisionToken || product.StockItemId != stockItemId)) throw new InvalidOperationException("Product changed, or stock identity was changed. Reload the catalog.");
            product.Title = title; product.Description = description; product.Category = category; product.NetUnitPrice = netUnitPrice; product.Published = published;
            product.CompanyPolicyId = policy.Id; product.CurrencyCode = policy.CurrencyCode; product.TaxPolicyId = taxPolicyId; product.RevisionToken = Token();
            if (!id.HasValue) db.EcommerceProducts.Add(product);
            Audit(t, "Ecommerce.ProductPublished", $"stock:{stockItemId};published:{published};tax:{taxPolicyId};price:{netUnitPrice}"); await db.SaveChangesAsync(ct); return product;
        }, ct);
    }
    public async Task<EcommerceCartPage> Cart(CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.View", ct); var t = await tenant.GetAsync(ct); var customer = await OwnCustomer(t, ct);
        var party = await db.TradingParties.AsNoTracking().SingleAsync(x => x.Id == customer.TradingPartyId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        return new(await db.EcommerceCarts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.EcommerceCustomerId == customer.Id, ct), party.Name) { CurrencyMinorUnits = await LockedCurrencyMinorUnits(t.company, ct) };
    }
    public async Task SetCartLine(long productId, decimal quantity, string? expectedCartToken, CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.View", ct); var t = await tenant.GetAsync(ct); BusinessInput.Decimal(quantity, "Quantity");
        await Atomic(async () =>
        {
            var customer = await OwnCustomer(t, ct);
            var cart = await db.EcommerceCarts.Include(x => x.Lines).SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.EcommerceCustomerId == customer.Id, ct);
            if (expectedCartToken != null && cart?.RevisionToken != expectedCartToken) throw new InvalidOperationException("Your cart changed. Reload before editing.");
            if (cart == null) { cart = new EcommerceCart { CompanyId = t.company, BranchId = t.branch, EcommerceCustomerId = customer.Id }; db.EcommerceCarts.Add(cart); }
            var line = cart.Lines.SingleOrDefault(x => x.EcommerceProductId == productId);
            if (quantity == 0) { if (line != null) db.EcommerceCartLines.Remove(line); }
            else
            {
                if (line == null && cart.Lines.Count >= 50) throw new InvalidOperationException("A cart supports at most 50 products.");
                var policy = await CompanyBusinessPolicyService.Current(db, t.company, ct);
                var product = await db.EcommerceProducts.SingleOrDefaultAsync(x => x.Id == productId && x.CompanyId == t.company && x.BranchId == t.branch && x.Published, ct) ?? throw new InvalidOperationException("This product is no longer published in the selected branch.");
                var tax = await TaxPolicyService.Require(db, t.company, product.TaxPolicyId, CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow), ct);
                if (product.CurrencyCode != policy.CurrencyCode) throw new InvalidOperationException("Product currency needs staff review.");
                var item = await db.StockItems.SingleAsync(x => x.Id == product.StockItemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
                await EcommerceStockReservations.DemandAvailable(db, item, quantity, ct);
                if (line == null) { line = new EcommerceCartLine { CompanyId = t.company, BranchId = t.branch, EcommerceProductId = product.Id }; cart.Lines.Add(line); }
                line.CompanyPolicyId = policy.Id; line.TaxPolicyId = tax.Id; line.ProductRevisionToken = product.RevisionToken; line.Title = product.Title; line.CurrencyCode = policy.CurrencyCode; line.Quantity = quantity; line.NetUnitPrice = product.NetUnitPrice;
                line.NetAmount = NetTotal(quantity, product.NetUnitPrice, policy.CurrencyMinorUnits);
                BusinessInput.Amount(line.NetAmount, "Line net amount"); line.TaxAmount = TaxPolicyService.Calculate(line.NetAmount, tax, policy.CurrencyMinorUnits); line.GrossAmount = BusinessInput.Amount(line.NetAmount + line.TaxAmount);
            }
            cart.RevisionToken = Token(); cart.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30); await db.SaveChangesAsync(ct); return true;
        }, ct);
    }
    public async Task<EcommerceOrder> Checkout(string checkoutToken, bool confirmOrder, CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.View", ct); var t = await tenant.GetAsync(ct); ValidToken(checkoutToken);
        if (!confirmOrder) throw new ArgumentException("Confirm the displayed total and branch collection terms before placing the order.");
        return await Atomic(async () =>
        {
            var customer = await OwnCustomer(t, ct);
            var prior = await db.EcommerceOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.EcommerceCustomerId == customer.Id && x.CheckoutToken == checkoutToken, ct);
            if (prior != null) return prior;
            var cart = await db.EcommerceCarts.Include(x => x.Lines).SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.EcommerceCustomerId == customer.Id, ct)
                ?? throw new InvalidOperationException("The cart is empty.");
            if (cart.RevisionToken != checkoutToken || cart.ExpiresUtc <= DateTimeOffset.UtcNow) throw new InvalidOperationException("Cart quote changed or expired. Refresh quantities to review current prices and tax.");
            if (cart.Lines.Count is < 1 or > 50) throw new InvalidOperationException("Choose between 1 and 50 products.");
            var policy = await CompanyBusinessPolicyService.Current(db, t.company, ct);
            var party = await db.TradingParties.SingleAsync(x => x.Id == customer.TradingPartyId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
            var order = new EcommerceOrder { CompanyId = t.company, BranchId = t.branch, EcommerceCustomerId = customer.Id, CompanyPolicyId = policy.Id, CustomerName = party.Name, Reference = "WEB-" + Token()[..16].ToUpperInvariant(), CheckoutToken = checkoutToken, CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits, CountryCode = policy.CountryCode, RegionCode = policy.RegionCode, CreatedBy = t.user };
            foreach (var cartLine in cart.Lines.OrderBy(x => x.EcommerceProductId))
            {
                var product = await db.EcommerceProducts.SingleOrDefaultAsync(x => x.Id == cartLine.EcommerceProductId && x.CompanyId == t.company && x.BranchId == t.branch && x.Published, ct) ?? throw new InvalidOperationException("A cart product is no longer published. Remove it before checking out.");
                var tax = await TaxPolicyService.Require(db, t.company, product.TaxPolicyId, CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow), ct);
                if (product.RevisionToken != cartLine.ProductRevisionToken || product.NetUnitPrice != cartLine.NetUnitPrice || cartLine.CompanyPolicyId != policy.Id || cartLine.TaxPolicyId != tax.Id || cartLine.CurrencyCode != policy.CurrencyCode)
                    throw new InvalidOperationException("A product price, tax or company policy changed. Refresh the cart before confirming.");
                var item = await db.StockItems.SingleAsync(x => x.Id == product.StockItemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
                await EcommerceStockReservations.DemandAvailable(db, item, cartLine.Quantity, ct);
                var net = NetTotal(cartLine.Quantity, product.NetUnitPrice, policy.CurrencyMinorUnits); var taxAmount = TaxPolicyService.Calculate(net, tax, policy.CurrencyMinorUnits);
                if (cartLine.NetAmount != net || cartLine.TaxAmount != taxAmount || cartLine.GrossAmount != net + taxAmount) throw new InvalidOperationException("The cart quote no longer matches approved server pricing.");
                item.ReservedQuantity = BusinessInput.Decimal(item.ReservedQuantity + cartLine.Quantity, "Reserved quantity");
                order.Lines.Add(new EcommerceOrderLine { CompanyId = t.company, BranchId = t.branch, EcommerceProductId = product.Id, StockItemId = item.Id, TaxPolicyId = tax.Id, Title = product.Title, UnitOfMeasure = item.UnitOfMeasure, Quantity = cartLine.Quantity, NetUnitPrice = product.NetUnitPrice, NetAmount = net, TaxAmount = taxAmount, GrossAmount = net + taxAmount, TaxTreatment = tax.TaxTreatment, TaxRatePercent = tax.RatePercent, TaxLiabilityAccount = tax.TaxLiabilityAccount });
            }
            order.NetAmount = BusinessInput.Amount(order.Lines.Sum(x => x.NetAmount)); order.TaxAmount = BusinessInput.Amount(order.Lines.Sum(x => x.TaxAmount), "Tax", false); order.GrossAmount = BusinessInput.Amount(order.Lines.Sum(x => x.GrossAmount));
            db.EcommerceOrders.Add(order); await db.SaveChangesAsync(ct);
            foreach (var line in order.Lines) db.EcommerceStockReservations.Add(new EcommerceStockReservation { CompanyId = t.company, BranchId = t.branch, StockItemId = line.StockItemId, EcommerceOrderId = order.Id, EcommerceOrderLineId = line.Id, Quantity = line.Quantity });
            AddEvent(t, order, "Submitted", "Customer explicitly confirmed server prices, configured tax, branch collection and pending manual payment verification.");
            db.EcommerceCartLines.RemoveRange(cart.Lines); cart.RevisionToken = Token(); cart.ExpiresUtc = DateTimeOffset.UtcNow;
            Audit(t, "Ecommerce.Checkout", order.Reference); await db.SaveChangesAsync(ct); return order;
        }, ct);
    }
    public async Task<EcommerceOrdersPage> Orders(bool staff, int page, string? search, string? status, CancellationToken ct)
    {
        await permissions.Demand(staff ? "Ecommerce.Fulfill" : "Ecommerce.View", ct); var t = await tenant.GetAsync(ct);
        search = BusinessInput.Text(search, "Search", 100, false); status = BusinessInput.Text(status, "Status", 30, false);
        var query = db.EcommerceOrders.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
        if (!staff) { var customer = await OwnCustomer(t, ct); query = query.Where(x => x.EcommerceCustomerId == customer.Id); }
        if (search.Length > 0) query = query.Where(x => x.Reference.Contains(search));
        if (status is "Submitted" or "Approved" or "Fulfilled" or "Cancelled") query = query.Where(x => x.Status == status); else status = "";
        var total = await query.CountAsync(ct); var pages = Math.Max(1, (int)Math.Ceiling(total / (decimal)PageSize)); page = Math.Clamp(page, 1, pages);
        return new(await query.OrderByDescending(x => x.Id).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct), page, pages, total, search, status, staff);
    }
    public async Task<EcommerceOrderPage> Order(long id, bool staff, CancellationToken ct)
    {
        await permissions.Demand(staff ? "Ecommerce.Fulfill" : "Ecommerce.View", ct); var t = await tenant.GetAsync(ct);
        var query = db.EcommerceOrders.AsNoTracking().Include(x => x.Lines).Include(x => x.Events).Where(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch);
        if (!staff) { var customer = await OwnCustomer(t, ct); query = query.Where(x => x.EcommerceCustomerId == customer.Id); }
        var order = await query.SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Order not found.");
        var ids = order.Lines.Select(x => x.Id).ToList();
        var returns = await db.EcommerceReturns.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && ids.Contains(x.EcommerceOrderLineId)).ToListAsync(ct);
        if (!staff)
        {
            order.Events = order.Events.Select(x => new EcommerceOrderEvent { Id = x.Id, Action = x.Action, CreatedUtc = x.CreatedUtc,
                Note = x.Action switch { "Submitted" => "Order received and stock reserved.", "Approved" => "Staff approved this order for branch collection.", "Fulfilled" => "Goods released for branch collection. Payment is handled separately.", "Cancelled" => "Order cancelled and reserved stock released.", "Returned" => "A received return and linked credit were recorded.", _ => "Order updated." } }).ToList();
            returns = returns.Select(x => new EcommerceReturn { Id = x.Id, EcommerceOrderLineId = x.EcommerceOrderLineId, Quantity = x.Quantity, NetAmount = x.NetAmount, TaxAmount = x.TaxAmount }).ToList();
        }
        return new(order, staff, staff ? await db.EcommerceFulfillments.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && ids.Contains(x.EcommerceOrderLineId)).ToListAsync(ct) : new List<EcommerceFulfillment>(), returns);
    }
    public async Task Approve(long orderId, string stateToken, string reviewNote, bool confirmReview, CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.Fulfill", ct); var t = await tenant.GetAsync(ct); ValidToken(stateToken); reviewNote = BusinessInput.Text(reviewNote, "Review note", 1000);
        if (!confirmReview) throw new ArgumentException("Confirm review of the customer, configured tax and collection arrangements.");
        await Atomic(async () =>
        {
            var order = await ScopedOrder(t, orderId, ct); RequireState(order, stateToken, "Submitted");
            await ValidatePostingPolicy(t, order, ct);
            order.Status = "Approved"; order.StateToken = Token(); AddEvent(t, order, "Approved", reviewNote); Audit(t, "Ecommerce.Approve", order.Reference); await db.SaveChangesAsync(ct); return true;
        }, ct);
    }
    public async Task Cancel(long orderId, string stateToken, string reason, bool staff, CancellationToken ct)
    {
        await permissions.Demand(staff ? "Ecommerce.Fulfill" : "Ecommerce.View", ct); var t = await tenant.GetAsync(ct); ValidToken(stateToken); reason = BusinessInput.Text(reason, "Cancellation reason", 1000);
        await Atomic(async () =>
        {
            var order = await ScopedOrder(t, orderId, ct);
            if (!staff) { var customer = await OwnCustomer(t, ct); if (order.EcommerceCustomerId != customer.Id) throw new KeyNotFoundException("Order not found."); }
            if (order.Status == "Cancelled") return true;
            if (order.StateToken != stateToken || order.Status is not ("Submitted" or "Approved")) throw new InvalidOperationException("The order changed or was fulfilled. Reload it; fulfilled stock requires a return.");
            var reservations = await db.EcommerceStockReservations.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.EcommerceOrderId == order.Id && x.Status == "Reserved").OrderBy(x => x.StockItemId).ToListAsync(ct);
            if (reservations.Count != order.Lines.Count) throw new InvalidOperationException("Reservation integrity check failed.");
            foreach (var reservation in reservations)
            {
                var item = await db.StockItems.SingleAsync(x => x.Id == reservation.StockItemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
                if (item.ReservedQuantity < reservation.Quantity) throw new InvalidOperationException("Reserved stock balance requires reconciliation.");
                item.ReservedQuantity -= reservation.Quantity; reservation.Status = "Released";
            }
            order.Status = "Cancelled"; order.StateToken = Token(); AddEvent(t, order, "Cancelled", reason); Audit(t, "Ecommerce.Cancel", order.Reference); await db.SaveChangesAsync(ct); return true;
        }, ct);
    }
    public async Task Fulfill(long orderId, string stateToken, bool confirmFulfillment, CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.Fulfill", ct); await permissions.Demand("Sales.Post", ct); var t = await tenant.GetAsync(ct); ValidToken(stateToken);
        if (!confirmFulfillment) throw new ArgumentException("Confirm goods are released for branch collection and the receivable will be posted; payment remains unverified.");
        await Atomic(async () =>
        {
            var order = await ScopedOrder(t, orderId, ct); if (order.Status == "Fulfilled") return true;
            RequireState(order, stateToken, "Approved"); await ValidatePostingPolicy(t, order, ct);
            var businessPolicy = await CompanyBusinessPolicyService.Current(db, t.company, ct);
            var customer = await db.EcommerceCustomers.SingleAsync(x => x.Id == order.EcommerceCustomerId && x.CompanyId == t.company && x.BranchId == t.branch && x.Active, ct);
            if (!await db.TradingParties.AnyAsync(x => x.Id == customer.TradingPartyId && x.CompanyId == t.company && x.BranchId == t.branch && x.Active && x.Kind == "Customer", ct)) throw new InvalidOperationException("The customer account is no longer active.");
            foreach (var line in order.Lines.OrderBy(x => x.StockItemId))
            {
                var reservation = await db.EcommerceStockReservations.SingleAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.EcommerceOrderId == order.Id && x.EcommerceOrderLineId == line.Id && x.StockItemId == line.StockItemId && x.Status == "Reserved", ct);
                var item = await db.StockItems.SingleAsync(x => x.Id == line.StockItemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
                if (reservation.Quantity != line.Quantity || item.ReservedQuantity < line.Quantity) throw new InvalidOperationException("Reservation integrity check failed.");
                item.ReservedQuantity -= line.Quantity; reservation.Status = "Consumed";
                var originalCost = item.UnitCost; var balance = await InventoryValuation.Load(db, item, ct); var cost = InventoryValuation.Remove(item, balance, line.Quantity);
                var reference = order.Reference + "-" + line.Id;
                var voucher = BusinessInput.Voucher(t.company, t.branch, "SAL-" + reference, ("1100", line.GrossAmount, 0), ("4100", 0, line.NetAmount), (line.TaxLiabilityAccount, 0, line.TaxAmount), ("5100", cost, 0), ("1300", 0, cost));
                voucher.Date = AccountingDate(businessPolicy);
                db.Vouchers.Add(voucher); db.StockMovements.Add(new StockMovement { CompanyId = t.company, BranchId = t.branch, StockItemId = item.Id, QuantityDelta = -line.Quantity, Reference = voucher.Reference }); await db.SaveChangesAsync(ct);
                var sale = new ProductSale { CompanyId = t.company, BranchId = t.branch, StockItemId = item.Id, Quantity = line.Quantity, UnitPrice = line.NetUnitPrice, UnitCost = originalCost, Reference = reference, VoucherId = voucher.Id };
                db.ProductSales.Add(sale); await db.SaveChangesAsync(ct);
                var document = new FinanceDocument { CompanyId = t.company, BranchId = t.branch, Kind = "Sale", PartyId = customer.TradingPartyId, SourceId = sale.Id, Reference = voucher.Reference, Amount = line.GrossAmount, CostAmount = cost, VoucherId = voucher.Id };
                db.FinanceDocuments.Add(document); await db.SaveChangesAsync(ct);
                db.EcommerceFulfillments.Add(new EcommerceFulfillment { CompanyId = t.company, BranchId = t.branch, EcommerceOrderLineId = line.Id, ProductSaleId = sale.Id, FinanceDocumentId = document.Id, CostAmount = cost });
            }
            order.Status = "Fulfilled"; order.StateToken = Token(); AddEvent(t, order, "Fulfilled", "Reserved goods released for branch collection. Sales, configured tax, receivable and inventory cost posted; no payment was charged or verified."); Audit(t, "Ecommerce.Fulfill", order.Reference); await db.SaveChangesAsync(ct); return true;
        }, ct);
    }
    public async Task<EcommerceReturn> Return(long orderId, long lineId, decimal quantity, string requestToken, string reason, bool confirmReceived, CancellationToken ct)
    {
        await permissions.Demand("Ecommerce.Fulfill", ct); await permissions.Demand("Sales.Post", ct); var t = await tenant.GetAsync(ct); ValidToken(requestToken); BusinessInput.Decimal(quantity, "Return quantity", true); reason = BusinessInput.Text(reason, "Return reason", 1000);
        if (!confirmReceived) throw new ArgumentException("Confirm the returned goods were physically received and are suitable to return to stock.");
        return await Atomic(async () =>
        {
            var order = await ScopedOrder(t, orderId, ct); if (order.Status != "Fulfilled") throw new InvalidOperationException("Only fulfilled orders can be returned.");
            var businessPolicy = await CompanyBusinessPolicyService.Current(db, t.company, ct);
            if (businessPolicy.CurrencyCode != order.CurrencyCode || businessPolicy.CurrencyMinorUnits != order.CurrencyMinorUnits) throw new InvalidOperationException("Return currency does not match the approved ledger currency.");
            var line = order.Lines.SingleOrDefault(x => x.Id == lineId) ?? throw new KeyNotFoundException("Order line not found.");
            var priorRequest = await db.EcommerceReturns.SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.RequestToken == requestToken, ct);
            if (priorRequest != null) { if (priorRequest.EcommerceOrderLineId != lineId || priorRequest.Quantity != quantity || priorRequest.Reason != reason) throw new InvalidOperationException("Return token was already used for a different request."); return priorRequest; }
            var fulfillment = await db.EcommerceFulfillments.SingleAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.EcommerceOrderLineId == lineId, ct);
            var previous = await db.EcommerceReturns.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.EcommerceOrderLineId == lineId).ToListAsync(ct);
            var cumulative = previous.Sum(x => x.Quantity) + quantity; if (cumulative > line.Quantity) throw new InvalidOperationException("Return exceeds the remaining fulfilled quantity.");
            decimal Portion(decimal amount) => cumulative == line.Quantity ? amount : Money(amount * cumulative / line.Quantity, order.CurrencyMinorUnits);
            var net = Portion(line.NetAmount) - previous.Sum(x => x.NetAmount); var tax = Portion(line.TaxAmount) - previous.Sum(x => x.TaxAmount);
            var cost = (cumulative == line.Quantity ? fulfillment.CostAmount : BusinessInput.Round(fulfillment.CostAmount * cumulative / line.Quantity)) - previous.Sum(x => x.CostAmount);
            if (net + tax <= 0 || cumulative < line.Quantity && Portion(line.NetAmount) + Portion(line.TaxAmount) >= line.GrossAmount) throw new ArgumentException("Return this remaining low-value quantity together to preserve exact money allocation.");
            var item = await db.StockItems.SingleAsync(x => x.Id == line.StockItemId && x.CompanyId == t.company && x.BranchId == t.branch, ct); var balance = await InventoryValuation.Load(db, item, ct); InventoryValuation.Add(item, balance, quantity, cost);
            var reference = "WEBRET-" + requestToken;
            var voucher = BusinessInput.Voucher(t.company, t.branch, "RET-" + reference, ("4100", net, 0), (line.TaxLiabilityAccount, tax, 0), ("1100", 0, net + tax), ("1300", cost, 0), ("5100", 0, cost));
            voucher.Date = AccountingDate(businessPolicy);
            db.Vouchers.Add(voucher); db.StockMovements.Add(new StockMovement { CompanyId = t.company, BranchId = t.branch, StockItemId = item.Id, QuantityDelta = quantity, Reference = voucher.Reference }); await db.SaveChangesAsync(ct);
            var stockReturn = new StockDocumentReturn { CompanyId = t.company, BranchId = t.branch, FinanceDocumentId = fulfillment.FinanceDocumentId, Reference = reference, Quantity = quantity, Amount = net + tax, CostAmount = cost, VoucherId = voucher.Id };
            db.StockDocumentReturns.Add(stockReturn); await db.SaveChangesAsync(ct);
            var record = new EcommerceReturn { CompanyId = t.company, BranchId = t.branch, EcommerceOrderLineId = lineId, StockDocumentReturnId = stockReturn.Id, RequestToken = requestToken, Quantity = quantity, NetAmount = net, TaxAmount = tax, CostAmount = cost, Reason = reason };
            db.EcommerceReturns.Add(record); AddEvent(t, order, "Returned", $"Line {lineId}; quantity {quantity}; return {reference}. Credit recorded; the full reason is retained with the linked return. Cash refund requires separate verified finance handling."); Audit(t, "Ecommerce.Return", reference); await db.SaveChangesAsync(ct); return record;
        }, ct);
    }
    private Task<EcommerceOrder> ScopedOrder((int company, int branch, string user) t, long id, CancellationToken ct)
        => db.EcommerceOrders.Include(x => x.Lines).SingleAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct);
    private static void RequireState(EcommerceOrder order, string token, string status)
    { if (order.StateToken != token || order.Status != status) throw new InvalidOperationException("The order changed or is not in the required state. Reload it before acting."); }
    private async Task ValidatePostingPolicy((int company, int branch, string user) t, EcommerceOrder order, CancellationToken ct)
    {
        var policy = await CompanyBusinessPolicyService.Current(db, t.company, ct);
        if (policy.CurrencyCode != order.CurrencyCode || policy.CurrencyMinorUnits != order.CurrencyMinorUnits || policy.CountryCode != order.CountryCode || policy.RegionCode != order.RegionCode)
            throw new InvalidOperationException("Company jurisdiction or currency changed. This order requires an approved correction before posting.");
        foreach (var line in order.Lines)
        {
            var tax = await TaxPolicyService.Require(db, t.company, line.TaxPolicyId, CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow), ct);
            if (tax.RatePercent != line.TaxRatePercent || tax.TaxTreatment != line.TaxTreatment || tax.TaxLiabilityAccount != line.TaxLiabilityAccount || TaxPolicyService.Calculate(line.NetAmount, tax, order.CurrencyMinorUnits) != line.TaxAmount)
                throw new InvalidOperationException("Tax approval no longer matches the accepted order snapshot.");
        }
    }
    private void AddEvent((int company, int branch, string user) t, EcommerceOrder order, string action, string note)
        => db.EcommerceOrderEvents.Add(new EcommerceOrderEvent { CompanyId = t.company, BranchId = t.branch, EcommerceOrderId = order.Id, Action = action, Note = note, ActorId = t.user });
    private void Audit((int company, int branch, string user) t, string action, string reference)
        => db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = action, Reference = $"branch:{t.branch};{reference}" });
}
