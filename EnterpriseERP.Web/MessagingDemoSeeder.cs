using System.Security.Cryptography;
using System.Text;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

/// <summary>Only called inside the existing explicitly opted-in enterprise demo transaction; never on normal startup.</summary>
public static class MessagingDemoSeeder
{
    public static async Task SeedAsync(AppDbContext db, IReadOnlyList<Company> companies, IReadOnlyList<Branch> branches,
        IReadOnlyList<AppUser> accounts, DateTimeOffset now, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null || companies.Count is not (5 or 10) || branches.Count != companies.Count || accounts.Count != companies.Count
            || companies.Where((c, i) => c.Slug != $"demo-farm-{i + 1}").Any()
            || accounts.Where((u, i) => u.Email != $"farmdemo{i + 1}@example.invalid").Any()
            || await db.InternalMessages.AnyAsync(ct) || await db.MessagingAutomationReads.AnyAsync(ct))
            throw new InvalidOperationException("Messaging samples require the new, guarded synthetic enterprise fixture transaction.");
        var scenarios = new[]
        {
            ("Morning milk handover", "SYNTHETIC DEMO: Review the recorded morning milk quantity before the afternoon handover."),
            ("Feed stock review", "SYNTHETIC DEMO: Compare the feed bin count with the stock ledger before planning the next purchase."),
            ("Animal follow-up", "SYNTHETIC DEMO: Check the recorded animal follow-up date and contact the authorized farm reviewer if needed."),
            ("Store transfer check", "SYNTHETIC DEMO: Review the completed branch transfer reference and confirm the destination stock record."),
            ("Daily farm summary", "SYNTHETIC DEMO: The branch summary is available for review. This sample does not authorize any posting.")
        };
        for (var i = 0; i < companies.Count; i++)
        {
            var company = companies[i]; var branch = branches[i]; var recipient = accounts[i];
            if (branch.CompanyId != company.Id) throw new InvalidOperationException("Synthetic messaging branch mismatch.");
            var sender = await db.Users.SingleAsync(x => x.Email == $"farmreviewer{i + 1}@example.invalid", ct);
            var grants = await db.UserPermissions.Where(x => x.CompanyId == company.Id && x.UserId == recipient.Id).Select(x => x.Permission).ToListAsync(ct);
            if (!grants.Contains("*"))
            {
                if (!grants.Contains("Messaging.View")) db.UserPermissions.Add(new UserPermission { CompanyId = company.Id, UserId = recipient.Id, Permission = "Messaging.View" });
                // Keep the original synthetic Viewer read-only.
                if (i < 4 && !grants.Contains("Messaging.Send")) db.UserPermissions.Add(new UserPermission { CompanyId = company.Id, UserId = recipient.Id, Permission = "Messaging.Send" });
            }
            var at = now.AddMinutes(-5);
            var (subject, body) = scenarios[i % scenarios.Length];
            var key = $"00000000000040008000{i + 1:000000000000}";
            var message = new InternalMessage
            {
                CompanyId = company.Id, BranchId = branch.Id, SenderUserId = sender.Id, RecipientUserId = recipient.Id,
                Subject = subject, Body = body, IdempotencyKey = key,
                PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{recipient.Id.Length}:{recipient.Id}{subject.Length}:{subject}{body.Length}:{body}"))),
                CreatedUtc = at
            };
            var notification = new UserInboxNotification
            {
                CompanyId = company.Id, BranchId = branch.Id, UserId = recipient.Id, Message = message, CreatedUtc = at,
                ReadUtc = i < 2 ? now.AddMinutes(-1) : null
            };
            db.UserInboxNotifications.Add(notification);
            db.MessagingOutbox.Add(new MessagingOutbox { CompanyId = company.Id, BranchId = branch.Id, UserId = recipient.Id, Notification = notification, CreatedUtcTicks = at.UtcTicks });
            foreach (var period in new[] { ("Minute", TimeSpan.TicksPerMinute), ("Day", TimeSpan.TicksPerDay) })
                db.MessagingRateBuckets.Add(new MessagingRateBucket { CompanyId = company.Id, BranchId = branch.Id, UserId = sender.Id, Period = period.Item1, WindowUtcTicks = at.UtcTicks - at.UtcTicks % period.Item2, Count = 1 });
            var automation = new InAppNotification { CompanyId = company.Id, BranchId = branch.Id, ExecutionKey = $"DEMO-MESSAGING-REVIEW-{i + 1}", Category = "Review", Message = "SYNTHETIC DEMO: Branch records are ready for an authorized review.", CreatedUtc = at };
            db.Notifications.Add(automation);
            db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = sender.Id, Action = "Messaging.Sent", Reference = key, CreatedUtc = at });
            await db.SaveChangesAsync(ct);
            db.MessagingAutomationReads.Add(new MessagingAutomationRead { CompanyId = company.Id, BranchId = branch.Id, UserId = sender.Id, NotificationId = automation.Id, ReadUtc = now.AddMinutes(-1) });
            db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = sender.Id, Action = "Messaging.AutomationRead", Reference = automation.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), CreatedUtc = now.AddMinutes(-1) });
            if (notification.ReadUtc.HasValue)
                db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = recipient.Id, Action = "Messaging.Read", Reference = notification.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), CreatedUtc = notification.ReadUtc.Value });
            await db.SaveChangesAsync(ct);
        }
    }
}
