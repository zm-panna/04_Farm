using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

internal static class InventoryValuation
{
    // Called only inside the owning workflow's serializable transaction.
    public static async Task<InventoryCostBalance> Load(AppDbContext db, StockItem item, CancellationToken ct)
    {
        var balance = await db.InventoryCostBalances.SingleOrDefaultAsync(x => x.CompanyId == item.CompanyId && x.BranchId == item.BranchId && x.StockItemId == item.Id, ct);
        if (balance != null) return balance;
        // Never silently invent an opening balance for pre-migration stock: it must be reviewed and imported.
        if (item.QuantityOnHand != 0) throw new InvalidOperationException("This stock item needs an approved opening inventory valuation before transactions can be posted.");
        balance = new InventoryCostBalance { CompanyId = item.CompanyId, BranchId = item.BranchId, StockItemId = item.Id };
        db.InventoryCostBalances.Add(balance);
        return balance;
    }
    public static decimal Remove(StockItem item, InventoryCostBalance balance, decimal quantity)
    {
        if (quantity <= 0 || quantity > item.QuantityOnHand - item.ReservedQuantity) throw new InvalidOperationException("Insufficient available stock after customer order reservations.");
        var cost = quantity == item.QuantityOnHand ? balance.Value : BusinessInput.Round(balance.Value * (quantity / item.QuantityOnHand));
        item.QuantityOnHand = BusinessInput.Decimal(item.QuantityOnHand - quantity, "Stock balance");
        balance.Value = BusinessInput.Amount(balance.Value - cost, "Inventory value", false);
        item.UnitCost = item.QuantityOnHand == 0 ? item.UnitCost : BusinessInput.Round(balance.Value / item.QuantityOnHand, "Average unit cost");
        return cost;
    }
    public static void Add(StockItem item, InventoryCostBalance balance, decimal quantity, decimal value)
    {
        item.QuantityOnHand = BusinessInput.Decimal(item.QuantityOnHand + quantity, "Stock balance");
        balance.Value = BusinessInput.Amount(balance.Value + value, "Inventory value", false);
        item.UnitCost = BusinessInput.Round(balance.Value / item.QuantityOnHand, "Average unit cost");
    }
}
