namespace EnterpriseERP.Domain;

// Payroll is an explicitly reviewed, daily-rated or monthly prorated accrual. No jurisdictional defaults or bank transfers.
public sealed class HRPayrollStaff
{
 public string PayBasis { get; set; } = "Daily";
 public int Id { get; set; }
 public int CompanyId { get; set; }
 public int BranchId { get; set; }
 public string Code { get; set; } = "";
 public string Name { get; set; } = "";
 public string JobTitle { get; set; } = "";
 public DateOnly StartDate { get; set; }
 public DateOnly? EndDate { get; set; }
 public bool Active { get; set; } = true;
 public string? IdentityUserId { get; set; }
 public decimal DailyRate { get; set; }
 public decimal MonthlySalary { get; set; }
 public decimal OvertimeHourlyRate { get; set; }
 public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}
public sealed class HRPayrollCompensation
{
 public long Id { get; set; }
 public int CompanyId { get; set; }
 public int BranchId { get; set; }
 public int StaffId { get; set; }
 public int Version { get; set; }
 public DateOnly EffectiveFrom { get; set; }
 public string PayBasis { get; set; } = "Daily";
 public decimal DailyRate { get; set; }
 public decimal MonthlySalary { get; set; }
 public decimal OvertimeHourlyRate { get; set; }
 public string ReviewNote { get; set; } = "";
 public string ReviewedBy { get; set; } = "";
 public DateTimeOffset ReviewedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class HRPayrollAttendance
{
 public long Id { get; set; }
 public int CompanyId { get; set; }
 public int BranchId { get; set; }
 public int StaffId { get; set; }
 public DateOnly Date { get; set; }
 public decimal PaidDays { get; set; }
 public decimal OvertimeHours { get; set; }
 public string Note { get; set; } = "";
 public string RecordedBy { get; set; } = "";
 public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}
public sealed class HRPayrollLeave
{
 public long Id { get; set; }
 public int CompanyId { get; set; }
 public int BranchId { get; set; }
 public int StaffId { get; set; }
 public DateOnly From { get; set; }
 public DateOnly Through { get; set; }
 public bool Paid { get; set; }
 public string Reason { get; set; } = "";
 public string Status { get; set; } = "Pending";
 public string RequestedBy { get; set; } = "";
 public string? ReviewedBy { get; set; }
 public string ReviewNote { get; set; } = "";
 public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}
public sealed class HRPayrollPolicy
{
 public string PayBasis { get; set; } = "Daily";
 public string MonthlyProrationMode { get; set; } = "NotApplicable";
 public decimal MonthlyFixedUnits { get; set; }
 public bool CapMonthlyPaidUnits { get; set; }
 public long CompanyBusinessPolicyId { get; set; }
 public string CountryCode { get; set; } = "";
 public string RegionCode { get; set; } = "";
 public string TimeZoneId { get; set; } = "";
 public int Id { get; set; }
 public int CompanyId { get; set; }
 public int BranchId { get; set; }
 public string Name { get; set; } = "";
 public string Currency { get; set; } = "";
 public int CurrencyDecimals { get; set; }
 public DateOnly EffectiveFrom { get; set; }
 public DateOnly EffectiveThrough { get; set; }
 public decimal DeductionPercent { get; set; }
 public decimal EmployerContributionPercent { get; set; }
 public string JurisdictionAndBasis { get; set; } = "";
 public string ExpenseAccount { get; set; } = "";
 public string PayableAccount { get; set; } = "";
 public string DeductionAccount { get; set; } = "";
 public string EmployerExpenseAccount { get; set; } = "";
 public string EmployerPayableAccount { get; set; } = "";
 public string CreatedBy { get; set; } = "";
 public bool Reviewed { get; set; }
 public string? ReviewedBy { get; set; }
 public string ReviewNote { get; set; } = "";
 public string Revision { get; set; } = Guid.NewGuid().ToString("N");
}
public sealed class HRPayrollRun
{
 public long Id { get; set; }
 public int CompanyId { get; set; }
 public int BranchId { get; set; }
 public int PolicyId { get; set; }
 public string Reference { get; set; } = "";
 public DateOnly PeriodStart { get; set; }
 public DateOnly PeriodEnd { get; set; }
 public string Currency { get; set; } = "";
 public int CurrencyDecimals { get; set; }
 public string Status { get; set; } = "Draft";
 public string SourceHash { get; set; } = "";
 public string CreatedBy { get; set; } = "";
 public string? ApprovedBy { get; set; }
 public string ReviewNote { get; set; } = "";
 public DateTimeOffset? ApprovedUtc { get; set; }
 public string? PostedBy { get; set; }
 public DateTimeOffset? PostedUtc { get; set; }
 public DateOnly? PostingDate { get; set; }
 public long? VoucherId { get; set; }
 public string Revision { get; set; } = Guid.NewGuid().ToString("N");
 public List<HRPayrollLine> Lines { get; set; } = new();
}
public sealed class HRPayrollLine
{
 public long CompensationId { get; set; }
 public int CompensationVersion { get; set; }
 public DateOnly CompensationEffectiveFrom { get; set; }
 public string PayBasis { get; set; } = "Daily";
 public string ProrationMode { get; set; } = "NotApplicable";
 public decimal ProrationDenominator { get; set; }
 public decimal PaidUnitsUsed { get; set; }
 public long Id { get; set; }
 public int CompanyId { get; set; }
 public int BranchId { get; set; }
 public long RunId { get; set; }
 public int StaffId { get; set; }
 public string StaffCode { get; set; } = "";
 public string StaffName { get; set; } = "";
 public decimal DailyRate { get; set; }
 public decimal MonthlySalary { get; set; }
 public decimal OvertimeHourlyRate { get; set; }
 public decimal AttendanceDays { get; set; }
 public decimal PaidLeaveDays { get; set; }
 public decimal OvertimeHours { get; set; }
 public decimal BasePay { get; set; }
 public decimal OvertimePay { get; set; }
 public decimal GrossPay { get; set; }
 public decimal Deductions { get; set; }
 public decimal NetPay { get; set; }
 public decimal EmployerContribution { get; set; }
}
