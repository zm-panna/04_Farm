using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed partial class AppDbContext
{
    public DbSet<InternalMessage> InternalMessages => Set<InternalMessage>();
    public DbSet<UserInboxNotification> UserInboxNotifications => Set<UserInboxNotification>();
    public DbSet<MessagingOutbox> MessagingOutbox => Set<MessagingOutbox>();
    public DbSet<MessagingRateBucket> MessagingRateBuckets => Set<MessagingRateBucket>();
    public DbSet<MessagingAutomationRead> MessagingAutomationReads => Set<MessagingAutomationRead>();

    // Call before ConfigureIntegrity so all new timestamps receive the SQLite UTC-ticks converter.
    private void ConfigureMessaging(ModelBuilder b)
    {
        b.Entity<InternalMessage>().Property(x => x.SenderUserId).HasMaxLength(128);
        b.Entity<InternalMessage>().Property(x => x.RecipientUserId).HasMaxLength(128);
        b.Entity<UserInboxNotification>().Property(x => x.UserId).HasMaxLength(128);
        b.Entity<MessagingOutbox>().Property(x => x.UserId).HasMaxLength(128);
        b.Entity<MessagingRateBucket>().Property(x => x.UserId).HasMaxLength(128);
        b.Entity<MessagingAutomationRead>().Property(x => x.UserId).HasMaxLength(128);
        b.Entity<MessagingRateBucket>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InternalMessage>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id, x.RecipientUserId });
        b.Entity<InternalMessage>().HasIndex(x => new { x.CompanyId, x.BranchId, x.SenderUserId, x.IdempotencyKey }).IsUnique();
        b.Entity<InternalMessage>().HasIndex(x => new { x.CompanyId, x.BranchId, x.SenderUserId, x.Id });
        b.Entity<InternalMessage>().Property(x => x.Subject).HasMaxLength(160);
        b.Entity<InternalMessage>().Property(x => x.Body).HasMaxLength(8000);
        b.Entity<InternalMessage>().Property(x => x.IdempotencyKey).HasMaxLength(32);
        b.Entity<InternalMessage>().Property(x => x.PayloadHash).HasMaxLength(64);
        b.Entity<InternalMessage>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InternalMessage>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        var len = Database.IsSqlite() ? "length" : "LEN";
        b.Entity<InternalMessage>().ToTable(t => t.HasCheckConstraint("CK_InternalMessage_Content",
            $"{len}([Subject]) BETWEEN 1 AND 160 AND {len}([Body]) BETWEEN 1 AND 8000 AND {len}([IdempotencyKey]) = 32 AND {len}([PayloadHash]) = 64"));
        b.Entity<UserInboxNotification>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id, x.UserId });
        b.Entity<UserInboxNotification>().HasIndex(x => new { x.CompanyId, x.BranchId, x.UserId, x.MessageId }).IsUnique();
        b.Entity<UserInboxNotification>().HasIndex(x => new { x.CompanyId, x.BranchId, x.UserId, x.ReadUtc, x.Id });
        b.Entity<UserInboxNotification>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<UserInboxNotification>().HasOne(x => x.Message).WithMany()
            .HasForeignKey(x => new { x.CompanyId, x.BranchId, x.MessageId, x.UserId })
            .HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id, x.RecipientUserId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MessagingOutbox>().HasIndex(x => x.NotificationId).IsUnique();
        b.Entity<MessagingOutbox>().HasIndex(x => new { x.HintProcessedUtcTicks, x.LeaseUntilUtcTicks, x.Id });
        b.Entity<MessagingOutbox>().Property(x => x.LeaseOwner).HasMaxLength(32);
        b.Entity<MessagingOutbox>().HasOne(x => x.Notification).WithMany()
            .HasForeignKey(x => new { x.CompanyId, x.BranchId, x.NotificationId, x.UserId })
            .HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MessagingRateBucket>().HasIndex(x => new { x.CompanyId, x.BranchId, x.UserId, x.Period, x.WindowUtcTicks }).IsUnique();
        b.Entity<MessagingRateBucket>().Property(x => x.Period).HasMaxLength(6);
        b.Entity<MessagingRateBucket>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<MessagingRateBucket>().ToTable(t => t.HasCheckConstraint("CK_MessagingRateBucket_Limit",
            "([Period] = 'Minute' AND [Count] BETWEEN 1 AND 20) OR ([Period] = 'Day' AND [Count] BETWEEN 1 AND 200)"));
        b.Entity<InAppNotification>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
        b.Entity<MessagingAutomationRead>().HasIndex(x => new { x.CompanyId, x.BranchId, x.UserId, x.NotificationId }).IsUnique();
        b.Entity<MessagingAutomationRead>().HasOne<InAppNotification>().WithMany()
            .HasForeignKey(x => new { x.CompanyId, x.BranchId, x.NotificationId })
            .HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MessagingAutomationRead>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }

    private void ValidateMessagingWrites()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<InternalMessage>().Any(x => x.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<MessagingAutomationRead>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Internal correspondence and automation acknowledgements are immutable.");
        foreach (var entry in ChangeTracker.Entries<InternalMessage>().Where(x => x.State == EntityState.Added))
        {
            var m = entry.Entity;
            if (string.IsNullOrWhiteSpace(m.Subject) || m.Subject.Length > 160 || string.IsNullOrWhiteSpace(m.Body) || m.Body.Length > 8000
                || !Guid.TryParseExact(m.IdempotencyKey, "N", out var key) || key == Guid.Empty || m.PayloadHash.Length != 64)
                throw new ArgumentException("A bounded subject, body and valid idempotency key are required.");
        }
        foreach (var entry in ChangeTracker.Entries<UserInboxNotification>().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            if (entry.State == EntityState.Deleted || entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("ReadUtc" or "RowVersion"))
                || entry.OriginalValues.GetValue<DateTimeOffset?>(nameof(UserInboxNotification.ReadUtc)) is not null || entry.Entity.ReadUtc is null)
                throw new InvalidOperationException("Only an unread notification can be acknowledged; its ownership and content are immutable.");
        }
        foreach (var entry in ChangeTracker.Entries<MessagingOutbox>().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
            if (entry.State == EntityState.Deleted || entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("LeaseOwner" or "LeaseUntilUtcTicks" or "Attempts" or "HintProcessedUtcTicks")))
                throw new InvalidOperationException("Outbox ownership is immutable.");
    }
}
