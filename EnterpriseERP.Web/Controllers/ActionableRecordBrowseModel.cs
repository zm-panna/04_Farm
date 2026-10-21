using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;

namespace EnterpriseERP.Web.Controllers;

// Query links and form state come from the same bounded results that feed action selectors.
public sealed class ActionableRecordBrowseModel
{
    public WorkspacePage Page { get; }
    public string Status { get; }
    public string Action { get; }
    public string Anchor { get; }
    public string PageField { get; }
    public string SearchField { get; }
    public string StatusField { get; }
    public string SearchLabel { get; }
    public string Hint { get; }
    public IReadOnlyList<string> StatusOptions { get; }

    public ActionableRecordBrowseModel(BillingPage model) : this(model.InvoicesPage, model.InvoiceStatus, true) { }
    public ActionableRecordBrowseModel(AutomationPage model) : this(model.ApprovalsPage, model.ApprovalStatus, false) { }

    private ActionableRecordBrowseModel(WorkspacePage page, string status, bool invoices)
    {
        Page = page;
        Status = status;
        Action = invoices ? "/Subscription" : "/Automation";
        Anchor = invoices ? "invoice-browse" : "approval-browse";
        PageField = invoices ? "invoicePage" : "approvalPage";
        SearchField = invoices ? "invoiceSearch" : "approvalSearch";
        StatusField = invoices ? "invoiceStatus" : "approvalStatus";
        SearchLabel = invoices ? "Search invoice reference" : "Search approval ID, rule ID, or requester";
        Hint = invoices ? "Choose an open invoice from the current results to record a payment." : "Choose a pending proposal from the current results to approve it.";
        StatusOptions = invoices ? ["All", "Open", "Paid", "Cancelled"] : ["All", "Pending", "Approved"];
    }

    public string PageLink(int page) => QueryHelpers.AddQueryString(Action, new Dictionary<string, string?>
    {
        [PageField] = Math.Clamp(page, 1, Page.TotalPages).ToString(CultureInfo.InvariantCulture),
        [SearchField] = Page.Search,
        [StatusField] = Status
    }) + "#" + Anchor;
}
