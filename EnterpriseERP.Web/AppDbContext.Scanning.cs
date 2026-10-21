using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;
public sealed partial class AppDbContext
{
    public DbSet<ScanLabel> ScanLabels => Set<ScanLabel>();
    public DbSet<ScanIntent> ScanIntents => Set<ScanIntent>();
    internal bool ScanWorkflowWrite { get; set; }
    // Set only by the server-side scanner service. Existing domain methods invoke this after
    // beginning their SERIALIZABLE transaction; reads, consumption and business writes commit together.
    internal Func<string, CancellationToken, Task>? PendingScanGuard { get; set; }
    internal Task ValidatePendingScan(string action, CancellationToken ct) => PendingScanGuard?.Invoke(action, ct) ?? Task.CompletedTask;
    private void ConfigureScanning(ModelBuilder b)
    {
        b.Entity<ScanLabel>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
        b.Entity<ScanLabel>().HasIndex(x => new { x.CompanyId, x.BranchId, x.ValueHash }).IsUnique();
        b.Entity<ScanLabel>().HasIndex(x => new { x.CompanyId, x.BranchId, x.Kind, x.Id });
        b.Entity<ScanLabel>().Property(x => x.Kind).HasMaxLength(20);
        b.Entity<ScanLabel>().Property(x => x.Format).HasMaxLength(12);
        b.Entity<ScanLabel>().Property(x => x.Value).HasMaxLength(80);
        b.Entity<ScanLabel>().Property(x => x.ValueHash).HasMaxLength(64);
        b.Entity<ScanLabel>().Property(x => x.RevocationReason).HasMaxLength(300);
        b.Entity<ScanLabel>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<ScanLabel>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ScanLabel>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.RevokedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ScanLabel>().HasOne<FixedAsset>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.FixedAssetId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ScanLabel>().HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.PurchaseOrderId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ScanLabel>().ToTable(t => t.HasCheckConstraint("CK_ScanLabel_Target",
            "([Kind]='Product' AND [StockItemId] IS NOT NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR " +
            "([Kind]='Animal' AND [StockItemId] IS NULL AND [AnimalId] IS NOT NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR " +
            "([Kind]='Asset' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NOT NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NULL) OR " +
            "([Kind]='PurchaseOrder' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NOT NULL AND [FinanceDocumentId] IS NULL) OR " +
            "([Kind]='Document' AND [StockItemId] IS NULL AND [AnimalId] IS NULL AND [FixedAssetId] IS NULL AND [PurchaseOrderId] IS NULL AND [FinanceDocumentId] IS NOT NULL)"));
        b.Entity<ScanIntent>().HasIndex(x => new { x.CompanyId, x.BranchId, x.OwnerUserId, x.RequestKey }).IsUnique();
        b.Entity<ScanIntent>().HasIndex(x => new { x.CompanyId, x.BranchId, x.OwnerUserId, x.Id });
        b.Entity<ScanIntent>().HasIndex(x => new { x.CompanyId, x.BranchId, x.OwnerUserId, x.CreatedUtc });
        b.Entity<ScanIntent>().HasIndex(x => new { x.CompanyId, x.BranchId, x.OwnerUserId, x.Status, x.ExpiresUtc });
        b.Entity<ScanIntent>().HasIndex(x => new { x.CompanyId, x.BusinessReference }).IsUnique();
        b.Entity<ScanIntent>().Property(x => x.OwnerUserId).HasMaxLength(128);
        b.Entity<ScanIntent>().Property(x => x.RequestKey).HasMaxLength(32);
        b.Entity<ScanIntent>().Property(x => x.Action).HasMaxLength(20);
        b.Entity<ScanIntent>().Property(x => x.Status).HasMaxLength(12);
        b.Entity<ScanIntent>().Property(x => x.PayloadHash).HasMaxLength(64);
        b.Entity<ScanIntent>().Property(x => x.SourceHash).HasMaxLength(64);
        b.Entity<ScanIntent>().Property(x => x.PayloadJson).HasMaxLength(12000);
        b.Entity<ScanIntent>().Property(x => x.ReviewJson).HasMaxLength(8000);
        b.Entity<ScanIntent>().Property(x => x.BusinessReference).HasMaxLength(40);
        b.Entity<ScanIntent>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<ScanIntent>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ScanIntent>().HasOne<ScanLabel>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.ScanLabelId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ScanIntent>().HasOne<ScanLabel>().WithMany().HasForeignKey(x => new { x.CompanyId, x.SecondaryBranchId, x.SecondaryLabelId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ScanIntent>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_ScanIntent_State", "([Status]='Pending' AND [CompletedUtc] IS NULL) OR ([Status] IN ('Committed','Cancelled') AND [CompletedUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_ScanIntent_Secondary", "([SecondaryBranchId] IS NULL AND [SecondaryLabelId] IS NULL) OR ([SecondaryBranchId] IS NOT NULL AND [SecondaryLabelId] IS NOT NULL)");
        });
    }
    private void ValidateScanningWrites()
    {
        ChangeTracker.DetectChanges();
        foreach (var e in ChangeTracker.Entries<ScanLabel>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (!ScanWorkflowWrite) throw new InvalidOperationException("Scan labels must use the reviewed label workflow.");
            if (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
                (e.OriginalValues.GetValue<DateTimeOffset?>(nameof(ScanLabel.RevokedUtc)) is not null || e.Entity.RevokedUtc is null ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("RevokedUtc" or "RevokedBy" or "RevocationReason" or "RowVersion"))))
                throw new InvalidOperationException("Label mappings are immutable; revoke and issue a new label.");
        }
        foreach (var e in ChangeTracker.Entries<ScanIntent>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (!ScanWorkflowWrite) throw new InvalidOperationException("Scan drafts must use the confirmation workflow.");
            if (e.State == EntityState.Deleted || e.State == EntityState.Modified &&
                (e.OriginalValues.GetValue<string>(nameof(ScanIntent.Status)) != "Pending" || e.Entity.Status is not ("Committed" or "Cancelled") || e.Entity.CompletedUtc is null ||
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("Status" or "CompletedUtc" or "RowVersion"))))
                throw new InvalidOperationException("Scan draft content and completed outcomes are immutable.");
        }
    }
}
