namespace EnterpriseERP.Domain;

/// <summary>Book policy is explicitly entered and separately reviewed; no statutory tax rate is inferred.</summary>
public sealed class FixedAsset
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string AssetCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateOnly AcquisitionDate { get; set; }
    public decimal Cost { get; set; }
    public string AcquisitionSettlement { get; set; } = "";
    public string SourceReference { get; set; } = "";
    public string DepreciationMethod { get; set; } = "";
    public int UsefulLifeMonths { get; set; }
    public decimal ResidualValue { get; set; }
    public DateOnly DepreciationStart { get; set; }
    public string PolicyReason { get; set; } = "";
    public string ReviewNote { get; set; } = "";
    public long BusinessPolicyId { get; set; }
    public int BusinessPolicyVersion { get; set; }
    public string CurrencyCode { get; set; } = "";
    public int CurrencyMinorUnits { get; set; }
    public string CancellationReason { get; set; } = "";
    public string? CancelledBy { get; set; }
    public DateTimeOffset? CancelledUtc { get; set; }
    public string Status { get; set; } = "Draft";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public string? PostedBy { get; set; }
    public DateTimeOffset? PostedUtc { get; set; }
    public long? AcquisitionVoucherId { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public DateOnly? LastDepreciationPeriod { get; set; }
    public int FinancialVersion { get; set; }
    public string Custodian { get; set; } = "";
    public string Location { get; set; } = "";
    public DateOnly? DisposedOn { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class FixedAssetCustody
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long FixedAssetId { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string PreviousCustodian { get; set; } = "";
    public string PreviousLocation { get; set; } = "";
    public string Custodian { get; set; } = "";
    public string Location { get; set; } = "";
    public string Reason { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class FixedAssetDepreciationRun
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public DateOnly Period { get; set; }
    public string Reference { get; set; } = "";
    public long BusinessPolicyId { get; set; }
    public int BusinessPolicyVersion { get; set; }
    public string CurrencyCode { get; set; } = "";
    public int CurrencyMinorUnits { get; set; }
    public string CancellationReason { get; set; } = "";
    public string? CancelledBy { get; set; }
    public DateTimeOffset? CancelledUtc { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal Total { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public string ReviewNote { get; set; } = "";
    public string? PostedBy { get; set; }
    public DateTimeOffset? PostedUtc { get; set; }
    public long? VoucherId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<FixedAssetDepreciationLine> Lines { get; set; } = [];
}
public sealed class FixedAssetDepreciationLine
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long FixedAssetDepreciationRunId { get; set; }
    public long FixedAssetId { get; set; }
    public string AssetCode { get; set; } = "";
    public DateOnly Period { get; set; }
    public int ExpectedFinancialVersion { get; set; }
    public decimal Cost { get; set; }
    public decimal ResidualValue { get; set; }
    public decimal AccumulatedBefore { get; set; }
    public decimal Amount { get; set; }
    public decimal AccumulatedAfter { get; set; }
    public bool Posted { get; set; }
}
public sealed class FixedAssetDisposal
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long FixedAssetId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly DisposalDate { get; set; }
    public string Counterparty { get; set; } = "";
    public string Reason { get; set; } = "";
    public decimal Proceeds { get; set; }
    public string Settlement { get; set; } = "";
    public decimal Cost { get; set; }
    public decimal AccumulatedDepreciation { get; set; }
    public decimal CarryingValue { get; set; }
    public decimal GainLoss { get; set; }
    public int ExpectedFinancialVersion { get; set; }
    public long BusinessPolicyId { get; set; }
    public int BusinessPolicyVersion { get; set; }
    public string CurrencyCode { get; set; } = "";
    public int CurrencyMinorUnits { get; set; }
    public string CancellationReason { get; set; } = "";
    public string? CancelledBy { get; set; }
    public DateTimeOffset? CancelledUtc { get; set; }
    public string Status { get; set; } = "Draft";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public string ReviewNote { get; set; } = "";
    public string? PostedBy { get; set; }
    public DateTimeOffset? PostedUtc { get; set; }
    public long? VoucherId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
