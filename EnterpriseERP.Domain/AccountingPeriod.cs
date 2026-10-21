namespace EnterpriseERP.Domain;
public sealed class AccountingPeriodClose
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public DateOnly ClosedThrough { get; set; }
    public string ClosedBy { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset ClosedUtc { get; set; } = DateTimeOffset.UtcNow;
}
