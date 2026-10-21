using System.Data;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

public sealed record BusinessPolicyDraft(string CountryCode, string? RegionCode, string CurrencyCode, int CurrencyMinorUnits,
    string TimeZoneId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string ReviewBasis, bool ExistingLedgerCurrencyConfirmed);
public sealed record TaxPolicyDraft(string Code, string Name, string TaxTreatment, decimal RatePercent, string TaxLiabilityAccount,
    string Reason, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public sealed class CompanyBusinessPolicyService(AppDbContext db, TenantContext tenant, PermissionService permission)
{
    public static async Task<CompanyBusinessPolicy> Require(AppDbContext db, int companyId, DateOnly effectiveDate, CancellationToken ct = default)
    {
        var policy = await db.CompanyBusinessPolicies.AsNoTracking().Where(x => x.CompanyId == companyId && x.Status == "Approved" && x.EffectiveFrom <= effectiveDate)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("A reviewed company jurisdiction, currency and timezone policy is required for this business date.");
        if (policy.EffectiveTo.HasValue && effectiveDate > policy.EffectiveTo.Value) throw new InvalidOperationException("The reviewed company business policy has expired for this date.");
        var denomination = await db.CompanyCurrencyLocks.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == companyId, ct);
        if (denomination == null || denomination.CurrencyCode != policy.CurrencyCode || denomination.CurrencyMinorUnits != policy.CurrencyMinorUnits)
            throw new InvalidOperationException("The company accounting currency has not been consistently reviewed and locked.");
        return policy;
    }
    public static async Task<CompanyBusinessPolicy> Current(AppDbContext db, int companyId, CancellationToken ct = default, DateTimeOffset? atUtc = null)
    {
        var instant = atUtc ?? DateTimeOffset.UtcNow;
        var versions = await db.CompanyBusinessPolicies.AsNoTracking().Where(x => x.CompanyId == companyId && x.Status == "Approved")
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version).ToListAsync(ct);
        var current = versions.FirstOrDefault(x => x.EffectiveFrom <= BusinessDate(x, instant))
            ?? throw new InvalidOperationException("A reviewed company business policy is required for the current local business date.");
        if (current.EffectiveTo.HasValue && BusinessDate(current, instant) > current.EffectiveTo.Value) throw new InvalidOperationException("The reviewed company business policy has expired.");
        var denomination = await db.CompanyCurrencyLocks.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == companyId, ct);
        if (denomination == null || denomination.CurrencyCode != current.CurrencyCode || denomination.CurrencyMinorUnits != current.CurrencyMinorUnits)
            throw new InvalidOperationException("The company accounting currency has not been consistently reviewed and locked.");
        return current;
    }
    // Legacy companies remain explicitly unconfigured. Once any approved currency policy exists,
    // monetary posting must validate its locked denomination; internal carrying-cost precision is separate.
    public static async Task<CompanyBusinessPolicy?> Configured(AppDbContext db, int companyId, CancellationToken ct = default, DateOnly? date = null, DateTimeOffset? atUtc = null)
    {
        var configured = await db.CompanyCurrencyLocks.AsNoTracking().AnyAsync(x => x.CompanyId == companyId, ct)
            || await db.CompanyBusinessPolicies.AsNoTracking().AnyAsync(x => x.CompanyId == companyId && x.Status == "Approved", ct);
        if (!configured) return null;
        return date.HasValue ? await Require(db, companyId, date.Value, ct) : await Current(db, companyId, ct, atUtc);
    }
    public static async Task RequireConfiguredMoney(AppDbContext db, int companyId, decimal amount, CancellationToken ct = default, bool positive = true, DateOnly? date = null, DateTimeOffset? atUtc = null)
    {
        BusinessInput.Amount(amount, "Money amount", positive);
        var policy = await Configured(db, companyId, ct, date, atUtc);
        if (policy != null && RoundMoney(amount, policy.CurrencyMinorUnits) != amount)
            throw new ArgumentException($"The reviewed {policy.CurrencyCode} currency permits {policy.CurrencyMinorUnits} decimal places for document totals and payments. Adjust the new amount or obtain a reviewed correction for historical fractional balances; no automatic revaluation is performed.");
    }
    public static DateOnly BusinessDate(CompanyBusinessPolicy policy, DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId)).DateTime);
    public static decimal RoundMoney(decimal amount, int minorUnits)
    {
        if (minorUnits is < 0 or > 3) throw new ArgumentException("Currency minor units must be from zero through three.");
        return BusinessInput.Decimal(decimal.Round(amount, minorUnits, MidpointRounding.AwayFromZero), "Rounded money", allowNegative: true);
    }
    public static void RequireMoney(decimal amount, int minorUnits, bool positive = false)
    {
        BusinessInput.Amount(amount, "Money amount", positive);
        if (RoundMoney(amount, minorUnits) != amount) throw new ArgumentException("Amount has more decimal places than the reviewed company currency permits.");
    }
    public async Task<CompanyBusinessPolicy> Propose(BusinessPolicyDraft input, CancellationToken ct = default)
    {
        await permission.Demand("BusinessPolicy.Manage", ct); var t = await tenant.GetAsync(ct);
        var country = BusinessInput.Text(input.CountryCode, "Country code", 2).ToUpperInvariant();
        var region = BusinessInput.Text(input.RegionCode, "State or region", 60, false).ToUpperInvariant();
        var currency = BusinessInput.Text(input.CurrencyCode, "Currency code", 3).ToUpperInvariant();
        if (country.Length != 2 || !country.All(char.IsAsciiLetter) || currency.Length != 3 || !currency.All(char.IsAsciiLetter)) throw new ArgumentException("Use two-letter country and three-letter currency codes.");
        if (country == "US" && region.Length == 0) throw new ArgumentException("Specify the US state or tax jurisdiction; country alone is insufficient.");
        if (input.CurrencyMinorUnits is < 0 or > 3) throw new ArgumentException("Currency minor units must be explicitly selected from zero through three.");
        var zone = BusinessInput.Text(input.TimeZoneId, "Timezone", 100);
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(zone); } catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new ArgumentException("Enter an installed IANA or Windows timezone identifier."); }
        DateRange(input.EffectiveFrom, input.EffectiveTo);
        var basis = BusinessInput.Text(input.ReviewBasis, "Review basis", 2000);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var locked = await db.CompanyCurrencyLocks.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company, ct);
            if (locked != null && (locked.CurrencyCode != currency || locked.CurrencyMinorUnits != input.CurrencyMinorUnits)) throw new InvalidOperationException("Company accounting currency and precision are locked. A different currency requires a separately reviewed migration or company.");
            if (await HasExistingMoney(db, t.company, ct) && !input.ExistingLedgerCurrencyConfirmed) throw new ArgumentException("Explicitly confirm that existing ledger, stock values and document amounts use this currency. No conversion will be performed.");
            var latest = await db.CompanyBusinessPolicies.AsNoTracking().Where(x => x.CompanyId == t.company && x.Status == "Approved").OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
            if (latest != null && (input.EffectiveFrom <= latest.EffectiveFrom || input.EffectiveFrom < BusinessDate(latest, DateTimeOffset.UtcNow))) throw new ArgumentException("A replacement policy must start after the existing version and cannot rewrite a past business date.");
            var version = (await db.CompanyBusinessPolicies.Where(x => x.CompanyId == t.company).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0) + 1;
            var row = new CompanyBusinessPolicy { CompanyId = t.company, Version = version, CountryCode = country, RegionCode = region, CurrencyCode = currency,
                CurrencyMinorUnits = input.CurrencyMinorUnits, TimeZoneId = zone, EffectiveFrom = input.EffectiveFrom, EffectiveTo = input.EffectiveTo,
                ReviewBasis = basis, ExistingLedgerCurrencyConfirmed = input.ExistingLedgerCurrencyConfirmed, CreatedBy = t.user };
            db.CompanyBusinessPolicies.Add(row); Audit(t, "BusinessPolicy.Propose", version.ToString());
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return row;
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    public async Task Review(long id, string versionToken, bool approve, CancellationToken ct = default)
    {
        await permission.Demand("BusinessPolicy.Approve", ct); var t = await tenant.GetAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var row = await db.CompanyBusinessPolicies.SingleOrDefaultAsync(x => x.CompanyId == t.company && x.Id == id, ct) ?? throw new ArgumentException("Policy unavailable.");
            OriginalVersion(row.RowVersion, versionToken);
            if (row.Status != "Draft") throw new InvalidOperationException("This policy was already reviewed.");
            if (row.CreatedBy == t.user) throw new InvalidOperationException("A different reviewer must approve or reject this policy.");
            if (approve)
            {
                if (await HasExistingMoney(db, t.company, ct) && !row.ExistingLedgerCurrencyConfirmed) throw new InvalidOperationException("Existing amounts require an explicit currency review before this policy can be approved.");
                var locked = await db.CompanyCurrencyLocks.SingleOrDefaultAsync(x => x.CompanyId == t.company, ct);
                if (locked == null) db.CompanyCurrencyLocks.Add(new CompanyCurrencyLock { CompanyId = t.company, CurrencyCode = row.CurrencyCode, CurrencyMinorUnits = row.CurrencyMinorUnits, ConfirmedBy = t.user, ConfirmedUtc = DateTimeOffset.UtcNow });
                else if (locked.CurrencyCode != row.CurrencyCode || locked.CurrencyMinorUnits != row.CurrencyMinorUnits) throw new InvalidOperationException("The company accounting currency is already locked to another denomination.");
                var latest = await db.CompanyBusinessPolicies.AsNoTracking().Where(x => x.CompanyId == t.company && x.Status == "Approved").OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
                if (latest != null && (row.EffectiveFrom <= latest.EffectiveFrom || row.EffectiveFrom < BusinessDate(latest, DateTimeOffset.UtcNow))) throw new InvalidOperationException("A newer approved policy supersedes this proposal; create a new prospective version.");
            }
            row.Status = approve ? "Approved" : "Rejected"; row.ApprovedBy = t.user; row.ApprovedUtc = DateTimeOffset.UtcNow;
            Audit(t, approve ? "BusinessPolicy.Approve" : "BusinessPolicy.Reject", row.Version.ToString());
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    internal static async Task<bool> HasExistingMoney(AppDbContext db, int company, CancellationToken ct) => await db.Vouchers.AnyAsync(x => x.CompanyId == company && x.Posted, ct)
        || await db.FinanceDocuments.AnyAsync(x => x.CompanyId == company, ct) || await db.StockItems.AnyAsync(x => x.CompanyId == company && (x.QuantityOnHand != 0 || x.UnitCost != 0), ct)
        || await db.Animals.AnyAsync(x => x.CompanyId == company && x.PurchaseCost != 0, ct) || await db.SubscriptionInvoices.AnyAsync(x => x.CompanyId == company, ct);
    internal static void DateRange(DateOnly from, DateOnly? to)
    { if (from < new DateOnly(2000,1,1) || from > new DateOnly(2100,12,31) || to.HasValue && (to.Value < from || to.Value > new DateOnly(2100,12,31))) throw new ArgumentException("Specify a valid policy period from 2000 through 2100."); }
    internal static void OriginalVersion(byte[] current, string token)
    { byte[] expected; try { expected = Convert.FromBase64String(token ?? ""); } catch (FormatException) { throw new ArgumentException("Invalid version token. Refresh the page."); } if (expected.Length == 0 || !current.SequenceEqual(expected)) throw new InvalidOperationException("This record changed. Refresh before reviewing it."); }
    private void Audit((int company, int branch, string user) t, string action, string reference) => db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = action, Reference = reference });
}

public sealed class TaxPolicyService(AppDbContext db, TenantContext tenant, PermissionService permission)
{
    public static async Task<TaxPolicy> Require(AppDbContext db, int companyId, long taxPolicyId, DateOnly date, CancellationToken ct = default)
    {
        var policy = await db.TaxPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == taxPolicyId && x.CompanyId == companyId && x.Status == "Approved", ct)
            ?? throw new InvalidOperationException("Select an explicitly reviewed tax policy for this company.");
        if (date < policy.EffectiveFrom || policy.EffectiveTo.HasValue && date > policy.EffectiveTo.Value) throw new InvalidOperationException("Tax policy is outside its effective period.");
        if (await db.TaxPolicies.AnyAsync(x => x.CompanyId == companyId && x.Code == policy.Code && x.Status == "Approved" && x.EffectiveFrom <= date && x.EffectiveFrom > policy.EffectiveFrom, ct)) throw new InvalidOperationException("A newer tax policy applies to this date; select its reviewed version.");
        var company = await CompanyBusinessPolicyService.Require(db, companyId, date, ct);
        if (policy.CurrencyCode != company.CurrencyCode || policy.CountryCode != company.CountryCode || policy.RegionCode != company.RegionCode) throw new InvalidOperationException("The tax policy does not match the effective company jurisdiction and currency.");
        return policy;
    }
    public static decimal Calculate(decimal net, TaxPolicy policy, int minorUnits)
    {
        CompanyBusinessPolicyService.RequireMoney(net, minorUnits);
        return CompanyBusinessPolicyService.RoundMoney(checked(net * policy.RatePercent / 100m), minorUnits);
    }
    public async Task<TaxPolicy> Propose(TaxPolicyDraft input, CancellationToken ct = default)
    {
        await permission.Demand("TaxPolicy.Manage", ct); var t = await tenant.GetAsync(ct);
        CompanyBusinessPolicyService.DateRange(input.EffectiveFrom, input.EffectiveTo);
        var company = await CompanyBusinessPolicyService.Require(db, t.company, input.EffectiveFrom, ct);
        var code = BusinessInput.Text(input.Code, "Tax policy code", 40).ToUpperInvariant(); var name = BusinessInput.Text(input.Name, "Tax policy name", 120);
        var reason = BusinessInput.Text(input.Reason, "Legal or accounting review basis", 2000);
        BusinessInput.Decimal(input.RatePercent, "Explicit tax percentage", maximum: 100);
        if (input.TaxTreatment is not ("Standard" or "Exempt" or "OutOfScope") || (input.TaxTreatment == "Standard") != (input.RatePercent > 0)) throw new ArgumentException("Standard tax needs a positive explicit rate; zero needs an explicit exemption or out-of-scope basis.");
        var account = BusinessInput.Text(input.TaxLiabilityAccount, "Tax liability account", 4);
        if (account.Length != 4 || account[0] != '2' || !account.All(char.IsAsciiDigit) || account == "2100") throw new ArgumentException("Use a reviewed four-digit liability account other than trade payable 2100.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var latest = await db.TaxPolicies.Where(x => x.CompanyId == t.company && x.Code == code && x.Status == "Approved").OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
            if (latest != null && (input.EffectiveFrom <= latest.EffectiveFrom || input.EffectiveFrom < CompanyBusinessPolicyService.BusinessDate(company, DateTimeOffset.UtcNow))) throw new ArgumentException("Replacement tax policies must be prospective and start after the previous version.");
            var version = (await db.TaxPolicies.Where(x => x.CompanyId == t.company && x.Code == code).Select(x => (int?)x.Version).MaxAsync(ct) ?? 0) + 1;
            var policy = new TaxPolicy { CompanyId = t.company, CompanyBusinessPolicyId = company.Id, Code = code, Version = version, Name = name,
                CurrencyCode = company.CurrencyCode, CountryCode = company.CountryCode, RegionCode = company.RegionCode, TaxTreatment = input.TaxTreatment,
                RatePercent = input.RatePercent, TaxLiabilityAccount = account, Reason = reason, EffectiveFrom = input.EffectiveFrom, EffectiveTo = input.EffectiveTo, CreatedBy = t.user };
            db.TaxPolicies.Add(policy); db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "TaxPolicy.Propose", Reference = code + "/" + version });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return policy;
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    public async Task Review(long id, string versionToken, bool approve, CancellationToken ct = default)
    {
        await permission.Demand("TaxPolicy.Approve", ct); var t = await tenant.GetAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var row = await db.TaxPolicies.SingleOrDefaultAsync(x => x.CompanyId == t.company && x.Id == id, ct) ?? throw new ArgumentException("Tax policy unavailable.");
            CompanyBusinessPolicyService.OriginalVersion(row.RowVersion, versionToken);
            if (row.Status != "Draft" || row.CreatedBy == t.user) throw new InvalidOperationException("An unreviewed tax policy needs a different reviewer.");
            if (approve)
            {
                var company = await CompanyBusinessPolicyService.Require(db, t.company, row.EffectiveFrom, ct);
                if (company.CurrencyCode != row.CurrencyCode || company.CountryCode != row.CountryCode || company.RegionCode != row.RegionCode) throw new InvalidOperationException("Company policy changed; prepare a new matching tax policy.");
                var latest = await db.TaxPolicies.Where(x => x.CompanyId == t.company && x.Code == row.Code && x.Status == "Approved").OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
                if (latest != null && (row.EffectiveFrom <= latest.EffectiveFrom || row.EffectiveFrom < CompanyBusinessPolicyService.BusinessDate(company, DateTimeOffset.UtcNow))) throw new InvalidOperationException("A newer tax policy supersedes this proposal; create a prospective version.");
            }
            row.Status = approve ? "Approved" : "Rejected"; row.ApprovedBy = t.user; row.ApprovedUtc = DateTimeOffset.UtcNow;
            db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = approve ? "TaxPolicy.Approve" : "TaxPolicy.Reject", Reference = row.Code + "/" + row.Version });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
}
