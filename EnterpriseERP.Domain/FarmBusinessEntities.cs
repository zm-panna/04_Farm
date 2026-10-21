namespace EnterpriseERP.Domain;

/// <summary>One fattening plan per animal. Actual results are derived from weight, feed, expense and sale ledgers.</summary>
public sealed class FatteningPlan
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int AnimalId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly TargetDate { get; set; }
    public decimal InitialWeightKg { get; set; }
    public decimal TargetWeightKg { get; set; }
    public decimal ExpectedSaleAmount { get; set; }
    public string RecordedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AnimalWeightRecord
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int AnimalId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly WeighedOn { get; set; }
    public decimal WeightKg { get; set; }
    public string Notes { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class FarmExpense
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? AnimalId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly ExpenseDate { get; set; }
    public string Category { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
    public long VoucherId { get; set; }
    public Voucher Voucher { get; set; } = null!;
    public string RecordedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BreedingCycle
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int AnimalId { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly ServiceDate { get; set; }
    public DateOnly ExpectedCalvingDate { get; set; }
    public string Method { get; set; } = "Natural";
    public string SireOrSemen { get; set; } = "";
    public string Status { get; set; } = "Bred";
    public bool IsClosed { get; set; }
    public DateOnly? LastEventDate { get; set; }
    public string RecordedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<BreedingCycleEvent> Events { get; set; } = new();
}

/// <summary>Append-only outcome history; a calving event does not silently create an animal or inventory valuation.</summary>
public sealed class BreedingCycleEvent
{
    public long Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public long BreedingCycleId { get; set; }
    public string Reference { get; set; } = "";
    public string Outcome { get; set; } = "";
    public DateOnly EventDate { get; set; }
    public string Notes { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
