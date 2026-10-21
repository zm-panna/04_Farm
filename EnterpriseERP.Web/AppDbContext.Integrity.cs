using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EnterpriseERP.Web;

public sealed partial class AppDbContext
{
    private void ConfigureIntegrity(ModelBuilder b)
    {
        b.Entity<Branch>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        b.Entity<Farm>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        b.Entity<Branch>().HasOne<Farm>().WithMany().HasForeignKey(x => new { x.CompanyId, x.FarmId })
            .HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var scopedPrincipals = new Dictionary<string, Type>
        {
            ["AnimalId"] = typeof(Animal), ["StockItemId"] = typeof(StockItem), ["VoucherId"] = typeof(Voucher),
            ["MatchedVoucherId"] = typeof(Voucher), ["FinanceDocumentId"] = typeof(FinanceDocument),
            ["FinancePaymentId"] = typeof(FinancePayment), ["BreedingCycleId"] = typeof(BreedingCycle)
        };
        foreach (var type in scopedPrincipals.Values.Distinct())
            b.Entity(type).HasAlternateKey("CompanyId", "BranchId", "Id");
        b.Entity<SupportTicket>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        b.Entity<SupportTicketActivity>().HasOne<SupportTicket>().WithMany().HasForeignKey(x => new { x.CompanyId, x.TicketId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SubscriptionInvoice>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        b.Entity<SubscriptionPayment>().HasOne<SubscriptionInvoice>().WithMany().HasForeignKey(x => new { x.CompanyId, x.InvoiceId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AutomationRule>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        foreach (var type in new[] { typeof(ScheduledJob), typeof(AutomationApproval), typeof(AutomationExecution) })
            b.Entity(type).HasOne(typeof(AutomationRule)).WithMany().HasForeignKey("CompanyId", "RuleId").HasPrincipalKey("CompanyId", "Id").OnDelete(DeleteBehavior.Restrict);
        foreach (var entity in b.Model.GetEntityTypes().Where(e => e.ClrType.Namespace == typeof(Company).Namespace).ToArray())
        {
            var type = entity.ClrType;
            if (entity.FindProperty("CompanyId") is not null)
                b.Entity(type).HasOne(typeof(Company)).WithMany().HasForeignKey("CompanyId").OnDelete(DeleteBehavior.Restrict);
            if (entity.FindProperty("BranchId") is not null)
                b.Entity(type).HasOne(typeof(Branch)).WithMany().HasForeignKey("CompanyId", "BranchId")
                    .HasPrincipalKey("CompanyId", "Id").OnDelete(DeleteBehavior.Restrict);
            if (entity.FindProperty("BranchId") is not null)
                foreach (var pair in scopedPrincipals)
                    if (entity.FindProperty(pair.Key) is not null)
                        b.Entity(type).HasOne(pair.Value).WithMany().HasForeignKey("CompanyId", "BranchId", pair.Key)
                            .HasPrincipalKey("CompanyId", "BranchId", "Id").OnDelete(DeleteBehavior.Restrict);
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string)))
            {
                if (property.GetMaxLength() is null)
                    property.SetMaxLength(property.Name == "Description" ? 8000 : property.Name is "Details" or "Note" or "Result" or "LastError" ? 4000 :
                        property.Name.EndsWith("UserId") || property.Name is "ActorId" or "RequesterId" or "ApproverId" or "RecordedBy" or "CreatedBy" or "AssignedTo" ? 128 : 200);
            }
        }
        if (Database.IsSqlite())
        {
            var converter = new ValueConverter<DateTimeOffset, long>(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero));
            foreach (var entity in b.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(converter);
                    if (property.Name == "RowVersion")
                    { property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never; property.IsConcurrencyToken = true; }
                }
        }
    }

    private void StampSqliteConcurrencyTokens()
    {
        if (!Database.IsSqlite()) return;
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var property = entry.Metadata.FindProperty("RowVersion");
            if (property is not null) entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    { ValidateScanningWrites(); ValidateMessagingWrites(); ValidateBusinessPolicyWrites(); ValidateEcommerceWrites(); ValidateHRPayrollWritesAsync(CancellationToken.None).GetAwaiter().GetResult(); ValidateCRMWritesAsync(CancellationToken.None).GetAwaiter().GetResult(); ValidateFixedAssetWritesAsync(CancellationToken.None).GetAwaiter().GetResult(); ValidateStockTransferWritesAsync(CancellationToken.None).GetAwaiter().GetResult(); ValidateAccountingWritesAsync(CancellationToken.None).GetAwaiter().GetResult(); StampSqliteConcurrencyTokens(); StampCacheEpochs(CancellationToken.None).GetAwaiter().GetResult(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { ValidateScanningWrites(); ValidateMessagingWrites(); ValidateBusinessPolicyWrites(); ValidateEcommerceWrites(); await ValidateHRPayrollWritesAsync(cancellationToken); await ValidateCRMWritesAsync(cancellationToken); await ValidateFixedAssetWritesAsync(cancellationToken); await ValidateStockTransferWritesAsync(cancellationToken); await ValidateAccountingWritesAsync(cancellationToken); StampSqliteConcurrencyTokens(); await StampCacheEpochs(cancellationToken); return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}
