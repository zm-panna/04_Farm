using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;
public sealed partial class AppDbContext
{
    public DbSet<CRMLead> CRMLeads => Set<CRMLead>();
    public DbSet<CRMConsentEvent> CRMConsentEvents => Set<CRMConsentEvent>();
    public DbSet<CRMOpportunity> CRMOpportunities => Set<CRMOpportunity>();
    public DbSet<CRMActivity> CRMActivities => Set<CRMActivity>();
    public DbSet<CRMQuote> CRMQuotes => Set<CRMQuote>();
    public DbSet<CRMQuoteLine> CRMQuoteLines => Set<CRMQuoteLine>();
    public DbSet<CRMSalesReady> CRMSalesReadyRecords => Set<CRMSalesReady>();

    private void ConfigureCRM(ModelBuilder b)
    {
        string C(string column) => Database.IsSqlite() ? $"CAST([{column}] AS REAL)" : $"[{column}]";
        foreach (var type in new[] { typeof(CRMLead), typeof(CRMOpportunity), typeof(CRMQuote), typeof(CRMActivity), typeof(CRMSalesReady) })
        {
            b.Entity(type).HasAlternateKey("CompanyId", "BranchId", "Id");
            b.Entity(type).HasIndex("CompanyId", "BranchId", "Reference").IsUnique();
            b.Entity(type).Property("Reference").HasMaxLength(80);
        }
        foreach (var type in new[] { typeof(CRMLead), typeof(CRMOpportunity), typeof(CRMQuote), typeof(CRMActivity) })
            b.Entity(type).Property("RowVersion").IsRowVersion();
        b.Entity<CRMLead>().HasOne<TradingParty>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.CustomerId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CRMLead>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<CRMLead>().Property(x => x.Phone).HasMaxLength(40);
        b.Entity<CRMLead>().Property(x => x.Address).HasMaxLength(500);
        b.Entity<CRMLead>().Property(x => x.ConsentEvidence).HasMaxLength(1000);
        b.Entity<CRMLead>().Property(x => x.Notes).HasMaxLength(2000);
        b.Entity<CRMLead>().ToTable(t => {
            t.HasCheckConstraint("CK_CRMLead_Status", "[Status] IN ('Active','Converted','Archived')");
            t.HasCheckConstraint("CK_CRMLead_Consent", "[MarketingConsent] IN ('Unknown','Granted','Withdrawn')");
            t.HasCheckConstraint("CK_CRMLead_Converted", "[Status] <> 'Converted' OR [CustomerId] IS NOT NULL");
        });
        foreach (var type in new[] { typeof(CRMOpportunity), typeof(CRMActivity), typeof(CRMQuote), typeof(CRMConsentEvent), typeof(CRMSalesReady) })
            b.Entity(type).HasOne(typeof(CRMLead)).WithMany().HasForeignKey("CompanyId", "BranchId", "LeadId").HasPrincipalKey("CompanyId", "BranchId", "Id").OnDelete(DeleteBehavior.Restrict);
        b.Entity<CRMOpportunity>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id, x.LeadId });
        foreach (var type in new[] { typeof(CRMActivity), typeof(CRMQuote), typeof(CRMSalesReady) })
            b.Entity(type).HasOne(typeof(CRMOpportunity)).WithMany().HasForeignKey("CompanyId", "BranchId", "OpportunityId", "LeadId").HasPrincipalKey("CompanyId", "BranchId", "Id", "LeadId").OnDelete(DeleteBehavior.Restrict);
        b.Entity<CRMConsentEvent>().Property(x => x.Evidence).HasMaxLength(1000);
        b.Entity<CRMConsentEvent>().ToTable(t => t.HasCheckConstraint("CK_CRMConsentEvent_Consent", "[Consent] IN ('Unknown','Granted','Withdrawn') AND [PreviousConsent] IN ('Unknown','Granted','Withdrawn')"));
        b.Entity<CRMOpportunity>().Property(x => x.ReviewNote).HasMaxLength(1000);
        b.Entity<CRMOpportunity>().ToTable(t => t.HasCheckConstraint("CK_CRMOpportunity_Stage", "[Stage] IN ('Qualified','Proposal','Negotiation','Won','Lost')"));
        b.Entity<CRMActivity>().Property(x => x.Notes).HasMaxLength(2000);
        b.Entity<CRMActivity>().Property(x => x.CompletionNote).HasMaxLength(1000);
        b.Entity<CRMActivity>().HasIndex(x => new { x.CompanyId, x.BranchId, x.Status, x.DueDate });
        b.Entity<CRMActivity>().ToTable(t => {
            t.HasCheckConstraint("CK_CRMActivity_Status", "[Status] IN ('Open','Done','Cancelled')");
            t.HasCheckConstraint("CK_CRMActivity_Kind", "[Kind] IN ('Task','Call','Meeting')");
            t.HasCheckConstraint("CK_CRMActivity_Completion", "([Status] = 'Open' AND [CompletedUtc] IS NULL AND [CompletedBy] IS NULL) OR ([Status] <> 'Open' AND [CompletedUtc] IS NOT NULL AND [CompletedBy] IS NOT NULL)");
        });
        foreach (var type in new[] { typeof(CRMQuote), typeof(CRMSalesReady) })
            b.Entity(type).HasOne(typeof(CompanyBusinessPolicy)).WithMany().HasForeignKey("CompanyId", "BusinessPolicyId").HasPrincipalKey("CompanyId", "Id").OnDelete(DeleteBehavior.Restrict);
        b.Entity<CRMQuote>().Property(x => x.CurrencyCode).HasMaxLength(3);
        b.Entity<CRMQuote>().Property(x => x.CustomerName).HasMaxLength(160);
        b.Entity<CRMQuote>().Property(x => x.Phone).HasMaxLength(40);
        b.Entity<CRMQuote>().Property(x => x.Address).HasMaxLength(500);
        b.Entity<CRMQuote>().Property(x => x.Terms).HasMaxLength(2000);
        b.Entity<CRMQuote>().Property(x => x.ReviewNote).HasMaxLength(1000);
        b.Entity<CRMQuote>().ToTable(t => {
            t.HasCheckConstraint("CK_CRMQuote_Status", "[Status] IN ('Draft','Reviewed','Accepted','Cancelled')");
            t.HasCheckConstraint("CK_CRMQuote_Amounts", $"{C("GrossAmount")} > 0 AND {C("DiscountAmount")} >= 0 AND {C("NetAmount")} > 0 AND {C("DiscountAmount")} < {C("GrossAmount")}");
            t.HasCheckConstraint("CK_CRMQuote_Review", "[Status] NOT IN ('Reviewed','Accepted') OR ([ReviewedBy] IS NOT NULL AND [ReviewedUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_CRMQuote_Accept", "[Status] <> 'Accepted' OR ([AcceptedBy] IS NOT NULL AND [AcceptedUtc] IS NOT NULL)");
        });
        b.Entity<CRMQuote>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.QuoteId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CRMQuoteLine>().HasIndex(x => new { x.CompanyId, x.BranchId, x.QuoteId, x.LineNumber }).IsUnique();
        b.Entity<CRMQuoteLine>().Property(x => x.UnitOfMeasure).HasMaxLength(30);
        b.Entity<CRMQuoteLine>().ToTable(t => t.HasCheckConstraint("CK_CRMQuoteLine_Amounts", $"[LineNumber] > 0 AND {C("Quantity")} > 0 AND {C("UnitPrice")} > 0 AND {C("GrossAmount")} > 0 AND {C("DiscountAmount")} >= 0 AND {C("DiscountAmount")} < {C("GrossAmount")} AND {C("NetAmount")} > 0"));
        b.Entity<CRMSalesReady>().HasOne<CRMQuote>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.QuoteId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CRMSalesReady>().HasOne<TradingParty>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.CustomerId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CRMSalesReady>().HasIndex(x => x.QuoteId).IsUnique();
        b.Entity<CRMSalesReady>().HasIndex(x => x.OpportunityId).IsUnique();
        b.Entity<CRMSalesReady>().Property(x => x.CurrencyCode).HasMaxLength(3);
        b.Entity<CRMSalesReady>().Property(x => x.AcceptanceEvidence).HasMaxLength(1000);
        b.Entity<CRMSalesReady>().ToTable(t => t.HasCheckConstraint("CK_CRMSalesReady_Net", $"{C("NetAmount")} > 0"));
    }

    private async Task ValidateCRMWritesAsync(CancellationToken ct)
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<CRMConsentEvent>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<CRMSalesReady>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("CRM consent history and accepted sales handoffs are immutable.");
        if (ChangeTracker.Entries<CRMActivity>().Any(x => x.State == EntityState.Modified && x.OriginalValues.GetValue<string>(nameof(CRMActivity.Status)) != "Open"))
            throw new InvalidOperationException("Completed and cancelled activity records cannot be rewritten.");
        foreach (var entry in ChangeTracker.Entries<CRMQuote>().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            var original = entry.OriginalValues.GetValue<string>(nameof(CRMQuote.Status));
            if (entry.State == EntityState.Deleted || original is "Accepted" or "Cancelled")
                throw new InvalidOperationException("Accepted and cancelled quotes cannot be rewritten or deleted.");
            if (original == "Reviewed" && entry.Entity.Status != "Draft")
            {
                var allowed = new[] { "Status", "ReviewNote", "AcceptedBy", "AcceptedUtc", "RowVersion" };
                if (entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)))
                    throw new InvalidOperationException("Return a reviewed quote to draft before changing its snapshot.");
            }
        }
        foreach (var entry in ChangeTracker.Entries<CRMQuoteLine>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var line = entry.Entity;
            var parent = ChangeTracker.Entries<CRMQuote>().SingleOrDefault(x => x.Entity.Id == line.QuoteId && x.Entity.CompanyId == line.CompanyId && x.Entity.BranchId == line.BranchId);
            var state = parent?.State == EntityState.Added ? "Draft" : parent?.OriginalValues.GetValue<string>(nameof(CRMQuote.Status));
            state ??= await CRMQuotes.AsNoTracking().Where(x => x.Id == line.QuoteId && x.CompanyId == line.CompanyId && x.BranchId == line.BranchId).Select(x => x.Status).SingleOrDefaultAsync(ct);
            if (state != "Draft") throw new InvalidOperationException("Only draft quote lines may be changed.");
        }
        if (ChangeTracker.Entries<CRMLead>().Any(x => x.State == EntityState.Deleted)
            || ChangeTracker.Entries<CRMOpportunity>().Any(x => x.State == EntityState.Deleted)
            || ChangeTracker.Entries<CRMActivity>().Any(x => x.State == EntityState.Deleted))
            throw new InvalidOperationException("Archive or close CRM records to preserve history; deletion is not supported.");
    }
}
