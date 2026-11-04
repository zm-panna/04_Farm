using System.ComponentModel.DataAnnotations;
using EnterpriseERP.Domain;
namespace EnterpriseERP.Web;

public sealed class FatteningPlanInput
{
    [Range(1, int.MaxValue)] public int AnimalId { get; set; }
    [Required, StringLength(85)] public string Reference { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly TargetDate { get; set; }
    [Range(typeof(decimal), "0.001", "5000")] public decimal InitialWeightKg { get; set; }
    [Range(typeof(decimal), "0.001", "5000")] public decimal TargetWeightKg { get; set; }
    [Range(typeof(decimal), "0", "1000000000000")] public decimal ExpectedSaleAmount { get; set; }
}
public sealed class WeightEntryInput
{
    [Range(1, int.MaxValue)] public int AnimalId { get; set; }
    [Required, StringLength(85)] public string Reference { get; set; } = "";
    public DateOnly WeighedOn { get; set; }
    [Range(typeof(decimal), "0.001", "5000")] public decimal WeightKg { get; set; }
    [StringLength(1000)] public string Notes { get; set; } = "";
}
public sealed class ExpenseInput
{
    public int? AnimalId { get; set; }
    [Required, StringLength(85)] public string Reference { get; set; } = "";
    public DateOnly ExpenseDate { get; set; }
    [Required] public string Category { get; set; } = "Veterinary";
    [Range(typeof(decimal), "0.001", "1000000000000")] public decimal Amount { get; set; }
    [Required, StringLength(1000)] public string Description { get; set; } = "";
}
public sealed class BreedingInput
{
    [Range(1, int.MaxValue)] public int AnimalId { get; set; }
    [Required, StringLength(85)] public string Reference { get; set; } = "";
    public DateOnly ServiceDate { get; set; }
    public DateOnly ExpectedCalvingDate { get; set; }
    [Required] public string Method { get; set; } = "Natural";
    [Required, StringLength(120)] public string SireOrSemen { get; set; } = "";
}
public sealed class BreedingOutcomeInput
{
    [Range(1, long.MaxValue)] public long CycleId { get; set; }
    [Required, StringLength(85)] public string Reference { get; set; } = "";
    [Required] public string Outcome { get; set; } = "Pregnant";
    public DateOnly EventDate { get; set; }
    [Required, StringLength(2000)] public string Notes { get; set; } = "";
}
public sealed record FatteningResult(FatteningPlan Plan, Animal Animal, decimal CurrentWeightKg,
    DateOnly WeighedOn, decimal FeedCost, decimal DirectExpenses, decimal? SaleAmount)
{
    public decimal WeightGainKg => CurrentWeightKg - Plan.InitialWeightKg;
    public int DaysMeasured => WeighedOn.DayNumber - Plan.StartDate.DayNumber;
    public decimal? DailyGainKg => DaysMeasured > 0 ? decimal.Round(WeightGainKg / DaysMeasured, 3) : null;
    public decimal RecordedCost => Animal.PurchaseCost + FeedCost + DirectExpenses;
    public decimal? RealizedMargin => SaleAmount.HasValue ? SaleAmount.Value - RecordedCost : null;
    public decimal ExpectedMargin => Plan.ExpectedSaleAmount - RecordedCost;
}
public sealed class FatteningPage
{
    public FatteningPlanInput Plan { get; set; } = new();
    public WeightEntryInput Weight { get; set; } = new();
    public List<Animal> Animals { get; set; } = new();
    public List<FatteningResult> Results { get; set; } = new();
    public List<AnimalWeightRecord> RecentWeights { get; set; } = new();
}
public sealed record WeightHistoryRow(AnimalWeightRecord Weight, string AnimalTag);
public sealed record WeightHistoryPage(List<WeightHistoryRow> Rows, Controllers.WorkspacePage Page);
public sealed class ExpensesPage
{
    public ExpenseInput Expense { get; set; } = new();
    public List<Animal> Animals { get; set; } = new();
    public List<FarmExpense> Records { get; set; } = new();
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal Total => Records.Sum(x => x.Amount);
}
public sealed class BreedingPage
{
    public DateOnly Today { get; set; }
    public BreedingInput Breeding { get; set; } = new();
    public BreedingOutcomeInput OutcomeEntry { get; set; } = new();
    public List<Animal> Animals { get; set; } = new();
    public List<BreedingCycle> Cycles { get; set; } = new();
    public Dictionary<int, string> AnimalTags { get; set; } = new();
}

public static class FarmBusinessRules
{
    public static readonly IReadOnlyDictionary<string, string> ExpenseAccounts = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Feed"] = "6100", ["Veterinary"] = "6200", ["Transport"] = "6300", ["Labor"] = "6400",
        ["Utilities"] = "6500", ["Maintenance"] = "6600", ["Other"] = "6900"
    };
    public static string Reference(string? value) => BusinessInput.Reference(value, 85);
    public static void DecimalValue(decimal value, string label, decimal maximum, bool allowZero = false) =>
        BusinessInput.Decimal(value, label, positive: !allowZero, maximum: maximum);
    public static void RecordedDate(DateOnly value, DateOnly today)
    {
        if (value < new DateOnly(2000, 1, 1) || value > today)
            throw new ArgumentException("Recorded date must be from 2000 through today in the approved company time zone.");
    }
    public static string Text(string? value, string label, int maximum, bool required = true)
    {
        // Descriptions support normal line breaks; references use the stricter shared validator.
        value = value?.Trim() ?? "";
        if ((required && value.Length == 0) || value.Length > maximum)
            throw new ArgumentException($"{label} is required and must be at most {maximum} characters.");
        return value;
    }
    public static bool ClosesCycle(string status, string outcome)
    {
        var valid = status switch
        {
            "Bred" => outcome is "Pregnant" or "NotPregnant" or "Calved" or "Aborted",
            "Pregnant" => outcome is "Calved" or "Aborted",
            _ => false
        };
        if (!valid) throw new ArgumentException("That outcome is not valid for the cycle's current status. Closed cycles cannot be changed.");
        return outcome is "NotPregnant" or "Calved" or "Aborted";
    }
}
