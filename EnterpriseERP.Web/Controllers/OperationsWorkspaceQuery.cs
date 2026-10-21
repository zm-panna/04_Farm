using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web.Controllers;

// Separate query state lets an operator locate both sides of an old reconciliation
// without losing the other selector or the independently selected report period.
public sealed class OperationsWorkspaceQuery
{
    public int OrderPage { get; set; } = 1;
    public string? OrderSearch { get; set; }
    public bool OrdersOpenOnly { get; set; }
    public int DocumentPage { get; set; } = 1;
    public string? DocumentSearch { get; set; }
    public int BankPage { get; set; } = 1;
    public string? BankSearch { get; set; }
    public bool BankUnmatchedOnly { get; set; }
    public int VoucherPage { get; set; } = 1;
    public string? VoucherSearch { get; set; }
}

public sealed record WorkspacePage(int PageNumber = 1, int PageSize = 200, int TotalCount = 0, string Search = "")
{
    public int TotalPages => TotalCount == 0 ? 1 : 1 + (TotalCount - 1) / PageSize;
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;
    public int Offset => (PageNumber - 1) * PageSize;

    public static string NormalizeSearch(string? search)
    {
        var value = search?.Trim() ?? "";
        return value.Length > 80 ? value[..80] : value;
    }

    public static async Task<WorkspacePage> CreateAsync<T>(IQueryable<T> query, int requestedPage, int pageSize, string search, CancellationToken ct)
    {
        var count = await query.CountAsync(ct);
        var lastPage = count == 0 ? 1 : 1 + (count - 1) / pageSize;
        // Clamp before multiplying: negative and int.MaxValue input cannot overflow Skip.
        return new WorkspacePage(Math.Clamp(requestedPage, 1, lastPage), pageSize, count, search);
    }
}

public enum WorkspaceBrowseKind { Orders, Documents, BankLines, CashVouchers }

public sealed class WorkspaceBrowseModel
{
    public string Action { get; }
    public string Anchor { get; }
    public WorkspacePage Page { get; }
    public string PageField { get; }
    public string SearchField { get; }
    public string? FilterField { get; }
    public string? FilterLabel { get; }
    public bool FilterValue { get; }
    public IReadOnlyDictionary<string, string?> PreservedState { get; }
    private readonly Dictionary<string, string?> state;

    public WorkspaceBrowseModel(OperationsWorkspaceModel model, WorkspaceBrowseKind kind)
    {
        Action = model.Kind switch { "Accounts" => "/Accounts", "Purchase" => "/Purchase", _ => "/Sales" };
        state = new Dictionary<string, string?>();
        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        if (model.Kind == "Accounts")
        {
            state["from"] = model.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            state["through"] = model.Through.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            state["account"] = model.Account;
            state["BankPage"] = Number(model.BankLinesPage.PageNumber);
            state["BankSearch"] = model.BankLinesPage.Search;
            state["BankUnmatchedOnly"] = model.BankUnmatchedOnly.ToString();
            state["VoucherPage"] = Number(model.CashVouchersPage.PageNumber);
            state["VoucherSearch"] = model.CashVouchersPage.Search;
        }
        else
        {
            state["DocumentPage"] = Number(model.DocumentsPage.PageNumber);
            state["DocumentSearch"] = model.DocumentsPage.Search;
            if (model.Kind == "Purchase")
            {
                state["OrderPage"] = Number(model.OrdersPage.PageNumber);
                state["OrderSearch"] = model.OrdersPage.Search;
                state["OrdersOpenOnly"] = model.OrdersOpenOnly.ToString();
                if (model.SelectedStockItemId.HasValue) state["itemId"] = Number(model.SelectedStockItemId.Value);
            }
        }
        (Page, PageField, SearchField, Anchor) = kind switch
        {
            WorkspaceBrowseKind.Orders => (model.OrdersPage, "OrderPage", "OrderSearch", "order-browse"),
            WorkspaceBrowseKind.Documents => (model.DocumentsPage, "DocumentPage", "DocumentSearch", "document-browse"),
            WorkspaceBrowseKind.BankLines => (model.BankLinesPage, "BankPage", "BankSearch", "bank-browse"),
            _ => (model.CashVouchersPage, "VoucherPage", "VoucherSearch", "voucher-browse")
        };
        if (kind == WorkspaceBrowseKind.Orders) (FilterField, FilterLabel, FilterValue) = ("OrdersOpenOnly", "Open orders only", model.OrdersOpenOnly);
        if (kind == WorkspaceBrowseKind.BankLines) (FilterField, FilterLabel, FilterValue) = ("BankUnmatchedOnly", "Unmatched lines only", model.BankUnmatchedOnly);
        PreservedState = state.Where(x => x.Key != PageField && x.Key != SearchField && x.Key != FilterField).ToDictionary(x => x.Key, x => x.Value);
    }

    public string PageLink(int page)
    {
        var values = new Dictionary<string, string?>(state) { [PageField] = Math.Clamp(page, 1, Page.TotalPages).ToString(CultureInfo.InvariantCulture) };
        return QueryHelpers.AddQueryString(Action, values) + "#" + Anchor;
    }
}

public sealed record BoundedRegisterPager(string Action,WorkspacePage Page);
