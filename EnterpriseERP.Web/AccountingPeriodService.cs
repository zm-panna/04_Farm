using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed class AccountingPeriodService(AppDbContext db, TenantContext tenant, PermissionService permission, TimeProvider? clock = null)
{
    public async Task Close(DateOnly through, string reason, CancellationToken ct)
    {
        await permission.Demand("Accounts.Close", ct); var t = await tenant.GetAsync(ct);
        reason = BusinessInput.Text(reason, "Close reason", 500);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        if (through < new DateOnly(2000, 1, 1) || through >= businessDay.Date)
            throw new ArgumentException("Close date must be a past day on or after 2000-01-01.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var item = await db.AccountingPeriodCloses.SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (item is not null && item.ClosedThrough >= through) throw new InvalidOperationException("The period is already closed through that date. This interface does not reopen closed books.");
        if (item is null) { item = new AccountingPeriodClose { CompanyId = t.company, BranchId = t.branch }; db.AccountingPeriodCloses.Add(item); }
        item.ClosedThrough = through; item.ClosedBy = t.user; item.ClosedUtc = DateTimeOffset.UtcNow; item.Reason = reason;
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Accounts.PeriodClosed", Reference = $"Branch {t.branch}; through {through:yyyy-MM-dd}" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
