using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
namespace EnterpriseERP.Web;
/// <summary>Atomic, tenant-scoped operational documents with exact inventory carrying values.</summary>
public sealed class IntegratedOperations(AppDbContext db, TenantContext context, PermissionService permissions, TimeProvider? clock = null)
{
    public async Task<PurchaseReceipt> Receive(int itemId, decimal quantity, decimal price, string reference, CancellationToken ct, int? partyId = null)
    {
        await permissions.Demand("Purchase.Post", ct); var t = await context.GetAsync(ct);
        BusinessInput.Decimal(quantity, "Quantity", true); BusinessInput.Amount(price, "Price"); reference = BusinessInput.Reference(reference);
        var total = BusinessInput.Total(quantity, price);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        await ValidateParty(t.company, t.branch, partyId, "Supplier", ct);
        var receipt = await ReceiveCore(t, itemId, quantity, price, reference, total, partyId, businessDay, ct);
        await tx.CommitAsync(ct); return receipt;
    }
    private async Task<PurchaseReceipt> ReceiveCore((int company, int branch, string user) t, int itemId, decimal quantity, decimal price, string reference, decimal total, int? partyId, CompanyBusinessDate businessDay, CancellationToken ct)
    {
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, total, ct, atUtc: businessDay.UtcInstant);
        if (await db.PurchaseReceipts.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct)) throw new InvalidOperationException("Duplicate purchase reference.");
        var item = await db.StockItems.SingleAsync(x => x.Id == itemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        var balance = await InventoryValuation.Load(db, item, ct); InventoryValuation.Add(item, balance, quantity, total);
        var voucher = BusinessInput.Voucher(t.company, t.branch, "PUR-" + reference, ("1300", total, 0), ("2100", 0, total)); voucher.Date = businessDay.PostingDate; db.Vouchers.Add(voucher);
        db.StockMovements.Add(new StockMovement { CompanyId = t.company, BranchId = t.branch, StockItemId = itemId, QuantityDelta = quantity, Reference = voucher.Reference });
        await db.SaveChangesAsync(ct);
        var receipt = new PurchaseReceipt { CompanyId = t.company, BranchId = t.branch, StockItemId = itemId, Quantity = quantity, UnitPrice = price, Reference = reference, VoucherId = voucher.Id };
        db.PurchaseReceipts.Add(receipt); await db.SaveChangesAsync(ct);
        db.FinanceDocuments.Add(new FinanceDocument { CompanyId = t.company, BranchId = t.branch, Kind = "Purchase", PartyId = partyId, SourceId = receipt.Id, Reference = voucher.Reference, Amount = total, CostAmount = total, VoucherId = voucher.Id });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Purchase.Post", Reference = reference });
        await db.SaveChangesAsync(ct); return receipt;
    }
    public async Task<ProductSale> Sell(int itemId, decimal quantity, decimal price, string reference, CancellationToken ct, int? partyId = null)
    {
        await permissions.Demand("Sales.Post", ct); var t = await context.GetAsync(ct);
        BusinessInput.Decimal(quantity, "Quantity", true); BusinessInput.Amount(price, "Price"); reference = BusinessInput.Reference(reference);
        var revenue = BusinessInput.Total(quantity, price);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, revenue, ct, atUtc: businessDay.UtcInstant);
        await ValidateParty(t.company, t.branch, partyId, "Customer", ct);
        if (await db.ProductSales.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct)) throw new InvalidOperationException("Duplicate sale reference.");
        var item = await db.StockItems.SingleAsync(x => x.Id == itemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        var originalUnitCost = item.UnitCost;
        var balance = await InventoryValuation.Load(db, item, ct); var cogs = InventoryValuation.Remove(item, balance, quantity);
        var voucher = BusinessInput.Voucher(t.company, t.branch, "SAL-" + reference, ("1100", revenue, 0), ("4100", 0, revenue), ("5100", cogs, 0), ("1300", 0, cogs)); voucher.Date = businessDay.PostingDate; db.Vouchers.Add(voucher);
        db.StockMovements.Add(new StockMovement { CompanyId = t.company, BranchId = t.branch, StockItemId = itemId, QuantityDelta = -quantity, Reference = voucher.Reference });
        await db.SaveChangesAsync(ct);
        var sale = new ProductSale { CompanyId = t.company, BranchId = t.branch, StockItemId = itemId, Reference = reference, Quantity = quantity, UnitPrice = price, UnitCost = originalUnitCost, VoucherId = voucher.Id };
        db.ProductSales.Add(sale); await db.SaveChangesAsync(ct);
        db.FinanceDocuments.Add(new FinanceDocument { CompanyId = t.company, BranchId = t.branch, Kind = "Sale", PartyId = partyId, SourceId = sale.Id, Reference = voucher.Reference, Amount = revenue, CostAmount = cogs, VoucherId = voucher.Id });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Sales.Post", Reference = reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return sale;
    }
    public async Task<StockDocumentReturn> Return(long documentId, decimal quantity, string reference, CancellationToken ct)
    {
        var t = await context.GetAsync(ct);
        var document = await db.FinanceDocuments.AsNoTracking().SingleAsync(x => x.Id == documentId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (await db.EcommerceFulfillments.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FinanceDocumentId == document.Id, ct))
            throw new InvalidOperationException("Use the ecommerce return workflow to preserve the original tax allocation.");
        if (document.Kind is not ("Purchase" or "Sale")) throw new ArgumentException("Only stock purchase and sales documents support returns.");
        await permissions.Demand(document.Kind == "Purchase" ? "Purchase.Post" : "Sales.Post", ct);
        BusinessInput.Decimal(quantity, "Return quantity", true); reference = BusinessInput.Reference(reference);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        if (await db.StockDocumentReturns.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct)) throw new InvalidOperationException("Duplicate return reference.");
        int itemId; decimal originalQuantity;
        if (document.Kind == "Purchase") { var source = await db.PurchaseReceipts.SingleAsync(x => x.Id == document.SourceId && x.CompanyId == t.company && x.BranchId == t.branch, ct); itemId = source.StockItemId; originalQuantity = source.Quantity; }
        else { var source = await db.ProductSales.SingleAsync(x => x.Id == document.SourceId && x.CompanyId == t.company && x.BranchId == t.branch, ct); itemId = source.StockItemId; originalQuantity = source.Quantity; }
        var previous = await db.StockDocumentReturns.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FinanceDocumentId == document.Id).ToListAsync(ct);
        var cumulativeQuantity = previous.Sum(x => x.Quantity) + quantity;
        if (cumulativeQuantity > originalQuantity) throw new InvalidOperationException("Return quantity exceeds the original unreturned quantity.");
        // Cumulative allocation ensures repeated partial returns exactly reverse the original document, including its rounding remainder.
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, document.Amount, ct, atUtc: businessDay.UtcInstant);
        var currencyPolicy = await CompanyBusinessPolicyService.Configured(db, t.company, ct, atUtc: businessDay.UtcInstant);
        var cumulativeReturnedAmount = currencyPolicy == null ? BusinessInput.Round(document.Amount * (cumulativeQuantity / originalQuantity))
            : CompanyBusinessPolicyService.RoundMoney(document.Amount * (cumulativeQuantity / originalQuantity), currencyPolicy.CurrencyMinorUnits);
        if (cumulativeQuantity < originalQuantity && cumulativeReturnedAmount >= document.Amount)
            throw new ArgumentException("This partial return would consume the document's remaining monetary value. Return the full remaining quantity together.");
        var amount = cumulativeReturnedAmount - previous.Sum(x => x.Amount);
        BusinessInput.Amount(amount, "Return amount");
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, amount, ct, atUtc: businessDay.UtcInstant);
        var item = await db.StockItems.SingleAsync(x => x.Id == itemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        var balance = await InventoryValuation.Load(db, item, ct);
        decimal cost; Voucher voucher;
        if (document.Kind == "Sale")
        {
            cost = BusinessInput.Round(document.CostAmount * (cumulativeQuantity / originalQuantity)) - previous.Sum(x => x.CostAmount);
            InventoryValuation.Add(item, balance, quantity, cost);
            voucher = BusinessInput.Voucher(t.company, t.branch, "RET-" + reference, ("4100", amount, 0), ("1100", 0, amount), ("1300", cost, 0), ("5100", 0, cost));
        }
        else
        {
            cost = InventoryValuation.Remove(item, balance, quantity);
            voucher = BusinessInput.Voucher(t.company, t.branch, "RET-" + reference, ("2100", amount, 0), ("1300", 0, cost), ("6100", Math.Max(0, cost - amount), Math.Max(0, amount - cost)));
        }
        voucher.Date = businessDay.PostingDate;
        db.Vouchers.Add(voucher);
        db.StockMovements.Add(new StockMovement { CompanyId = t.company, BranchId = t.branch, StockItemId = itemId, QuantityDelta = document.Kind == "Sale" ? quantity : -quantity, Reference = voucher.Reference });
        await db.SaveChangesAsync(ct);
        var record = new StockDocumentReturn { CompanyId = t.company, BranchId = t.branch, FinanceDocumentId = documentId, Reference = reference, Quantity = quantity, Amount = amount, CostAmount = cost, VoucherId = voucher.Id };
        db.StockDocumentReturns.Add(record);
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = document.Kind + ".Return", Reference = reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return record;
    }
    private async Task ValidateParty(int company, int branch, int? partyId, string kind, CancellationToken ct)
    {
        if (partyId.HasValue && !await db.TradingParties.AnyAsync(x => x.Id == partyId && x.CompanyId == company && x.BranchId == branch && x.Kind == kind && x.Active, ct))
            throw new ArgumentException("Choose an active " + kind.ToLowerInvariant() + " in the selected branch.");
    }
    public async Task<TradingParty> CreateParty(string kind, string code, string name, string? email, string? phone, string? address, CancellationToken ct)
    {
        await permissions.Demand("Parties.Manage", ct); var t = await context.GetAsync(ct);
        if (kind is not ("Supplier" or "Customer")) throw new ArgumentException("Choose Supplier or Customer.");
        code = BusinessInput.Text(code, "Party code", 30).ToUpperInvariant(); name = BusinessInput.Text(name, "Party name", 160);
        email = BusinessInput.Text(email, "Email", 200, false); phone = BusinessInput.Text(phone, "Phone", 40, false); address = BusinessInput.Text(address, "Address", 500, false);
        if (email.Length > 0 && !System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new ArgumentException("Enter a valid email address.");
        if (await db.TradingParties.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Code == code, ct)) throw new InvalidOperationException("Party code already exists in this branch.");
        var party = new TradingParty { CompanyId = t.company, BranchId = t.branch, Kind = kind, Code = code, Name = name, Email = email, Phone = phone, Address = address };
        db.TradingParties.Add(party); db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Party.Create", Reference = code });
        await db.SaveChangesAsync(ct); return party;
    }
    public async Task SetPartyActive(int partyId, bool active, CancellationToken ct)
    {
        await permissions.Demand("Parties.Manage", ct); var t = await context.GetAsync(ct);
        var party = await db.TradingParties.SingleAsync(x => x.Id == partyId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        party.Active = active; db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = active ? "Party.Activate" : "Party.Deactivate", Reference = party.Code });
        await db.SaveChangesAsync(ct);
    }
    public async Task<PurchaseOrder> CreateOrder(int supplierId, int itemId, decimal quantity, decimal price, string reference, CancellationToken ct)
    {
        await permissions.Demand("Purchase.Post", ct); var t = await context.GetAsync(ct);
        BusinessInput.Decimal(quantity, "Order quantity", true); BusinessInput.Amount(price, "Agreed unit price"); reference = BusinessInput.Reference(reference);
        var amount = BusinessInput.Total(quantity, price, "Order total");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, amount, ct, atUtc: businessDay.UtcInstant);
        await ValidateParty(t.company, t.branch, supplierId, "Supplier", ct);
        if (!await db.StockItems.AnyAsync(x => x.Id == itemId && x.CompanyId == t.company && x.BranchId == t.branch, ct)) throw new ArgumentException("Select a stock item in this branch.");
        if (await db.PurchaseOrders.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct)) throw new InvalidOperationException("Duplicate order reference.");
        var order = new PurchaseOrder { CompanyId = t.company, BranchId = t.branch, SupplierId = supplierId, StockItemId = itemId, Quantity = quantity, UnitPrice = price, Amount = amount, Reference = reference, CreatedBy = t.user };
        db.PurchaseOrders.Add(order); db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "PurchaseOrder.Create", Reference = reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return order;
    }
    public async Task ApproveOrder(long orderId, CancellationToken ct)
    {
        await permissions.Demand("Purchase.Approve", ct); var t = await context.GetAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.PurchaseOrders.SingleAsync(x => x.Id == orderId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (order.Status != "Draft") throw new InvalidOperationException("Only a draft order can be approved.");
        await ValidateParty(t.company, t.branch, order.SupplierId, "Supplier", ct);
        order.Status = "Approved"; order.ApprovedBy = t.user; order.ApprovedUtc = DateTimeOffset.UtcNow;
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "PurchaseOrder.Approve", Reference = order.Reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task CancelOrder(long orderId, CancellationToken ct)
    {
        await permissions.Demand("Purchase.Approve", ct); var t = await context.GetAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await db.PurchaseOrders.SingleAsync(x => x.Id == orderId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (order.Status is "Received" or "Cancelled") throw new InvalidOperationException("Completed or cancelled orders cannot be cancelled again.");
        order.Status = "Cancelled"; db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "PurchaseOrder.Cancel", Reference = order.Reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<PurchaseReceipt> ReceiveOrder(long orderId, decimal quantity, string reference, CancellationToken ct)
    {
        await permissions.Demand("Purchase.Post", ct); var t = await context.GetAsync(ct);
        BusinessInput.Decimal(quantity, "Receipt quantity", true); reference = BusinessInput.Reference(reference);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.ValidatePendingScan("ReceiveOrder", ct);
        var order = await db.PurchaseOrders.SingleAsync(x => x.Id == orderId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (order.Status is not ("Approved" or "PartiallyReceived")) throw new InvalidOperationException("Only an approved, open order can be received.");
        if (quantity > order.Quantity - order.ReceivedQuantity) throw new InvalidOperationException("Receipt exceeds the order's remaining quantity.");
        await ValidateParty(t.company, t.branch, order.SupplierId, "Supplier", ct);
        var newQuantity = order.ReceivedQuantity + quantity;
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, order.Amount, ct, atUtc: businessDay.UtcInstant);
        var currencyPolicy = await CompanyBusinessPolicyService.Configured(db, t.company, ct, atUtc: businessDay.UtcInstant);
        var cumulativeAmount = newQuantity == order.Quantity ? order.Amount : currencyPolicy == null ? BusinessInput.Round(newQuantity * order.UnitPrice)
            : CompanyBusinessPolicyService.RoundMoney(newQuantity * order.UnitPrice, currencyPolicy.CurrencyMinorUnits);
        if (newQuantity < order.Quantity && cumulativeAmount >= order.Amount)
            throw new ArgumentException("This partial receipt would consume the order's remaining monetary value. Receive the full remaining quantity together.");
        var amount = BusinessInput.Amount(cumulativeAmount - order.ReceivedAmount, "Receipt total");
        var receipt = await ReceiveCore(t, order.StockItemId, quantity, order.UnitPrice, reference, amount, order.SupplierId, businessDay, ct);
        order.ReceivedQuantity = newQuantity; order.ReceivedAmount = cumulativeAmount;
        order.Status = newQuantity == order.Quantity ? "Received" : "PartiallyReceived";
        db.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt { CompanyId = t.company, BranchId = t.branch, PurchaseOrderId = order.Id, PurchaseReceiptId = receipt.Id, Quantity = quantity, Amount = amount });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "PurchaseOrder.Receive", Reference = order.Reference + "/" + reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return receipt;
    }
    public async Task SetReorderPolicy(int itemId, decimal reorderPoint, decimal targetQuantity, CancellationToken ct)
    {
        await permissions.Demand("Inventory.Edit", ct); var t = await context.GetAsync(ct);
        BusinessInput.Decimal(reorderPoint, "Reorder point"); BusinessInput.Decimal(targetQuantity, "Target quantity");
        if (targetQuantity < reorderPoint) throw new ArgumentException("Target quantity must be at least the reorder point.");
        if (!await db.StockItems.AnyAsync(x => x.Id == itemId && x.CompanyId == t.company && x.BranchId == t.branch, ct)) throw new ArgumentException("Select a stock item in this branch.");
        var policy = await db.StockReorderPolicies.SingleOrDefaultAsync(x => x.StockItemId == itemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (policy == null) { policy = new StockReorderPolicy { CompanyId = t.company, BranchId = t.branch, StockItemId = itemId }; db.StockReorderPolicies.Add(policy); }
        policy.ReorderPoint = reorderPoint; policy.TargetQuantity = targetQuantity;
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Inventory.ReorderPolicy", Reference = itemId.ToString() });
        await db.SaveChangesAsync(ct);
    }

}
