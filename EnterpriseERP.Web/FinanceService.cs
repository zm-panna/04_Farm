using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
namespace EnterpriseERP.Web;
public sealed record LedgerLine(DateTimeOffset Date, string Reference, string Account, decimal Debit, decimal Credit);
public sealed record TrialBalanceLine(string Account, decimal Debit, decimal Credit);
public sealed record FinancialStatement(decimal Revenue, decimal CostOfSales, decimal OtherExpense, decimal Profit, decimal Assets, decimal Liabilities, decimal Equity);
public sealed record DocumentBalance(long Id, string Kind, string Reference, decimal Amount, decimal Returned, decimal Paid, decimal Outstanding, string Party = "Unassigned", bool PartyAssigned = true);
public sealed class FinanceService(AppDbContext db, TenantContext tenant, PermissionService permission, TimeProvider? clock = null)
{
    public async Task<IReadOnlyList<LedgerLine>> Ledger(string? account, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        await permission.Demand("Accounts.View", ct); var t = await tenant.GetAsync(ct);
        if (to <= from) throw new ArgumentException("The end date must be after the start date.");
        var q = db.VoucherLines.AsNoTracking().Join(db.Vouchers.AsNoTracking().Where(v => v.CompanyId == t.company && v.BranchId == t.branch && v.Posted && v.Date >= from && v.Date < to), l => l.VoucherId, v => v.Id, (l, v) => new { l, v });
        if (!string.IsNullOrWhiteSpace(account)) { account = BusinessInput.Text(account, "Account", 20); q = q.Where(x => x.l.AccountCode == account); }
        return await q.OrderBy(x => x.v.Date).ThenBy(x => x.l.Id).Select(x => new LedgerLine(x.v.Date, x.v.Reference, x.l.AccountCode, x.l.Debit, x.l.Credit)).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<TrialBalanceLine>> TrialBalance(DateTimeOffset through, CancellationToken ct)
    {
        var lines = await Ledger(null, DateTimeOffset.MinValue, through, ct);
        return lines.GroupBy(x => x.Account).Select(g => new TrialBalanceLine(g.Key, Math.Max(0, g.Sum(x => x.Debit - x.Credit)), Math.Max(0, g.Sum(x => x.Credit - x.Debit)))).OrderBy(x => x.Account).ToList();
    }
    public async Task<FinancialStatement> Statement(DateTimeOffset through, CancellationToken ct)
    {
        var lines = await Ledger(null, DateTimeOffset.MinValue, through, ct);
        if (lines.Any(x => x.Account.Length != 4 || !x.Account.All(char.IsAsciiDigit) || x.Account[0] is < '1' or > '6')) throw new InvalidOperationException("A posted account is outside the configured financial-statement classes. Review the chart before publishing statements.");
        decimal Net(string prefix) => lines.Where(x => x.Account.StartsWith(prefix, StringComparison.Ordinal)).Sum(x => x.Debit - x.Credit);
        var sales = -Net("4"); var cogs = Net("5"); var expenses = Net("6");
        return new(sales, cogs, expenses, sales - cogs - expenses, Net("1"), -Net("2"), -Net("3"));
    }
    public async Task<IReadOnlyList<DocumentBalance>> Documents(CancellationToken ct)
    {
        await permission.Demand("Accounts.View", ct); var t = await tenant.GetAsync(ct);
        var documents = await db.FinanceDocuments.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderByDescending(x => x.Id).ToListAsync(ct);
        var parties = await db.TradingParties.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).ToListAsync(ct);
        var returns = await db.StockDocumentReturns.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).ToListAsync(ct);
        var allocations = await db.FinancePaymentAllocations.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).ToListAsync(ct);
        return documents.Select(d => { var returned = returns.Where(x => x.FinanceDocumentId == d.Id).Sum(x => x.Amount); var paid = allocations.Where(x => x.FinanceDocumentId == d.Id).Sum(x => x.SignedAmount); var party = parties.FirstOrDefault(p => p.Id == d.PartyId); return new DocumentBalance(d.Id, d.Kind, d.Reference, d.Amount, returned, paid, d.Amount - returned - paid, party?.Name ?? "Unassigned", party is not null); }).ToList();
    }
    // Backwards-compatible API signature, but no unallocated postings are permitted.
    public async Task<long> RecordPayment(string reference, string partyType, decimal amount, CancellationToken ct, long? documentId = null)
    {
        await permission.Demand("Accounts.Post", ct);
        if (!documentId.HasValue) throw new ArgumentException("A purchase or sales document is required for every payment.");
        if (partyType is not ("Customer" or "Supplier")) throw new ArgumentException("Customer or Supplier required.");
        return await RecordDocumentPayment(documentId.Value, reference, amount, ct, partyType);
    }
    public async Task<long> RecordDocumentPayment(long documentId, string reference, decimal amount, CancellationToken ct, string? expectedPartyType = null)
    {
        await permission.Demand("Accounts.Post", ct); var t = await tenant.GetAsync(ct);
        reference = BusinessInput.Reference(reference); BusinessInput.Amount(amount);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, amount, ct, atUtc: businessDay.UtcInstant);
        if (await db.FinancePayments.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct)) throw new InvalidOperationException("Duplicate payment reference.");
        var document = await db.FinanceDocuments.SingleAsync(x => x.Id == documentId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        var customer = document.Kind is "Sale" or "AnimalSale";
        if (!customer && document.Kind is not ("Purchase" or "AnimalPurchase")) throw new InvalidOperationException("Unsupported payment document.");
        var partyType = customer ? "Customer" : "Supplier";
        if (expectedPartyType != null && expectedPartyType != partyType) throw new ArgumentException("Payment party does not match the selected document.");
        var credits = await db.StockDocumentReturns.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FinanceDocumentId == documentId).Select(x => x.Amount).ToListAsync(ct);
        var settlements = await db.FinancePaymentAllocations.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.FinanceDocumentId == documentId).Select(x => x.SignedAmount).ToListAsync(ct);
        var outstanding = document.Amount - credits.Sum() - settlements.Sum();
        if (outstanding == 0 || amount > Math.Abs(outstanding)) throw new InvalidOperationException("Payment/refund exceeds the document's remaining balance.");
        try { await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, Math.Abs(outstanding), ct, atUtc: businessDay.UtcInstant); }
        catch (ArgumentException) { throw new InvalidOperationException("The historical document balance cannot be represented in the reviewed currency precision. An authorized reviewed correction is required before payment or refund; no amount was changed."); }
        var refund = outstanding < 0;
        var cashDebit = customer != refund;
        var control = customer ? "1100" : "2100";
        var voucher = BusinessInput.Voucher(t.company, t.branch, "PAY-" + reference, ("1000", cashDebit ? amount : 0, cashDebit ? 0 : amount), (control, cashDebit ? 0 : amount, cashDebit ? amount : 0));
        voucher.Date = businessDay.PostingDate;
        db.Vouchers.Add(voucher); await db.SaveChangesAsync(ct);
        var payment = new FinancePayment { CompanyId = t.company, BranchId = t.branch, Reference = reference, PartyType = refund ? partyType + "Refund" : partyType, Amount = amount, VoucherId = voucher.Id };
        db.FinancePayments.Add(payment); await db.SaveChangesAsync(ct);
        db.FinancePaymentAllocations.Add(new FinancePaymentAllocation { CompanyId = t.company, BranchId = t.branch, FinanceDocumentId = documentId, FinancePaymentId = payment.Id, SignedAmount = refund ? -amount : amount });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = refund ? "Finance.Refund" : "Finance.Payment", Reference = reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return voucher.Id;
    }
    public async Task<BankStatementLine> RecordBankLine(string reference, decimal amount, DateOnly date, CancellationToken ct)
    {
        await permission.Demand("Accounts.Reconcile", ct); var t = await tenant.GetAsync(ct);
        reference = BusinessInput.Reference(reference); BusinessInput.Decimal(amount, "Bank amount", allowNegative: true);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        if (amount == 0 || date < new DateOnly(1900, 1, 1) || date > businessDay.Date) throw new ArgumentException("Enter a nonzero bank amount and a valid value date through today.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, Math.Abs(amount), ct, date: date);
        var line = new BankStatementLine { CompanyId = t.company, BranchId = t.branch, ExternalReference = reference, Amount = amount, ValueDate = date };
        db.BankStatementLines.Add(line);
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Bank.StatementLineCreated", Reference = $"branch:{t.branch}; {reference}" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return line;
    }
    public async Task Reconcile(long statementLineId, long voucherId, CancellationToken ct)
    {
        await permission.Demand("Accounts.Reconcile", ct); var t = await tenant.GetAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var line = await db.BankStatementLines.SingleAsync(x => x.Id == statementLineId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (line.MatchedVoucherId != null) throw new InvalidOperationException("Already reconciled.");
        if (await db.BankStatementLines.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.MatchedVoucherId == voucherId, ct)) throw new InvalidOperationException("This voucher is already matched to another bank statement line.");
        var voucher = await db.Vouchers.Include(x => x.Lines).SingleAsync(x => x.Id == voucherId && x.CompanyId == t.company && x.BranchId == t.branch && x.Posted, ct);
        var bankNet = voucher.Lines.Where(x => x.AccountCode == "1000").Sum(x => x.Debit - x.Credit);
        if (bankNet == 0 || bankNet != line.Amount) throw new InvalidOperationException("Bank amount does not match the voucher cash/bank movement.");
        line.MatchedVoucherId = voucherId;
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Bank.Reconcile", Reference = line.ExternalReference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
