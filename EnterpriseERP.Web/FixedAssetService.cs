using System.ComponentModel.DataAnnotations;
using System.Data;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed class FixedAssetInput
{
    [Required, StringLength(60)] public string AssetCode { get; set; } = "";
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [Required, StringLength(120)] public string Category { get; set; } = "";
    [StringLength(160)] public string SerialNumber { get; set; } = "";
    [StringLength(1000)] public string Notes { get; set; } = "";
    public DateOnly AcquisitionDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public decimal Cost { get; set; }
    [Required] public string AcquisitionSettlement { get; set; } = "";
    [Required, StringLength(80)] public string SourceReference { get; set; } = "";
    [Required] public string DepreciationMethod { get; set; } = "";
    public int UsefulLifeMonths { get; set; }
    public decimal ResidualValue { get; set; }
    public DateOnly DepreciationStart { get; set; } = new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
    [Required, StringLength(1000)] public string PolicyReason { get; set; } = "";
    [Required, StringLength(160)] public string Custodian { get; set; } = "";
    [Required, StringLength(160)] public string Location { get; set; } = "";
}
public sealed class FixedAssetMetadataInput
{
    public long Id { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [Required, StringLength(120)] public string Category { get; set; } = "";
    [StringLength(160)] public string SerialNumber { get; set; } = "";
    [StringLength(1000)] public string Notes { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
}
public sealed class FixedAssetActionInput
{
    public long Id { get; set; }
    public byte[] RowVersion { get; set; } = [];
    [StringLength(1000)] public string ReviewNote { get; set; } = "";
    public bool ConfirmReviewed { get; set; }
}
public sealed class FixedAssetCustodyInput
{
    public long Id { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Required, StringLength(160)] public string Custodian { get; set; } = "";
    [Required, StringLength(160)] public string Location { get; set; } = "";
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
}
public sealed class FixedAssetRunInput
{
    [Required, StringLength(80)] public string Reference { get; set; } = "";
    public DateOnly Period { get; set; } = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddDays(-1);
}
public sealed class FixedAssetDisposalInput
{
    public long FixedAssetId { get; set; }
    public byte[] AssetRowVersion { get; set; } = [];
    [Required, StringLength(80)] public string Reference { get; set; } = "";
    public DateOnly DisposalDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Required, StringLength(160)] public string Counterparty { get; set; } = "";
    [Required, StringLength(1000)] public string Reason { get; set; } = "";
    public decimal Proceeds { get; set; }
    [Required] public string Settlement { get; set; } = "";
}

/// <summary>Records book entries only. Never sends money, submits taxes, or assumes a jurisdiction's tax rules.</summary>
public sealed class FixedAssetService(AppDbContext db, TenantContext tenant, PermissionService permission)
{
    private async Task<T> Write<T>(string grant, Func<(int company, int branch, string user), Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var previous = db.FixedAssetWorkflowWrite;
        try
        {
            await permission.Demand(grant, ct); var t = await tenant.GetAsync(ct);
            db.FixedAssetWorkflowWrite = true;
            var result = await action(t); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result;
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.FixedAssetWorkflowWrite = previous; }
    }
    private async Task<FixedAsset> Asset(long id, (int company, int branch, string user) t, CancellationToken ct) =>
        await db.FixedAssets.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct)
            ?? throw new KeyNotFoundException("Asset not found in the active company and branch.");
    private void Token<TEntity>(TEntity entity, byte[]? token) where TEntity : class
    {
        var property = db.Entry(entity).Property<byte[]>("RowVersion");
        if (token is not { Length: > 0 } || !property.CurrentValue.SequenceEqual(token))
            throw new DbUpdateConcurrencyException("This record changed, or the original form token is missing. Reload and review it again; no changes were saved.");
        property.OriginalValue = token.ToArray();
    }
    private void Audit((int company, int branch, string user) t, string action, string reference) =>
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "FixedAssets." + action, Reference = $"Branch {t.branch}; {reference}" });
    public static decimal CurrencyAmount(decimal amount, int minorUnits, string label)
    {
        BusinessInput.Amount(amount, label, false);
        if (minorUnits is < 0 or > 3 || decimal.Round(amount, minorUnits) != amount)
            throw new ArgumentException($"{label} must use the approved currency's {minorUnits} decimal places.");
        return amount;
    }
    public static DateOnly MonthEnd(DateOnly date) => new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
    public static DateOnly NextPeriod(FixedAsset a) => a.LastDepreciationPeriod.HasValue ? MonthEnd(a.LastDepreciationPeriod.Value.AddDays(1)) : MonthEnd(a.DepreciationStart);
    public static decimal DepreciationAmount(FixedAsset a, DateOnly period)
    {
        if (a.Status != "Active" || a.DepreciationMethod != "StraightLineMonthly" || a.Cost <= a.ResidualValue || a.AccumulatedDepreciation >= a.Cost - a.ResidualValue) return 0;
        if (period != MonthEnd(period) || NextPeriod(a) != period) throw new InvalidOperationException("Depreciation must be posted one completed month at a time, without skipped or repeated periods.");
        var months = (period.Year - a.DepreciationStart.Year) * 12 + period.Month - a.DepreciationStart.Month + 1;
        var target = decimal.Round((a.Cost - a.ResidualValue) * Math.Min(months, a.UsefulLifeMonths) / a.UsefulLifeMonths, a.CurrencyMinorUnits, MidpointRounding.AwayFromZero);
        return CurrencyAmount(Math.Max(0, Math.Min(a.Cost - a.ResidualValue - a.AccumulatedDepreciation, target - a.AccumulatedDepreciation)), a.CurrencyMinorUnits, "Depreciation");
    }
    private async Task OpenDate(int company, int branch, DateOnly date, CancellationToken ct)
    {
        var policy = await CompanyBusinessPolicyService.Require(db, company, date, ct);
        if (date < new DateOnly(2000, 1, 1) || date > CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow))
            throw new ArgumentException("Recorded date must be from 2000 through today in the approved company time zone.");
        if (await db.AccountingPeriodCloses.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.BranchId == branch && x.ClosedThrough >= date, ct))
            throw new InvalidOperationException("Posting date is in a closed accounting period.");
    }
    private static DateTimeOffset PostingDate(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    private static string Review(FixedAssetActionInput input, string creator, string approver)
    {
        if (!input.ConfirmReviewed) throw new ArgumentException("Confirm that you reviewed the source evidence, policy, amounts and journal before approval.");
        if (creator == approver) throw new InvalidOperationException("A different authorized user must approve this record.");
        return BusinessInput.Text(input.ReviewNote, "Review note", 1000);
    }
    public Task<FixedAsset> Create(FixedAssetInput input, CancellationToken ct = default) => Write("FixedAssets.Create", async t =>
    {
        var policy = await CompanyBusinessPolicyService.Require(db, t.company, input.AcquisitionDate, ct);
        await OpenDate(t.company, t.branch, input.AcquisitionDate, ct);
        var cost = CurrencyAmount(input.Cost, policy.CurrencyMinorUnits, "Acquisition carrying cost");
        var residual = CurrencyAmount(input.ResidualValue, policy.CurrencyMinorUnits, "Residual value");
        if (residual > cost) throw new ArgumentException("Residual value cannot exceed acquisition cost.");
        if (input.AcquisitionSettlement is not ("Cash" or "Payable")) throw new ArgumentException("Select cash/bank recorded or other payable recorded.");
        if (input.DepreciationMethod is not ("StraightLineMonthly" or "None")) throw new ArgumentException("Select an explicit depreciation method.");
        if (input.DepreciationStart < input.AcquisitionDate || input.DepreciationStart.Year > 9998)
            throw new ArgumentException("Depreciation start must be on or after acquisition and before year 9999.");
        if (input.DepreciationMethod == "StraightLineMonthly" && (input.UsefulLifeMonths is < 1 or > 1200 || input.DepreciationStart.Day != 1))
            throw new ArgumentException("Monthly straight line requires 1–1200 useful-life months and an explicit first-of-month start. Partial-month depreciation is not inferred.");
        if (input.DepreciationMethod == "None" && (input.UsefulLifeMonths != 0 || residual != cost))
            throw new ArgumentException("Non-depreciable assets require zero life months and residual value equal to cost, with a documented reason.");
        var asset = new FixedAsset {
            CompanyId = t.company, BranchId = t.branch, AssetCode = BusinessInput.Text(input.AssetCode, "Asset code", 60).ToUpperInvariant(),
            SourceReference = BusinessInput.Reference(input.SourceReference).ToUpperInvariant(), Name = BusinessInput.Text(input.Name, "Asset name", 160),
            Category = BusinessInput.Text(input.Category, "Category", 120), SerialNumber = BusinessInput.Text(input.SerialNumber, "Serial number", 160, false), Notes = BusinessInput.Text(input.Notes, "Notes", 1000, false),
            AcquisitionDate = input.AcquisitionDate, Cost = cost, AcquisitionSettlement = input.AcquisitionSettlement, DepreciationMethod = input.DepreciationMethod,
            UsefulLifeMonths = input.UsefulLifeMonths, ResidualValue = residual, DepreciationStart = input.DepreciationStart, PolicyReason = BusinessInput.Text(input.PolicyReason, "Policy reason and source evidence", 1000),
            Custodian = BusinessInput.Text(input.Custodian, "Custodian", 160), Location = BusinessInput.Text(input.Location, "Location", 160), CreatedBy = t.user,
            BusinessPolicyId = policy.Id, BusinessPolicyVersion = policy.Version, CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits
        };
        if (await db.FixedAssets.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && (x.AssetCode == asset.AssetCode || x.SourceReference == asset.SourceReference), ct))
            throw new InvalidOperationException("Asset code or source reference already exists. Cancelled references remain reserved to preserve history.");
        db.FixedAssets.Add(asset); await db.SaveChangesAsync(ct); Audit(t, "Create", $"Asset {asset.Id}; {asset.AssetCode}"); return asset;
    }, ct);
    public Task<FixedAsset> ApproveAcquisition(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Approve", async t =>
    {
        var a = await Asset(input.Id, t, ct); Token(a, input.RowVersion);
        if (a.Status != "Draft") throw new InvalidOperationException("Only a draft acquisition can be approved.");
        await OpenDate(t.company, t.branch, a.AcquisitionDate, ct);
        a.ReviewNote = Review(input, a.CreatedBy, t.user); a.Status = "Approved"; a.ApprovedBy = t.user; a.ApprovedUtc = DateTimeOffset.UtcNow;
        Audit(t, "AcquisitionApprove", $"Asset {a.Id}; policy {a.DepreciationMethod}/{a.UsefulLifeMonths} months"); return a;
    }, ct);
    public Task<FixedAsset> PostAcquisition(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Post", async t =>
    {
        var a = await Asset(input.Id, t, ct); Token(a, input.RowVersion);
        if (a.Status != "Approved" || a.ApprovedBy == a.CreatedBy || string.IsNullOrWhiteSpace(a.ApprovedBy)) throw new InvalidOperationException("Acquisition requires a different user's completed approval.");
        await OpenDate(t.company, t.branch, a.AcquisitionDate, ct);
        var policy = await CompanyBusinessPolicyService.Require(db, t.company, a.AcquisitionDate, ct);
        if (policy.Id != a.BusinessPolicyId) throw new InvalidOperationException("Company policy changed. Cancel this unposted acquisition and prepare it under the current approved policy.");
        if (a.Cost > 0)
        {
            var voucher = BusinessInput.Voucher(t.company, t.branch, $"FA-ACQ-{t.branch}-{a.Id}", ("1600", a.Cost, 0), (a.AcquisitionSettlement == "Cash" ? "1000" : "2500", 0, a.Cost));
            voucher.Date = PostingDate(a.AcquisitionDate); db.Vouchers.Add(voucher); await db.SaveChangesAsync(ct); a.AcquisitionVoucherId = voucher.Id;
        }
        a.Status = "Active"; a.PostedBy = t.user; a.PostedUtc = DateTimeOffset.UtcNow; a.FinancialVersion++;
        db.FixedAssetCustodies.Add(new FixedAssetCustody { CompanyId = t.company, BranchId = t.branch, FixedAssetId = a.Id, EffectiveDate = a.AcquisitionDate, Custodian = a.Custodian, Location = a.Location, Reason = "Initial custody at acquisition posting", RecordedBy = t.user });
        Audit(t, "AcquisitionPost", $"Asset {a.Id}; voucher {a.AcquisitionVoucherId}; {a.CurrencyCode} {a.Cost}"); return a;
    }, ct);
    public Task<FixedAsset> CancelAcquisition(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Create", async t =>
    {
        var a = await Asset(input.Id, t, ct); Token(a, input.RowVersion);
        if (a.Status is not ("Draft" or "Approved")) throw new InvalidOperationException("Only an unposted acquisition can be cancelled.");
        a.CancellationReason = BusinessInput.Text(input.ReviewNote, "Cancellation reason", 1000); a.CancelledBy = t.user; a.CancelledUtc = DateTimeOffset.UtcNow; a.Status = "Cancelled";
        Audit(t, "AcquisitionCancel", $"Asset {a.Id}"); return a;
    }, ct);
    public Task<FixedAsset> EditMetadata(FixedAssetMetadataInput input, CancellationToken ct = default) => Write("FixedAssets.Edit", async t =>
    {
        var a = await Asset(input.Id, t, ct); Token(a, input.RowVersion);
        if (a.Status == "Cancelled") throw new InvalidOperationException("Cancelled acquisition records are immutable.");
        a.Name = BusinessInput.Text(input.Name, "Asset name", 160); a.Category = BusinessInput.Text(input.Category, "Category", 120);
        a.SerialNumber = BusinessInput.Text(input.SerialNumber, "Serial number", 160, false); a.Notes = BusinessInput.Text(input.Notes, "Notes", 1000, false);
        Audit(t, "MetadataEdit", $"Asset {a.Id}"); return a;
    }, ct);
    public Task<FixedAsset> ChangeCustody(FixedAssetCustodyInput input, CancellationToken ct = default) => Write("FixedAssets.Custody", async t =>
    {
        await db.ValidatePendingScan("Custody", ct);
        var a = await Asset(input.Id, t, ct); Token(a, input.RowVersion);
        if (a.Status != "Active") throw new InvalidOperationException("Only an active asset can change custody.");
        var policy = await CompanyBusinessPolicyService.Require(db, t.company, input.EffectiveDate, ct);
        if (input.EffectiveDate < a.AcquisitionDate || input.EffectiveDate > CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow)) throw new ArgumentException("Custody date must be between acquisition and today.");
        var previous = await db.FixedAssetCustodies.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FixedAssetId == a.Id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (previous is not null && input.EffectiveDate < previous.EffectiveDate) throw new ArgumentException("Custody cannot be backdated before the latest custody event.");
        var custodian = BusinessInput.Text(input.Custodian, "Custodian", 160); var location = BusinessInput.Text(input.Location, "Location", 160);
        if (custodian == a.Custodian && location == a.Location) throw new ArgumentException("Choose a changed custodian or location.");
        db.FixedAssetCustodies.Add(new FixedAssetCustody { CompanyId = t.company, BranchId = t.branch, FixedAssetId = a.Id, EffectiveDate = input.EffectiveDate,
            PreviousCustodian = a.Custodian, PreviousLocation = a.Location, Custodian = custodian, Location = location, Reason = BusinessInput.Text(input.Reason, "Custody reason", 1000), RecordedBy = t.user });
        a.Custodian = custodian; a.Location = location; Audit(t, "Custody", $"Asset {a.Id}"); return a;
    }, ct);
    private async Task<List<FixedAsset>> Eligible((int company, int branch, string user) t, DateOnly period, CancellationToken ct)
    {
        var assets = await db.FixedAssets.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Status == "Active" && x.DepreciationMethod == "StraightLineMonthly" && x.DepreciationStart <= period).OrderBy(x => x.Id).ToListAsync(ct);
        var remaining = assets.Where(x => x.AccumulatedDepreciation < x.Cost - x.ResidualValue && NextPeriod(x) <= period).ToList();
        if (remaining.Any(x => NextPeriod(x) < period)) throw new InvalidOperationException("Earlier depreciation periods are missing. Post the earliest due month first.");
        if (remaining.Count > 5000) throw new InvalidOperationException("This run exceeds the supported 5,000-asset limit. Contact the administrator before posting.");
        return remaining;
    }
    public Task<FixedAssetDepreciationRun> CreateRun(FixedAssetRunInput input, CancellationToken ct = default) => Write("FixedAssets.Depreciate", async t =>
    {
        await OpenDate(t.company, t.branch, input.Period, ct);
        if (input.Period != MonthEnd(input.Period)) throw new ArgumentException("Select the last day of a completed month.");
        var policy = await CompanyBusinessPolicyService.Require(db, t.company, input.Period, ct);
        if (await db.FixedAssetDepreciationRuns.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Period == input.Period && x.Status != "Cancelled" && x.Status != "Posted", ct))
            throw new InvalidOperationException("An unposted run already exists for this branch and month. Review or cancel that run before retrying.");
        var assets = await Eligible(t, input.Period, ct);
        if (assets.Count == 0) throw new InvalidOperationException("No depreciable active assets are due in this month. Zero-cost, non-depreciable, disposed and residual-floor assets are excluded.");
        var run = new FixedAssetDepreciationRun { CompanyId = t.company, BranchId = t.branch, Period = input.Period, Reference = BusinessInput.Reference(input.Reference).ToUpperInvariant(), CreatedBy = t.user,
            BusinessPolicyId = policy.Id, BusinessPolicyVersion = policy.Version, CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits };
        foreach (var a in assets)
        {
            if (a.CurrencyCode != policy.CurrencyCode || a.CurrencyMinorUnits != policy.CurrencyMinorUnits) throw new InvalidOperationException("Asset and approved company currency settings differ; a separately reviewed migration is required.");
            var amount = DepreciationAmount(a, input.Period);
            run.Lines.Add(new FixedAssetDepreciationLine { CompanyId = t.company, BranchId = t.branch, FixedAssetId = a.Id, AssetCode = a.AssetCode, Period = input.Period,
                ExpectedFinancialVersion = a.FinancialVersion, Cost = a.Cost, ResidualValue = a.ResidualValue, AccumulatedBefore = a.AccumulatedDepreciation, Amount = amount, AccumulatedAfter = a.AccumulatedDepreciation + amount });
        }
        run.Total = BusinessInput.Amount(run.Lines.Sum(x => x.Amount), "Run total", false); db.FixedAssetDepreciationRuns.Add(run); await db.SaveChangesAsync(ct);
        Audit(t, "DepreciationDraft", $"Run {run.Id}; period {run.Period:yyyy-MM-dd}"); return run;
    }, ct);
    private async Task<FixedAssetDepreciationRun> Run(long id, (int company, int branch, string user) t, CancellationToken ct) =>
        await db.FixedAssetDepreciationRuns.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct)
            ?? throw new KeyNotFoundException("Depreciation run not found in this branch.");
    private async Task<List<FixedAsset>> ValidateRun(FixedAssetDepreciationRun run, (int company, int branch, string user) t, CancellationToken ct)
    {
        await OpenDate(t.company, t.branch, run.Period, ct); var policy = await CompanyBusinessPolicyService.Require(db, t.company, run.Period, ct);
        if (policy.Id != run.BusinessPolicyId) throw new InvalidOperationException("Company policy changed. Cancel and recreate this run.");
        var assets = await Eligible(t, run.Period, ct);
        if (assets.Count != run.Lines.Count || run.Total != run.Lines.Sum(x => x.Amount)) throw new InvalidOperationException("The run's eligible assets changed. Cancel and recreate the run for review.");
        foreach (var line in run.Lines)
        {
            var a = assets.SingleOrDefault(x => x.Id == line.FixedAssetId);
            if (a is null || a.FinancialVersion != line.ExpectedFinancialVersion || a.AccumulatedDepreciation != line.AccumulatedBefore || line.Amount != DepreciationAmount(a, run.Period)
                || line.AccumulatedAfter != line.AccumulatedBefore + line.Amount || line.Cost != a.Cost || line.ResidualValue != a.ResidualValue || line.Posted)
                throw new InvalidOperationException("An asset changed after this preview. Cancel and recreate the run; no partial posting is allowed.");
        }
        return assets;
    }
    public Task<FixedAssetDepreciationRun> ApproveRun(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Approve", async t =>
    {
        var r = await Run(input.Id, t, ct); Token(r, input.RowVersion);
        if (r.Status != "Draft") throw new InvalidOperationException("Only a draft depreciation run can be approved.");
        await ValidateRun(r, t, ct); r.ReviewNote = Review(input, r.CreatedBy, t.user); r.Status = "Approved"; r.ApprovedBy = t.user; r.ApprovedUtc = DateTimeOffset.UtcNow;
        Audit(t, "DepreciationApprove", $"Run {r.Id}"); return r;
    }, ct);
    public Task<FixedAssetDepreciationRun> PostRun(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Post", async t =>
    {
        var r = await Run(input.Id, t, ct); Token(r, input.RowVersion);
        if (r.Status != "Approved" || r.ApprovedBy == r.CreatedBy || string.IsNullOrEmpty(r.ApprovedBy)) throw new InvalidOperationException("Depreciation requires a different user's approval before posting.");
        var assets = await ValidateRun(r, t, ct);
        if (r.Total > 0)
        {
            var voucher = BusinessInput.Voucher(t.company, t.branch, $"FA-DEP-{t.branch}-{r.Id}", ("6800", r.Total, 0), ("1690", 0, r.Total));
            voucher.Date = PostingDate(r.Period); db.Vouchers.Add(voucher); await db.SaveChangesAsync(ct); r.VoucherId = voucher.Id;
        }
        foreach (var line in r.Lines) { var a = assets.Single(x => x.Id == line.FixedAssetId); a.AccumulatedDepreciation = line.AccumulatedAfter; a.LastDepreciationPeriod = r.Period; a.FinancialVersion++; line.Posted = true; }
        r.Status = "Posted"; r.PostedBy = t.user; r.PostedUtc = DateTimeOffset.UtcNow;
        Audit(t, "DepreciationPost", $"Run {r.Id}; voucher {r.VoucherId}; {r.CurrencyCode} {r.Total}"); return r;
    }, ct);
    public Task<FixedAssetDepreciationRun> CancelRun(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Depreciate", async t =>
    {
        var r = await Run(input.Id, t, ct); Token(r, input.RowVersion);
        if (r.Status is not ("Draft" or "Approved")) throw new InvalidOperationException("Only an unposted run can be cancelled.");
        r.CancellationReason = BusinessInput.Text(input.ReviewNote, "Cancellation reason", 1000); r.CancelledBy = t.user; r.CancelledUtc = DateTimeOffset.UtcNow; r.Status = "Cancelled";
        Audit(t, "DepreciationCancel", $"Run {r.Id}"); return r;
    }, ct);
    private static void DisposalDate(FixedAsset a, DateOnly date)
    {
        if (date < a.AcquisitionDate || a.LastDepreciationPeriod.HasValue && date < a.LastDepreciationPeriod.Value) throw new ArgumentException("Disposal cannot predate acquisition or already-posted depreciation.");
        if (a.DepreciationMethod == "StraightLineMonthly" && a.AccumulatedDepreciation < a.Cost - a.ResidualValue && NextPeriod(a) < new DateOnly(date.Year, date.Month, 1))
            throw new InvalidOperationException("Post depreciation through the month before disposal first. Disposal does not invent partial-month depreciation.");
    }
    public Task<FixedAssetDisposal> CreateDisposal(FixedAssetDisposalInput input, CancellationToken ct = default) => Write("FixedAssets.Dispose", async t =>
    {
        var a = await Asset(input.FixedAssetId, t, ct); Token(a, input.AssetRowVersion);
        if (a.Status != "Active") throw new InvalidOperationException("Only an active asset can be disposed.");
        await OpenDate(t.company, t.branch, input.DisposalDate, ct); DisposalDate(a, input.DisposalDate);
        if (input.Settlement is not ("Cash" or "Receivable")) throw new ArgumentException("Select cash/bank or other receivable recorded.");
        var policy = await CompanyBusinessPolicyService.Require(db, t.company, input.DisposalDate, ct);
        if (policy.CurrencyCode != a.CurrencyCode || policy.CurrencyMinorUnits != a.CurrencyMinorUnits) throw new InvalidOperationException("Company and asset currency policies differ.");
        var proceeds = CurrencyAmount(input.Proceeds, a.CurrencyMinorUnits, "Disposal proceeds");
        if (await db.FixedAssetDisposals.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FixedAssetId == a.Id && x.Status != "Cancelled", ct))
            throw new InvalidOperationException("An uncancelled disposal already exists for this asset.");
        var d = new FixedAssetDisposal { CompanyId = t.company, BranchId = t.branch, FixedAssetId = a.Id, Reference = BusinessInput.Reference(input.Reference).ToUpperInvariant(),
            DisposalDate = input.DisposalDate, Counterparty = BusinessInput.Text(input.Counterparty, "Buyer / disposal recipient", 160), Reason = BusinessInput.Text(input.Reason, "Disposal reason", 1000),
            Proceeds = proceeds, Settlement = input.Settlement, Cost = a.Cost, AccumulatedDepreciation = a.AccumulatedDepreciation, CarryingValue = a.Cost - a.AccumulatedDepreciation,
            GainLoss = proceeds - (a.Cost - a.AccumulatedDepreciation), ExpectedFinancialVersion = a.FinancialVersion, CreatedBy = t.user,
            BusinessPolicyId = policy.Id, BusinessPolicyVersion = policy.Version, CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits };
        db.FixedAssetDisposals.Add(d); await db.SaveChangesAsync(ct); Audit(t, "DisposalDraft", $"Asset {a.Id}; disposal {d.Id}"); return d;
    }, ct);
    private async Task<FixedAssetDisposal> Disposal(long id, (int company, int branch, string user) t, CancellationToken ct) =>
        await db.FixedAssetDisposals.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct)
            ?? throw new KeyNotFoundException("Disposal not found in this branch.");
    private async Task<FixedAsset> ValidateDisposal(FixedAssetDisposal d, (int company, int branch, string user) t, CancellationToken ct)
    {
        var a = await Asset(d.FixedAssetId, t, ct);
        if (a.Status != "Active" || a.FinancialVersion != d.ExpectedFinancialVersion || a.Cost != d.Cost || a.AccumulatedDepreciation != d.AccumulatedDepreciation)
            throw new InvalidOperationException("Asset financial values changed after this preview. Cancel and recreate this disposal.");
        await OpenDate(t.company, t.branch, d.DisposalDate, ct); DisposalDate(a, d.DisposalDate);
        if ((await CompanyBusinessPolicyService.Require(db, t.company, d.DisposalDate, ct)).Id != d.BusinessPolicyId) throw new InvalidOperationException("Company policy changed. Cancel and recreate the disposal.");
        return a;
    }
    public Task<FixedAssetDisposal> ApproveDisposal(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Approve", async t =>
    {
        var d = await Disposal(input.Id, t, ct); Token(d, input.RowVersion);
        if (d.Status != "Draft") throw new InvalidOperationException("Only a draft disposal can be approved.");
        await ValidateDisposal(d, t, ct); d.ReviewNote = Review(input, d.CreatedBy, t.user); d.Status = "Approved"; d.ApprovedBy = t.user; d.ApprovedUtc = DateTimeOffset.UtcNow;
        Audit(t, "DisposalApprove", $"Asset {d.FixedAssetId}; disposal {d.Id}"); return d;
    }, ct);
    public Task<FixedAssetDisposal> PostDisposal(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Post", async t =>
    {
        var d = await Disposal(input.Id, t, ct); Token(d, input.RowVersion);
        if (d.Status != "Approved" || d.ApprovedBy == d.CreatedBy || string.IsNullOrWhiteSpace(d.ApprovedBy)) throw new InvalidOperationException("Disposal requires a different user's approval before posting.");
        var a = await ValidateDisposal(d, t, ct);
        if (d.Cost > 0 || d.Proceeds > 0)
        {
            var voucher = BusinessInput.Voucher(t.company, t.branch, $"FA-DSP-{t.branch}-{d.Id}",
                (d.Settlement == "Cash" ? "1000" : "1500", d.Proceeds, 0), ("1690", d.AccumulatedDepreciation, 0),
                ("6801", Math.Max(0, -d.GainLoss), 0), ("1600", 0, d.Cost), ("4800", 0, Math.Max(0, d.GainLoss)));
            voucher.Date = PostingDate(d.DisposalDate); db.Vouchers.Add(voucher); await db.SaveChangesAsync(ct); d.VoucherId = voucher.Id;
        }
        a.Status = "Disposed"; a.DisposedOn = d.DisposalDate; a.FinancialVersion++;
        d.Status = "Posted"; d.PostedBy = t.user; d.PostedUtc = DateTimeOffset.UtcNow;
        Audit(t, "DisposalPost", $"Asset {a.Id}; disposal {d.Id}; voucher {d.VoucherId}"); return d;
    }, ct);
    public Task<FixedAssetDisposal> CancelDisposal(FixedAssetActionInput input, CancellationToken ct = default) => Write("FixedAssets.Dispose", async t =>
    {
        var d = await Disposal(input.Id, t, ct); Token(d, input.RowVersion);
        if (d.Status is not ("Draft" or "Approved")) throw new InvalidOperationException("Only an unposted disposal can be cancelled.");
        d.CancellationReason = BusinessInput.Text(input.ReviewNote, "Cancellation reason", 1000); d.CancelledBy = t.user; d.CancelledUtc = DateTimeOffset.UtcNow; d.Status = "Cancelled";
        Audit(t, "DisposalCancel", $"Asset {d.FixedAssetId}; disposal {d.Id}"); return d;
    }, ct);
}
