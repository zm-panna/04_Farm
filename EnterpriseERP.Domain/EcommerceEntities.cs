namespace EnterpriseERP.Domain;

public sealed class EcommerceCustomer
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string UserId { get; set; } = "";
    public int TradingPartyId { get; set; }
    public bool Active { get; set; } = true;
    public string LinkedBy { get; set; } = "";
    public DateTimeOffset LinkedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class EcommerceProduct
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int StockItemId { get; set; }
    public long CompanyPolicyId { get; set; }
    public long TaxPolicyId { get; set; }
    public string CurrencyCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal NetUnitPrice { get; set; }
    public bool Published { get; set; }
    public string RevisionToken { get; set; } = Guid.NewGuid().ToString("N");
}
public sealed class EcommerceCart
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long EcommerceCustomerId { get; set; }
    public string RevisionToken { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset ExpiresUtc { get; set; } = DateTimeOffset.UtcNow.AddMinutes(30);
    public List<EcommerceCartLine> Lines { get; set; } = new();
}
public sealed class EcommerceCartLine
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long EcommerceCartId { get; set; }
    public long EcommerceProductId { get; set; }
    public long CompanyPolicyId { get; set; }
    public long TaxPolicyId { get; set; }
    public string ProductRevisionToken { get; set; } = "";
    public string Title { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal NetUnitPrice { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
}
public sealed class EcommerceOrder
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long EcommerceCustomerId { get; set; }
    public long CompanyPolicyId { get; set; }
    public string Reference { get; set; } = "";
    public string CheckoutToken { get; set; } = "";
    public string StateToken { get; set; } = Guid.NewGuid().ToString("N");
    public string Status { get; set; } = "Submitted";
    public string CustomerName { get; set; } = "";
    public string CurrencyCode { get; set; } = "";
    public int CurrencyMinorUnits { get; set; }
    public string CountryCode { get; set; } = "";
    public string RegionCode { get; set; } = "";
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string PaymentStatus { get; set; } = "PendingUnverified";
    public string DeliveryMethod { get; set; } = "BranchCollection";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "";
    public List<EcommerceOrderLine> Lines { get; set; } = new();
    public List<EcommerceOrderEvent> Events { get; set; } = new();
}
public sealed class EcommerceOrderLine
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long EcommerceOrderId { get; set; }
    public long EcommerceProductId { get; set; }
    public int StockItemId { get; set; }
    public long TaxPolicyId { get; set; }
    public string Title { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal NetUnitPrice { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal TaxRatePercent { get; set; }
    public string TaxTreatment { get; set; } = "";
    public string TaxLiabilityAccount { get; set; } = "";
}
public sealed class EcommerceStockReservation
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int StockItemId { get; set; }
    public long EcommerceOrderId { get; set; }
    public long EcommerceOrderLineId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "Reserved";
}
public sealed class EcommerceOrderEvent
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long EcommerceOrderId { get; set; }
    public string Action { get; set; } = "";
    public string Note { get; set; } = "";
    public string ActorId { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class EcommerceFulfillment
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long EcommerceOrderLineId { get; set; }
    public long ProductSaleId { get; set; }
    public long FinanceDocumentId { get; set; }
    public decimal CostAmount { get; set; }
}
public sealed class EcommerceReturn
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long EcommerceOrderLineId { get; set; }
    public long StockDocumentReturnId { get; set; }
    public string RequestToken { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal NetAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal CostAmount { get; set; }
    public string Reason { get; set; } = "";
}
