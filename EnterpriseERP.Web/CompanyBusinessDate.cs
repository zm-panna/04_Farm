using EnterpriseERP.Domain;

namespace EnterpriseERP.Web;

/// <summary>A single captured instant and its reviewed company business day. Never rewrites historical timestamps.</summary>
public sealed record CompanyBusinessDate(DateTimeOffset UtcInstant, DateOnly Date, CompanyBusinessPolicy? Policy)
{
    // Vouchers encode a business date at UTC midnight, as the existing explicitly dated ledgers do.
    // Only a genuinely unconfigured legacy company retains the former UTC-instant posting convention.
    public DateTimeOffset PostingDate => Policy is null ? UtcInstant : AccountingDate(Date);
    public static DateTimeOffset AccountingDate(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public static async Task<CompanyBusinessDate> Current(AppDbContext db, int company, CancellationToken ct = default, TimeProvider? clock = null)
    {
        var instant = (clock ?? TimeProvider.System).GetUtcNow();
        var policy = await CompanyBusinessPolicyService.Configured(db, company, ct, atUtc: instant);
        var date = policy is null ? DateOnly.FromDateTime(instant.UtcDateTime) : CompanyBusinessPolicyService.BusinessDate(policy, instant);
        return new(instant, date, policy);
    }
}
