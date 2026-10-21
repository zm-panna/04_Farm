using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed record MessagingSendRequest(string? RecipientUserId, string? Subject, string? Body, string? IdempotencyKey);
public sealed record MessagingSendResult(long MessageId, bool Replayed);
public sealed record MessagingRecipient(string UserId, string DisplayName);
public sealed record MessagingListItem(long Id, long MessageId, string Subject, string Sender, string Recipient, DateTimeOffset CreatedUtc, DateTimeOffset? ReadUtc);
public sealed record MessagingPage<T>(IReadOnlyList<T> Items, long? NextBeforeId, int UnreadCount);
public sealed record MessagingDetail(long MessageId, string Subject, string Body, string Sender, string Recipient, DateTimeOffset CreatedUtc);
public sealed record MessagingAutomationItem(long Id, string Message, string Category, DateTimeOffset CreatedUtc, bool IsRead);
public sealed class MessagingFloodException(int retryAfterSeconds) : Exception("Internal message limit reached. Retry later.")
{ public int RetryAfterSeconds { get; } = retryAfterSeconds; }

public sealed class MessagingService(AppDbContext db, MessagingAccess access)
{
    public const int PageSize = 30;
    private static string Text(string? value, string label, int max)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > max || value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException($"{label} is required and must be at most {max} characters without control characters.");
        return value;
    }
    private static string Key(string? value) => Guid.TryParse(value, out var key) && key != Guid.Empty
        ? key.ToString("N") : throw new ArgumentException("A nonempty UUID idempotency key is required.");
    private static string Hash(string recipient, string subject, string body) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{recipient.Length}:{recipient}{subject.Length}:{subject}{body.Length}:{body}")));

    public async Task<MessagingSendResult> SendAsync(ClaimsPrincipal principal, MessagingSendRequest request, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Messaging.Send", ct);
        await access.DemandAsync(principal, "Messaging.View", ct);
        var recipient = Text(request.RecipientUserId, "Recipient", 128);
        var subject = Text(request.Subject, "Subject", 160);
        var body = Text(request.Body, "Message", 8000);
        var key = Key(request.IdempotencyKey);
        var hash = Hash(recipient, subject, body);
        // Deliberately identical error for a nonexistent, out-of-scope, or no-longer-eligible recipient.
        if (await access.EligibleAsync(actor.CompanyId, actor.BranchId, recipient, "Messaging.View", ct) is null)
            throw new ArgumentException("Choose an available recipient in the current company and branch.");
        async Task<MessagingSendResult?> Existing()
        {
            var existing = await db.InternalMessages.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId
                && x.SenderUserId == actor.UserId && x.IdempotencyKey == key).Select(x => new { x.Id, x.PayloadHash }).SingleOrDefaultAsync(ct);
            if (existing is null) return null;
            if (!string.Equals(existing.PayloadHash, hash, StringComparison.Ordinal))
                throw new InvalidOperationException("The idempotency key was already used for different message content.");
            return new MessagingSendResult(existing.Id, true);
        }
        var replay = await Existing();
        if (replay is not null) return replay;
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Revalidate inside the transaction so membership revocation cannot race a stale preflight.
            actor = await access.DemandAsync(principal, "Messaging.Send", ct);
            await access.DemandAsync(principal, "Messaging.View", ct);
            if (await access.EligibleAsync(actor.CompanyId, actor.BranchId, recipient, "Messaging.View", ct) is null)
                throw new ArgumentException("Choose an available recipient in the current company and branch.");
            replay = await Existing();
            if (replay is not null) { await transaction.CommitAsync(ct); return replay; }
            var now = DateTimeOffset.UtcNow;
            await BudgetAsync(actor, "Minute", now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerMinute, 20, now, ct);
            await BudgetAsync(actor, "Day", now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerDay, 200, now, ct);
            var message = new InternalMessage
            {
                CompanyId = actor.CompanyId, BranchId = actor.BranchId, SenderUserId = actor.UserId, RecipientUserId = recipient,
                Subject = subject, Body = body, IdempotencyKey = key, PayloadHash = hash, CreatedUtc = now
            };
            var notification = new UserInboxNotification
            {
                CompanyId = actor.CompanyId, BranchId = actor.BranchId, UserId = recipient, Message = message, CreatedUtc = now
            };
            db.UserInboxNotifications.Add(notification);
            db.MessagingOutbox.Add(new MessagingOutbox
            {
                CompanyId = actor.CompanyId, BranchId = actor.BranchId, UserId = recipient, Notification = notification, CreatedUtcTicks = now.UtcTicks
            });
            db.Audits.Add(new AuditEntry { CompanyId = actor.CompanyId, ActorId = actor.UserId, Action = "Messaging.Sent", Reference = key, CreatedUtc = now });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new MessagingSendResult(message.Id, false);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            // A simultaneous repeat may have committed first. Any other conflict remains a safe retryable failure.
            replay = await Existing();
            if (replay is not null) return replay;
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task BudgetAsync(MessagingActor actor, string period, long window, int maximum, DateTimeOffset now, CancellationToken ct)
    {
        var row = await db.MessagingRateBuckets.SingleOrDefaultAsync(x => x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId
            && x.UserId == actor.UserId && x.Period == period && x.WindowUtcTicks == window, ct);
        if (row is null)
            db.MessagingRateBuckets.Add(new MessagingRateBucket { CompanyId = actor.CompanyId, BranchId = actor.BranchId, UserId = actor.UserId, Period = period, WindowUtcTicks = window, Count = 1 });
        else
        {
            if (row.Count >= maximum)
                throw new MessagingFloodException((int)Math.Max(1, (window + (period == "Day" ? TimeSpan.TicksPerDay : TimeSpan.TicksPerMinute) - now.UtcTicks) / TimeSpan.TicksPerSecond));
            row.Count++;
        }
    }

    public async Task<IReadOnlyList<MessagingRecipient>> RecipientsAsync(ClaimsPrincipal principal, string? query, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Messaging.Send", ct);
        await access.DemandAsync(principal, "Messaging.View", ct);
        var term = query?.Trim() ?? "";
        if (term.Length < 2 || term.Length > 100) return [];
        var now = DateTimeOffset.UtcNow;
        return await db.Users.AsNoTracking().Where(u => u.UserName != null && u.UserName.Contains(term) && u.SecurityStamp != null
            && (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now)
            && db.UserCompanyAccess.Any(a => a.UserId == u.Id && a.CompanyId == actor.CompanyId)
            && db.UserBranchAccess.Any(a => a.UserId == u.Id && a.CompanyId == actor.CompanyId && a.BranchId == actor.BranchId)
            && db.UserPermissions.Any(p => p.UserId == u.Id && p.CompanyId == actor.CompanyId && (p.Permission == "Messaging.View" || p.Permission == "*")))
            .OrderBy(x => x.UserName).ThenBy(x => x.Id).Select(x => new MessagingRecipient(x.Id, x.UserName!)).Take(30).ToListAsync(ct);
    }

    public async Task<MessagingPage<MessagingListItem>> InboxAsync(ClaimsPrincipal principal, long? beforeId, bool unreadOnly, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Messaging.View", ct);
        var scoped = db.UserInboxNotifications.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId && x.UserId == actor.UserId);
        var unread = await scoped.CountAsync(x => x.ReadUtc == null, ct);
        var rows = await scoped.Where(x => (!beforeId.HasValue || x.Id < beforeId.Value) && (!unreadOnly || x.ReadUtc == null))
            .OrderByDescending(x => x.Id).Select(x => new MessagingListItem(x.Id, x.MessageId, x.Message.Subject,
                db.Users.Where(u => u.Id == x.Message.SenderUserId).Select(u => u.UserName).FirstOrDefault() ?? "User",
                db.Users.Where(u => u.Id == x.UserId).Select(u => u.UserName).FirstOrDefault() ?? "User", x.CreatedUtc, x.ReadUtc))
            .Take(PageSize + 1).ToListAsync(ct);
        return new(rows.Take(PageSize).ToArray(), rows.Count > PageSize ? rows[PageSize - 1].Id : null, unread);
    }

    public async Task<MessagingPage<MessagingListItem>> SentAsync(ClaimsPrincipal principal, long? beforeId, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Messaging.View", ct);
        var rows = await db.InternalMessages.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId
            && x.SenderUserId == actor.UserId && (!beforeId.HasValue || x.Id < beforeId.Value)).OrderByDescending(x => x.Id)
            .Select(x => new MessagingListItem(x.Id, x.Id, x.Subject,
                db.Users.Where(u => u.Id == x.SenderUserId).Select(u => u.UserName).FirstOrDefault() ?? "User",
                db.Users.Where(u => u.Id == x.RecipientUserId).Select(u => u.UserName).FirstOrDefault() ?? "User", x.CreatedUtc, null))
            .Take(PageSize + 1).ToListAsync(ct);
        return new(rows.Take(PageSize).ToArray(), rows.Count > PageSize ? rows[PageSize - 1].Id : null, 0);
    }

    public async Task<MessagingDetail> DetailAsync(ClaimsPrincipal principal, long id, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Messaging.View", ct);
        return await db.InternalMessages.AsNoTracking().Where(x => x.Id == id && x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId
            && (x.RecipientUserId == actor.UserId || x.SenderUserId == actor.UserId))
            .Select(x => new MessagingDetail(x.Id, x.Subject, x.Body,
                db.Users.Where(u => u.Id == x.SenderUserId).Select(u => u.UserName).FirstOrDefault() ?? "User",
                db.Users.Where(u => u.Id == x.RecipientUserId).Select(u => u.UserName).FirstOrDefault() ?? "User", x.CreatedUtc))
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
    }

    public async Task ReadAsync(ClaimsPrincipal principal, long notificationId, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Messaging.View", ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            actor = await access.DemandAsync(principal, "Messaging.View", ct);
            var row = await db.UserInboxNotifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.CompanyId == actor.CompanyId
                && x.BranchId == actor.BranchId && x.UserId == actor.UserId, ct) ?? throw new KeyNotFoundException();
            if (row.ReadUtc is null)
            {
                row.ReadUtc = DateTimeOffset.UtcNow;
                db.Audits.Add(new AuditEntry { CompanyId = actor.CompanyId, ActorId = actor.UserId, Action = "Messaging.Read", Reference = row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }

    public async Task<MessagingPage<MessagingAutomationItem>> AutomationAsync(ClaimsPrincipal principal, long? beforeId, bool unreadOnly, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Automation.Review", ct);
        var scoped = db.Notifications.AsNoTracking().Where(x => x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId);
        var unread = await scoped.CountAsync(x => !db.MessagingAutomationReads.Any(r => r.CompanyId == actor.CompanyId && r.BranchId == actor.BranchId && r.UserId == actor.UserId && r.NotificationId == x.Id), ct);
        var rows = await scoped.Where(x => (!beforeId.HasValue || x.Id < beforeId.Value)
                && (!unreadOnly || !db.MessagingAutomationReads.Any(r => r.CompanyId == actor.CompanyId && r.BranchId == actor.BranchId && r.UserId == actor.UserId && r.NotificationId == x.Id)))
            .OrderByDescending(x => x.Id).Take(PageSize + 1)
            .Select(x => new MessagingAutomationItem(x.Id, x.Message, x.Category, x.CreatedUtc,
                db.MessagingAutomationReads.Any(r => r.CompanyId == actor.CompanyId && r.BranchId == actor.BranchId && r.UserId == actor.UserId && r.NotificationId == x.Id)))
            .ToListAsync(ct);
        return new(rows.Take(PageSize).ToArray(), rows.Count > PageSize ? rows[PageSize - 1].Id : null, unread);
    }

    public async Task<MessagingAutomationItem> AutomationDetailAsync(ClaimsPrincipal principal, long id, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Automation.Review", ct);
        return await db.Notifications.AsNoTracking().Where(x => x.Id == id && x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId)
            .Select(x => new MessagingAutomationItem(x.Id, x.Message, x.Category, x.CreatedUtc,
                db.MessagingAutomationReads.Any(r => r.CompanyId == actor.CompanyId && r.BranchId == actor.BranchId && r.UserId == actor.UserId && r.NotificationId == x.Id)))
            .SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException();
    }

    public async Task ReadAutomationAsync(ClaimsPrincipal principal, long id, CancellationToken ct = default)
    {
        var actor = await access.DemandAsync(principal, "Automation.Review", ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            actor = await access.DemandAsync(principal, "Automation.Review", ct);
            if (!await db.Notifications.AnyAsync(x => x.Id == id && x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId, ct)) throw new KeyNotFoundException();
            if (!await db.MessagingAutomationReads.AnyAsync(x => x.CompanyId == actor.CompanyId && x.BranchId == actor.BranchId && x.UserId == actor.UserId && x.NotificationId == id, ct))
            {
                db.MessagingAutomationReads.Add(new MessagingAutomationRead { CompanyId = actor.CompanyId, BranchId = actor.BranchId, UserId = actor.UserId, NotificationId = id, ReadUtc = DateTimeOffset.UtcNow });
                db.Audits.Add(new AuditEntry { CompanyId = actor.CompanyId, ActorId = actor.UserId, Action = "Messaging.AutomationRead", Reference = id.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
}
