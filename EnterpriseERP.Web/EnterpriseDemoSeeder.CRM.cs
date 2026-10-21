using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;
public static partial class EnterpriseDemoSeeder
{
    // Opt-in enterprise fixtures only. Contacts, acceptance and consent evidence are explicitly fictional.
    internal static async Task SeedCRM(AppDbContext db, Company company, Branch branch, AppUser maker, AppUser reviewer,
        CompanyBusinessPolicy policy, TaxPolicy tax, int number, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var reference = $"DEMO-CRM-{number}";
        if (await db.CRMLeads.AnyAsync(x => x.CompanyId == company.Id && x.BranchId == branch.Id && x.Reference == reference, ct)) return;
        var customer = await db.TradingParties.Where(x => x.CompanyId == company.Id && x.BranchId == branch.Id && x.Kind == "Customer" && x.Active).OrderBy(x => x.Id).FirstAsync(ct);
        var product = await db.StockItems.Where(x => x.CompanyId == company.Id && x.BranchId == branch.Id && x.UnitOfMeasure != "").OrderBy(x => x.Id).FirstAsync(ct);
        var lead = new CRMLead { CompanyId = company.Id, BranchId = branch.Id, Reference = reference, Name = $"Fictional CRM buyer {number}", ContactName = "Synthetic contact", Email = $"crm-demo-{number}@example.invalid", Address = "Synthetic demonstration address",
            Source = "Opt-in enterprise demo", Notes = "FICTIONAL DEMO ONLY. Not a real sales commitment or consent grant.", MarketingConsent = "Granted", ConsentEvidence = "FICTIONAL test opt-in; no real person's marketing consent is asserted.", ConsentRecordedUtc = now,
            Status = "Converted", CustomerId = customer.Id, CreatedBy = maker.Id, CreatedUtc = now, UpdatedUtc = now };
        db.CRMLeads.Add(lead); await db.SaveChangesAsync(ct);
        db.CRMConsentEvents.Add(new CRMConsentEvent { CompanyId = company.Id, BranchId = branch.Id, LeadId = lead.Id, PreviousConsent = "Unknown", Consent = "Granted", Evidence = lead.ConsentEvidence, ActorId = maker.Id, RecordedUtc = now });
        var opportunity = new CRMOpportunity { CompanyId = company.Id, BranchId = branch.Id, LeadId = lead.Id, Reference = reference + "-OPP", Title = "Fictional reviewed farm supply opportunity", Stage = "Won", ExpectedCloseDate = today,
            ReviewNote = "FICTIONAL accepted quote fixture; no sale or invoice posted.", CreatedBy = maker.Id, ReviewedBy = reviewer.Id, CreatedUtc = now, UpdatedUtc = now };
        db.CRMOpportunities.Add(opportunity); await db.SaveChangesAsync(ct);
        db.CRMActivities.Add(new CRMActivity { CompanyId = company.Id, BranchId = branch.Id, LeadId = lead.Id, OpportunityId = opportunity.Id, Reference = reference + "-TASK", Kind = "Task", Title = "Synthetic internal quotation review", DueDate = today,
            Notes = "Demonstration of a completed internal task; no call or message was sent.", Status = "Done", CompletionNote = "FICTIONAL review complete.", CreatedBy = maker.Id, CompletedBy = reviewer.Id, CreatedUtc = now, CompletedUtc = now });
        var price = CompanyBusinessPolicyService.RoundMoney(100m + number, policy.CurrencyMinorUnits);
        var net = CompanyBusinessPolicyService.RoundMoney(2m * price, policy.CurrencyMinorUnits);
        var quote = new CRMQuote { CompanyId = company.Id, BranchId = branch.Id, OpportunityId = opportunity.Id, LeadId = lead.Id, Reference = reference + "-QUOTE", BusinessPolicyId = policy.Id, BusinessPolicyVersion = policy.Version,
            CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits, QuoteDate = today, ValidUntil = today.AddDays(14), Status = "Accepted", CustomerName = lead.Name, ContactName = lead.ContactName, Email = lead.Email, Address = lead.Address,
            Terms = "FICTIONAL net-only estimate; tax is not calculated in CRM.", ReviewNote = "FICTIONAL reviewed snapshot", GrossAmount = net, NetAmount = net, CreatedBy = maker.Id, ReviewedBy = reviewer.Id, AcceptedBy = reviewer.Id, CreatedUtc = now, ReviewedUtc = now, AcceptedUtc = now,
            Lines = [new CRMQuoteLine { CompanyId = company.Id, BranchId = branch.Id, LineNumber = 1, StockItemId = product.Id, SKU = product.SKU, ProductName = product.Name, UnitOfMeasure = product.UnitOfMeasure, Quantity = 2m, UnitPrice = price, GrossAmount = net, NetAmount = net }] };
        db.CRMQuotes.Add(quote); await db.SaveChangesAsync(ct);
        db.CRMSalesReadyRecords.Add(new CRMSalesReady { CompanyId = company.Id, BranchId = branch.Id, QuoteId = quote.Id, OpportunityId = opportunity.Id, LeadId = lead.Id, CustomerId = customer.Id, Reference = reference + "-READY", BusinessPolicyId = policy.Id, CurrencyCode = policy.CurrencyCode, CurrencyMinorUnits = policy.CurrencyMinorUnits,
            CustomerCode = customer.Code, CustomerName = customer.Name, NetAmount = net, AcceptanceEvidence = "FICTIONAL acceptance fixture; no real customer acceptance or statutory tax determination.", CreatedBy = reviewer.Id, CreatedUtc = now });
        db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = reviewer.Id, Action = "CRM.DemoSeeded", Reference = $"Branch:{branch.Id}/{reference}", CreatedUtc = now });
        await db.SaveChangesAsync(ct);
    }
}
