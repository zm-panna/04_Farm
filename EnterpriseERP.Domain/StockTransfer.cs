namespace EnterpriseERP.Domain;

/// <summary>An append-only, atomic movement between two branches of one company.</summary>
public sealed class StockTransfer
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int SourceBranchId { get; set; }
    public int DestinationBranchId { get; set; }
    public int SourceStockItemId { get; set; }
    public int DestinationStockItemId { get; set; }
    public string Reference { get; set; } = "";
    public string Note { get; set; } = "";
    public string SourceBranchName { get; set; } = "";
    public string DestinationBranchName { get; set; } = "";
    public string SourceSku { get; set; } = "";
    public string DestinationSku { get; set; } = "";
    public string SourceProductName { get; set; } = "";
    public string DestinationProductName { get; set; } = "";
    public string UnitOfMeasure { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal CostAmount { get; set; }
    public decimal SourceQuantityBefore { get; set; }
    public decimal SourceQuantityAfter { get; set; }
    public decimal DestinationQuantityBefore { get; set; }
    public decimal DestinationQuantityAfter { get; set; }
    public decimal SourceValueBefore { get; set; }
    public decimal SourceValueAfter { get; set; }
    public decimal DestinationValueBefore { get; set; }
    public decimal DestinationValueAfter { get; set; }
    public long SourceMovementId { get; set; }
    public long DestinationMovementId { get; set; }
    public long? SourceVoucherId { get; set; }
    public long? DestinationVoucherId { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
