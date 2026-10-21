using EnterpriseERP.Domain;

namespace EnterpriseERP.Web;

/// <summary>Reject precision loss before SQL decimal(18,3) storage. Monetary calculations round once at posting.</summary>
public static class BusinessInput
{
    public const decimal Maximum = 999999999999999.999m;
    public static decimal Decimal(decimal value, string label, bool positive = false, bool allowNegative = false, decimal maximum = Maximum)
    {
        if (value > maximum || value < -maximum || (!allowNegative && value < 0) || (positive && value <= 0) || decimal.Round(value, 3) != value)
            throw new ArgumentException($"{label} must {(positive ? "be positive and " : "")}have at most 3 decimal places and be within the allowed range.");
        return value;
    }
    public static decimal Amount(decimal value, string label = "Amount", bool positive = true) => Decimal(value, label, positive);
    public static decimal Round(decimal value, string label = "Calculated amount") => Decimal(decimal.Round(value, 3, MidpointRounding.AwayFromZero), label);
    public static decimal Total(decimal quantity, decimal price, string label = "Total")
    {
        try { return Amount(Round(checked(quantity * price), label), label); }
        catch (OverflowException) { throw new ArgumentException(label + " exceeds the supported range."); }
    }
    public static string Text(string? value, string label, int maxLength, bool required = true)
    {
        value = value?.Trim() ?? "";
        if ((required && value.Length == 0) || value.Length > maxLength || value.Any(char.IsControl))
            throw new ArgumentException($"{label} is required and must be at most {maxLength} characters without control characters.");
        return value;
    }
    public static string Reference(string? value, int maxLength = 80) => Text(value, "Reference", maxLength);
    public static Voucher Voucher(int company, int branch, string reference, params (string code, decimal debit, decimal credit)[] entries)
    {
        var lines = entries.Where(x => x.debit != 0 || x.credit != 0).Select(x => new VoucherLine
        {
            AccountCode = Text(x.code, "Account code", 20), Debit = Amount(x.debit, "Debit", false), Credit = Amount(x.credit, "Credit", false)
        }).ToList();
        ValidateLines(lines);
        return new Voucher { CompanyId = company, BranchId = branch, Reference = Reference(reference, 100), Posted = true, Lines = lines };
    }
    public static void ValidateLines(IReadOnlyCollection<VoucherLine>? lines)
    {
        if (lines == null || lines.Count < 2 || lines.Count > 200) throw new ArgumentException("A voucher requires 2–200 lines.");
        foreach (var line in lines)
        {
            line.AccountCode = Text(line.AccountCode, "Account code", 20);
            if (line.AccountCode.Length != 4 || !line.AccountCode.All(char.IsAsciiDigit) || line.AccountCode[0] is < '1' or > '6') throw new ArgumentException("Use a four-digit account in configured classes 1xxx through 6xxx.");
            Amount(line.Debit, "Debit", false); Amount(line.Credit, "Credit", false);
            if ((line.Debit > 0) == (line.Credit > 0)) throw new ArgumentException("Each voucher line must contain either a positive debit or a positive credit.");
        }
        var debit = lines.Sum(x => x.Debit); var credit = lines.Sum(x => x.Credit);
        Amount(debit, "Voucher total");
        if (debit != credit) throw new ArgumentException("Voucher debits and credits must balance.");
    }
}
