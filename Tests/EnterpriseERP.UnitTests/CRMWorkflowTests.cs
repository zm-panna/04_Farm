using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class CRMWorkflowTests
{
    private static CRMService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static CRMController Controller(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions, Service(f))
    { ControllerContext = new ControllerContext { HttpContext = f.Http }, TempData = new TempDataDictionary(f.Http, f.Services.GetRequiredService<ITempDataProvider>()) };
    private static CRMLeadInput LeadInput(CRMLead lead) => new() { Id = lead.Id, Reference = lead.Reference, Name = lead.Name, ContactName = lead.ContactName, Email = lead.Email, Phone = lead.Phone, Address = lead.Address, Source = lead.Source, Notes = lead.Notes, MarketingConsent = lead.MarketingConsent, ConsentEvidence = lead.ConsentEvidence, RowVersion = lead.RowVersion.ToArray() };
    private static async Task<(CRMLead lead, CRMOpportunity opportunity, StockItem product, DateOnly today)> Seed(SecurityTestScope f, string suffix = "1")
    {
        if (!await f.Db.UserPermissions.AnyAsync(x => x.UserId == "operator-a" && x.Permission == "*")) await f.GrantAsync("*");
        var policy = await CompanyBusinessPolicyTestData.SeedAsync(f); var today = CompanyBusinessPolicyService.BusinessDate(policy, DateTimeOffset.UtcNow);
        var lead = await Service(f).SaveLead(new CRMLeadInput { Reference = "LEAD-" + suffix, Name = "Farm buyer", ContactName = "Buyer", Email = "buyer@example.invalid", Phone = "+8801000000000", Address = "Reviewed address" });
        var opportunity = await Service(f).SaveOpportunity(new CRMOpportunityInput { LeadId = lead.Id, LeadRowVersion = lead.RowVersion, Reference = "OPP-" + suffix, Title = "Feed purchase", ExpectedCloseDate = today.AddDays(10), ReviewNote = "Need and contact reviewed", Reviewed = true });
        var product = new StockItem { CompanyId = 1, BranchId = 11, SKU = "SKU-" + suffix, Name = "Feed", UnitOfMeasure = "KG" }; f.Db.StockItems.Add(product); await f.Db.SaveChangesAsync();
        return (lead, opportunity, product, today);
    }
    private static CRMQuoteInput QuoteInput(long opportunity, int product, DateOnly today, string reference = "Q1") => new()
    { OpportunityId = opportunity, Reference = reference, QuoteDate = today, ValidUntil = today.AddDays(7), Terms = "Internal net estimate; tax separately reviewed", Lines = [new() { StockItemId = product, Quantity = 2.5m, UnitPrice = 3.33m, DiscountAmount = 0.33m }, new() { StockItemId = product, Quantity = 1.125m, UnitPrice = 2m }] };
    private static async Task<CRMQuote> ReviewedQuote(SecurityTestScope f, long opportunity, int product, DateOnly today, string reference = "Q1")
    { var quote = await Service(f).SaveQuote(QuoteInput(opportunity, product, today, reference)); return await Service(f).ReviewQuote(quote.Id, quote.RowVersion.ToArray(), "Review", "Snapshot reviewed, net amounts only", true); }
    private static async Task<CRMAcceptQuoteInput> Acceptance(SecurityTestScope f, CRMQuote quote, string reference = "HANDOFF1")
    {
        var opportunity = await f.Db.CRMOpportunities.AsNoTracking().SingleAsync(x => x.Id == quote.OpportunityId);
        var lead = await f.Db.CRMLeads.AsNoTracking().SingleAsync(x => x.Id == quote.LeadId);
        return new() { QuoteId = quote.Id, RowVersion = quote.RowVersion.ToArray(), OpportunityRowVersion = opportunity.RowVersion, LeadRowVersion = lead.RowVersion, NewCustomerCode = "CUST1", SalesReadyReference = reference, AcceptanceEvidence = "Customer acceptance manually verified against this quote", Reviewed = true };
    }

    [Fact]
    public async Task MultiLineQuoteSnapshotsPolicyProductsContactsAndRoundsEachLineWithoutPosting()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f);
        var quote = await Service(f).SaveQuote(QuoteInput(s.opportunity.Id, s.product.Id, s.today));
        Assert.Equal(10.58m, quote.GrossAmount); Assert.Equal(0.33m, quote.DiscountAmount); Assert.Equal(10.25m, quote.NetAmount);
        Assert.Equal(8.33m, quote.Lines[0].GrossAmount); Assert.Equal(2.25m, quote.Lines[1].GrossAmount); Assert.Equal(2, quote.Lines.Count);
        Assert.Equal("BDT", quote.CurrencyCode); Assert.Equal(2, quote.CurrencyMinorUnits); Assert.True(quote.BusinessPolicyId > 0);
        s.product.Name = "Renamed product"; s.lead.Name = "Changed live name"; await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        quote = await f.Db.CRMQuotes.Include(x => x.Lines).SingleAsync(); Assert.Equal("Farm buyer", quote.CustomerName); Assert.All(quote.Lines, x => Assert.Equal("Feed", x.ProductName));
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.StockMovements.ToListAsync()); Assert.Empty(await f.Db.ProductSales.ToListAsync()); Assert.Empty(await f.Db.Notifications.ToListAsync());
    }
    [Fact]
    public async Task QuoteConversionIsAtomicScopedAndCreatesExactlyOneInternalCustomerHandoff()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f);
        var quote = await ReviewedQuote(f, s.opportunity.Id, s.product.Id, s.today);
        var other = await Service(f).SaveQuote(QuoteInput(s.opportunity.Id, s.product.Id, s.today, "Q-OTHER"));
        var input = await Acceptance(f, quote); var handoff = await Service(f).AcceptQuote(input);
        Assert.Equal("Accepted", (await f.Db.CRMQuotes.SingleAsync(x => x.Id == quote.Id)).Status);
        Assert.Equal("Cancelled", (await f.Db.CRMQuotes.SingleAsync(x => x.Id == other.Id)).Status);
        Assert.Equal("Won", (await f.Db.CRMOpportunities.SingleAsync()).Stage); Assert.Equal("Converted", (await f.Db.CRMLeads.SingleAsync()).Status);
        var customer = Assert.Single(await f.Db.TradingParties.ToListAsync()); Assert.Equal("Customer", customer.Kind); Assert.Equal(1, customer.CompanyId); Assert.Equal(11, customer.BranchId); Assert.Equal("buyer@example.invalid", customer.Email);
        Assert.Equal(customer.Id, handoff.CustomerId); Assert.Equal(quote.NetAmount, handoff.NetAmount); Assert.Equal(customer.Id, (await f.Db.CRMLeads.SingleAsync()).CustomerId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).AcceptQuote(input));
        Assert.Single(await f.Db.CRMSalesReadyRecords.ToListAsync()); Assert.Single(await f.Db.TradingParties.ToListAsync());
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.StockMovements.ToListAsync()); Assert.Empty(await f.Db.ProductSales.ToListAsync());
        Assert.Contains(await f.Db.Audits.ToListAsync(), x => x.Action == "CRM.QuoteAccepted" && x.Reference.Contains("Branch:11/"));
    }
    [Fact]
    public async Task ConversionRollbackAfterCustomerSavedClearsTrackerAndSafeRetryCreatesNoDuplicate()
    {
        var interceptor = new FailHandoffSave(); await using var f = await SecurityTestScope.CreateAsync(interceptor); var s = await Seed(f);
        var quote = await ReviewedQuote(f, s.opportunity.Id, s.product.Id, s.today); var input = await Acceptance(f, quote);
        interceptor.Enabled = true; await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).AcceptQuote(input));
        Assert.Empty(f.Db.ChangeTracker.Entries()); Assert.Empty(await f.Db.TradingParties.ToListAsync()); Assert.Empty(await f.Db.CRMSalesReadyRecords.ToListAsync()); Assert.Equal("Reviewed", (await f.Db.CRMQuotes.SingleAsync()).Status); Assert.Equal("Proposal", (await f.Db.CRMOpportunities.SingleAsync()).Stage); Assert.Null((await f.Db.CRMLeads.SingleAsync()).CustomerId);
        Assert.DoesNotContain(await f.Db.Audits.ToListAsync(), x => x.Action == "CRM.CustomerCreated" || x.Action == "CRM.QuoteAccepted");
        interceptor.Enabled = false; await Service(f).AcceptQuote(input); Assert.Single(await f.Db.TradingParties.ToListAsync()); Assert.Single(await f.Db.CRMSalesReadyRecords.ToListAsync());
    }
    [Fact]
    public async Task ExistingScopedCustomerIsLinkedWithoutOverwritingContactOrTransferringConsent()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f);
        var customer = new TradingParty { CompanyId = 1, BranchId = 11, Kind = "Customer", Code = "EXISTING", Name = "Existing buyer", Email = "existing@example.invalid" }; f.Db.TradingParties.Add(customer); await f.Db.SaveChangesAsync();
        var quote = await ReviewedQuote(f, s.opportunity.Id, s.product.Id, s.today); var input = await Acceptance(f, quote); input.NewCustomerCode = ""; input.ExistingCustomerId = customer.Id;
        var handoff = await Service(f).AcceptQuote(input); Assert.Equal(customer.Id, handoff.CustomerId); Assert.Single(await f.Db.TradingParties.ToListAsync()); Assert.Equal("existing@example.invalid", (await f.Db.TradingParties.SingleAsync()).Email); Assert.Equal("Unknown", (await f.Db.CRMLeads.SingleAsync()).MarketingConsent);
    }
    [Fact]
    public async Task ConsentNeedsReviewedEvidenceAndFreshIdentityEvidenceAndHistoryCannotChange()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f); var input = LeadInput(s.lead); input.MarketingConsent = "Granted";
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SaveLead(input)); input.ConsentEvidence = "Recorded opt-in for marketing at form 24";
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SaveLead(input)); input.ConsentReviewed = true;
        var lead = await Service(f).SaveLead(input); Assert.Single(await f.Db.CRMConsentEvents.ToListAsync());
        var stale = LeadInput(lead); input = LeadInput(lead); input.Email = "new@example.invalid"; input.ConsentReviewed = true;
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SaveLead(input)); input.ConsentEvidence = "New contact opt-in form 25"; lead = await Service(f).SaveLead(input);
        stale.Name = "Old form"; await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).SaveLead(stale));
        input = LeadInput(lead); input.MarketingConsent = "Withdrawn"; input.ConsentReviewed = true; input.ConsentEvidence = "Withdrawal recorded by contact"; await Service(f).SaveLead(input);
        Assert.Equal(3, await f.Db.CRMConsentEvents.CountAsync()); Assert.Equal("Withdrawn", (await f.Db.CRMLeads.SingleAsync()).MarketingConsent);
        var history = await f.Db.CRMConsentEvents.FirstAsync(); history.Evidence = "rewrite"; await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear();
        f.Db.CRMConsentEvents.Remove(await f.Db.CRMConsentEvents.FirstAsync()); Assert.Throws<InvalidOperationException>(() => f.Db.SaveChanges());
    }
    [Fact]
    public async Task ConsentCannotBeGrantedWithoutAnyContactMethod()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("CRM.Manage");
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SaveLead(new CRMLeadInput { Reference = "NO-CONTACT", Name = "Buyer", MarketingConsent = "Granted", ConsentEvidence = "Claimed evidence", ConsentReviewed = true }));
        Assert.Empty(await f.Db.CRMLeads.ToListAsync());
    }
    [Theory]
    [InlineData("quantity")]
    [InlineData("price")]
    [InlineData("discount")]
    [InlineData("currency_precision")]
    [InlineData("quantity_precision")]
    [InlineData("too_many")]
    [InlineData("overflow")]
    public async Task InvalidQuoteArithmeticCannotPersistPartialSnapshots(string kind)
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f); var input = QuoteInput(s.opportunity.Id, s.product.Id, s.today);
        switch (kind)
        {
            case "quantity": input.Lines[0].Quantity = 0; break;
            case "price": input.Lines[0].UnitPrice = -1; break;
            case "discount": input.Lines[0].DiscountAmount = 9; break;
            case "currency_precision": input.Lines[0].UnitPrice = 1.001m; break;
            case "quantity_precision": input.Lines[0].Quantity = 0.0001m; break;
            case "too_many": input.Lines = Enumerable.Range(0, 51).Select(_ => new CRMQuoteLineInput { StockItemId = s.product.Id, Quantity = 1, UnitPrice = 1 }).ToList(); break;
            case "overflow": input.Lines[0].Quantity = BusinessInput.Maximum; input.Lines[0].UnitPrice = 999999999999999m; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SaveQuote(input)); Assert.Empty(await f.Db.CRMQuotes.ToListAsync()); Assert.Empty(await f.Db.CRMQuoteLines.ToListAsync());
    }
    [Fact]
    public async Task DraftRevisionReplacesLinesAndRequiresFreshReviewBeforeAcceptance()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f); var draft = QuoteInput(s.opportunity.Id, s.product.Id, s.today); var quote = await Service(f).SaveQuote(draft); var stale = quote.RowVersion.ToArray();
        draft.Id = quote.Id; draft.RowVersion = quote.RowVersion.ToArray(); draft.Lines = [new() { StockItemId = s.product.Id, Quantity = 3, UnitPrice = 5 }]; quote = await Service(f).SaveQuote(draft);
        Assert.Single(await f.Db.CRMQuoteLines.ToListAsync()); Assert.Equal(15m, quote.NetAmount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ReviewQuote(quote.Id, stale, "Review", "stale", true));
        quote = await f.Db.CRMQuotes.Include(x => x.Lines).SingleAsync(); quote = await Service(f).ReviewQuote(quote.Id, quote.RowVersion.ToArray(), "Review", "reviewed", true);
        var accept = await Acceptance(f, quote); quote = await Service(f).ReviewQuote(quote.Id, quote.RowVersion.ToArray(), "ReturnToDraft", "Change pricing", true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).AcceptQuote(accept)); Assert.Equal("Draft", (await f.Db.CRMQuotes.SingleAsync()).Status);
    }
    [Fact]
    public async Task StageTransitionsRequireReviewAndWonRequiresQuoteConversion()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f); var version = s.opportunity.RowVersion.ToArray();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).TransitionOpportunity(s.opportunity.Id, version, "Proposal", "Review", false));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).TransitionOpportunity(s.opportunity.Id, version, "Won", "Review", true));
        var quote = await Service(f).SaveQuote(QuoteInput(s.opportunity.Id, s.product.Id, s.today));
        var opportunity = await Service(f).TransitionOpportunity(s.opportunity.Id, version, "Lost", "Customer declined", true);
        Assert.Equal("Cancelled", (await f.Db.CRMQuotes.SingleAsync()).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).TransitionOpportunity(opportunity.Id, opportunity.RowVersion.ToArray(), "Qualified", "Reopen", true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ReviewQuote(quote.Id, quote.RowVersion.ToArray(), "Review", "review", true));
    }
    [Fact]
    public async Task ActivitiesDueDoneAndArchivePreserveHistoryAndRejectRepeatedOrStaleActions()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f);
        var input = new CRMActivityInput { LeadId = s.lead.Id, OpportunityId = s.opportunity.Id, Reference = "TASK1", Kind = "Call", Title = "Record a manually arranged call", DueDate = s.today.AddDays(-1) };
        var activity = await Service(f).SaveActivity(input); var stale = activity.RowVersion.ToArray();
        input.Id = activity.Id; input.RowVersion = stale; input.DueDate = s.today.AddDays(1); activity = await Service(f).SaveActivity(input);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CloseActivity(activity.Id, stale, "Done", "done"));
        activity = await f.Db.CRMActivities.SingleAsync(); var version = activity.RowVersion.ToArray(); await Service(f).CloseActivity(activity.Id, version, "Done", "Manual call recorded; no automated outreach");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CloseActivity(activity.Id, version, "Done", "Duplicate"));
        Assert.Equal("Done", (await f.Db.CRMActivities.SingleAsync()).Status); Assert.NotNull((await f.Db.CRMActivities.SingleAsync()).CompletedUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ArchiveLead(s.lead.Id, s.lead.RowVersion.ToArray(), true, true));
        var opp = await f.Db.CRMOpportunities.SingleAsync(); await Service(f).TransitionOpportunity(opp.Id, opp.RowVersion.ToArray(), "Lost", "Closed", true);
        var lead = await f.Db.CRMLeads.SingleAsync(); lead = await Service(f).ArchiveLead(lead.Id, lead.RowVersion.ToArray(), true, true); Assert.Equal("Archived", lead.Status);
        lead = await Service(f).ArchiveLead(lead.Id, lead.RowVersion.ToArray(), false, true); Assert.Equal("Active", lead.Status);
        Assert.Single(await f.Db.CRMActivities.ToListAsync()); Assert.Empty(await f.Db.Notifications.ToListAsync());
    }
    [Fact]
    public async Task EveryReadAndWriteUsesActiveCompanyAndBranchIncludingConversionCustomerAndProduct()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f); var quote = await ReviewedQuote(f, s.opportunity.Id, s.product.Id, s.today); var acceptance = await Acceptance(f, quote);
        var otherProduct = new StockItem { CompanyId = 1, BranchId = 12, SKU = "OTHER", Name = "Other branch", UnitOfMeasure = "KG" };
        var otherCustomer = new TradingParty { CompanyId = 1, BranchId = 12, Kind = "Customer", Code = "OTHER", Name = "Other branch" }; f.Db.StockItems.Add(otherProduct); f.Db.TradingParties.Add(otherCustomer); await f.Db.SaveChangesAsync();
        var wrong = QuoteInput(s.opportunity.Id, otherProduct.Id, s.today, "CROSS"); await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SaveQuote(wrong));
        acceptance.ExistingCustomerId = otherCustomer.Id; acceptance.NewCustomerCode = ""; await Assert.ThrowsAsync<ArgumentException>(() => Service(f).AcceptQuote(acceptance));
        f.Db.UserBranchAccess.Add(new UserBranchAccess { CompanyId = 1, BranchId = 12, UserId = "operator-a" }); await f.Db.SaveChangesAsync();
        foreach (var target in new[] { (user: "operator-a", company: 1, branch: 12), (user: "operator-b", company: 2, branch: 21) })
        {
            if (target.company == 2) await f.GrantAsync("*", target.user, target.company); f.ActAs(target.user, target.company, target.branch);
            var controller = Controller(f);
            Assert.IsType<NotFoundResult>(await controller.Lead(s.lead.Id, default)); Assert.IsType<NotFoundResult>(await controller.Opportunity(s.opportunity.Id, default)); Assert.IsType<NotFoundResult>(await controller.Quote(quote.Id, default));
            foreach (var section in new[] { "leads", "opportunities", "activities", "quotes", "sales-ready" }) Assert.Equal(0, Assert.IsType<CRMListModel>(Assert.IsType<ViewResult>(await controller.Index(section)).Model).Total);
            await Assert.ThrowsAsync<ArgumentException>(() => Service(f).ReviewQuote(quote.Id, quote.RowVersion, "Review", "Cross-scope", true));
            await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SaveLead(LeadInput(s.lead)));
            await Assert.ThrowsAsync<ArgumentException>(() => Service(f).AcceptQuote(acceptance));
        }
        Assert.Empty(await f.Db.CRMSalesReadyRecords.ToListAsync());
    }
    [Fact]
    public async Task ConversionRejectsDraftUnreviewedExpiredAndChangedLeadWithoutAnyCustomerWrites()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f); var quote = await Service(f).SaveQuote(QuoteInput(s.opportunity.Id, s.product.Id, s.today));
        var accept = await Acceptance(f, quote); await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).AcceptQuote(accept));
        quote = await f.Db.CRMQuotes.Include(x => x.Lines).SingleAsync(); quote = await Service(f).ReviewQuote(quote.Id, quote.RowVersion.ToArray(), "Review", "Reviewed", true); accept = await Acceptance(f, quote);
        accept.Reviewed = false; await Assert.ThrowsAsync<ArgumentException>(() => Service(f).AcceptQuote(accept)); accept.Reviewed = true;
        var lead = await f.Db.CRMLeads.SingleAsync(); var change = LeadInput(lead); change.Notes = "Changed context"; await Service(f).SaveLead(change); await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).AcceptQuote(accept));
        quote = await f.Db.CRMQuotes.Include(x => x.Lines).SingleAsync(); quote = await Service(f).ReviewQuote(quote.Id, quote.RowVersion.ToArray(), "ReturnToDraft", "Expired", true);
        var draft = QuoteInput(s.opportunity.Id, s.product.Id, s.today.AddDays(-10)); draft.Id = quote.Id; draft.Reference = quote.Reference; draft.RowVersion = quote.RowVersion.ToArray(); draft.ValidUntil = s.today.AddDays(-1); quote = await Service(f).SaveQuote(draft);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).ReviewQuote(quote.Id, quote.RowVersion.ToArray(), "Review", "Expired", true));
        Assert.Empty(await f.Db.TradingParties.ToListAsync()); Assert.Empty(await f.Db.CRMSalesReadyRecords.ToListAsync());
    }
    [Fact]
    public async Task AcceptedSnapshotAndHandoffCannotBeMutatedOrDeletedBySyncOrAsyncSave()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f); var quote = await ReviewedQuote(f, s.opportunity.Id, s.product.Id, s.today); await Service(f).AcceptQuote(await Acceptance(f, quote));
        quote = await f.Db.CRMQuotes.Include(x => x.Lines).SingleAsync(); quote.CustomerName = "rewrite"; await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear();
        var line = await f.Db.CRMQuoteLines.FirstAsync(); line.Quantity = 100; Assert.Throws<InvalidOperationException>(() => f.Db.SaveChanges()); f.Db.ChangeTracker.Clear();
        f.Db.CRMQuoteLines.Remove(await f.Db.CRMQuoteLines.FirstAsync()); await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear();
        f.Db.CRMSalesReadyRecords.Remove(await f.Db.CRMSalesReadyRecords.SingleAsync()); Assert.Throws<InvalidOperationException>(() => f.Db.SaveChanges());
    }
    [Fact]
    public async Task ViewManageAndReviewPermissionsAreSeparateAndNoGrantCannotMutate()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f);
        f.Db.UserPermissions.RemoveRange(await f.Db.UserPermissions.ToListAsync()); await f.Db.SaveChangesAsync(); await f.GrantAsync("CRM.View");
        Assert.IsType<ViewResult>(await Controller(f).Index()); await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).SaveLead(new CRMLeadInput { Reference = "DENIED", Name = "Denied" }));
        await f.GrantAsync("CRM.Manage"); var quote = await Service(f).SaveQuote(QuoteInput(s.opportunity.Id, s.product.Id, s.today));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).ReviewQuote(quote.Id, quote.RowVersion, "Review", "Denied", true));
        Assert.Equal("Draft", (await f.Db.CRMQuotes.SingleAsync()).Status);
    }
    [Fact]
    public async Task ReferenceSearchAndStatusPaginationStayScopedAndRetainFilters()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); await CompanyBusinessPolicyTestData.SeedAsync(f);
        for (var i = 0; i < 31; i++) await Service(f).SaveLead(new CRMLeadInput { Reference = "PAGE-" + i.ToString("D2"), Name = "Searchable lead " + i });
        var first = Assert.IsType<CRMListModel>(Assert.IsType<ViewResult>(await Controller(f).Index("leads", "searchable", "Active", page: 1)).Model);
        var second = Assert.IsType<CRMListModel>(Assert.IsType<ViewResult>(await Controller(f).Index("leads", "searchable", "Active", page: 2)).Model);
        Assert.Equal(31, first.Total); Assert.Equal(25, first.Leads.Count); Assert.Equal(6, second.Leads.Count); Assert.Empty(first.Leads.Select(x => x.Id).Intersect(second.Leads.Select(x => x.Id))); Assert.Equal("searchable", second.Query); Assert.Equal("Active", second.Status);
    }
    [Fact]
    public async Task QuoteRequiresApprovedCompanyCurrencyPolicyAndRejectsUnknownStockUnits()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f);
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockItems SET UnitOfMeasure={""} WHERE Id={s.product.Id}"); f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).SaveQuote(QuoteInput(s.opportunity.Id, s.product.Id, s.today)));
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockItems SET UnitOfMeasure={"KG"} WHERE Id={s.product.Id}");
        await f.Db.Database.ExecuteSqlRawAsync("DELETE FROM CompanyCurrencyLocks");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).SaveQuote(QuoteInput(s.opportunity.Id, s.product.Id, s.today))); Assert.Empty(await f.Db.CRMQuotes.ToListAsync());
    }
    [Fact]
    public async Task DatabaseConcurrencyCollisionRollsBackCustomerAndAllowsFreshReviewedRetry()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f);
        var quote = await ReviewedQuote(f, s.opportunity.Id, s.product.Id, s.today); var stale = await Acceptance(f, quote);
        await using (var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(f.Db.Database.GetDbConnection()).Options))
        {
            var row = await other.CRMQuotes.SingleAsync(); row.ReviewNote = "Concurrent reviewer note"; await other.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(f).AcceptQuote(stale));
        Assert.Empty(f.Db.ChangeTracker.Entries()); Assert.Empty(await f.Db.TradingParties.ToListAsync()); Assert.Empty(await f.Db.CRMSalesReadyRecords.ToListAsync());
        quote = await f.Db.CRMQuotes.Include(x => x.Lines).SingleAsync(); Assert.Equal("Reviewed", quote.Status);
        await Service(f).AcceptQuote(await Acceptance(f, quote)); Assert.Single(await f.Db.CRMSalesReadyRecords.ToListAsync()); Assert.Single(await f.Db.TradingParties.ToListAsync());
    }
    [Fact]
    public async Task OptionalCRMFieldsAreNotImplicitlyRequiredByMvc()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var lead = Controller(f);
        Assert.True(lead.TryValidateModel(new CRMLeadInput { Reference = "OPTIONAL", Name = "Buyer", ContactName = null, Email = null, Phone = null, Address = null, Source = null, Notes = null, ConsentEvidence = null }));
        Assert.True(Controller(f).TryValidateModel(new CRMActivityInput { Reference = "TASK", Title = "Internal task", Notes = null }));
        Assert.True(Controller(f).TryValidateModel(new CRMQuoteInput { Reference = "QUOTE", Terms = null }));
        Assert.True(Controller(f).TryValidateModel(new CRMAcceptQuoteInput { SalesReadyReference = "HANDOFF", AcceptanceEvidence = "Evidence", ExistingCustomerId = 1, NewCustomerCode = null }));
    }
    [Fact]
    public async Task OpenActivityCanRetainClosedOpportunityDuringEditWithoutSilentlyLosingItsLink()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var s = await Seed(f);
        var activity = await Service(f).SaveActivity(new CRMActivityInput { LeadId = s.lead.Id, OpportunityId = s.opportunity.Id, Reference = "FOLLOW-UP", Title = "Follow-up", DueDate = s.today });
        await Service(f).TransitionOpportunity(s.opportunity.Id, s.opportunity.RowVersion.ToArray(), "Lost", "Customer declined", true);
        var editor = Assert.IsType<CRMActivityEditor>(Assert.IsType<ViewResult>(await Controller(f).EditActivity(activity.Id, default)).Model);
        Assert.Contains(editor.Opportunities, x => x.Id == s.opportunity.Id && x.Stage == "Lost");
        editor.Input.DueDate = s.today.AddDays(1); var saved = await Service(f).SaveActivity(editor.Input);
        Assert.Equal(s.opportunity.Id, saved.OpportunityId); Assert.Equal(s.today.AddDays(1), saved.DueDate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).SaveActivity(new CRMActivityInput { LeadId = s.lead.Id, OpportunityId = s.opportunity.Id, Reference = "CLOSED-NEW", Title = "New task", DueDate = s.today }));
    }
    private sealed class FailHandoffSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<CRMSalesReady>().Any(x => x.State == EntityState.Added)) throw new InvalidOperationException("Injected failure after customer creation and before handoff commit.");
            return ValueTask.FromResult(result);
        }
    }
}
