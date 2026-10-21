namespace EnterpriseERP.Domain;
public sealed class InAppNotification
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public string ExecutionKey { get; set; } = "";
    public string Message { get; set; } = "";
    public string Category { get; set; } = "Review";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
