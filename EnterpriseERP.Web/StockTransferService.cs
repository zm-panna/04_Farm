using System.Data;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed class StockTransferService(AppDbContext db, TenantContext tenant, PermissionService permission, TimeProvider? clock = null)
{
    public static string NormalizeReference(string? reference) => BusinessInput.Reference(reference).ToUpperInvariant();
    public static string OutReference(string reference) => "XFER-OUT-" + reference;
    public static string InReference(string reference) => "XFER-IN-" + reference;

    public async Task<StockTransfer> Post(int sourceItemId, int destinationBranchId, int destinationItemId,
        decimal quantity, string reference, string? note, CancellationToken ct)
    {
        reference = NormalizeReference(reference);
        note = BusinessInput.Text(note, "Transfer note", 500, required: false);
        BusinessInput.Decimal(quantity, "Transfer quantity", positive: true);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.ValidatePendingScan("Transfer", ct);
        try
        {
            // Grants and both branch identities are checked inside the same transaction as the balances.
            await permission.Demand("Inventory.Transfer", ct);
            var t = await tenant.GetAsync(ct);
            if (destinationBranchId == t.branch) throw new ArgumentException("Choose a different destination branch.");
            if (!await db.UserBranchAccess.AsNoTracking().AnyAsync(x => x.UserId == t.user && x.CompanyId == t.company && x.BranchId == destinationBranchId, ct))
                throw new UnauthorizedAccessException("Access to the destination branch is required.");
            var destinationBranch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == destinationBranchId && x.CompanyId == t.company, ct)
                ?? throw new UnauthorizedAccessException("Destination branch is not available in this company.");
            if (await db.StockTransfers.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct))
                throw new InvalidOperationException("This transfer reference has already been posted. Open the existing transfer before retrying.");
            var source = await db.StockItems.SingleOrDefaultAsync(x => x.Id == sourceItemId && x.CompanyId == t.company && x.BranchId == t.branch, ct)
                ?? throw new ArgumentException("Select a source product in the active branch.");
            var destination = await db.StockItems.SingleOrDefaultAsync(x => x.Id == destinationItemId && x.CompanyId == t.company && x.BranchId == destinationBranchId, ct)
                ?? throw new ArgumentException("Select a destination product in the chosen branch.");
            if (!string.Equals(source.SKU.Trim(), destination.SKU.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Source and destination products must have the same SKU and use the same stock unit.");
            if (string.IsNullOrWhiteSpace(source.UnitOfMeasure) || string.IsNullOrWhiteSpace(destination.UnitOfMeasure) || source.UnitOfMeasure != destination.UnitOfMeasure)
                throw new ArgumentException("Both products need the same reviewed stock unit before transfer. Set unknown units in Product Master; unit conversion is not supported.");
            if (source.QuantityOnHand < quantity) throw new InvalidOperationException("Insufficient source stock.");
            var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
            var sourceBalance = await InventoryValuation.Load(db, source, ct);
            var destinationBalance = await InventoryValuation.Load(db, destination, ct);
            var transfer = new StockTransfer
            {
                CompanyId = t.company, SourceBranchId = t.branch, DestinationBranchId = destinationBranchId,
                SourceStockItemId = source.Id, DestinationStockItemId = destination.Id, Reference = reference, Note = note,
                SourceBranchName = await db.Branches.Where(x => x.Id == t.branch && x.CompanyId == t.company).Select(x => x.Name).SingleAsync(ct),
                DestinationBranchName = destinationBranch.Name, SourceSku = source.SKU, DestinationSku = destination.SKU,
                SourceProductName = source.Name, DestinationProductName = destination.Name,
                UnitOfMeasure = source.UnitOfMeasure, Quantity = quantity, SourceQuantityBefore = source.QuantityOnHand, DestinationQuantityBefore = destination.QuantityOnHand,
                SourceValueBefore = sourceBalance.Value, DestinationValueBefore = destinationBalance.Value, CreatedBy = t.user,
                CreatedUtc = DateTimeOffset.UtcNow
            };
            transfer.CostAmount = InventoryValuation.Remove(source, sourceBalance, quantity);
            InventoryValuation.Add(destination, destinationBalance, quantity, transfer.CostAmount);
            transfer.SourceQuantityAfter = source.QuantityOnHand; transfer.DestinationQuantityAfter = destination.QuantityOnHand;
            transfer.SourceValueAfter = sourceBalance.Value; transfer.DestinationValueAfter = destinationBalance.Value;
            var outgoing = new StockMovement { CompanyId = t.company, BranchId = t.branch, StockItemId = source.Id,
                Reference = OutReference(reference), QuantityDelta = -quantity, CreatedUtc = transfer.CreatedUtc };
            var incoming = new StockMovement { CompanyId = t.company, BranchId = destinationBranchId, StockItemId = destination.Id,
                Reference = InReference(reference), QuantityDelta = quantity, CreatedUtc = transfer.CreatedUtc };
            db.StockMovements.AddRange(outgoing, incoming);
            Voucher? sourceVoucher = null, destinationVoucher = null;
            // Free stock still has two physical movements. Do not manufacture zero-value journal lines.
            if (transfer.CostAmount > 0)
            {
                sourceVoucher = BusinessInput.Voucher(t.company, t.branch, outgoing.Reference,
                    ("1290", transfer.CostAmount, 0), ("1300", 0, transfer.CostAmount));
                destinationVoucher = BusinessInput.Voucher(t.company, destinationBranchId, incoming.Reference,
                    ("1300", transfer.CostAmount, 0), ("1290", 0, transfer.CostAmount));
                sourceVoucher.Date = destinationVoucher.Date = businessDay.Policy is null ? transfer.CreatedUtc : businessDay.PostingDate;
                db.Vouchers.AddRange(sourceVoucher, destinationVoucher);
            }
            await db.SaveChangesAsync(ct);
            transfer.SourceMovementId = outgoing.Id; transfer.DestinationMovementId = incoming.Id;
            transfer.SourceVoucherId = sourceVoucher?.Id; transfer.DestinationVoucherId = destinationVoucher?.Id;
            db.StockTransfers.Add(transfer);
            db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Inventory.Transfer", Reference = reference, CreatedUtc = transfer.CreatedUtc });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return transfer;
        }
        catch
        {
            // An aborted unit of work must not be accidentally persisted by a later retry in the same request.
            await tx.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
