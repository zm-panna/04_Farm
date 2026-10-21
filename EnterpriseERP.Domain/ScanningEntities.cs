namespace EnterpriseERP.Domain;

/// <summary>A label is an identifier, never an authentication credential. Revoked values are never reused.</summary>
public sealed class ScanLabel
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string Kind { get; set; } = "";
    public string Format { get; set; } = "Internal";
    public string Value { get; set; } = "";
    public string ValueHash { get; set; } = "";
    public int? StockItemId { get; set; }
    public int? AnimalId { get; set; }
    public long? FixedAssetId { get; set; }
    public long? PurchaseOrderId { get; set; }
    public long? FinanceDocumentId { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
    public string? RevokedBy { get; set; }
    public string RevocationReason { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>User-owned immutable reviewed command; outcome changes in the business transaction, never after it.</summary>
public sealed class ScanIntent
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string OwnerUserId { get; set; } = "";
    public string RequestKey { get; set; } = "";
    public string Action { get; set; } = "";
    public long ScanLabelId { get; set; }
    public int? SecondaryBranchId { get; set; }
    public long? SecondaryLabelId { get; set; }
    public string PayloadJson { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string ReviewJson { get; set; } = "";
    public string BusinessReference { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
