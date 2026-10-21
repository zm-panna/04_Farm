namespace EnterpriseERP.Domain;

/// <summary>A reviewed company-wide accounting denomination. Once approved, currency cannot be relabelled.</summary>
public sealed class CompanyCurrencyLock
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public int CurrencyMinorUnits { get; set; }
    public string ConfirmedBy { get; set; } = "";
    public DateTimeOffset ConfirmedUtc { get; set; }
}
public sealed class CompanyBusinessPolicy
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int Version { get; set; }
    public string CountryCode { get; set; } = "";
    public string RegionCode { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public int CurrencyMinorUnits { get; set; }
    public string TimeZoneId { get; set; } = "";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string ReviewBasis { get; set; } = "";
    public bool ExistingLedgerCurrencyConfirmed { get; set; }
    public string Status { get; set; } = "Draft";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
/// <summary>Explicitly configured exclusive sales tax, not a tax-law rules engine.</summary>
public sealed class TaxPolicy
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public long CompanyBusinessPolicyId { get; set; }
    public string Code { get; set; } = "";
    public int Version { get; set; }
    public string Name { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string RegionCode { get; set; } = "";
    public string TaxTreatment { get; set; } = "";
    public decimal RatePercent { get; set; }
    public string TaxLiabilityAccount { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Status { get; set; } = "Draft";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
