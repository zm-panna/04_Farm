using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;

public sealed class CRMListModel
{
    public string Section { get; set; } = "leads";
    public string? Query { get; set; }
    public string? Status { get; set; }
    public long? LeadId { get; set; }
    public int Page { get; set; }
    public int Total { get; set; }
    public bool CanManage { get; set; }
    public DateOnly Today { get; set; }
    public string? PolicyNotice { get; set; }
    public List<CRMLead> Leads { get; set; } = [];
    public List<CRMOpportunity> Opportunities { get; set; } = [];
    public List<CRMActivity> Activities { get; set; } = [];
    public List<CRMQuote> Quotes { get; set; } = [];
    public List<CRMSalesReady> Handoffs { get; set; } = [];
}
public sealed record CRMLeadDetail(CRMLead Lead, List<CRMConsentEvent> ConsentEvents, List<CRMOpportunity> Opportunities, List<CRMActivity> Activities, bool CanManage, int ConsentPage, int ConsentTotal);
public sealed record CRMOpportunityEditor(CRMOpportunityInput Input, CRMLead Lead);
public sealed record CRMOpportunityDetail(CRMOpportunity Opportunity, CRMLead Lead, List<CRMQuote> Quotes, bool CanManage, bool CanReview);
public sealed record CRMActivityEditor(CRMActivityInput Input, CRMLead Lead, List<CRMOpportunity> Opportunities);
public sealed record CRMActivityDetail(CRMActivity Activity, CRMLead Lead, bool CanManage);
public sealed record CRMQuoteEditor(CRMQuoteInput Input, CRMOpportunity Opportunity, CRMLead Lead, List<StockItem> Products, string CurrencyCode, int CurrencyMinorUnits);
public sealed record CRMQuoteDetail(CRMQuote Quote, CRMOpportunity Opportunity, CRMLead Lead, List<TradingParty> Customers, CRMAcceptQuoteInput Acceptance, CRMSalesReady? Handoff, string? CustomerQuery, bool CanManage, bool CanReview);
public sealed record CRMSalesReadyDetail(CRMSalesReady Handoff, CRMQuote Quote);

[Authorize, Route("crm")]
public sealed class CRMController(AppDbContext db, TenantContext tenant, PermissionService permission, CRMService crm) : Controller
{
    private async Task<(int company, int branch, string user)> Scope(CancellationToken ct)
    { await permission.Demand("CRM.View", ct); return await tenant.GetAsync(ct); }
    private Task<bool> Can((int company, int branch, string user) t, string name, CancellationToken ct) => db.UserPermissions.AsNoTracking().AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == name || x.Permission == "*"), ct);
    private static bool Expected(Exception ex) => ex is ArgumentException or InvalidOperationException or DbUpdateException;
    private void Error(Exception ex) => ModelState.AddModelError("", ex is DbUpdateException ? "This record changed or its reference already exists. Reload and review the latest version before retrying." : ex.Message);
    private async Task<(DateOnly date, string? notice)> Today(int company, CancellationToken ct)
    {
        try { var policy = await CompanyBusinessPolicyService.Current(db, company, ct); return (CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow), null); }
        catch (InvalidOperationException ex) { return (DateOnly.FromDateTime(DateTime.UtcNow), ex.Message + " Due-date highlighting temporarily uses UTC; configure Business policies before quoting."); }
    }
    [HttpGet("")]
    public async Task<IActionResult> Index(string section = "leads", string? q = null, string? status = null, long? leadId = null, int page = 1, CancellationToken ct = default)
    {
        var t = await Scope(ct); if (section is not ("leads" or "opportunities" or "activities" or "quotes" or "sales-ready")) section = "leads";
        q = q?.Trim(); if (q?.Length > 200) q = q[..200]; var search = q?.ToUpperInvariant(); page = Math.Clamp(page, 1, 100000); var today = await Today(t.company, ct);
        var model = new CRMListModel { Section = section, Query = q, Status = status, LeadId = leadId, Page = page, CanManage = await Can(t, "CRM.Manage", ct), Today = today.date, PolicyNotice = today.notice };
        if (section == "leads")
        {
            var rows = db.CRMLeads.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
            if (!string.IsNullOrEmpty(search)) rows = rows.Where(x => x.Reference.Contains(search) || x.Name.ToUpper().Contains(search) || x.ContactName.ToUpper().Contains(search) || x.Email.ToUpper().Contains(search));
            if (!string.IsNullOrEmpty(status)) rows = rows.Where(x => x.Status == status);
            model.Total = await rows.CountAsync(ct); model.Leads = await rows.OrderByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        }
        else if (section == "opportunities")
        {
            var rows = db.CRMOpportunities.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
            if (leadId.HasValue) rows = rows.Where(x => x.LeadId == leadId); if (!string.IsNullOrEmpty(search)) rows = rows.Where(x => x.Reference.Contains(search) || x.Title.ToUpper().Contains(search)); if (!string.IsNullOrEmpty(status)) rows = rows.Where(x => x.Stage == status);
            model.Total = await rows.CountAsync(ct); model.Opportunities = await rows.OrderByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        }
        else if (section == "activities")
        {
            var rows = db.CRMActivities.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
            if (leadId.HasValue) rows = rows.Where(x => x.LeadId == leadId); if (!string.IsNullOrEmpty(search)) rows = rows.Where(x => x.Reference.Contains(search) || x.Title.ToUpper().Contains(search));
            if (status == "Overdue") rows = rows.Where(x => x.Status == "Open" && x.DueDate < today.date); else if (!string.IsNullOrEmpty(status)) rows = rows.Where(x => x.Status == status);
            model.Total = await rows.CountAsync(ct); model.Activities = await rows.OrderBy(x => x.DueDate).ThenByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        }
        else if (section == "quotes")
        {
            var rows = db.CRMQuotes.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
            if (leadId.HasValue) rows = rows.Where(x => x.LeadId == leadId); if (!string.IsNullOrEmpty(search)) rows = rows.Where(x => x.Reference.Contains(search) || x.CustomerName.ToUpper().Contains(search)); if (!string.IsNullOrEmpty(status)) rows = rows.Where(x => x.Status == status);
            model.Total = await rows.CountAsync(ct); model.Quotes = await rows.OrderByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        }
        else
        {
            var rows = db.CRMSalesReadyRecords.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch);
            if (leadId.HasValue) rows = rows.Where(x => x.LeadId == leadId); if (!string.IsNullOrEmpty(search)) rows = rows.Where(x => x.Reference.Contains(search) || x.CustomerName.ToUpper().Contains(search) || x.CustomerCode.Contains(search));
            model.Total = await rows.CountAsync(ct); model.Handoffs = await rows.OrderByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        }
        return View(model);
    }

    [HttpGet("leads/new")]
    public async Task<IActionResult> NewLead(CancellationToken ct) { await permission.Demand("CRM.Manage", ct); await Scope(ct); return View("LeadEdit", new CRMLeadInput()); }
    [HttpGet("leads/{id:long}/edit")]
    public async Task<IActionResult> EditLead(long id, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct); var t = await Scope(ct); var lead = await db.CRMLeads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (lead == null) return NotFound();
        return View("LeadEdit", new CRMLeadInput { Id = lead.Id, Reference = lead.Reference, Name = lead.Name, ContactName = lead.ContactName, Email = lead.Email, Phone = lead.Phone, Address = lead.Address, Source = lead.Source, Notes = lead.Notes, MarketingConsent = lead.MarketingConsent, ConsentEvidence = lead.ConsentEvidence, RowVersion = lead.RowVersion });
    }
    [HttpPost("leads/save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLead(CRMLeadInput input, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct);
        if (ModelState.IsValid) try { var lead = await crm.SaveLead(input, ct); TempData["Success"] = "Lead contact and consent details saved."; return RedirectToAction(nameof(Lead), new { id = lead.Id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        return View("LeadEdit", input);
    }
    [HttpGet("leads/{id:long}")]
    public async Task<IActionResult> Lead(long id, CancellationToken ct, int consentPage = 1)
    {
        var t = await Scope(ct); var lead = await db.CRMLeads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (lead == null) return NotFound();
        var events = db.CRMConsentEvents.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.LeadId == id); consentPage = Math.Clamp(consentPage, 1, 100000);
        return View("Lead", new CRMLeadDetail(lead, await events.OrderByDescending(x => x.Id).Skip((consentPage - 1) * 20).Take(20).ToListAsync(ct),
            await db.CRMOpportunities.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.LeadId == id).OrderByDescending(x => x.Id).Take(10).ToListAsync(ct),
            await db.CRMActivities.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.LeadId == id).OrderByDescending(x => x.Id).Take(10).ToListAsync(ct), await Can(t, "CRM.Manage", ct), consentPage, await events.CountAsync(ct)));
    }
    [HttpPost("leads/{id:long}/archive"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveLead(long id, byte[] rowVersion, bool archive, bool reviewed, CancellationToken ct)
    {
        if (ModelState.IsValid) try { await crm.ArchiveLead(id, rowVersion, archive, reviewed, ct); TempData["Success"] = archive ? "Lead archived; its history is preserved." : "Lead restored."; return RedirectToAction(nameof(Lead), new { id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        return await Lead(id, ct);
    }
    [HttpGet("opportunities/new")]
    public async Task<IActionResult> NewOpportunity(long leadId, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct); var t = await Scope(ct); var lead = await db.CRMLeads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == leadId && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (lead == null) return NotFound();
        return View("OpportunityEdit", new CRMOpportunityEditor(new CRMOpportunityInput { LeadId = lead.Id, LeadRowVersion = lead.RowVersion, ExpectedCloseDate = (await Today(t.company, ct)).date.AddDays(30) }, lead));
    }
    [HttpGet("opportunities/{id:long}/edit")]
    public async Task<IActionResult> EditOpportunity(long id, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct); var t = await Scope(ct); var row = await db.CRMOpportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (row == null) return NotFound();
        var lead = await db.CRMLeads.AsNoTracking().SingleAsync(x => x.Id == row.LeadId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        return View("OpportunityEdit", new CRMOpportunityEditor(new CRMOpportunityInput { Id = id, LeadId = lead.Id, Reference = row.Reference, Title = row.Title, ExpectedCloseDate = row.ExpectedCloseDate, ReviewNote = row.ReviewNote, LeadRowVersion = lead.RowVersion, RowVersion = row.RowVersion }, lead));
    }
    [HttpPost("opportunities/save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOpportunity([Bind(Prefix = "Input")] CRMOpportunityInput input, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct);
        if (ModelState.IsValid) try { var row = await crm.SaveOpportunity(input, ct); TempData["Success"] = "Reviewed opportunity saved."; return RedirectToAction(nameof(Opportunity), new { id = row.Id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        var t = await Scope(ct); var lead = await db.CRMLeads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.LeadId && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (lead == null) return NotFound();
        return View("OpportunityEdit", new CRMOpportunityEditor(input, lead));
    }
    [HttpGet("opportunities/{id:long}")]
    public async Task<IActionResult> Opportunity(long id, CancellationToken ct)
    {
        var t = await Scope(ct); var row = await db.CRMOpportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (row == null) return NotFound();
        var lead = await db.CRMLeads.AsNoTracking().SingleAsync(x => x.Id == row.LeadId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        return View("Opportunity", new CRMOpportunityDetail(row, lead, await db.CRMQuotes.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.OpportunityId == id).OrderByDescending(x => x.Id).Take(10).ToListAsync(ct), await Can(t, "CRM.Manage", ct), await Can(t, "CRM.Review", ct)));
    }
    [HttpPost("opportunities/{id:long}/stage"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Stage(long id, byte[] rowVersion, string targetStage, string note, bool reviewed, CancellationToken ct)
    {
        if (ModelState.IsValid) try { await crm.TransitionOpportunity(id, rowVersion, targetStage, note, reviewed, ct); TempData["Success"] = "Reviewed opportunity stage saved."; return RedirectToAction(nameof(Opportunity), new { id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        return await Opportunity(id, ct);
    }
    private async Task<CRMActivityEditor?> ActivityEditor(CRMActivityInput input, CancellationToken ct)
    {
        var t = await Scope(ct); var lead = await db.CRMLeads.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.LeadId && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (lead == null) return null;
        var opportunities = await db.CRMOpportunities.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.LeadId == lead.Id && CRMService.OpenStages.Contains(x.Stage)).OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);
        if (input.Id != 0 && input.OpportunityId.HasValue && opportunities.All(x => x.Id != input.OpportunityId))
        {
            var current = await db.CRMOpportunities.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.LeadId == lead.Id && x.Id == input.OpportunityId, ct);
            if (current != null) opportunities.Add(current);
        }
        return new CRMActivityEditor(input, lead, opportunities);
    }
    [HttpGet("activities/new")]
    public async Task<IActionResult> NewActivity(long leadId, long? opportunityId, CancellationToken ct)
    { await permission.Demand("CRM.Manage", ct); var t = await Scope(ct); var model = await ActivityEditor(new CRMActivityInput { LeadId = leadId, OpportunityId = opportunityId, DueDate = (await Today(t.company, ct)).date }, ct); return model == null ? NotFound() : View("ActivityEdit", model); }
    [HttpGet("activities/{id:long}/edit")]
    public async Task<IActionResult> EditActivity(long id, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct); var t = await Scope(ct); var row = await db.CRMActivities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (row == null) return NotFound();
        return View("ActivityEdit", await ActivityEditor(new CRMActivityInput { Id = id, LeadId = row.LeadId, OpportunityId = row.OpportunityId, Reference = row.Reference, Kind = row.Kind, Title = row.Title, Notes = row.Notes, DueDate = row.DueDate, RowVersion = row.RowVersion }, ct));
    }
    [HttpPost("activities/save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveActivity([Bind(Prefix = "Input")] CRMActivityInput input, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct);
        if (ModelState.IsValid) try { var row = await crm.SaveActivity(input, ct); TempData["Success"] = "Internal activity saved; no communication was sent."; return RedirectToAction(nameof(Activity), new { id = row.Id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        var model = await ActivityEditor(input, ct); return model == null ? NotFound() : View("ActivityEdit", model);
    }
    [HttpGet("activities/{id:long}")]
    public async Task<IActionResult> Activity(long id, CancellationToken ct)
    {
        var t = await Scope(ct); var row = await db.CRMActivities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (row == null) return NotFound();
        var lead = await db.CRMLeads.AsNoTracking().SingleAsync(x => x.Id == row.LeadId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        return View("Activity", new CRMActivityDetail(row, lead, await Can(t, "CRM.Manage", ct)));
    }
    [HttpPost("activities/{id:long}/close"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseActivity(long id, byte[] rowVersion, string status, string note, CancellationToken ct)
    {
        if (ModelState.IsValid) try { await crm.CloseActivity(id, rowVersion, status, note, ct); TempData["Success"] = "Activity closed with its completion record."; return RedirectToAction(nameof(Activity), new { id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        return await Activity(id, ct);
    }
    private async Task<CRMQuoteEditor?> QuoteEditor(CRMQuoteInput input, CancellationToken ct)
    {
        var t = await Scope(ct); var opportunity = await db.CRMOpportunities.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.OpportunityId && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (opportunity == null) return null;
        var lead = await db.CRMLeads.AsNoTracking().SingleAsync(x => x.Id == opportunity.LeadId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        string currency = "Policy required"; var units = 3;
        try { var policy = await CompanyBusinessPolicyService.Require(db, t.company, input.QuoteDate, ct); currency = policy.CurrencyCode; units = policy.CurrencyMinorUnits; } catch (InvalidOperationException ex) { ModelState.AddModelError("", ex.Message); }
        return new CRMQuoteEditor(input, opportunity, lead, await db.StockItems.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderBy(x => x.SKU).ToListAsync(ct), currency, units);
    }
    [HttpGet("quotes/new")]
    public async Task<IActionResult> NewQuote(long opportunityId, CancellationToken ct)
    { await permission.Demand("CRM.Manage", ct); var t = await Scope(ct); var today = await Today(t.company, ct); var model = await QuoteEditor(new CRMQuoteInput { OpportunityId = opportunityId, QuoteDate = today.date, ValidUntil = today.date.AddDays(30), Lines = Enumerable.Range(0, 5).Select(_ => new CRMQuoteLineInput()).ToList() }, ct); return model == null ? NotFound() : View("QuoteEdit", model); }
    [HttpGet("quotes/{id:long}/edit")]
    public async Task<IActionResult> EditQuote(long id, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct); var t = await Scope(ct); var row = await db.CRMQuotes.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (row == null) return NotFound();
        var lines = row.Lines.OrderBy(x => x.LineNumber).Select(x => new CRMQuoteLineInput { StockItemId = x.StockItemId, Quantity = x.Quantity, UnitPrice = x.UnitPrice, DiscountAmount = x.DiscountAmount }).ToList();
        lines.AddRange(Enumerable.Range(0, Math.Min(5, 50 - lines.Count)).Select(_ => new CRMQuoteLineInput()));
        return View("QuoteEdit", await QuoteEditor(new CRMQuoteInput { Id = id, OpportunityId = row.OpportunityId, Reference = row.Reference, QuoteDate = row.QuoteDate, ValidUntil = row.ValidUntil, Terms = row.Terms, RowVersion = row.RowVersion, Lines = lines }, ct));
    }
    [HttpPost("quotes/save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQuote([Bind(Prefix = "Input")] CRMQuoteInput input, CancellationToken ct)
    {
        await permission.Demand("CRM.Manage", ct);
        if (ModelState.IsValid) try { input.Lines = input.Lines.Where(x => x.StockItemId != 0 || x.Quantity != 0 || x.UnitPrice != 0 || x.DiscountAmount != 0).ToList(); var quote = await crm.SaveQuote(input, ct); TempData["Success"] = "Net-only quote draft saved. Product and recipient details were snapshotted; no quote was sent."; return RedirectToAction(nameof(Quote), new { id = quote.Id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        var model = await QuoteEditor(input, ct); return model == null ? NotFound() : View("QuoteEdit", model);
    }
    private async Task<CRMQuoteDetail?> QuoteDetail(long id, string? customerQuery, CRMAcceptQuoteInput? acceptance, CancellationToken ct)
    {
        var t = await Scope(ct); var quote = await db.CRMQuotes.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (quote == null) return null;
        var opportunity = await db.CRMOpportunities.AsNoTracking().SingleAsync(x => x.Id == quote.OpportunityId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        var lead = await db.CRMLeads.AsNoTracking().SingleAsync(x => x.Id == quote.LeadId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        var customers = db.TradingParties.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == "Customer" && x.Active);
        customerQuery = customerQuery?.Trim(); if (customerQuery?.Length > 200) customerQuery = customerQuery[..200];
        var query = customerQuery?.ToUpperInvariant(); if (!string.IsNullOrWhiteSpace(query)) customers = customers.Where(x => x.Name.ToUpper().Contains(query) || x.Code.ToUpper().Contains(query));
        var rows = await customers.OrderBy(x => x.Code).Take(25).ToListAsync(ct);
        var selected = acceptance?.ExistingCustomerId ?? lead.CustomerId;
        if (selected.HasValue && rows.All(x => x.Id != selected)) { var linked = await db.TradingParties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selected && x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == "Customer" && x.Active, ct); if (linked != null) rows.Add(linked); }
        var handoff = await db.CRMSalesReadyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.QuoteId == id, ct);
        return new CRMQuoteDetail(quote, opportunity, lead, rows, acceptance ?? new CRMAcceptQuoteInput { QuoteId = id, RowVersion = quote.RowVersion, LeadRowVersion = lead.RowVersion, OpportunityRowVersion = opportunity.RowVersion, ExistingCustomerId = lead.CustomerId }, handoff, customerQuery, await Can(t, "CRM.Manage", ct), await Can(t, "CRM.Review", ct));
    }
    [HttpGet("quotes/{id:long}")]
    public async Task<IActionResult> Quote(long id, CancellationToken ct, string? customerQuery = null)
    { var model = await QuoteDetail(id, customerQuery, null, ct); return model == null ? NotFound() : View("Quote", model); }
    [HttpPost("quotes/{id:long}/review"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewQuote(long id, byte[] rowVersion, string action, string note, bool reviewed, CancellationToken ct)
    {
        if (ModelState.IsValid) try { await crm.ReviewQuote(id, rowVersion, action, note, reviewed, ct); TempData["Success"] = "Quote review recorded; no communication was sent."; return RedirectToAction(nameof(Quote), new { id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        return await Quote(id, ct);
    }
    [HttpPost("quotes/accept"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptQuote([Bind(Prefix = "Acceptance")] CRMAcceptQuoteInput input, CancellationToken ct)
    {
        await permission.Demand("CRM.Review", ct);
        if (ModelState.IsValid) try { var handoff = await crm.AcceptQuote(input, ct); TempData["Success"] = "Quote acceptance recorded and internal sales-ready handoff created. No stock, invoice, tax or ledger posting was made."; return RedirectToAction(nameof(SalesReady), new { id = handoff.Id }); } catch (Exception ex) when (Expected(ex)) { Error(ex); }
        var model = await QuoteDetail(input.QuoteId, null, input, ct); return model == null ? NotFound() : View("Quote", model);
    }
    [HttpGet("sales-ready/{id:long}")]
    public async Task<IActionResult> SalesReady(long id, CancellationToken ct)
    {
        var t = await Scope(ct); var row = await db.CRMSalesReadyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct); if (row == null) return NotFound();
        var quote = await db.CRMQuotes.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == row.QuoteId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        return View("SalesReady", new CRMSalesReadyDetail(row, quote));
    }
}
