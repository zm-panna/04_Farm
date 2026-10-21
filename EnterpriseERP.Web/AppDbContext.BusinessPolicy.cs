using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

public sealed partial class AppDbContext
{
    public DbSet<CompanyCurrencyLock> CompanyCurrencyLocks => Set<CompanyCurrencyLock>();
    public DbSet<CompanyBusinessPolicy> CompanyBusinessPolicies => Set<CompanyBusinessPolicy>();
    public DbSet<TaxPolicy> TaxPolicies => Set<TaxPolicy>();
    private void ConfigureBusinessPolicy(ModelBuilder b)
    {
        b.Entity<CompanyCurrencyLock>().HasIndex(x => x.CompanyId).IsUnique();
        b.Entity<CompanyCurrencyLock>().ToTable(t => t.HasCheckConstraint("CK_CompanyCurrencyLock_MinorUnits", "[CurrencyMinorUnits] BETWEEN 0 AND 3"));
        b.Entity<CompanyBusinessPolicy>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        b.Entity<CompanyBusinessPolicy>().HasIndex(x => new { x.CompanyId, x.Version }).IsUnique();
        b.Entity<CompanyBusinessPolicy>().HasIndex(x => new { x.CompanyId, x.EffectiveFrom, x.Status });
        b.Entity<CompanyBusinessPolicy>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<CompanyBusinessPolicy>().Property(x => x.ReviewBasis).HasMaxLength(2000);
        b.Entity<CompanyBusinessPolicy>().ToTable(t => { t.HasCheckConstraint("CK_CompanyBusinessPolicy_Status", "[Status] IN ('Draft','Approved','Rejected')"); t.HasCheckConstraint("CK_CompanyBusinessPolicy_MinorUnits", "[CurrencyMinorUnits] BETWEEN 0 AND 3"); });
        b.Entity<TaxPolicy>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        b.Entity<TaxPolicy>().HasIndex(x => new { x.CompanyId, x.Code, x.Version }).IsUnique();
        b.Entity<TaxPolicy>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<TaxPolicy>().Property(x => x.Reason).HasMaxLength(2000);
        b.Entity<TaxPolicy>().HasOne<CompanyBusinessPolicy>().WithMany().HasForeignKey(x => new { x.CompanyId, x.CompanyBusinessPolicyId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        string rate = Database.IsSqlite() ? "CAST([RatePercent] AS REAL)" : "[RatePercent]";
        b.Entity<TaxPolicy>().ToTable(t => { t.HasCheckConstraint("CK_TaxPolicy_Status", "[Status] IN ('Draft','Approved','Rejected')"); t.HasCheckConstraint("CK_TaxPolicy_Rate", $"{rate} >= 0 AND {rate} <= 100 AND (([TaxTreatment] = 'Standard' AND {rate} > 0) OR ([TaxTreatment] IN ('Exempt','OutOfScope') AND {rate} = 0))"); });
        b.Entity<StockItem>().Property(x => x.ReservedQuantity).IsConcurrencyToken();
        string qty(string n) => Database.IsSqlite() ? $"CAST([{n}] AS REAL)" : $"[{n}]";
        b.Entity<StockItem>().ToTable(t => t.HasCheckConstraint("CK_StockItem_Reservations", $"{qty("ReservedQuantity")} >= 0 AND {qty("ReservedQuantity")} <= {qty("QuantityOnHand")}"));
    }
    private void ValidateBusinessPolicyWrites()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<CompanyCurrencyLock>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("The company accounting currency is locked. Use a separately reviewed migration/new company, never relabel historical amounts.");
        foreach (var e in ChangeTracker.Entries().Where(x => x.Entity is CompanyBusinessPolicy or TaxPolicy && x.State is EntityState.Modified or EntityState.Deleted))
        {
            if (e.State == EntityState.Deleted || e.OriginalValues.GetValue<string>("Status") != "Draft")
                throw new InvalidOperationException("Reviewed policy versions are immutable; create a new effective-dated version.");
            if (e.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("Status" or "ApprovedBy" or "ApprovedUtc" or "RowVersion")))
                throw new InvalidOperationException("Policy proposals cannot be rewritten. Reject and create a corrected version.");
            if (e.CurrentValues.GetValue<string>("Status") is not ("Approved" or "Rejected")) throw new InvalidOperationException("Invalid policy review transition.");
            if (e.CurrentValues.GetValue<string>("Status") == "Approved" && e.CurrentValues.GetValue<string>("ApprovedBy") == e.OriginalValues.GetValue<string>("CreatedBy"))
                throw new InvalidOperationException("A different reviewer must approve the policy.");
        }
    }
}
