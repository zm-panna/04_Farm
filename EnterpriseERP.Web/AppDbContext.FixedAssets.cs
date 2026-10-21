using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;
public sealed partial class AppDbContext
{
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<FixedAssetCustody> FixedAssetCustodies => Set<FixedAssetCustody>();
    public DbSet<FixedAssetDepreciationRun> FixedAssetDepreciationRuns => Set<FixedAssetDepreciationRun>();
    public DbSet<FixedAssetDepreciationLine> FixedAssetDepreciationLines => Set<FixedAssetDepreciationLine>();
    public DbSet<FixedAssetDisposal> FixedAssetDisposals => Set<FixedAssetDisposal>();
    // Financial records are writable only during the service's transactional state machine.
    internal bool FixedAssetWorkflowWrite { get; set; }
    private void ConfigureFixedAssets(ModelBuilder b)
    {
        var a = b.Entity<FixedAsset>();
        a.HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
        a.HasIndex(x => new { x.CompanyId, x.BranchId, x.AssetCode }).IsUnique();
        a.HasIndex(x => new { x.CompanyId, x.BranchId, x.SourceReference }).IsUnique();
        a.HasIndex(x => x.AcquisitionVoucherId).IsUnique().HasFilter("[AcquisitionVoucherId] IS NOT NULL");
        a.Property(x => x.RowVersion).IsRowVersion();
        a.Property(x => x.AssetCode).HasMaxLength(60); a.Property(x => x.Name).HasMaxLength(160);
        a.Property(x => x.SourceReference).HasMaxLength(80); a.Property(x => x.PolicyReason).HasMaxLength(1000);
        a.Property(x => x.ReviewNote).HasMaxLength(1000); a.Property(x => x.Notes).HasMaxLength(1000);
        a.HasOne<Voucher>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.AcquisitionVoucherId })
            .HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var r = b.Entity<FixedAssetDepreciationRun>();
        r.HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
        r.HasIndex(x => new { x.CompanyId, x.BranchId, x.Reference }).IsUnique();
        r.HasIndex(x => new { x.CompanyId, x.BranchId, x.Period }).IsUnique().HasFilter("[Status] <> 'Cancelled' AND [Status] <> 'Posted'");
        r.Property(x => x.Reference).HasMaxLength(80); r.Property(x => x.ReviewNote).HasMaxLength(1000);
        r.Property(x => x.RowVersion).IsRowVersion();
        r.HasIndex(x => x.VoucherId).IsUnique().HasFilter("[VoucherId] IS NOT NULL");
        r.HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.FixedAssetDepreciationRunId })
            .HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var l = b.Entity<FixedAssetDepreciationLine>();
        l.HasIndex(x => new { x.CompanyId, x.BranchId, x.FixedAssetId, x.Period }).IsUnique().HasFilter("[Posted] = 1");
        l.HasIndex(x => new { x.FixedAssetDepreciationRunId, x.FixedAssetId }).IsUnique();
        l.Property(x => x.AssetCode).HasMaxLength(60);
        var c = b.Entity<FixedAssetCustody>();
        c.HasIndex(x => new { x.CompanyId, x.BranchId, x.FixedAssetId, x.Id });
        c.Property(x => x.Reason).HasMaxLength(1000);
        var d = b.Entity<FixedAssetDisposal>();
        d.HasIndex(x => new { x.CompanyId, x.BranchId, x.Reference }).IsUnique();
        d.HasIndex(x => new { x.CompanyId, x.BranchId, x.FixedAssetId }).IsUnique().HasFilter("[Status] <> 'Cancelled'");
        d.HasIndex(x => x.VoucherId).IsUnique().HasFilter("[VoucherId] IS NOT NULL");
        d.Property(x => x.RowVersion).IsRowVersion(); d.Property(x => x.Reference).HasMaxLength(80);
        d.Property(x => x.Reason).HasMaxLength(1000); d.Property(x => x.ReviewNote).HasMaxLength(1000);
        foreach (var type in new[] { typeof(FixedAssetCustody), typeof(FixedAssetDepreciationLine), typeof(FixedAssetDisposal) })
            b.Entity(type).HasOne(typeof(FixedAsset)).WithMany().HasForeignKey("CompanyId", "BranchId", "FixedAssetId")
                .HasPrincipalKey("CompanyId", "BranchId", "Id").OnDelete(DeleteBehavior.Restrict);
        foreach (var type in new[] { typeof(FixedAsset), typeof(FixedAssetDepreciationRun), typeof(FixedAssetDisposal) })
        {
            b.Entity(type).HasOne(typeof(CompanyBusinessPolicy)).WithMany().HasForeignKey("CompanyId", "BusinessPolicyId")
                .HasPrincipalKey("CompanyId", "Id").OnDelete(DeleteBehavior.Restrict);
            b.Entity(type).Property("CancellationReason").HasMaxLength(1000);
            b.Entity(type).Property("CurrencyCode").HasMaxLength(3);
        }
        string N(string name) => Database.IsSqlite() ? $"CAST([{name}] AS REAL)" : $"[{name}]";
        a.ToTable(t => {
            t.HasCheckConstraint("CK_FixedAsset_Values", $"{N("Cost")} >= 0 AND {N("ResidualValue")} >= 0 AND {N("ResidualValue")} <= {N("Cost")} AND {N("AccumulatedDepreciation")} >= 0 AND ROUND({N("Cost")} - {N("ResidualValue")} - {N("AccumulatedDepreciation")}, 3) >= 0");
            t.HasCheckConstraint("CK_FixedAsset_Policy", "([DepreciationMethod] = 'StraightLineMonthly' AND [UsefulLifeMonths] BETWEEN 1 AND 1200) OR ([DepreciationMethod] = 'None' AND [UsefulLifeMonths] = 0)");
            t.HasCheckConstraint("CK_FixedAsset_Status", "[Status] IN ('Draft','Approved','Active','Disposed','Cancelled')");
        });
        r.ToTable(t => { t.HasCheckConstraint("CK_FixedAssetRun_Status", "[Status] IN ('Draft','Approved','Posted','Cancelled')"); t.HasCheckConstraint("CK_FixedAssetRun_Total", N("Total") + " >= 0"); });
        l.ToTable(t => t.HasCheckConstraint("CK_FixedAssetLine_Values", $"{N("Amount")} >= 0 AND {N("AccumulatedBefore")} >= 0 AND ROUND({N("Cost")} - {N("ResidualValue")} - {N("AccumulatedAfter")}, 3) >= 0"));
        d.ToTable(t => { t.HasCheckConstraint("CK_FixedAssetDisposal_Status", "[Status] IN ('Draft','Approved','Posted','Cancelled')"); t.HasCheckConstraint("CK_FixedAssetDisposal_Values", $"{N("Proceeds")} >= 0 AND {N("CarryingValue")} >= 0"); });
    }
    private Task ValidateFixedAssetWritesAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        var changes = ChangeTracker.Entries().Where(e => e.Entity is FixedAsset or FixedAssetCustody or FixedAssetDepreciationRun or FixedAssetDepreciationLine or FixedAssetDisposal)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        if (changes.Length != 0 && !FixedAssetWorkflowWrite)
            throw new InvalidOperationException("Use the reviewed fixed-asset workflow to change asset records.");
        foreach (var entry in changes)
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("Asset history cannot be deleted. Cancel an unposted draft instead.");
            if (entry.State == EntityState.Modified && (entry.Entity is FixedAssetCustody
                || entry.Entity is FixedAssetDepreciationRun or FixedAssetDisposal && entry.OriginalValues.GetValue<string>("Status") is "Posted" or "Cancelled"
                || entry.Entity is FixedAssetDepreciationLine && entry.OriginalValues.GetValue<bool>("Posted")))
                throw new InvalidOperationException("Posted and cancelled asset records are immutable.");
            if (entry.Entity is FixedAsset asset)
            {
                BusinessInput.Amount(asset.Cost, "Asset cost", false); BusinessInput.Amount(asset.ResidualValue, "Residual value", false);
                BusinessInput.Amount(asset.AccumulatedDepreciation, "Accumulated depreciation", false);
                if (asset.AccumulatedDepreciation > asset.Cost - asset.ResidualValue) throw new InvalidOperationException("Depreciation cannot pass the residual-value floor.");
                if (entry.State == EntityState.Modified)
                    foreach (var name in new[] { nameof(FixedAsset.AssetCode), nameof(FixedAsset.SourceReference), nameof(FixedAsset.Cost), nameof(FixedAsset.ResidualValue), nameof(FixedAsset.DepreciationMethod), nameof(FixedAsset.UsefulLifeMonths), nameof(FixedAsset.DepreciationStart), nameof(FixedAsset.AcquisitionDate), nameof(FixedAsset.AcquisitionSettlement), nameof(FixedAsset.PolicyReason), nameof(FixedAsset.CreatedBy) })
                        if (entry.Property(name).IsModified) throw new InvalidOperationException("Asset financial policy and source facts are immutable. Cancel an unposted draft and create a corrected one.");
            }
        }
        ct.ThrowIfCancellationRequested(); return Task.CompletedTask;
    }
}
