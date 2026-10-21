namespace EnterpriseERP.Domain;

// Separate exact inventory value from the rounded informational unit cost.
public sealed class InventoryCostBalance
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int StockItemId { get; set; }
    public decimal Value { get; set; }
}
public sealed class FinanceDocument
{
    public int? PartyId { get; set; }
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string Kind { get; set; } = "";
    public long SourceId { get; set; }
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal CostAmount { get; set; }
    public long VoucherId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class FinancePaymentAllocation
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long FinanceDocumentId { get; set; }
    public long FinancePaymentId { get; set; }
    // Positive reduces the amount due; negative is a customer/supplier refund.
    public decimal SignedAmount { get; set; }
}
public sealed class StockDocumentReturn
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long FinanceDocumentId { get; set; }
    public string Reference { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal CostAmount { get; set; }
    public long VoucherId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TradingParty
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string Kind { get; set; } = "Supplier";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Active { get; set; } = true;
}
/// <summary>Explicitly single-line purchase order; no shipment or supplier communication is sent automatically.</summary>
public sealed class PurchaseOrder
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string Reference { get; set; } = "";
    public int SupplierId { get; set; }
    public int StockItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal ReceivedAmount { get; set; }
    public string Status { get; set; } = "Draft";
    public string CreatedBy { get; set; } = "";
    public string? ApprovedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ApprovedUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
public sealed class PurchaseOrderReceipt
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long PurchaseOrderId { get; set; }
    public long PurchaseReceiptId { get; set; }
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}
public sealed class StockReorderPolicy
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int StockItemId { get; set; }
    public decimal ReorderPoint { get; set; } = 5;
    public decimal TargetQuantity { get; set; } = 10;
}
