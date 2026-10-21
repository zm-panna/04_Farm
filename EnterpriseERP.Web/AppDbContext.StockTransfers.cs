using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed partial class AppDbContext
{
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();

    private void ConfigureStockTransfers(ModelBuilder b)
    {
        string C(string column) => Database.IsSqlite() ? $"CAST([{column}] AS REAL)" : $"[{column}]";
        b.Entity<StockItem>().Property(x => x.UnitOfMeasure).HasMaxLength(30).HasDefaultValue("");
        var transfer = b.Entity<StockTransfer>();
        transfer.Property(x => x.UnitOfMeasure).HasMaxLength(30);
        transfer.HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
        transfer.HasIndex(x => new { x.CompanyId, x.SourceBranchId, x.CreatedUtc });
        transfer.HasIndex(x => new { x.CompanyId, x.DestinationBranchId, x.CreatedUtc });
        transfer.HasIndex(x => x.SourceMovementId).IsUnique();
        transfer.HasIndex(x => x.DestinationMovementId).IsUnique();
        transfer.HasIndex(x => x.SourceVoucherId).IsUnique().HasFilter("[SourceVoucherId] IS NOT NULL");
        transfer.HasIndex(x => x.DestinationVoucherId).IsUnique().HasFilter("[DestinationVoucherId] IS NOT NULL");
        transfer.Property(x => x.Reference).HasMaxLength(80);
        transfer.Property(x => x.Note).HasMaxLength(500);
        transfer.Property(x => x.CreatedBy).HasMaxLength(128);
        transfer.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.CompanyId, x.SourceBranchId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<Branch>().WithMany().HasForeignKey(x => new { x.CompanyId, x.DestinationBranchId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.CompanyId, x.SourceBranchId, x.SourceStockItemId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<StockItem>().WithMany().HasForeignKey(x => new { x.CompanyId, x.DestinationBranchId, x.DestinationStockItemId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<StockMovement>().WithMany().HasForeignKey(x => new { x.CompanyId, x.SourceBranchId, x.SourceMovementId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<StockMovement>().WithMany().HasForeignKey(x => new { x.CompanyId, x.DestinationBranchId, x.DestinationMovementId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<Voucher>().WithMany().HasForeignKey(x => new { x.CompanyId, x.SourceBranchId, x.SourceVoucherId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transfer.HasOne<Voucher>().WithMany().HasForeignKey(x => new { x.CompanyId, x.DestinationBranchId, x.DestinationVoucherId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        transfer.ToTable(t =>
        {
            t.HasCheckConstraint("CK_StockTransfer_DifferentBranches", "[SourceBranchId] <> [DestinationBranchId] AND [SourceStockItemId] <> [DestinationStockItemId] AND [SourceMovementId] <> [DestinationMovementId]");
            t.HasCheckConstraint("CK_StockTransfer_PositiveQuantity", $"{C("Quantity")} > 0 AND {C("CostAmount")} >= 0");
            t.HasCheckConstraint("CK_StockTransfer_NonnegativeBalances", string.Join(" AND ", new[] { "SourceQuantityBefore", "SourceQuantityAfter", "DestinationQuantityBefore", "DestinationQuantityAfter", "SourceValueBefore", "SourceValueAfter", "DestinationValueBefore", "DestinationValueAfter" }.Select(x => $"{C(x)} >= 0")));
            t.HasCheckConstraint("CK_StockTransfer_PairedVouchers", $"({C("CostAmount")} = 0 AND [SourceVoucherId] IS NULL AND [DestinationVoucherId] IS NULL) OR ({C("CostAmount")} > 0 AND [SourceVoucherId] IS NOT NULL AND [DestinationVoucherId] IS NOT NULL AND [SourceVoucherId] <> [DestinationVoucherId])");
        });
    }

    private async Task ValidateStockTransferWritesAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<StockMovement>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<StockTransfer>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Posted stock movements and transfers are immutable. Correct a transfer with a new opposite-direction transfer and reference.");
        var products = ChangeTracker.Entries<StockItem>().Where(x => x.State is EntityState.Added or EntityState.Modified).ToArray();
        foreach (var product in products)
        {
            var value = product.Entity;
            if (product.State == EntityState.Modified && !string.IsNullOrEmpty(product.OriginalValues.GetValue<string>(nameof(StockItem.UnitOfMeasure)))
                && product.OriginalValues.GetValue<string>(nameof(StockItem.UnitOfMeasure)) != value.UnitOfMeasure)
                throw new InvalidOperationException("An established stock unit cannot be changed. Create a new SKU for a different physical unit.");
            if (string.IsNullOrEmpty(value.UnitOfMeasure)) continue; // Legacy/imported stock stays explicitly unknown until reviewed.
            if (value.UnitOfMeasure != StockUnitPolicy.Normalize(value.UnitOfMeasure)) throw new InvalidOperationException("Stock units must be normalized before saving.");
            var sku = value.SKU.Trim().ToUpperInvariant();
            var changingIds = products.Select(x => x.Entity.Id).ToArray();
            if (products.Any(x => x.Entity != value && x.Entity.CompanyId == value.CompanyId && x.Entity.SKU.Trim().ToUpperInvariant() == sku
                    && !string.IsNullOrEmpty(x.Entity.UnitOfMeasure) && x.Entity.UnitOfMeasure != value.UnitOfMeasure)
                || await StockItems.AsNoTracking().AnyAsync(x => x.CompanyId == value.CompanyId && x.SKU.Trim().ToUpper() == sku && !changingIds.Contains(x.Id)
                    && x.UnitOfMeasure != "" && x.UnitOfMeasure != value.UnitOfMeasure, ct))
                throw new InvalidOperationException("The same SKU must use one stock unit across this company.");
        }
        foreach (var entry in ChangeTracker.Entries<StockTransfer>().Where(x => x.State == EntityState.Added))
        {
            var item = entry.Entity;
            if (item.Reference != StockTransferService.NormalizeReference(item.Reference)) throw new InvalidOperationException("Transfer references must be normalized before posting.");
            if (item.UnitOfMeasure != StockUnitPolicy.Normalize(item.UnitOfMeasure)) throw new InvalidOperationException("Transfer requires a reviewed, normalized stock unit.");
            var productIds = new[] { item.SourceStockItemId, item.DestinationStockItemId };
            if (await StockItems.AsNoTracking().CountAsync(x => productIds.Contains(x.Id) && x.CompanyId == item.CompanyId && x.UnitOfMeasure == item.UnitOfMeasure, ct) != 2)
                throw new InvalidOperationException("Transfer unit must match both source and destination products.");
            BusinessInput.Decimal(item.Quantity, "Transfer quantity", true); BusinessInput.Amount(item.CostAmount, "Transfer value", false);
            if (item.SourceQuantityBefore - item.Quantity != item.SourceQuantityAfter || item.DestinationQuantityBefore + item.Quantity != item.DestinationQuantityAfter
                || item.SourceValueBefore - item.CostAmount != item.SourceValueAfter || item.DestinationValueBefore + item.CostAmount != item.DestinationValueAfter)
                throw new InvalidOperationException("Transfer quantity and carrying-value snapshots must balance.");
            var movementIds = new[] { item.SourceMovementId, item.DestinationMovementId };
            var movements = await StockMovements.AsNoTracking().Where(x => movementIds.Contains(x.Id)).ToListAsync(ct);
            if (!movements.Any(x => x.Id == item.SourceMovementId && x.CompanyId == item.CompanyId && x.BranchId == item.SourceBranchId && x.StockItemId == item.SourceStockItemId && x.QuantityDelta == -item.Quantity && x.Reference == StockTransferService.OutReference(item.Reference))
                || !movements.Any(x => x.Id == item.DestinationMovementId && x.CompanyId == item.CompanyId && x.BranchId == item.DestinationBranchId && x.StockItemId == item.DestinationStockItemId && x.QuantityDelta == item.Quantity && x.Reference == StockTransferService.InReference(item.Reference)))
                throw new InvalidOperationException("Transfer requires its exact paired stock movements.");
            if (item.CostAmount == 0)
            {
                if (item.SourceVoucherId.HasValue || item.DestinationVoucherId.HasValue) throw new InvalidOperationException("A zero-value transfer cannot invent accounting value.");
                continue;
            }
            var vouchers = await Vouchers.AsNoTracking().Include(x => x.Lines).Where(x => x.Id == item.SourceVoucherId || x.Id == item.DestinationVoucherId).ToListAsync(ct);
            bool Matches(Voucher voucher, int branch, string reference, string debit, string credit) =>
                voucher.CompanyId == item.CompanyId && voucher.BranchId == branch && voucher.Posted && voucher.Reference == reference && voucher.Lines.Count == 2
                && voucher.Lines.Any(x => x.AccountCode == debit && x.Debit == item.CostAmount && x.Credit == 0)
                && voucher.Lines.Any(x => x.AccountCode == credit && x.Credit == item.CostAmount && x.Debit == 0);
            if (!vouchers.Any(x => x.Id == item.SourceVoucherId && Matches(x, item.SourceBranchId, StockTransferService.OutReference(item.Reference), "1290", "1300"))
                || !vouchers.Any(x => x.Id == item.DestinationVoucherId && Matches(x, item.DestinationBranchId, StockTransferService.InReference(item.Reference), "1300", "1290")))
                throw new InvalidOperationException("Transfer requires balanced source and destination journals for its exact carrying value.");
        }
    }
}
