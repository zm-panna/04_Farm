using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.UnitTests;
/// <summary>Only synthetic unit-test policies. Never called by production bootstrap or demo seed.</summary>
public static class CompanyBusinessPolicyTestData
{
    public static async Task<CompanyBusinessPolicy> SeedAsync(SecurityTestScope f, int company = 1, string currency = "BDT", int minorUnits = 2)
    {
        var existing = await f.Db.CompanyBusinessPolicies.FirstOrDefaultAsync(x => x.CompanyId == company && x.Status == "Approved");
        if (existing != null) return existing;
        var row = new CompanyBusinessPolicy { CompanyId = company, Version = 1, CountryCode = "BD", CurrencyCode = currency, CurrencyMinorUnits = minorUnits,
            TimeZoneId = "Asia/Dhaka", EffectiveFrom = new DateOnly(2000,1,1), ReviewBasis = "Synthetic isolated test policy, not a statutory rate or deployment default.",
            ExistingLedgerCurrencyConfirmed = true, Status = "Approved", CreatedBy = "test-proposer", ApprovedBy = "test-reviewer", ApprovedUtc = DateTimeOffset.UtcNow };
        f.Db.CompanyCurrencyLocks.Add(new CompanyCurrencyLock { CompanyId = company, CurrencyCode = currency, CurrencyMinorUnits = minorUnits, ConfirmedBy = "test-reviewer", ConfirmedUtc = DateTimeOffset.UtcNow });
        f.Db.CompanyBusinessPolicies.Add(row); await f.Db.SaveChangesAsync(); return row;
    }
    public static async Task<TaxPolicy> SeedTaxAsync(SecurityTestScope f, int company = 1, decimal rate = 0, string code = "TEST")
    {
        var business = await SeedAsync(f, company);
        var row = new TaxPolicy { CompanyId = company, CompanyBusinessPolicyId = business.Id, Code = code, Version = 1, Name = "Synthetic test sales tax", CurrencyCode = business.CurrencyCode,
            CountryCode = business.CountryCode, RegionCode = business.RegionCode, TaxTreatment = rate > 0 ? "Standard" : "OutOfScope", RatePercent = rate,
            TaxLiabilityAccount = "2200", Reason = "Synthetic unit test only, not statutory tax guidance", EffectiveFrom = new DateOnly(2000,1,1), Status = "Approved", CreatedBy = "test-proposer", ApprovedBy = "test-reviewer", ApprovedUtc = DateTimeOffset.UtcNow };
        f.Db.TaxPolicies.Add(row); await f.Db.SaveChangesAsync(); return row;
    }
}
