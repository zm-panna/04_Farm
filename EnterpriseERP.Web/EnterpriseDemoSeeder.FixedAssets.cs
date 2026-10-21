using EnterpriseERP.Domain;

namespace EnterpriseERP.Web;

public static partial class EnterpriseDemoSeeder
{
    /// <summary>Fictional opt-in fixtures only. The caller owns the transaction and supplies two distinct synthetic identities.</summary>
    internal static async Task SeedFixedAssets(AppDbContext db, Company company, Branch branch, AppUser maker, AppUser reviewer,
        CompanyBusinessPolicy policy, TaxPolicy tax, int number, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        if (maker.Id == reviewer.Id) throw new InvalidOperationException("Synthetic acquisition review requires two distinct fixture identities.");
        var previous = db.FixedAssetWorkflowWrite; db.FixedAssetWorkflowWrite = true;
        try
        {
            var acquired = new DateOnly(today.Year, today.Month, 1).AddMonths(-2); var period = FixedAssetService.MonthEnd(acquired); var disposed = period.AddDays(1);
            var a = new FixedAsset {
                CompanyId = company.Id, BranchId = branch.Id, AssetCode = $"DEMO-FA-{number:D2}", Name = $"Fictional irrigation pump {number}", Category = "Synthetic equipment",
                SerialNumber = $"DEMO-ONLY-{number:D3}", Notes = "FICTIONAL DEMONSTRATION DATA. No real asset, payment, statutory policy or accountant sign-off is represented.",
                SourceReference = $"DEMO-FA-INVOICE-{number:D2}", AcquisitionDate = acquired, Cost = 1200, AcquisitionSettlement = "Cash",
                DepreciationMethod = "StraightLineMonthly", UsefulLifeMonths = 12, ResidualValue = 100, DepreciationStart = acquired,
                PolicyReason = "Synthetic monthly straight-line estimate solely to exercise the reviewed workflow; not a statutory or recommended useful life.",
                Custodian = $"Fictional custodian {number}", Location = "Fictional pump house", CreatedBy = maker.Id, CreatedUtc = now,
                BusinessPolicyId = policy.Id, BusinessPolicyVersion = policy.Version, CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits
            };
            db.FixedAssets.Add(a); await db.SaveChangesAsync(ct);
            a.Status = "Approved"; a.ApprovedBy = reviewer.Id; a.ApprovedUtc = now; a.ReviewNote = "Fictional reviewer identity in an opt-in sample; not a real accounting approval.";
            AuditAssetDemo(db, company.Id, branch.Id, maker.Id, "Create", a.Id, now); AuditAssetDemo(db, company.Id, branch.Id, reviewer.Id, "AcquisitionApprove", a.Id, now); await db.SaveChangesAsync(ct);
            var acquisition = BusinessInput.Voucher(company.Id, branch.Id, $"FA-ACQ-{branch.Id}-{a.Id}", ("1600", a.Cost, 0), ("1000", 0, a.Cost));
            acquisition.Date = new DateTimeOffset(acquired.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); db.Vouchers.Add(acquisition); await db.SaveChangesAsync(ct);
            a.AcquisitionVoucherId = acquisition.Id; a.Status = "Active"; a.PostedBy = maker.Id; a.PostedUtc = now; a.FinancialVersion = 1;
            db.FixedAssetCustodies.Add(new FixedAssetCustody { CompanyId = company.Id, BranchId = branch.Id, FixedAssetId = a.Id, EffectiveDate = acquired, Custodian = a.Custodian, Location = a.Location,
                Reason = "Fictional initial custody at sample acquisition", RecordedBy = maker.Id, CreatedUtc = now });
            AuditAssetDemo(db, company.Id, branch.Id, maker.Id, "AcquisitionPost", a.Id, now); await db.SaveChangesAsync(ct);
            var amount = FixedAssetService.DepreciationAmount(a, period);
            var run = new FixedAssetDepreciationRun { CompanyId = company.Id, BranchId = branch.Id, Reference = $"DEMO-FA-DEP-{number:D2}", Period = period, Total = amount,
                CreatedBy = maker.Id, CreatedUtc = now, BusinessPolicyId = policy.Id, BusinessPolicyVersion = policy.Version, CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits,
                Lines = [new FixedAssetDepreciationLine { CompanyId = company.Id, BranchId = branch.Id, FixedAssetId = a.Id, AssetCode = a.AssetCode, Period = period, ExpectedFinancialVersion = a.FinancialVersion,
                    Cost = a.Cost, ResidualValue = a.ResidualValue, Amount = amount, AccumulatedAfter = amount }] };
            db.FixedAssetDepreciationRuns.Add(run); await db.SaveChangesAsync(ct); AuditAssetDemo(db, company.Id, branch.Id, maker.Id, "DepreciationDraft", a.Id, now);
            run.Status = "Approved"; run.ApprovedBy = reviewer.Id; run.ApprovedUtc = now; run.ReviewNote = "Synthetic review only; exact sample journal checked.";
            AuditAssetDemo(db, company.Id, branch.Id, reviewer.Id, "DepreciationApprove", a.Id, now); await db.SaveChangesAsync(ct);
            var depreciation = BusinessInput.Voucher(company.Id, branch.Id, $"FA-DEP-{branch.Id}-{run.Id}", ("6800", amount, 0), ("1690", 0, amount));
            depreciation.Date = new DateTimeOffset(period.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); db.Vouchers.Add(depreciation); await db.SaveChangesAsync(ct);
            run.VoucherId = depreciation.Id; run.Status = "Posted"; run.PostedBy = maker.Id; run.PostedUtc = now; run.Lines[0].Posted = true;
            a.AccumulatedDepreciation = amount; a.LastDepreciationPeriod = period; a.FinancialVersion++;
            AuditAssetDemo(db, company.Id, branch.Id, maker.Id, "DepreciationPost", a.Id, now); await db.SaveChangesAsync(ct);
            var d = new FixedAssetDisposal { CompanyId = company.Id, BranchId = branch.Id, FixedAssetId = a.Id, Reference = $"DEMO-FA-DSP-{number:D2}", DisposalDate = disposed,
                Counterparty = "Fictional demo buyer", Reason = "Fictional equipment replacement to demonstrate a balanced gain journal", Proceeds = 1150, Settlement = "Receivable",
                Cost = a.Cost, AccumulatedDepreciation = amount, CarryingValue = a.Cost - amount, GainLoss = 1150 - (a.Cost - amount), ExpectedFinancialVersion = a.FinancialVersion,
                CreatedBy = maker.Id, CreatedUtc = now, BusinessPolicyId = policy.Id, BusinessPolicyVersion = policy.Version, CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits };
            db.FixedAssetDisposals.Add(d); await db.SaveChangesAsync(ct); AuditAssetDemo(db, company.Id, branch.Id, maker.Id, "DisposalDraft", a.Id, now);
            d.Status = "Approved"; d.ApprovedBy = reviewer.Id; d.ApprovedUtc = now; d.ReviewNote = "Fictional independent review for an opt-in test fixture only.";
            AuditAssetDemo(db, company.Id, branch.Id, reviewer.Id, "DisposalApprove", a.Id, now); await db.SaveChangesAsync(ct);
            var disposal = BusinessInput.Voucher(company.Id, branch.Id, $"FA-DSP-{branch.Id}-{d.Id}", ("1500", d.Proceeds, 0), ("1690", amount, 0), ("1600", 0, d.Cost), ("4800", 0, d.GainLoss));
            disposal.Date = new DateTimeOffset(disposed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); db.Vouchers.Add(disposal); await db.SaveChangesAsync(ct);
            d.VoucherId = disposal.Id; d.Status = "Posted"; d.PostedBy = maker.Id; d.PostedUtc = now; a.Status = "Disposed"; a.DisposedOn = disposed; a.FinancialVersion++;
            AuditAssetDemo(db, company.Id, branch.Id, maker.Id, "DisposalPost", a.Id, now); await db.SaveChangesAsync(ct);
        }
        finally { db.FixedAssetWorkflowWrite = previous; }
    }
    private static void AuditAssetDemo(AppDbContext db, int company, int branch, string actor, string action, long assetId, DateTimeOffset now) =>
        db.Audits.Add(new AuditEntry { CompanyId = company, ActorId = actor, Action = "FixedAssets." + action, Reference = $"Branch {branch}; asset {assetId}; FICTIONAL SAMPLE", CreatedUtc = now });
}
