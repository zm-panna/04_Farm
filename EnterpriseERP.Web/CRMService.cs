using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Net.Mail;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed class CRMLeadInput
{
    public long Id { get; set; }
    [Required, StringLength(80)] public string Reference { get; set; } = "";
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(200)] public string? ContactName { get; set; } = "";
    [StringLength(200)] public string? Email { get; set; } = "";
    [StringLength(40)] public string? Phone { get; set; } = "";
    [StringLength(500)] public string? Address { get; set; } = "";
    [StringLength(200)] public string? Source { get; set; } = "";
    [StringLength(2000)] public string? Notes { get; set; } = "";
    public string MarketingConsent { get; set; } = "Unknown";
    [StringLength(1000)] public string? ConsentEvidence { get; set; } = "";
    public bool ConsentReviewed { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CRMOpportunityInput
{
    public long Id { get; set; }
    public long LeadId { get; set; }
    [Required, StringLength(80)] public string Reference { get; set; } = "";
    [Required, StringLength(200)] public string Title { get; set; } = "";
    public DateOnly ExpectedCloseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
    [StringLength(1000)] public string ReviewNote { get; set; } = "";
    public bool Reviewed { get; set; }
    public byte[] LeadRowVersion { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CRMActivityInput
{
    public long Id { get; set; }
    public long LeadId { get; set; }
    public long? OpportunityId { get; set; }
    [Required, StringLength(80)] public string Reference { get; set; } = "";
    public string Kind { get; set; } = "Task";
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [StringLength(2000)] public string? Notes { get; set; } = "";
    public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CRMQuoteLineInput
{
    public int StockItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
}
public sealed class CRMQuoteInput
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }
    [Required, StringLength(80)] public string Reference { get; set; } = "";
    public DateOnly QuoteDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly ValidUntil { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
    [StringLength(2000)] public string? Terms { get; set; } = "";
    public List<CRMQuoteLineInput> Lines { get; set; } = [];
    public byte[] RowVersion { get; set; } = [];
}
public sealed class CRMAcceptQuoteInput
{
    public long QuoteId { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public byte[] OpportunityRowVersion { get; set; } = [];
    public byte[] LeadRowVersion { get; set; } = [];
    public int? ExistingCustomerId { get; set; }
    [StringLength(30)] public string? NewCustomerCode { get; set; } = "";
    [Required, StringLength(80)] public string SalesReadyReference { get; set; } = "";
    [Required, StringLength(1000)] public string AcceptanceEvidence { get; set; } = "";
    public bool Reviewed { get; set; }
}

public sealed class CRMService(AppDbContext db, TenantContext tenant, PermissionService permission)
{
    public const string AcceptanceDisclosure = "Acceptance records your review of customer acceptance, links or creates a customer, marks the opportunity won, and creates an internal sales-ready handoff. It sends no quote, email or message; reserves no stock; and creates no sale, invoice, tax posting or general-ledger entry. All amounts are net estimates; tax is not calculated here. Review currency, tax, stock and pricing separately before posting Sales.";
    public static readonly string[] OpenStages = ["Qualified", "Proposal", "Negotiation"];
    public static string Reference(string? value) => BusinessInput.Reference(value).ToUpperInvariant();
    private static string Text(string? value, string label, int length, bool required = true) => BusinessInput.Text(value, label, length, required);
    private static void Date(DateOnly date, string label)
    { if (date.Year is < 2000 or > 2100) throw new ArgumentException(label + " must be a valid date between 2000 and 2100."); }
    private static void Reviewed(bool reviewed)
    { if (!reviewed) throw new ArgumentException("Review the displayed information and explicitly confirm this action."); }
    private static void Version(byte[]? expected, byte[] actual)
    { if (expected is null || expected.Length == 0 || !expected.SequenceEqual(actual)) throw new InvalidOperationException("This record changed or the action was already completed. Reload it and review the latest version."); }
    private static void Open(CRMOpportunity opportunity)
    { if (!OpenStages.Contains(opportunity.Stage)) throw new InvalidOperationException("The opportunity is closed. No further pipeline or quote actions are available."); }
    private static void Available(CRMLead lead)
    { if (lead.Status == "Archived") throw new InvalidOperationException("Restore this archived lead before adding or changing work."); }
    private async Task<T> Mutate<T>(string grant, Func<(int company, int branch, string user), Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await permission.Demand(grant, ct);
            var result = await action(await tenant.GetAsync(ct));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
    private void Audit((int company, int branch, string user) t, string action, string reference)
        => db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = action, Reference = $"Branch:{t.branch}/{reference}" });
    private async Task<CRMLead> Lead(long id, (int company, int branch, string user) t, CancellationToken ct)
        => await db.CRMLeads.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new ArgumentException("Lead is not available in the active branch.");
    private async Task<CRMOpportunity> Opportunity(long id, (int company, int branch, string user) t, CancellationToken ct)
        => await db.CRMOpportunities.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new ArgumentException("Opportunity is not available in the active branch.");
    private async Task<CRMQuote> Quote(long id, (int company, int branch, string user) t, CancellationToken ct)
        => await db.CRMQuotes.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new ArgumentException("Quote is not available in the active branch.");

    public Task<CRMLead> SaveLead(CRMLeadInput input, CancellationToken ct = default) => Mutate("CRM.Manage", async t =>
    {
        var reference = Reference(input.Reference);
        var name = Text(input.Name, "Lead name", 160); var contact = Text(input.ContactName, "Contact name", 200, false);
        var email = Text(input.Email, "Email", 200, false); var phone = Text(input.Phone, "Phone", 40, false);
        if (email.Length > 0 && (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)) throw new ArgumentException("Enter a valid email address without a display name.");
        var address = Text(input.Address, "Address", 500, false); var source = Text(input.Source, "Source", 200, false); var notes = Text(input.Notes, "Notes", 2000, false);
        if (input.MarketingConsent is not ("Unknown" or "Granted" or "Withdrawn")) throw new ArgumentException("Select a valid marketing-consent state.");
        var evidence = Text(input.ConsentEvidence, "Consent evidence", 1000, input.MarketingConsent != "Unknown");
        if (input.MarketingConsent == "Granted" && email.Length == 0 && phone.Length == 0) throw new ArgumentException("Granted marketing consent requires a contact email or phone and recorded evidence.");
        var lead = input.Id == 0 ? new CRMLead { CompanyId = t.company, BranchId = t.branch, Reference = reference, CreatedBy = t.user } : await Lead(input.Id, t, ct);
        if (input.Id != 0) { Version(input.RowVersion, lead.RowVersion); Available(lead); if (reference != lead.Reference) throw new ArgumentException("A saved lead reference cannot be changed."); }
        if (await db.CRMLeads.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Reference == reference && x.Id != input.Id, ct)) throw new ArgumentException("This lead reference already exists in the active branch.");
        if (lead.MarketingConsent == "Granted" && input.MarketingConsent == "Granted" && (lead.Email != email || lead.Phone != phone || lead.ContactName != contact || lead.Name != name)
            && (!input.ConsentReviewed || evidence == lead.ConsentEvidence))
            throw new ArgumentException("The contact identity changed. Set consent to Unknown, or record fresh reviewed consent evidence for this contact.");
        var changedConsent = lead.MarketingConsent != input.MarketingConsent || lead.ConsentEvidence != evidence;
        if (changedConsent) Reviewed(input.ConsentReviewed);
        var previous = lead.MarketingConsent;
        lead.Name = name; lead.ContactName = contact; lead.Email = email; lead.Phone = phone; lead.Address = address; lead.Source = source; lead.Notes = notes;
        lead.MarketingConsent = input.MarketingConsent; lead.ConsentEvidence = evidence; lead.UpdatedUtc = DateTimeOffset.UtcNow;
        if (changedConsent) lead.ConsentRecordedUtc = lead.UpdatedUtc;
        if (input.Id == 0) db.CRMLeads.Add(lead);
        await db.SaveChangesAsync(ct);
        if (changedConsent) db.CRMConsentEvents.Add(new CRMConsentEvent { CompanyId = t.company, BranchId = t.branch, LeadId = lead.Id, PreviousConsent = previous, Consent = lead.MarketingConsent, Evidence = evidence, ActorId = t.user });
        Audit(t, input.Id == 0 ? "CRM.LeadCreated" : "CRM.LeadUpdated", lead.Reference);
        if (changedConsent) Audit(t, "CRM.ConsentRecorded", lead.Reference + "/" + lead.MarketingConsent);
        return lead;
    }, ct);

    public Task<CRMLead> ArchiveLead(long id, byte[] rowVersion, bool archive, bool reviewed, CancellationToken ct = default) => Mutate("CRM.Manage", async t =>
    {
        Reviewed(reviewed); var lead = await Lead(id, t, ct); Version(rowVersion, lead.RowVersion);
        if (archive)
        {
            Available(lead);
            if (await db.CRMOpportunities.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.LeadId == id && OpenStages.Contains(x.Stage), ct)
                || await db.CRMActivities.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.LeadId == id && x.Status == "Open", ct))
                throw new InvalidOperationException("Close this lead's open opportunities and activities before archiving it.");
            lead.Status = "Archived";
        }
        else { if (lead.Status != "Archived") throw new InvalidOperationException("Only archived leads can be restored."); lead.Status = lead.CustomerId.HasValue ? "Converted" : "Active"; }
        lead.UpdatedUtc = DateTimeOffset.UtcNow; Audit(t, archive ? "CRM.LeadArchived" : "CRM.LeadRestored", lead.Reference); return lead;
    }, ct);

    public Task<CRMOpportunity> SaveOpportunity(CRMOpportunityInput input, CancellationToken ct = default) => Mutate("CRM.Manage", async t =>
    {
        Reviewed(input.Reviewed); var reference = Reference(input.Reference); var title = Text(input.Title, "Opportunity title", 200); var note = Text(input.ReviewNote, "Qualification/review note", 1000); Date(input.ExpectedCloseDate, "Expected close date");
        var lead = await Lead(input.LeadId, t, ct); Available(lead); Version(input.LeadRowVersion, lead.RowVersion);
        var opportunity = input.Id == 0 ? new CRMOpportunity { CompanyId = t.company, BranchId = t.branch, LeadId = lead.Id, Reference = reference, CreatedBy = t.user } : await Opportunity(input.Id, t, ct);
        if (input.Id != 0)
        {
            Version(input.RowVersion, opportunity.RowVersion); Open(opportunity);
            if (opportunity.LeadId != lead.Id || opportunity.Reference != reference) throw new ArgumentException("A saved opportunity's lead and reference cannot change.");
        }
        if (await db.CRMOpportunities.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Reference == reference && x.Id != input.Id, ct)) throw new ArgumentException("This opportunity reference already exists.");
        opportunity.Title = title; opportunity.ExpectedCloseDate = input.ExpectedCloseDate; opportunity.ReviewNote = note; opportunity.ReviewedBy = t.user; opportunity.UpdatedUtc = DateTimeOffset.UtcNow;
        if (input.Id == 0) db.CRMOpportunities.Add(opportunity);
        Audit(t, input.Id == 0 ? "CRM.OpportunityQualified" : "CRM.OpportunityUpdated", reference); return opportunity;
    }, ct);

    public Task<CRMOpportunity> TransitionOpportunity(long id, byte[] rowVersion, string targetStage, string note, bool reviewed, CancellationToken ct = default) => Mutate("CRM.Review", async t =>
    {
        Reviewed(reviewed); note = Text(note, "Stage review note", 950); var opportunity = await Opportunity(id, t, ct); Version(rowVersion, opportunity.RowVersion); Open(opportunity);
        var allowed = targetStage == "Lost" || (opportunity.Stage == "Qualified" && targetStage == "Proposal") || (opportunity.Stage == "Proposal" && targetStage == "Negotiation") || (opportunity.Stage == "Negotiation" && targetStage == "Proposal");
        if (!allowed) throw new ArgumentException("Invalid stage transition. Won is available only through reviewed quote acceptance.");
        if (targetStage == "Lost")
        {
            foreach (var quote in await db.CRMQuotes.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.OpportunityId == id && (x.Status == "Draft" || x.Status == "Reviewed")).ToListAsync(ct))
            { quote.Status = "Cancelled"; quote.ReviewNote = "Opportunity closed lost: " + note; }
        }
        var old = opportunity.Stage; opportunity.Stage = targetStage; opportunity.ReviewNote = note; opportunity.ReviewedBy = t.user; opportunity.UpdatedUtc = DateTimeOffset.UtcNow;
        Audit(t, "CRM.OpportunityStage", opportunity.Reference + "/" + old + ">" + targetStage); return opportunity;
    }, ct);

    public Task<CRMActivity> SaveActivity(CRMActivityInput input, CancellationToken ct = default) => Mutate("CRM.Manage", async t =>
    {
        var reference = Reference(input.Reference); var title = Text(input.Title, "Activity title", 200); var notes = Text(input.Notes, "Activity notes", 2000, false); Date(input.DueDate, "Due date");
        if (input.Kind is not ("Task" or "Call" or "Meeting")) throw new ArgumentException("Select a valid activity type.");
        var lead = await Lead(input.LeadId, t, ct); Available(lead);
        var activity = input.Id == 0 ? new CRMActivity { CompanyId = t.company, BranchId = t.branch, LeadId = lead.Id, Reference = reference, CreatedBy = t.user } :
            await db.CRMActivities.SingleOrDefaultAsync(x => x.Id == input.Id && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new ArgumentException("Activity is not available in the active branch.");
        if (input.Id != 0)
        { Version(input.RowVersion, activity.RowVersion); if (activity.Status != "Open") throw new InvalidOperationException("Closed activities cannot be changed."); if (activity.Reference != reference || activity.LeadId != lead.Id) throw new ArgumentException("A saved activity's lead and reference cannot change."); }
        if (input.OpportunityId.HasValue)
        {
            var opportunity = await Opportunity(input.OpportunityId.Value, t, ct);
            if (opportunity.LeadId != lead.Id) throw new ArgumentException("The activity opportunity must belong to this lead.");
            // A task may remain open after its opportunity closes. Preserve that historical link
            // while editing the existing task; new or reassigned work still needs an open opportunity.
            if (input.Id == 0 || activity.OpportunityId != input.OpportunityId) Open(opportunity);
        }
        if (await db.CRMActivities.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Reference == reference && x.Id != input.Id, ct)) throw new ArgumentException("This activity reference already exists.");
        activity.OpportunityId = input.OpportunityId; activity.Kind = input.Kind; activity.Title = title; activity.Notes = notes; activity.DueDate = input.DueDate;
        if (input.Id == 0) db.CRMActivities.Add(activity); Audit(t, input.Id == 0 ? "CRM.ActivityCreated" : "CRM.ActivityUpdated", reference); return activity;
    }, ct);

    public Task<CRMActivity> CloseActivity(long id, byte[] rowVersion, string status, string note, CancellationToken ct = default) => Mutate("CRM.Manage", async t =>
    {
        if (status is not ("Done" or "Cancelled")) throw new ArgumentException("Choose Done or Cancelled."); note = Text(note, "Completion/cancellation note", 1000);
        var activity = await db.CRMActivities.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new ArgumentException("Activity is not available in the active branch.");
        Version(rowVersion, activity.RowVersion); if (activity.Status != "Open") throw new InvalidOperationException("This activity is already closed.");
        activity.Status = status; activity.CompletionNote = note; activity.CompletedBy = t.user; activity.CompletedUtc = DateTimeOffset.UtcNow;
        Audit(t, "CRM.Activity" + status, activity.Reference); return activity;
    }, ct);

    public Task<CRMQuote> SaveQuote(CRMQuoteInput input, CancellationToken ct = default) => Mutate("CRM.Manage", async t =>
    {
        var reference = Reference(input.Reference); var terms = Text(input.Terms, "Quote terms", 2000, false); Date(input.QuoteDate, "Quote date"); Date(input.ValidUntil, "Validity date");
        if (input.ValidUntil < input.QuoteDate) throw new ArgumentException("Quote validity cannot end before the quote date.");
        var policy = await CompanyBusinessPolicyService.Require(db, t.company, input.QuoteDate, ct);
        var opportunity = await Opportunity(input.OpportunityId, t, ct); Open(opportunity); var lead = await Lead(opportunity.LeadId, t, ct); Available(lead);
        var quote = input.Id == 0 ? new CRMQuote { CompanyId = t.company, BranchId = t.branch, OpportunityId = opportunity.Id, LeadId = lead.Id, Reference = reference, CreatedBy = t.user } : await Quote(input.Id, t, ct);
        if (input.Id != 0) { Version(input.RowVersion, quote.RowVersion); if (quote.Status != "Draft") throw new InvalidOperationException("Only a draft quote can be edited."); if (quote.OpportunityId != opportunity.Id || quote.Reference != reference) throw new ArgumentException("A saved quote's opportunity and reference cannot change."); }
        if (await db.CRMQuotes.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Reference == reference && x.Id != input.Id, ct)) throw new ArgumentException("This quote reference already exists.");
        if (input.Lines == null || input.Lines.Count is < 1 or > 50) throw new ArgumentException("A quote requires 1–50 nonempty lines.");
        var lines = new List<CRMQuoteLine>();
        foreach (var line in input.Lines)
        {
            BusinessInput.Decimal(line.Quantity, "Line quantity", positive: true); BusinessInput.Amount(line.UnitPrice, "Unit price"); BusinessInput.Amount(line.DiscountAmount, "Discount", false);
            if (decimal.Round(line.UnitPrice, policy.CurrencyMinorUnits) != line.UnitPrice || decimal.Round(line.DiscountAmount, policy.CurrencyMinorUnits) != line.DiscountAmount)
                throw new ArgumentException($"Unit prices and discounts must use at most {policy.CurrencyMinorUnits} decimal places for {policy.CurrencyCode}.");
            var product = await db.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == line.StockItemId && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new ArgumentException("Every quote product must belong to the active branch.");
            if (string.IsNullOrWhiteSpace(product.UnitOfMeasure)) throw new ArgumentException("Every quote product requires a reviewed stock unit in Product Master.");
            decimal gross;
            try { gross = BusinessInput.Amount(decimal.Round(checked(line.Quantity * line.UnitPrice), policy.CurrencyMinorUnits, MidpointRounding.AwayFromZero), "Line gross amount"); }
            catch (OverflowException) { throw new ArgumentException("Line amount exceeds the supported range."); }
            if (line.DiscountAmount >= gross) throw new ArgumentException("A line discount must be less than its gross amount.");
            lines.Add(new CRMQuoteLine { CompanyId = t.company, BranchId = t.branch, LineNumber = lines.Count + 1, StockItemId = product.Id, SKU = product.SKU, ProductName = product.Name, UnitOfMeasure = product.UnitOfMeasure, Quantity = line.Quantity, UnitPrice = line.UnitPrice, GrossAmount = gross, DiscountAmount = line.DiscountAmount, NetAmount = gross - line.DiscountAmount });
        }
        quote.GrossAmount = BusinessInput.Amount(lines.Sum(x => x.GrossAmount), "Quote gross amount"); quote.DiscountAmount = BusinessInput.Amount(lines.Sum(x => x.DiscountAmount), "Quote discount", false); quote.NetAmount = BusinessInput.Amount(lines.Sum(x => x.NetAmount), "Quote net amount");
        quote.BusinessPolicyId = policy.Id; quote.BusinessPolicyVersion = policy.Version; quote.CurrencyCode = policy.CurrencyCode; quote.CurrencyMinorUnits = policy.CurrencyMinorUnits;
        quote.QuoteDate = input.QuoteDate; quote.ValidUntil = input.ValidUntil; quote.Terms = terms;
        quote.CustomerName = lead.Name; quote.ContactName = lead.ContactName; quote.Email = lead.Email; quote.Phone = lead.Phone; quote.Address = lead.Address;
        if (input.Id == 0) { quote.Lines = lines; db.CRMQuotes.Add(quote); }
        else
        {
            db.CRMQuoteLines.RemoveRange(quote.Lines);
            // Delete first to release the unique line numbers, within this transaction; snapshots remain atomic.
            await db.SaveChangesAsync(ct);
            quote.Lines = lines;
        }
        Audit(t, input.Id == 0 ? "CRM.QuoteCreated" : "CRM.QuoteDraftUpdated", quote.Reference); return quote;
    }, ct);

    public Task<CRMQuote> ReviewQuote(long id, byte[] rowVersion, string action, string note, bool reviewed, CancellationToken ct = default) => Mutate("CRM.Review", async t =>
    {
        Reviewed(reviewed); note = Text(note, "Quote review note", 950); var quote = await Quote(id, t, ct); Version(rowVersion, quote.RowVersion);
        var opportunity = await Opportunity(quote.OpportunityId, t, ct); Open(opportunity);
        if (action == "Review")
        {
            if (quote.Status != "Draft") throw new InvalidOperationException("Only a draft quote can be reviewed.");
            await ValidateQuoteForAcceptance(quote, t.company, ct);
            quote.Status = "Reviewed"; quote.ReviewedBy = t.user; quote.ReviewedUtc = DateTimeOffset.UtcNow;
            if (opportunity.Stage == "Qualified") { opportunity.Stage = "Proposal"; opportunity.ReviewedBy = t.user; opportunity.ReviewNote = "Quote reviewed: " + quote.Reference; opportunity.UpdatedUtc = DateTimeOffset.UtcNow; }
        }
        else if (action == "ReturnToDraft")
        { if (quote.Status != "Reviewed") throw new InvalidOperationException("Only a reviewed quote can return to draft."); quote.Status = "Draft"; quote.ReviewedBy = null; quote.ReviewedUtc = null; }
        else if (action == "Cancel")
        { if (quote.Status is not ("Draft" or "Reviewed")) throw new InvalidOperationException("This quote is already final."); quote.Status = "Cancelled"; }
        else throw new ArgumentException("Select Review, ReturnToDraft or Cancel.");
        quote.ReviewNote = note; Audit(t, "CRM.Quote" + action, quote.Reference); return quote;
    }, ct);

    private async Task ValidateQuoteForAcceptance(CRMQuote quote, int company, CancellationToken ct)
    {
        var policy = await CompanyBusinessPolicyService.Require(db, company, quote.QuoteDate, ct);
        var currentPolicy = await CompanyBusinessPolicyService.Current(db, company, ct);
        var today = CompanyBusinessPolicyService.BusinessDate(currentPolicy, DateTimeOffset.UtcNow);
        if (quote.QuoteDate > today || quote.ValidUntil < today) throw new InvalidOperationException("The quote is future-dated or expired in the company's timezone. Return it to draft and review its dates.");
        if (policy.Id != quote.BusinessPolicyId || policy.Version != quote.BusinessPolicyVersion || policy.CurrencyCode != quote.CurrencyCode || policy.CurrencyMinorUnits != quote.CurrencyMinorUnits
            || currentPolicy.CurrencyCode != quote.CurrencyCode || currentPolicy.CurrencyMinorUnits != quote.CurrencyMinorUnits)
            throw new InvalidOperationException("The company's approved currency policy changed. Return the quote to draft and review its currency and amounts.");
        if (quote.Lines.Count is < 1 or > 50 || quote.Lines.Any(x => x.CompanyId != quote.CompanyId || x.BranchId != quote.BranchId || x.NetAmount <= 0
            || x.GrossAmount != decimal.Round(x.Quantity * x.UnitPrice, quote.CurrencyMinorUnits, MidpointRounding.AwayFromZero) || x.NetAmount != x.GrossAmount - x.DiscountAmount)
            || quote.NetAmount != quote.Lines.Sum(x => x.NetAmount) || quote.GrossAmount != quote.Lines.Sum(x => x.GrossAmount) || quote.DiscountAmount != quote.Lines.Sum(x => x.DiscountAmount))
            throw new InvalidOperationException("The quote snapshot failed its amount integrity check.");
    }

    public Task<CRMSalesReady> AcceptQuote(CRMAcceptQuoteInput input, CancellationToken ct = default) => Mutate("CRM.Review", async t =>
    {
        Reviewed(input.Reviewed); var reference = Reference(input.SalesReadyReference); var evidence = Text(input.AcceptanceEvidence, "Customer acceptance evidence", 1000);
        var quote = await Quote(input.QuoteId, t, ct); Version(input.RowVersion, quote.RowVersion);
        if (quote.Status != "Reviewed") throw new InvalidOperationException("Only a reviewed, unaccepted quote may be accepted.");
        var opportunity = await Opportunity(quote.OpportunityId, t, ct); Version(input.OpportunityRowVersion, opportunity.RowVersion); Open(opportunity);
        var lead = await Lead(quote.LeadId, t, ct); Version(input.LeadRowVersion, lead.RowVersion); Available(lead);
        if (opportunity.LeadId != lead.Id) throw new InvalidOperationException("The quote's lead and opportunity do not match.");
        await ValidateQuoteForAcceptance(quote, t.company, ct);
        if (await db.CRMSalesReadyRecords.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && (x.Reference == reference || x.OpportunityId == opportunity.Id || x.QuoteId == quote.Id), ct)) throw new InvalidOperationException("This reference, quote or opportunity has already been converted. Open the existing sales-ready record.");
        TradingParty customer;
        if (lead.CustomerId.HasValue && input.ExistingCustomerId != lead.CustomerId) throw new ArgumentException("This lead already has a customer. Explicitly select its linked customer to avoid creating a duplicate.");
        if (input.ExistingCustomerId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(input.NewCustomerCode)) throw new ArgumentException("Choose either an existing customer or a new customer code.");
            customer = await db.TradingParties.SingleOrDefaultAsync(x => x.Id == input.ExistingCustomerId && x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == "Customer" && x.Active, ct) ?? throw new ArgumentException("Select an active customer in the active branch.");
        }
        else
        {
            var code = Text(input.NewCustomerCode, "New customer code", 30).ToUpperInvariant();
            if (await db.TradingParties.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Code == code, ct)) throw new ArgumentException("This party code already exists. Review and select the existing customer instead.");
            customer = new TradingParty { CompanyId = t.company, BranchId = t.branch, Kind = "Customer", Code = code, Name = quote.CustomerName, Email = quote.Email, Phone = quote.Phone, Address = quote.Address, Active = true };
            db.TradingParties.Add(customer); await db.SaveChangesAsync(ct); Audit(t, "CRM.CustomerCreated", code);
        }
        quote.Status = "Accepted"; quote.AcceptedBy = t.user; quote.AcceptedUtc = DateTimeOffset.UtcNow;
        opportunity.Stage = "Won"; opportunity.ReviewNote = "Accepted quote " + quote.Reference; opportunity.ReviewedBy = t.user; opportunity.UpdatedUtc = quote.AcceptedUtc.Value;
        lead.CustomerId = customer.Id; lead.Status = "Converted"; lead.UpdatedUtc = quote.AcceptedUtc.Value;
        foreach (var other in await db.CRMQuotes.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.OpportunityId == opportunity.Id && x.Id != quote.Id && (x.Status == "Draft" || x.Status == "Reviewed")).ToListAsync(ct))
        { other.Status = "Cancelled"; other.ReviewNote = "Superseded by accepted quote " + quote.Reference; }
        var handoff = new CRMSalesReady { CompanyId = t.company, BranchId = t.branch, QuoteId = quote.Id, OpportunityId = opportunity.Id, LeadId = lead.Id, CustomerId = customer.Id, Reference = reference, CurrencyCode = quote.CurrencyCode, BusinessPolicyId = quote.BusinessPolicyId, CurrencyMinorUnits = quote.CurrencyMinorUnits, CustomerCode = customer.Code, CustomerName = customer.Name, NetAmount = quote.NetAmount, AcceptanceEvidence = evidence, CreatedBy = t.user };
        db.CRMSalesReadyRecords.Add(handoff); Audit(t, "CRM.QuoteAccepted", quote.Reference + "/" + reference); return handoff;
    }, ct);
}
