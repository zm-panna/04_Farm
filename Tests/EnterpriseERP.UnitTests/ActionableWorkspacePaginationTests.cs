using System.Security.Claims;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class ActionableWorkspacePaginationTests
{
    private static WorkspaceController Billing(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions,
        new FarmHealthFeedService(f.Db, f.Tenant, f.Permissions), new ReportService(f.Db, f.Tenant, f.Permissions),
        new FinanceService(f.Db, f.Tenant, f.Permissions), new SubscriptionService(f.Db, f.Tenant, f.Permissions, f.Accessor))
    {
        ControllerContext = new ControllerContext { HttpContext = f.Http },
        Url = new UrlHelper(new ActionContext(f.Http, new RouteData(), new ActionDescriptor())),
        TempData = new TempDataDictionary(f.Http, f.Services.GetRequiredService<ITempDataProvider>())
    };

    private static AutomationWorkspaceController Automation(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions,
        new AutomationApprovalService(f.Db, f.Tenant, f.Permissions), null!, new ConfigurationBuilder().Build())
    {
        ControllerContext = new ControllerContext { HttpContext = f.Http },
        Url = new UrlHelper(new ActionContext(f.Http, new RouteData(), new ActionDescriptor())),
        TempData = new TempDataDictionary(f.Http, f.Services.GetRequiredService<ITempDataProvider>())
    };

    private static T Model<T>(IActionResult result) => Assert.IsType<T>(Assert.IsType<ViewResult>(result).Model);

    private static async Task<SubscriptionInvoice> OldDebt(SecurityTestScope f)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var old = new SubscriptionInvoice { CompanyId = 1, Reference = "OLD-DEBT", DueDate = today.AddDays(-90), Amount = 100 };
        f.Db.SubscriptionInvoices.Add(old);
        f.Db.SubscriptionInvoices.AddRange(Enumerable.Range(1, 100).Select(i => new SubscriptionInvoice
        { CompanyId = 1, Reference = $"RECENT-{i:000}", DueDate = today, Amount = 100, Paid = 100 }));
        f.Db.SubscriptionInvoices.Add(new SubscriptionInvoice
        { CompanyId = 2, Reference = "OLD-DEBT", DueDate = today.AddDays(-120), Amount = 900 });
        await f.Db.SaveChangesAsync();
        return old;
    }

    private static async Task<(AutomationRule Rule, long OldId)> OldApproval(SecurityTestScope f)
    {
        await f.GrantAsync("Automation.Request");
        var rule = new AutomationRule { CompanyId = 1, Name = "Review stock", Trigger = "Manual", Action = "Review", Active = true };
        var otherRule = new AutomationRule { CompanyId = 2, Name = "Other company", Trigger = "Manual", Action = "Review", Active = true };
        f.Db.AutomationRules.AddRange(rule, otherRule);
        await f.Db.SaveChangesAsync();
        var id = await new AutomationApprovalService(f.Db, f.Tenant, f.Permissions).Request(rule.Id, "Review old proposal", default);
        f.Db.AutomationApprovals.AddRange(Enumerable.Range(1, 50).Select(_ => new AutomationApproval
        { CompanyId = 1, RuleId = rule.Id, RequesterId = "operator-a", ApproverId = "approver-a", Status = "Approved", PayloadHash = "historical" }));
        f.Db.AutomationApprovals.Add(new AutomationApproval
        { CompanyId = 2, RuleId = otherRule.Id, RequesterId = "operator-b", Status = "Pending", PayloadHash = "other-company" });
        await f.Db.SaveChangesAsync();
        return (rule, id);
    }

    [Fact]
    public async Task Oldest101stInvoiceRemainsReachableAndPayableWhileSuspended()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var old = await OldDebt(f);
        var controller = Billing(f);
        var recent = Model<BillingPage>(await controller.Subscription(default));
        Assert.Equal(100, recent.Invoices.Count);
        Assert.Equal(101, recent.InvoicesPage.TotalCount);
        Assert.Equal(2, recent.InvoicesPage.TotalPages);
        Assert.Equal("All", recent.InvoiceStatus);
        Assert.DoesNotContain(recent.Invoices, x => x.Id == old.Id);
        Assert.Equal(new SubscriptionState(100, 90, "Suspended"), recent.State);

        var second = Model<BillingPage>(await controller.Subscription(default, invoicePage: 2));
        Assert.Equal(old.Id, Assert.Single(second.Invoices).Id);
        var open = Model<BillingPage>(await controller.Subscription(default, invoiceStatus: "open"));
        Assert.Equal(old.Id, Assert.Single(open.Invoices, x => !x.Cancelled && x.Amount > x.Paid).Id);
        Assert.Equal(1, open.InvoicesPage.TotalCount);
        var searched = Model<BillingPage>(await controller.Subscription(default, invoiceSearch: " OLD-DEBT "));
        Assert.Equal(old.Id, Assert.Single(searched.Invoices).Id);
        Assert.Equal("OLD-DEBT", searched.InvoicesPage.Search);

        await f.GrantAsync("Subscription.Pay");
        ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim("permission", "platform.billing"));
        Assert.IsType<RedirectToActionResult>(await controller.SubscriptionPayment(Assert.Single(open.Invoices).Id, "RECEIVED-OLD", 100, default));
        Assert.Equal("Current", Model<BillingPage>(await controller.Subscription(default)).State.Status);
        Assert.Equal(old.Id, (await f.Db.SubscriptionPayments.SingleAsync()).InvoiceId);
    }

    [Fact]
    public async Task InvoiceBrowseFiltersHaveDisjointStatusesAndDoNotChangeDebtEvaluation()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await OldDebt(f);
        f.Db.SubscriptionInvoices.Add(new SubscriptionInvoice
        { CompanyId = 1, Reference = "CANCELLED", DueDate = DateOnly.FromDateTime(DateTime.UtcNow), Amount = 400, Cancelled = true });
        await f.Db.SaveChangesAsync();
        var controller = Billing(f);
        var paid = Model<BillingPage>(await controller.Subscription(default, invoiceStatus: "PAID"));
        Assert.Equal(100, paid.Invoices.Count);
        Assert.All(paid.Invoices, x => Assert.True(!x.Cancelled && x.Paid >= x.Amount));
        var cancelled = Model<BillingPage>(await controller.Subscription(default, invoiceStatus: " Cancelled "));
        Assert.Equal("CANCELLED", Assert.Single(cancelled.Invoices).Reference);
        Assert.Equal("Suspended", cancelled.State.Status);
        Assert.Equal(100, cancelled.State.Due);
        var empty = Model<BillingPage>(await controller.Subscription(default, invoiceSearch: "missing", invoicePage: int.MaxValue));
        Assert.Empty(empty.Invoices);
        Assert.Equal(1, empty.InvoicesPage.PageNumber);
        Assert.Equal(1, empty.InvoicesPage.TotalPages);
        Assert.Equal("Suspended", empty.State.Status);
    }

    [Fact]
    public async Task InvoicePagesAreBoundedStableAndClampInvalidPageNumbers()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await OldDebt(f);
        var controller = Billing(f);
        var first = Model<BillingPage>(await controller.Subscription(default, invoicePage: int.MinValue));
        Assert.Equal(1, first.InvoicesPage.PageNumber);
        Assert.Equal(100, first.InvoicesPage.PageSize);
        Assert.Equal(first.Invoices.Select(x => x.Id).OrderDescending(), first.Invoices.Select(x => x.Id));
        var last = Model<BillingPage>(await controller.Subscription(default, invoicePage: int.MaxValue));
        Assert.Equal(2, last.InvoicesPage.PageNumber);
        Assert.Single(last.Invoices);
        Assert.Empty(first.Invoices.Select(x => x.Id).Intersect(last.Invoices.Select(x => x.Id)));
        var clipped = Model<BillingPage>(await controller.Subscription(default, invoiceSearch: new string('x', 90), invoiceStatus: "invalid"));
        Assert.Equal(80, clipped.InvoicesPage.Search.Length);
        Assert.Equal("All", clipped.InvoiceStatus);
    }

    [Fact]
    public async Task InvoiceSearchAndPageTotalsAreCompanyScopedAndPaymentStillRequiresAuthority()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var old = await OldDebt(f);
        var controller = Billing(f);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.SubscriptionPayment(old.Id, "DENIED", 1, default));
        await f.GrantAsync("Subscription.Pay");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.SubscriptionPayment(old.Id, "DENIED", 1, default));
        Assert.Empty(await f.Db.SubscriptionPayments.ToListAsync());
        f.ActAs("operator-b", 2, 21);
        var other = Model<BillingPage>(await controller.Subscription(default, invoiceSearch: "OLD-DEBT"));
        Assert.Equal(1, other.InvoicesPage.TotalCount);
        Assert.Equal(2, Assert.Single(other.Invoices).CompanyId);
        Assert.Equal(900, other.State.Due);
        f.ActAs("operator-a", 2, 21);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Subscription(default, invoicePage: 2));
        f.ActAs(authenticated: false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Subscription(default));
    }

    [Fact]
    public async Task Oldest51stApprovalRemainsReachableAndIndependentlyApprovable()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var (_, oldId) = await OldApproval(f);
        await f.GrantAsync("Automation.Review", "approver-a");
        await f.GrantAsync("Automation.Approve", "approver-a");
        f.ActAs("approver-a");
        var controller = Automation(f);
        var recent = Model<AutomationPage>(await controller.Index(default));
        Assert.Equal(50, recent.Approvals.Count);
        Assert.Equal(51, recent.ApprovalsPage.TotalCount);
        Assert.Equal("All", recent.ApprovalStatus);
        Assert.DoesNotContain(recent.Approvals, x => x.Id == oldId);
        var second = Model<AutomationPage>(await controller.Index(default, approvalPage: 2));
        Assert.Equal(oldId, Assert.Single(second.Approvals).Id);
        var pending = Model<AutomationPage>(await controller.Index(default, approvalStatus: "pending"));
        Assert.Equal(oldId, Assert.Single(pending.Approvals, x => x.Status == "Pending").Id);
        Assert.Equal(1, pending.ApprovalsPage.TotalCount);
        Assert.IsType<RedirectToActionResult>(await controller.Approve(Assert.Single(pending.Approvals).Id, "Review old proposal", default));
        Assert.Equal("Approved", (await f.Db.AutomationApprovals.AsNoTracking().SingleAsync(x => x.Id == oldId)).Status);
        Assert.Empty(await f.Db.AutomationExecutions.ToListAsync());
        Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }

    [Fact]
    public async Task ApprovalSearchFindsExactIdRuleAndRequesterWithinCurrentCompany()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var (rule, oldId) = await OldApproval(f);
        await f.GrantAsync("Automation.Review");
        var controller = Automation(f);
        var byId = Model<AutomationPage>(await controller.Index(default, approvalSearch: $" {oldId} ", approvalStatus: "Pending"));
        Assert.Equal(oldId, Assert.Single(byId.Approvals).Id);
        var byRule = Model<AutomationPage>(await controller.Index(default, approvalSearch: rule.Id.ToString(), approvalStatus: "Approved"));
        Assert.Equal(50, byRule.ApprovalsPage.TotalCount);
        Assert.All(byRule.Approvals, x => Assert.Equal(rule.Id, x.RuleId));
        var byRequester = Model<AutomationPage>(await controller.Index(default, approvalSearch: "operator-a", approvalPage: 2));
        Assert.Equal(51, byRequester.ApprovalsPage.TotalCount);
        Assert.Equal(oldId, Assert.Single(byRequester.Approvals).Id);
        var otherRequester = Model<AutomationPage>(await controller.Index(default, approvalSearch: "operator-b"));
        Assert.Empty(otherRequester.Approvals);
        Assert.Equal(0, otherRequester.ApprovalsPage.TotalCount);
    }

    [Fact]
    public async Task PendingApprovalPagesAreBoundedAndSearchIsNormalized()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await OldApproval(f);
        await f.Db.AutomationApprovals.Where(x => x.CompanyId == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Pending"));
        await f.GrantAsync("Automation.Review");
        var controller = Automation(f);
        var first = Model<AutomationPage>(await controller.Index(default, approvalPage: int.MinValue, approvalStatus: " Pending "));
        Assert.Equal(1, first.ApprovalsPage.PageNumber);
        Assert.Equal(50, first.ApprovalsPage.PageSize);
        Assert.Equal(50, first.Approvals.Count);
        Assert.Equal(51, first.ApprovalsPage.TotalCount);
        Assert.Equal(first.Approvals.Select(x => x.Id).OrderDescending(), first.Approvals.Select(x => x.Id));
        var last = Model<AutomationPage>(await controller.Index(default, approvalPage: int.MaxValue, approvalStatus: "Pending"));
        Assert.Equal(2, last.ApprovalsPage.PageNumber);
        Assert.Single(last.Approvals);
        Assert.Empty(first.Approvals.Select(x => x.Id).Intersect(last.Approvals.Select(x => x.Id)));
        var empty = Model<AutomationPage>(await controller.Index(default, approvalPage: int.MaxValue, approvalSearch: new string('x', 90), approvalStatus: "invalid"));
        Assert.Empty(empty.Approvals);
        Assert.Equal(1, empty.ApprovalsPage.PageNumber);
        Assert.Equal(80, empty.ApprovalsPage.Search.Length);
        Assert.Equal("All", empty.ApprovalStatus);
    }

    [Fact]
    public async Task ApprovalBrowseStillRequiresReviewPermissionAndAuthorizedTenantSelection()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await OldApproval(f);
        var controller = Automation(f);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Index(default, approvalStatus: "Pending"));
        await f.GrantAsync("Automation.Approve");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Index(default, approvalPage: 2));
        await f.GrantAsync("Automation.Review", "operator-b", 2);
        f.ActAs("operator-b", 2, 21);
        var other = Model<AutomationPage>(await controller.Index(default, approvalStatus: "Pending"));
        Assert.Equal(1, other.ApprovalsPage.TotalCount);
        Assert.Equal(2, Assert.Single(other.Approvals).CompanyId);
        f.ActAs("operator-a", 2, 21);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Index(default));
        f.ActAs(authenticated: false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Index(default));
    }

    [Fact]
    public void BrowserPageLinksPreserveEncodedSearchAndStatusWithBoundedPage()
    {
        var invoice = new BillingPage(new SubscriptionState(0, 0, "Current"), [], [])
        { InvoicesPage = new WorkspacePage(1, 100, 101, "ref &/?#"), InvoiceStatus = "Open" };
        var invoiceLink = new ActionableRecordBrowseModel(invoice).PageLink(int.MaxValue);
        var invoiceUri = new Uri("https://example.invalid" + invoiceLink);
        var query = QueryHelpers.ParseQuery(invoiceUri.Query);
        Assert.Equal("/Subscription", invoiceUri.AbsolutePath);
        Assert.Equal("#invoice-browse", invoiceUri.Fragment);
        Assert.Equal("2", query["invoicePage"].ToString());
        Assert.Equal("ref &/?#", query["invoiceSearch"].ToString());
        Assert.Equal("Open", query["invoiceStatus"].ToString());
        var approval = new AutomationPage([], [], [], [], false)
        { ApprovalsPage = new WorkspacePage(2, 50, 51, "operator+a"), ApprovalStatus = "Pending" };
        var approvalUri = new Uri("https://example.invalid" + new ActionableRecordBrowseModel(approval).PageLink(int.MinValue));
        query = QueryHelpers.ParseQuery(approvalUri.Query);
        Assert.Equal("/Automation", approvalUri.AbsolutePath);
        Assert.Equal("#approval-browse", approvalUri.Fragment);
        Assert.Equal("1", query["approvalPage"].ToString());
        Assert.Equal("operator+a", query["approvalSearch"].ToString());
        Assert.Equal("Pending", query["approvalStatus"].ToString());
    }
}
