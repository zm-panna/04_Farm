using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class FixedAssetWorkflowTests
{
    private static readonly DateOnly Jan = new(2025, 1, 31);
    private static FixedAssetService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static async Task Setup(SecurityTestScope f)
    { await f.GrantAsync("*"); await f.GrantAsync("*", "approver-a"); await CompanyBusinessPolicyTestData.SeedAsync(f); }
    private static FixedAssetInput Input(string code = "PUMP", decimal cost = 1000, decimal residual = 100, int life = 12) => new() {
        AssetCode = code, Name = "Irrigation pump", Category = "Equipment", SourceReference = "INV-" + code,
        AcquisitionDate = new DateOnly(2025, 1, 1), Cost = cost, AcquisitionSettlement = "Cash", DepreciationMethod = "StraightLineMonthly",
        UsefulLifeMonths = life, ResidualValue = residual, DepreciationStart = new DateOnly(2025, 1, 1),
        PolicyReason = "Synthetic useful-life estimate approved for test only", Custodian = "Farm operator", Location = "Pump house"
    };
    private static FixedAssetActionInput Action(long id, byte[] token) => new() { Id = id, RowVersion = token.ToArray(), ConfirmReviewed = true, ReviewNote = "Reviewed source evidence, estimates and exact journal" };
    private static async Task<FixedAsset> Current(SecurityTestScope f, long id) => await f.Db.FixedAssets.AsNoTracking().SingleAsync(x => x.Id == id);
    private static async Task<FixedAsset> Active(SecurityTestScope f, FixedAssetInput? input = null)
    {
        var a = await Service(f).Create(input ?? Input()); f.ActAs("approver-a");
        a = await Service(f).ApproveAcquisition(Action(a.Id, a.RowVersion)); f.ActAs();
        return await Service(f).PostAcquisition(Action(a.Id, a.RowVersion));
    }
    private static async Task<FixedAssetDepreciationRun> Run(SecurityTestScope f, DateOnly period, string? reference = null)
    {
        var r = await Service(f).CreateRun(new() { Period = period, Reference = reference ?? $"DEP-{period:yyyy-MM}" });
        f.ActAs("approver-a"); r = await Service(f).ApproveRun(Action(r.Id, r.RowVersion)); f.ActAs();
        return await Service(f).PostRun(Action(r.Id, r.RowVersion));
    }
    private static FixedAssetDisposalInput Disposal(FixedAsset a, DateOnly? date = null, decimal proceeds = 800, string reference = "DSP") => new() {
        FixedAssetId = a.Id, AssetRowVersion = a.RowVersion.ToArray(), DisposalDate = date ?? new DateOnly(2025, 1, 15), Reference = reference,
        Counterparty = "Synthetic buyer", Reason = "Reviewed equipment replacement", Proceeds = proceeds, Settlement = "Receivable"
    };
    private static async Task<FixedAssetDisposal> PostDisposal(SecurityTestScope f, FixedAssetDisposalInput input)
    {
        var d = await Service(f).CreateDisposal(input); f.ActAs("approver-a");
        d = await Service(f).ApproveDisposal(Action(d.Id, d.RowVersion)); f.ActAs(); return await Service(f).PostDisposal(Action(d.Id, d.RowVersion));
    }
    [Fact]
    public async Task AcquisitionHasReviewedPolicyBalancedJournalCustodyAndCurrencySnapshot()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f);
        Assert.Equal("Active", a.Status); Assert.Equal("approver-a", a.ApprovedBy); Assert.Equal("BDT", a.CurrencyCode); Assert.Equal(2, a.CurrencyMinorUnits); Assert.True(a.BusinessPolicyId > 0);
        var v = await f.Db.Vouchers.Include(x => x.Lines).SingleAsync(); Assert.Equal(a.AcquisitionVoucherId, v.Id);
        Assert.Equal(1000, v.Lines.Sum(x => x.Debit)); Assert.Equal(1000, v.Lines.Sum(x => x.Credit)); Assert.Contains(v.Lines, x => x.AccountCode == "1600" && x.Debit == 1000);
        Assert.Contains(v.Lines, x => x.AccountCode == "1000" && x.Credit == 1000); Assert.Single(await f.Db.FixedAssetCustodies.ToListAsync());
        Assert.Contains(await f.Db.Audits.ToListAsync(), x => x.Action == "FixedAssets.AcquisitionPost");
    }
    [Fact]
    public async Task ExplicitIndependentPolicyReviewAndMoneyPrecisionAreMandatory()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Create(Input())); await CompanyBusinessPolicyTestData.SeedAsync(f);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Create(Input(cost: 100.001m)));
        var missing = Input(); missing.DepreciationMethod = ""; await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Create(missing));
        var residual = Input(residual: 1001); await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Create(residual));
        var partial = Input(); partial.DepreciationStart = new(2025, 1, 15); await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Create(partial));
        var a = await Service(f).Create(Input());
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ApproveAcquisition(Action(a.Id, a.RowVersion)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).PostAcquisition(Action(a.Id, a.RowVersion)));
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Equal("Draft", (await Current(f, a.Id)).Status);
    }
    [Fact]
    public async Task CumulativeMonthlyRoundingHitsExactResidualWithoutDuplicatePeriod()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f, Input(cost: 1, residual: .02m, life: 3));
        var jan = await Run(f, Jan); var feb = await Run(f, new(2025, 2, 28)); var mar = await Run(f, new(2025, 3, 31));
        Assert.Equal(new[] { .33m, .32m, .33m }, new[] { jan.Total, feb.Total, mar.Total });
        var updated = await Current(f, a.Id); Assert.Equal(.98m, updated.AccumulatedDepreciation); Assert.Equal(.02m, updated.Cost - updated.AccumulatedDepreciation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CreateRun(new() { Period = new(2025, 4, 30), Reference = "NO-EXCESS" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CreateRun(new() { Period = Jan, Reference = "DUPLICATE" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).PostRun(Action(mar.Id, mar.RowVersion)));
        Assert.Equal(3, await f.Db.FixedAssetDepreciationLines.CountAsync(x => x.Posted));
        foreach (var v in await f.Db.Vouchers.Include(x => x.Lines).ToListAsync()) Assert.Equal(v.Lines.Sum(x => x.Debit), v.Lines.Sum(x => x.Credit));
    }
    [Fact]
    public async Task TinyRoundedZeroMonthAdvancesPeriodWithoutZeroVoucher()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f, Input(cost: .01m, residual: 0, life: 3));
        var jan = await Run(f, Jan); Assert.Equal(0, jan.Total); Assert.Null(jan.VoucherId); Assert.Equal(Jan, (await Current(f, a.Id)).LastDepreciationPeriod);
        var feb = await Run(f, new(2025, 2, 28)); Assert.Equal(.01m, feb.Total); Assert.NotNull(feb.VoucherId);
        Assert.Equal(.01m, (await Current(f, a.Id)).AccumulatedDepreciation);
    }
    [Fact]
    public async Task ZeroNondepreciableAndDisposedAssetsAreExcluded()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f);
        var zero = await Active(f, Input("ZERO", 0, 0)); Assert.Null(zero.AcquisitionVoucherId);
        var non = Input("LAND", 100, 100, 0); non.DepreciationMethod = "None"; await Active(f, non);
        var sold = await Active(f, Input("SOLD")); await PostDisposal(f, Disposal(sold));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CreateRun(new() { Period = Jan, Reference = "EMPTY" }));
        Assert.Empty(await f.Db.FixedAssetDepreciationRuns.ToListAsync());
        var zeroDisposal = await PostDisposal(f, Disposal(await Current(f, zero.Id), proceeds: 0, reference: "ZERO-DSP")); Assert.Null(zeroDisposal.VoucherId);
    }
    [Fact]
    public async Task RunCannotSkipMonthsOrUseFutureOrNonMonthEndDates()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); await Active(f);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CreateRun(new() { Period = new(2025, 2, 28), Reference = "SKIP" }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).CreateRun(new() { Period = new(2025, 1, 20), Reference = "PARTIAL" }));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).CreateRun(new() { Period = new(2099, 1, 31), Reference = "FUTURE" }));
        Assert.Empty(await f.Db.FixedAssetDepreciationRuns.ToListAsync());
    }
    [Fact]
    public async Task RunApprovalIsIndependentAndOriginalTokenCannotBeReplayed()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); await Active(f);
        var r = await Service(f).CreateRun(new() { Period = Jan, Reference = "REVIEW" }); var original = r.RowVersion.ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ApproveRun(Action(r.Id, original)));
        f.ActAs("approver-a"); r = await Service(f).ApproveRun(Action(r.Id, original)); f.ActAs();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(f).PostRun(Action(r.Id, original)));
        r = await f.Db.FixedAssetDepreciationRuns.AsNoTracking().SingleAsync(); await Service(f).PostRun(Action(r.Id, r.RowVersion));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(f).PostRun(Action(r.Id, original)));
        Assert.Single(await f.Db.FixedAssetDepreciationLines.Where(x => x.Posted).ToListAsync());
    }
    [Fact]
    public async Task NewEligibleAssetInvalidatesPreviewAndCancellationAllowsFreshReviewedRun()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); await Active(f);
        var r = await Service(f).CreateRun(new() { Period = Jan, Reference = "OLD" }); await Active(f, Input("SECOND"));
        f.ActAs("approver-a"); await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ApproveRun(Action(r.Id, r.RowVersion))); f.ActAs();
        r = await f.Db.FixedAssetDepreciationRuns.AsNoTracking().SingleAsync(); await Service(f).CancelRun(Action(r.Id, r.RowVersion));
        var fresh = await Run(f, Jan, "FRESH"); Assert.Equal(2, fresh.Lines.Count); Assert.Equal(150, fresh.Total);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service(f).ApproveRun(Action(r.Id, (await f.Db.FixedAssetDepreciationRuns.AsNoTracking().SingleAsync(x => x.Id == r.Id)).RowVersion)));
    }
    [Fact]
    public async Task SupplementalRunCoversLateAcquisitionWithoutRepostingExistingAssetPeriod()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var first = await Active(f);
        await Run(f, Jan, "FIRST-JAN"); var late = await Active(f, Input("LATE"));
        var supplemental = await Run(f, Jan, "SUPPLEMENTAL-JAN"); Assert.Single(supplemental.Lines);
        Assert.Equal(late.Id, supplemental.Lines[0].FixedAssetId);
        Assert.Equal(1, await f.Db.FixedAssetDepreciationLines.CountAsync(x => x.FixedAssetId == first.Id && x.Period == Jan && x.Posted));
        Assert.Equal(2, await f.Db.FixedAssetDepreciationRuns.CountAsync(x => x.Status == "Posted"));
    }
    [Fact]
    public async Task MetadataChangeDoesNotInvalidateFinanciallyUnchangedReviewedRun()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f);
        var r = await Service(f).CreateRun(new() { Period = Jan, Reference = "METADATA" });
        await Service(f).EditMetadata(new() { Id = a.Id, RowVersion = a.RowVersion, Name = "Improved description", Category = a.Category });
        f.ActAs("approver-a"); r = await Service(f).ApproveRun(Action(r.Id, r.RowVersion)); f.ActAs();
        await Service(f).PostRun(Action(r.Id, r.RowVersion)); Assert.Equal(75, (await Current(f, a.Id)).AccumulatedDepreciation);
    }
    [Fact]
    public async Task MetadataAndCustodyPreserveOriginalConcurrencyAndHistory()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f); var original = a.RowVersion.ToArray();
        await Service(f).EditMetadata(new() { Id = a.Id, RowVersion = original, Name = "Reviewed pump", Category = "Equipment", SerialNumber = "S1" });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(f).EditMetadata(new() { Id = a.Id, RowVersion = original, Name = "Stale overwrite", Category = "Wrong" }));
        a = await Current(f, a.Id); Assert.Equal("Reviewed pump", a.Name); Assert.Equal(1000, a.Cost);
        await Service(f).ChangeCustody(new() { Id = a.Id, RowVersion = a.RowVersion, EffectiveDate = new(2025, 1, 2), Custodian = "New custodian", Location = "North shed", Reason = "Signed handover" });
        var history = await f.Db.FixedAssetCustodies.OrderBy(x => x.Id).ToListAsync(); Assert.Equal(2, history.Count); Assert.Equal("Farm operator", history[1].PreviousCustodian);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(f).ChangeCustody(new() { Id = a.Id, RowVersion = a.RowVersion, EffectiveDate = new(2025, 1, 3), Custodian = "Replay", Location = "Yard", Reason = "Replay" }));
        a = await Current(f, a.Id); await Assert.ThrowsAsync<ArgumentException>(() => Service(f).ChangeCustody(new() { Id = a.Id, RowVersion = a.RowVersion, EffectiveDate = new(2025, 1, 1), Custodian = "Backdate", Location = "Yard", Reason = "Backdate" }));
        Assert.Equal(2, await f.Db.FixedAssetCustodies.CountAsync());
    }
    [Theory]
    [InlineData(1200, "4800", 200)]
    [InlineData(800, "6801", 200)]
    [InlineData(1000, "", 0)]
    [InlineData(0, "6801", 1000)]
    public async Task DisposalRemovesExactCostAndBooksBalancedGainLossOnce(decimal proceeds, string account, decimal gainLoss)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f);
        var d = await PostDisposal(f, Disposal(a, proceeds: proceeds)); var v = await f.Db.Vouchers.Include(x => x.Lines).SingleAsync(x => x.Id == d.VoucherId);
        Assert.Equal(v.Lines.Sum(x => x.Debit), v.Lines.Sum(x => x.Credit)); Assert.Contains(v.Lines, x => x.AccountCode == "1600" && x.Credit == 1000);
        if (gainLoss > 0) Assert.Contains(v.Lines, x => x.AccountCode == account && x.Debit + x.Credit == gainLoss);
        if (proceeds > 0) Assert.Contains(v.Lines, x => x.AccountCode == "1500" && x.Debit == proceeds);
        Assert.Equal("Disposed", (await Current(f, a.Id)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service(f).CreateDisposal(Disposal(await Current(f, a.Id), reference: "SECOND")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).PostDisposal(Action(d.Id, d.RowVersion)));
        Assert.Single(await f.Db.FixedAssetDisposals.Where(x => x.Status == "Posted").ToListAsync());
    }
    [Fact]
    public async Task DisposalRequiresPriorDepreciationAndCancelsStaleReviewedFinancialSnapshot()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CreateDisposal(Disposal(a, new(2025, 2, 10))));
        a = await Current(f, a.Id); var d = await Service(f).CreateDisposal(Disposal(a, Jan));
        f.ActAs("approver-a"); d = await Service(f).ApproveDisposal(Action(d.Id, d.RowVersion)); f.ActAs();
        await Run(f, Jan); await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).PostDisposal(Action(d.Id, d.RowVersion)));
        Assert.Equal("Active", (await Current(f, a.Id)).Status); Assert.False(await f.Db.Vouchers.AnyAsync(x => x.Reference.StartsWith("FA-DSP")));
        d = await f.Db.FixedAssetDisposals.AsNoTracking().SingleAsync(); await Service(f).CancelDisposal(Action(d.Id, d.RowVersion));
        var fresh = await PostDisposal(f, Disposal(await Current(f, a.Id), new(2025, 2, 10), 950, "FRESH"));
        Assert.Equal(925, fresh.CarryingValue); Assert.Equal(25, fresh.GainLoss);
        var v = await f.Db.Vouchers.Include(x => x.Lines).SingleAsync(x => x.Id == fresh.VoucherId); Assert.Contains(v.Lines, x => x.AccountCode == "1690" && x.Debit == 75);
    }
    [Fact]
    public async Task ClosedPeriodBlocksAcquisitionRunAndDisposalEvenWhenZeroMonetaryValue()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f);
        var zero = await Service(f).Create(Input("ZERO", 0, 0)); f.ActAs("approver-a"); zero = await Service(f).ApproveAcquisition(Action(zero.Id, zero.RowVersion)); f.ActAs();
        var run = await Service(f).CreateRun(new() { Period = Jan, Reference = "CLOSED-RUN" });
        var disposal = await Service(f).CreateDisposal(Disposal(a));
        f.ActAs("approver-a"); run = await Service(f).ApproveRun(Action(run.Id, run.RowVersion)); disposal = await Service(f).ApproveDisposal(Action(disposal.Id, disposal.RowVersion)); f.ActAs();
        f.Db.AccountingPeriodCloses.Add(new() { CompanyId = 1, BranchId = 11, ClosedThrough = Jan, ClosedBy = "approver-a", Reason = "Closed test period" }); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).PostAcquisition(Action(zero.Id, zero.RowVersion)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).PostRun(Action(run.Id, run.RowVersion)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).PostDisposal(Action(disposal.Id, disposal.RowVersion)));
        Assert.Single(await f.Db.Vouchers.ToListAsync()); Assert.Equal(0, (await Current(f, a.Id)).AccumulatedDepreciation);
    }
    [Fact]
    public async Task AcquisitionCancellationHasNoJournalAndCannotBeReplayedOrRevived()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Service(f).Create(Input());
        a = await Service(f).CancelAcquisition(Action(a.Id, a.RowVersion)); Assert.Equal("Cancelled", a.Status);
        f.ActAs("approver-a"); await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ApproveAcquisition(Action(a.Id, a.RowVersion))); f.ActAs();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Create(Input()));
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.FixedAssetCustodies.ToListAsync());
    }
    [Fact]
    public async Task TenantBranchScopePermissionsAndCompositeForeignKeysPreventLeakage()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f);
        var controller = new FixedAssetsController(f.Db, f.Tenant, f.Permissions, Service(f));
        f.Db.UserBranchAccess.Add(new UserBranchAccess { CompanyId = 1, BranchId = 12, UserId = "operator-a" }); await f.Db.SaveChangesAsync(); f.ActAs(branch: 12);
        Assert.IsType<NotFoundResult>(await controller.Details(a.Id, default));
        Assert.Empty(Assert.IsType<FixedAssetList>(Assert.IsType<ViewResult>(await controller.Index(null, null)).Model).Assets);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).PostAcquisition(Action(a.Id, a.RowVersion)));
        await f.GrantAsync("*", "operator-b", 2); f.ActAs("operator-b", 2, 21); Assert.IsType<NotFoundResult>(await controller.Details(a.Id, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(f).CreateDisposal(Disposal(a)));
        f.ActAs(); await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync("UPDATE FixedAssetCustodies SET CompanyId=2, BranchId=21"));
        f.Db.UserPermissions.RemoveRange(await f.Db.UserPermissions.Where(x => x.UserId == "operator-a").ToListAsync()); await f.Db.SaveChangesAsync();
        await f.GrantAsync("FixedAssets.View"); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).Create(Input("DENIED")));
    }
    [Fact]
    public async Task CompanyPolicyCompositeForeignKeyRejectsAnotherCompanyPolicy()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var other = await CompanyBusinessPolicyTestData.SeedAsync(f, 2);
        var a = await Active(f); await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE FixedAssets SET BusinessPolicyId={other.Id} WHERE Id={a.Id}"));
        Assert.NotEqual(other.Id, (await Current(f, a.Id)).BusinessPolicyId);
    }
    [Theory]
    [InlineData("USD", 2, 1.29)]
    [InlineData("KWD", 3, 1.286)]
    [InlineData("JPY", 0, 1)]
    public async Task MonthlyRoundingUsesExplicitCompanyCurrencyPrecision(string currency, int digits, decimal expected)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); await f.GrantAsync("*", "approver-a");
        await CompanyBusinessPolicyTestData.SeedAsync(f, currency: currency, minorUnits: digits);
        var a = await Active(f, Input(cost: 10, residual: 1, life: 7)); var r = await Run(f, Jan);
        Assert.Equal(expected, r.Total); Assert.Equal(currency, a.CurrencyCode); Assert.Equal(currency, r.CurrencyCode); Assert.Equal(digits, r.CurrencyMinorUnits);
    }
    [Fact]
    public async Task DirectFinancialAndHistoryEditsAreRejectedByBothSavePaths()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); var a = await Active(f);
        a.Cost = 1; await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear();
        var custody = await f.Db.FixedAssetCustodies.SingleAsync(); custody.Custodian = "Rewrite history"; Assert.Throws<InvalidOperationException>(() => f.Db.SaveChanges()); f.Db.ChangeTracker.Clear();
        f.Db.FixedAssets.Remove(await f.Db.FixedAssets.SingleAsync()); await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }
    [Theory]
    [InlineData("Acquisition")]
    [InlineData("Depreciation")]
    [InlineData("Disposal")]
    public async Task FailureAfterJournalSaveRollsBackBalancesHistoryAndAuditAndPermitsSafeRetry(string step)
    {
        var fail = new FailPosting(); await using var f = await SecurityTestScope.CreateAsync(fail); await Setup(f);
        long assetId; FixedAssetActionInput command;
        if (step == "Acquisition") { var a = await Service(f).Create(Input()); f.ActAs("approver-a"); a = await Service(f).ApproveAcquisition(Action(a.Id, a.RowVersion)); f.ActAs(); assetId = a.Id; command = Action(a.Id, a.RowVersion); }
        else { var a = await Active(f); assetId = a.Id; if (step == "Depreciation") { var r = await Service(f).CreateRun(new() { Period = Jan, Reference = "ATOMIC" }); f.ActAs("approver-a"); r = await Service(f).ApproveRun(Action(r.Id, r.RowVersion)); command = Action(r.Id, r.RowVersion); }
            else { var d = await Service(f).CreateDisposal(Disposal(a)); f.ActAs("approver-a"); d = await Service(f).ApproveDisposal(Action(d.Id, d.RowVersion)); command = Action(d.Id, d.RowVersion); } f.ActAs(); }
        var initialVouchers = await f.Db.Vouchers.CountAsync(); var initialAudits = await f.Db.Audits.CountAsync(); fail.Step = step;
        Task Post() => step == "Acquisition" ? Service(f).PostAcquisition(command) : step == "Depreciation" ? Service(f).PostRun(command) : Service(f).PostDisposal(command);
        await Assert.ThrowsAsync<InvalidOperationException>(Post); Assert.Equal(initialVouchers, await f.Db.Vouchers.CountAsync()); Assert.Equal(initialAudits, await f.Db.Audits.CountAsync());
        var asset = await Current(f, assetId); Assert.Equal(step == "Acquisition" ? "Approved" : "Active", asset.Status); Assert.Equal(0, asset.AccumulatedDepreciation);
        Assert.False(await f.Db.FixedAssetDepreciationLines.AnyAsync(x => x.Posted)); fail.Step = ""; await Post();
        Assert.Equal(initialVouchers + 1, await f.Db.Vouchers.CountAsync());
    }
    [Fact]
    public async Task CancelledRequestDoesNotLeavePartialAcquisitionOrTrackingWrites()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f); using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(f).Create(Input(), cts.Token));
        Assert.Empty(await f.Db.FixedAssets.ToListAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
        await Active(f); Assert.Single(await f.Db.FixedAssets.ToListAsync());
    }
    [Fact]
    public async Task RegisterSearchFilterAndPaginationDoNotLoseBoundaryRows()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await Setup(f);
        for (var n = 1; n <= 27; n++) await Service(f).Create(Input($"A{n:D3}"));
        var c = new FixedAssetsController(f.Db, f.Tenant, f.Permissions, Service(f));
        var first = Assert.IsType<FixedAssetList>(Assert.IsType<ViewResult>(await c.Index("A", "Draft", 1)).Model);
        var second = Assert.IsType<FixedAssetList>(Assert.IsType<ViewResult>(await c.Index("A", "Draft", 2)).Model);
        Assert.Equal(27, first.Total); Assert.Equal(25, first.Assets.Count); Assert.Equal(2, second.Assets.Count); Assert.Equal(27, first.Assets.Concat(second.Assets).Select(x => x.Id).Distinct().Count());
        Assert.Empty(Assert.IsType<FixedAssetList>(Assert.IsType<ViewResult>(await c.Index(null, "Active")).Model).Assets);
    }
    private sealed class FailPosting : SaveChangesInterceptor
    {
        public string Step { get; set; } = "";
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var db = eventData.Context!;
            if (Step == "Acquisition" && db.ChangeTracker.Entries<FixedAsset>().Any(x => x.State == EntityState.Modified && x.Entity.Status == "Active")
                || Step == "Depreciation" && db.ChangeTracker.Entries<FixedAssetDepreciationRun>().Any(x => x.State == EntityState.Modified && x.Entity.Status == "Posted")
                || Step == "Disposal" && db.ChangeTracker.Entries<FixedAssetDisposal>().Any(x => x.State == EntityState.Modified && x.Entity.Status == "Posted"))
                throw new InvalidOperationException("Injected failure after journal save and before terminal workflow save.");
            return ValueTask.FromResult(result);
        }
    }
}
