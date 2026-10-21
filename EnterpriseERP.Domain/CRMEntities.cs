namespace EnterpriseERP.Domain;

// CRM is an internal pipeline. No entity in this module authorizes outbound communication or accounting.
public sealed class CRMLead
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string Reference { get; set; } = "";
    public string Name { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Source { get; set; } = "";
    public string Notes { get; set; } = "";
    public string MarketingConsent { get; set; } = "Unknown";
    public string ConsentEvidence { get; set; } = "";
    public DateTimeOffset? ConsentRecordedUtc { get; set; }
    public string Status { get; set; } = "Active";
    public int? CustomerId { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CRMConsentEvent
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long LeadId { get; set; }
    public string PreviousConsent { get; set; } = "Unknown";
    public string Consent { get; set; } = "Unknown";
    public string Evidence { get; set; } = "";
    public string ActorId { get; set; } = "";
    public DateTimeOffset RecordedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CRMOpportunity
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long LeadId { get; set; }
    public string Reference { get; set; } = "";
    public string Title { get; set; } = "";
    public string Stage { get; set; } = "Qualified";
    public DateOnly ExpectedCloseDate { get; set; }
    public string ReviewNote { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string ReviewedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CRMActivity
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long LeadId { get; set; }
    public long? OpportunityId { get; set; }
    public string Reference { get; set; } = "";
    public string Kind { get; set; } = "Task";
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = "Open";
    public string CompletionNote { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string? CompletedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CRMQuote
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long OpportunityId { get; set; }
    public long LeadId { get; set; }
    public string Reference { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public long BusinessPolicyId { get; set; }
    public int CurrencyMinorUnits { get; set; }
    public int BusinessPolicyVersion { get; set; }
    public DateOnly QuoteDate { get; set; }
    public DateOnly ValidUntil { get; set; }
    public string Status { get; set; } = "Draft";
    public string CustomerName { get; set; } = "";
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Terms { get; set; } = "";
    public string ReviewNote { get; set; } = "";
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? ReviewedBy { get; set; }
    public string? AcceptedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedUtc { get; set; }
    public DateTimeOffset? AcceptedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public List<CRMQuoteLine> Lines { get; set; } = [];
}
public sealed class CRMQuoteLine
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long QuoteId { get; set; }
    public int LineNumber { get; set; }
    public int StockItemId { get; set; }
    public string SKU { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
}
// An immutable handoff for staff to review before separately posting a sale in the core Sales module.
public sealed class CRMSalesReady
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long QuoteId { get; set; }
    public long OpportunityId { get; set; }
    public long LeadId { get; set; }
    public int CustomerId { get; set; }
    public string Reference { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public long BusinessPolicyId { get; set; }
    public int CurrencyMinorUnits { get; set; }
    public string CustomerCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public decimal NetAmount { get; set; }
    public string AcceptanceEvidence { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
