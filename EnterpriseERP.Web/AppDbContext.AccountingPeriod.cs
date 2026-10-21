using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed partial class AppDbContext
{
    public DbSet<AccountingPeriodClose> AccountingPeriodCloses => Set<AccountingPeriodClose>();
    private void CheckPostedImmutability()
    {
        CheckOpeningImmutability();
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<Voucher>().Any(x => x.State is EntityState.Modified or EntityState.Deleted && x.OriginalValues.GetValue<bool>(nameof(Voucher.Posted))))
            throw new InvalidOperationException("Posted vouchers are immutable. Post an authorized reversal or correction instead.");
        if (ChangeTracker.Entries<VoucherLine>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Posted journal lines cannot be edited or deleted.");
    }
    private async Task ValidateAccountingWritesAsync(CancellationToken ct)
    {
        CheckPostedImmutability();
        var vouchers = ChangeTracker.Entries<Voucher>().Where(x => x.State == EntityState.Added && x.Entity.Posted).Select(x => x.Entity).ToArray();
        if (ChangeTracker.Entries<VoucherLine>().Any(x => x.State == EntityState.Added && !vouchers.Any(v => v.Lines.Contains(x.Entity))))
            throw new InvalidOperationException("Journal lines must be added with their new balanced voucher; posted vouchers cannot be extended.");
        foreach (var voucher in vouchers)
        {
            BusinessInput.ValidateLines(voucher.Lines);
            var closed = await AccountingPeriodCloses.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == voucher.CompanyId && x.BranchId == voucher.BranchId, ct);
            if (closed is not null && DateOnly.FromDateTime(voucher.Date.UtcDateTime) <= closed.ClosedThrough)
                throw new InvalidOperationException("Posting date is in a closed accounting period.");
        }
    }
}
