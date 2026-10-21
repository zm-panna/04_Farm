using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed class ScanLabelInput
{
    [Required] public string Kind { get; set; } = "";
    public long TargetId { get; set; }
    [Required] public string Format { get; set; } = "Internal";
    [StringLength(80)] public string Value { get; set; } = "";
}
public sealed class ScanDraftInput
{
    [Required, StringLength(32)] public string RequestKey { get; set; } = "";
    [Required] public string Action { get; set; } = "";
    public long LabelId { get; set; }
    public long? SecondaryLabelId { get; set; }
    public int? DestinationBranchId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly? Date { get; set; }
    public DateOnly? NextDueDate { get; set; }
    [StringLength(20)] public string EventType { get; set; } = "";
    [StringLength(1000)] public string Notes { get; set; } = "";
    [StringLength(160)] public string Custodian { get; set; } = "";
    [StringLength(160)] public string Location { get; set; } = "";
}
public sealed record ScanTarget(long Id, string Title, string State, string Unit, decimal? Available, string Link);
public sealed record ScanDocumentPreview(string Kind, decimal Amount, string CurrencyCode, int DisplayDecimals);
public sealed record ScanPreview(long LabelId, int BranchId, string Kind, string Format, string Value, ScanTarget Target, string[] Actions, ScanDocumentPreview? Document = null);
public sealed record ScanImpact(decimal? JournalAmount, string CurrencyCode, int DisplayDecimals, string PreviousCustodian, string PreviousLocation, decimal? PreviousWeightKg);
public sealed record ScanReview(ScanPreview Primary, ScanPreview? Secondary, ScanDraftInput Input, DateOnly BusinessDate, string Consequence, ScanImpact Impact);
public sealed record ScanDraftView(long Id, string Status, string BusinessReference, DateTimeOffset ExpiresUtc, ScanReview Review);
public sealed record ScanLabelView(long Id, string Kind, string Format, string Value, string Title, bool Revoked, string Reason);

/// <summary>Scanner identifiers never authorize access. No posting occurs on resolution or drafting.
/// Confirmation rechecks labels, permissions, ownership and source versions inside the existing domain transaction.</summary>
public sealed class ScanService(AppDbContext db, TenantContext tenant, PermissionService permission,
    FarmService farm, FarmHealthFeedService health, FarmBusinessService business, IntegratedOperations operations,
    StockTransferService transfers, FixedAssetService assets, TimeProvider? clock = null)
{
    public static readonly string[] Kinds = ["Product", "Animal", "Asset", "PurchaseOrder", "Document"];
    private DateTimeOffset Now => (clock ?? TimeProvider.System).GetUtcNow();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static string ViewGrant(string kind) => kind switch
    { "Product" => "Inventory.View", "Animal" => "Livestock.View", "Asset" => "FixedAssets.View", "PurchaseOrder" => "Purchase.View", "Document" => "Accounts.View", _ => throw new ArgumentException("Choose a supported scan context.") };
    private static string[] ActionGrants(string action) => action switch
    {
        "AdjustStock" => ["Inventory.View", "Inventory.Move"], "Transfer" => ["Inventory.View", "Inventory.Transfer"],
        "Weight" => ["Livestock.View", "Fattening.Create"], "Health" => ["Livestock.View", "AnimalHealth.Create"],
        "Feed" => ["Livestock.View", "Inventory.View", "FeedManagement.Create", "Inventory.Move"],
        "ReceiveOrder" => ["Purchase.View", "Purchase.Post", "Inventory.View"], "Custody" => ["FixedAssets.View", "FixedAssets.Custody"],
        _ => throw new ArgumentException("Choose a supported scan action.")
    };
    private static string ActionKind(string action) => action switch
    { "AdjustStock" or "Transfer" => "Product", "Weight" or "Health" or "Feed" => "Animal", "ReceiveOrder" => "PurchaseOrder", "Custody" => "Asset", _ => throw new ArgumentException("Choose a supported scan action.") };
    private async Task<(int company, int branch, string user)> Access(CancellationToken ct)
    { await permission.Demand("Scanning.Use", ct); return await tenant.GetAsync(ct); }
    private async Task DemandAction(string action, CancellationToken ct)
    { foreach (var grant in ActionGrants(action)) await permission.Demand(grant, ct); }
    private async Task DemandBranch((int company, int branch, string user) t, int branch, CancellationToken ct)
    {
        if (branch == t.branch) return;
        await permission.Demand("Inventory.Transfer", ct);
        if (!await db.UserBranchAccess.AsNoTracking().AnyAsync(x => x.UserId == t.user && x.CompanyId == t.company && x.BranchId == branch, ct)
            || !await db.Branches.AsNoTracking().AnyAsync(x => x.Id == branch && x.CompanyId == t.company, ct))
            throw new UnauthorizedAccessException("Access to the destination branch is required.");
    }
    private void Audit((int company, int branch, string user) t, string action, string reference) => db.Audits.Add(new AuditEntry
    { CompanyId = t.company, ActorId = t.user, Action = "Scanning." + action, Reference = $"branch:{t.branch};{reference}", CreatedUtc = Now });
    private async Task<T> Write<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var previous = db.ScanWorkflowWrite; db.ScanWorkflowWrite = true;
        try { await Access(ct); var result = await action(); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return result; }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        finally { db.ScanWorkflowWrite = previous; }
    }
    public static bool ValidGtin(string value, int length)
    {
        if (value.Length != length || value.Any(c => c is < '0' or > '9')) return false;
        var sum = 0; var factor = 3;
        for (var i = value.Length - 2; i >= 0; --i) { sum += (value[i] - '0') * factor; factor = 4 - factor; }
        return (10 - sum % 10) % 10 == value[^1] - '0';
    }
    private static string CleanCode(string? value)
    {
        value = value?.Trim() ?? "";
        if (value.Length is < 1 or > 80 || value.Any(c => c is < '!' or > '~') || value.Contains("://", StringComparison.Ordinal))
            throw new ArgumentException("Enter a label value of 1 to 80 visible ASCII characters; URLs are not accepted.");
        return value;
    }
    private static string[] EquivalentValues(string value) => ScanRetailBarcode.Equivalents(value);
    public async Task<ScanLabelView> CreateLabel(ScanLabelInput input, CancellationToken ct = default)
    {
        var t = await Access(ct); await permission.Demand("Scanning.ManageLabels", ct); await permission.Demand(ViewGrant(input.Kind), ct);
        if (input.TargetId <= 0 || input.TargetId > int.MaxValue && input.Kind is "Product" or "Animal") throw new ArgumentException("Select an available target in this branch.");
        var value = input.Format == "Internal" ? "FARM1-" + input.Kind.ToUpperInvariant() + "-" + Guid.NewGuid().ToString("N").ToUpperInvariant() : CleanCode(input.Value);
        if (input.Format != "Internal" && (input.Kind != "Product" || value.StartsWith("FARM1-", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("External barcode mappings are available only for products; internal labels are generated by the server.");
        if (input.Format is not ("Internal" or "SKU" or "Code128" or "EAN13" or "EAN8" or "UPCA" or "UPCE") ||
            input.Format == "EAN13" && !ValidGtin(value, 13) || input.Format == "EAN8" && !ValidGtin(value, 8) || input.Format == "UPCA" && !ValidGtin(value, 12) || input.Format == "UPCE" && (value.Length != 8 || !EquivalentValues(value).Any(x => x.Length == 12)))
            throw new ArgumentException("Choose a supported label format and valid barcode checksum.");
        return await Write(async () =>
        {
            await permission.Demand("Scanning.ManageLabels", ct);
            var label = new ScanLabel { CompanyId = t.company, BranchId = t.branch, Kind = input.Kind, Format = input.Format, Value = value,
                ValueHash = Hash(value), CreatedBy = t.user, CreatedUtc = Now,
                StockItemId = input.Kind == "Product" ? (int)input.TargetId : null, AnimalId = input.Kind == "Animal" ? (int)input.TargetId : null,
                FixedAssetId = input.Kind == "Asset" ? input.TargetId : null, PurchaseOrderId = input.Kind == "PurchaseOrder" ? input.TargetId : null,
                FinanceDocumentId = input.Kind == "Document" ? input.TargetId : null };
            var target = await Target(label, ct);
            if (input.Format == "SKU")
            {
                var sku = await db.StockItems.Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Id == input.TargetId).Select(x => x.SKU).SingleAsync(ct);
                if (!string.Equals(sku, value, StringComparison.Ordinal)) throw new ArgumentException("A SKU mapping must match the product SKU exactly, including case and leading zeros.");
            }
            var hashes = EquivalentValues(value).Select(Hash).ToArray();
            if (await db.ScanLabels.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && hashes.Contains(x.ValueHash), ct))
                throw new ScanWorkflowException("This label value or equivalent barcode is already assigned, including revoked labels.");
            db.ScanLabels.Add(label); Audit(t, "LabelCreated", $"{label.Kind}:{input.TargetId};format:{label.Format}");
            await db.SaveChangesAsync(ct);
            return new ScanLabelView(label.Id, label.Kind, label.Format, label.Value, target.Title, false, "");
        }, ct);
    }
    public async Task RevokeLabel(long id, string reason, CancellationToken ct = default)
    {
        var t = await Access(ct); await permission.Demand("Scanning.ManageLabels", ct);
        reason = BusinessInput.Text(reason, "Revocation reason", 300);
        await Write(async () =>
        {
            await permission.Demand("Scanning.ManageLabels", ct);
            var label = await db.ScanLabels.SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch, ct) ?? throw new KeyNotFoundException("Label unavailable in this context.");
            await db.Entry(label).ReloadAsync(ct); await permission.Demand(ViewGrant(label.Kind), ct);
            if (label.RevokedUtc is not null) return 0;
            label.RevokedUtc = Now; label.RevokedBy = t.user; label.RevocationReason = reason;
            Audit(t, "LabelRevoked", $"label:{label.Id}"); return 0;
        }, ct);
    }
    public async Task<ScanPreview> Resolve(string kind, string value, int? destinationBranchId = null, CancellationToken ct = default)
    {
        var t = await Access(ct); await permission.Demand(ViewGrant(kind), ct);
        var branch = destinationBranchId ?? t.branch;
        if (branch != t.branch && kind != "Product") throw new ArgumentException("Only transfer products can be scanned in a destination branch.");
        await DemandBranch(t, branch, ct); value = CleanCode(value);
        var hashes = EquivalentValues(value).Select(Hash).ToArray();
        var matches = await db.ScanLabels.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == branch && hashes.Contains(x.ValueHash)).Take(3).ToListAsync(ct);
        matches = matches.Where(x => string.Equals(x.Value, value, StringComparison.Ordinal) || x.Format != "SKU" && EquivalentValues(value).Contains(x.Value, StringComparer.Ordinal)).ToList();
        if (matches.Count != 1 || matches[0].Kind != kind || matches[0].RevokedUtc is not null || !EquivalentValues(value).Contains(matches[0].Value, StringComparer.Ordinal))
            throw new KeyNotFoundException("No unique active label is available in this context.");
        return await Preview(matches[0], ct);
    }
    private async Task<ScanLabel> Label(long id, int company, int branch, CancellationToken ct) =>
        await db.ScanLabels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == company && x.BranchId == branch && x.RevokedUtc == null, ct)
        ?? throw new ScanWorkflowException("The label is unavailable or revoked. Scan a current label again.");
    private async Task<ScanPreview> Preview(ScanLabel label, CancellationToken ct)
    {
        await permission.Demand(ViewGrant(label.Kind), ct);
        var possible = label.Kind switch { "Product" => new[] { "AdjustStock", "Transfer" }, "Animal" => new[] { "Weight", "Health", "Feed" }, "Asset" => new[] { "Custody" }, "PurchaseOrder" => new[] { "ReceiveOrder" }, _ => [] };
        var actions = new List<string>();
        foreach (var a in possible) { try { await DemandAction(a, ct); actions.Add(a); } catch (UnauthorizedAccessException) { } }
        ScanDocumentPreview? document = null;
        if (label.Kind == "Document")
        {
            var d = await db.FinanceDocuments.AsNoTracking().SingleAsync(x => x.CompanyId == label.CompanyId && x.BranchId == label.BranchId && x.Id == label.FinanceDocumentId, ct);
            var currency = await db.CompanyCurrencyLocks.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == label.CompanyId, ct);
            var precision = currency is null || decimal.Round(d.Amount, currency.CurrencyMinorUnits) != d.Amount ? 3 : currency.CurrencyMinorUnits;
            document = new(d.Kind, d.Amount, currency?.CurrencyCode ?? "", precision);
        }
        return new(label.Id, label.BranchId, label.Kind, label.Format, label.Value, await Target(label, ct), actions.ToArray(), document);
    }
    private async Task<ScanTarget> Target(ScanLabel label, CancellationToken ct)
    {
        var c = label.CompanyId; var b = label.BranchId;
        switch (label.Kind)
        {
            case "Product":
                var s = await db.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == c && x.BranchId == b && x.Id == label.StockItemId, ct) ?? throw new KeyNotFoundException("Target unavailable in this branch.");
                return new(s.Id, s.SKU + " · " + s.Name, "Available", s.UnitOfMeasure, s.QuantityOnHand - s.ReservedQuantity, "/Inventory?search=" + Uri.EscapeDataString(s.SKU));
            case "Animal":
                var a = await db.Animals.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == c && x.BranchId == b && x.Id == label.AnimalId, ct) ?? throw new KeyNotFoundException("Target unavailable in this branch.");
                return new(a.Id, a.Tag + " · " + a.Breed, a.Sold ? "Sold" : "Active", "KG", null, "/animals?q=" + Uri.EscapeDataString(a.Tag));
            case "Asset":
                var f = await db.FixedAssets.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == c && x.BranchId == b && x.Id == label.FixedAssetId, ct) ?? throw new KeyNotFoundException("Target unavailable in this branch.");
                return new(f.Id, f.AssetCode + " · " + f.Name, f.Status, "", null, "/fixed-assets/" + f.Id);
            case "PurchaseOrder":
                var p = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == c && x.BranchId == b && x.Id == label.PurchaseOrderId, ct) ?? throw new KeyNotFoundException("Target unavailable in this branch.");
                return new(p.Id, p.Reference, p.Status, "", p.Quantity - p.ReceivedQuantity, "/Purchase?OrderSearch=" + Uri.EscapeDataString(p.Reference));
            case "Document":
                var d = await db.FinanceDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == c && x.BranchId == b && x.Id == label.FinanceDocumentId, ct) ?? throw new KeyNotFoundException("Target unavailable in this branch.");
                var route = d.Kind == "Purchase" ? "/Purchase" : d.Kind is "Sale" or "AnimalSale" ? "/Sales" : "/Accounts";
                var link = route + "?DocumentSearch=" + Uri.EscapeDataString(d.Reference);
                try { await permission.Demand(route == "/Purchase" ? "Purchase.View" : route == "/Sales" ? "Sales.View" : "Accounts.View", ct); }
                catch (UnauthorizedAccessException) { link = ""; }
                return new(d.Id, d.Reference, d.Kind, "", null, link);
            default: throw new ArgumentException("Choose a supported scan context.");
        }
    }
    public async Task<object> Options(string kind, string? search, int page = 1, CancellationToken ct = default)
    {
        var t = await Access(ct); await permission.Demand("Scanning.ManageLabels", ct); await permission.Demand(ViewGrant(kind), ct);
        search = search?.Trim() ?? "";
        if (search.Length > 80 || page is < 1 or > 10000) throw new ArgumentException("Choose a valid search and page.");
        IQueryable<ScanOption> query = kind switch
        {
            "Product" => db.StockItems.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && (search == "" || x.SKU.Contains(search) || x.Name.Contains(search))).Select(x => new ScanOption { Id = x.Id, Title = x.SKU + " · " + x.Name }),
            "Animal" => db.Animals.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && (search == "" || x.Tag.Contains(search))).Select(x => new ScanOption { Id = x.Id, Title = x.Tag }),
            "Asset" => db.FixedAssets.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && (search == "" || x.AssetCode.Contains(search) || x.Name.Contains(search))).Select(x => new ScanOption { Id = x.Id, Title = x.AssetCode + " · " + x.Name }),
            "PurchaseOrder" => db.PurchaseOrders.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && (search == "" || x.Reference.Contains(search))).Select(x => new ScanOption { Id = x.Id, Title = x.Reference }),
            "Document" => db.FinanceDocuments.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && (search == "" || x.Reference.Contains(search))).Select(x => new ScanOption { Id = x.Id, Title = x.Reference }),
            _ => throw new ArgumentException("Choose a supported scan context.")
        };
        return new { page, total = await query.CountAsync(ct), rows = await query.OrderBy(x => x.Id).Skip((page - 1) * 20).Take(20).ToListAsync(ct) };
    }
    public async Task<object> Labels(string kind, string? search, int page = 1, CancellationToken ct = default)
    {
        var t = await Access(ct); await permission.Demand("Scanning.ManageLabels", ct); await permission.Demand(ViewGrant(kind), ct);
        search = search?.Trim() ?? ""; if (search.Length > 80 || page is < 1 or > 10000) throw new ArgumentException("Choose a valid search and page.");
        var q = db.ScanLabels.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == kind && (search == "" || x.Value.Contains(search)));
        var rows = await q.OrderByDescending(x => x.Id).Skip((page - 1) * 20).Take(20).Select(x => new { x.Id, x.Kind, x.Format, x.Value, revoked = x.RevokedUtc != null, x.RevocationReason }).ToListAsync(ct);
        return new { page, total = await q.CountAsync(ct), rows };
    }
    public async Task<ScanLabelView> PrintLabel(long id, CancellationToken ct = default)
    {
        var t = await Access(ct); await permission.Demand("Scanning.ManageLabels", ct);
        var label = await Label(id, t.company, t.branch, ct); await permission.Demand(ViewGrant(label.Kind), ct);
        // Printable label deliberately contains no target name, contact, cost, animal health or employee information.
        return new(label.Id, label.Kind, label.Format, label.Value, "", false, "");
    }
    public async Task<object> Branches(CancellationToken ct = default)
    {
        var t = await Access(ct); await permission.Demand("Inventory.Transfer", ct);
        return await db.Branches.AsNoTracking().Where(x => x.CompanyId == t.company && x.Id != t.branch && db.UserBranchAccess.Any(a => a.UserId == t.user && a.CompanyId == t.company && a.BranchId == x.Id)).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name }).Take(100).ToListAsync(ct);
    }
    private static ScanDraftInput Normalize(ScanDraftInput input)
    {
        input.Notes ??= ""; input.Custodian ??= ""; input.Location ??= ""; input.EventType ??= "";
        if (!Guid.TryParseExact(input.RequestKey, "N", out var key) || key == Guid.Empty || input.LabelId <= 0) throw new ArgumentException("A valid scan request key and label are required.");
        _ = ActionKind(input.Action);
        if (input.Notes.Length > 1000 || input.Custodian.Length > 160 || input.Location.Length > 160 || input.EventType.Length > 20) throw new ArgumentException("Scan entry text exceeds the supported length.");
        if (input.Action is "Weight" or "Health" or "Feed" or "Custody" && input.Date is null) throw new ArgumentException("Choose the event date explicitly.");
        if (input.Action is "AdjustStock" or "Transfer" or "Weight" or "Feed" or "ReceiveOrder")
        {
            BusinessInput.Decimal(input.Quantity, "Scan quantity", input.Action != "AdjustStock", allowNegative: input.Action == "AdjustStock");
            if (input.Quantity == 0 || input.Quantity < 0 && input.Action != "AdjustStock") throw new ArgumentException("Choose a nonzero quantity with a valid sign.");
        }
        if (input.Action == "Weight") FarmBusinessRules.DecimalValue(input.Quantity, "Weight", 5000);
        if (input.Action == "Transfer" && input.Notes.Length > 500) throw new ArgumentException("Transfer note is required and must be at most 500 characters without control characters.");
        if (input.Action == "AdjustStock" && string.IsNullOrWhiteSpace(input.Notes)) throw new ArgumentException("A stock adjustment reason is required.");
        if (input.Action == "Health" && input.EventType is not ("Vaccination" or "Deworming" or "Treatment" or "VetVisit" or "Observation")) throw new ArgumentException("Choose a valid health event type.");
        if (input.Action == "Custody" && (string.IsNullOrWhiteSpace(input.Notes) || string.IsNullOrWhiteSpace(input.Custodian) || string.IsNullOrWhiteSpace(input.Location))) throw new ArgumentException("Custodian, location and reason are required.");
        if (input.Action is "Feed" or "Transfer" ? input.SecondaryLabelId is null : input.SecondaryLabelId is not null) throw new ArgumentException("Choose the required secondary product label only for feed or transfer.");
        if (input.Action == "Transfer" ? input.DestinationBranchId is null : input.DestinationBranchId is not null) throw new ArgumentException("Choose a destination branch only for a transfer.");
        // Ignore no fields silently: non-applicable values must be empty to keep the reviewed command unambiguous.
        if (input.Action is not ("Weight" or "Health" or "Feed" or "Custody") && input.Date is not null || input.Action != "Health" && (input.NextDueDate is not null || input.EventType != "") ||
            input.Action != "Custody" && (input.Custodian != "" || input.Location != "") || input.Action is "Health" or "Custody" && input.Quantity != 0)
            throw new ArgumentException("Remove fields that do not apply to the selected scan action.");
        input.RequestKey = key.ToString("N"); input.Notes = input.Notes.Trim(); input.Custodian = input.Custodian.Trim(); input.Location = input.Location.Trim();
        return input;
    }
    private async Task<(ScanLabel primary, ScanLabel? secondary)> Sources(ScanDraftInput input, (int company, int branch, string user) t, CancellationToken ct)
    {
        var primary = await Label(input.LabelId, t.company, t.branch, ct);
        if (primary.Kind != ActionKind(input.Action)) throw new ArgumentException("The scanned context does not match this action.");
        ScanLabel? secondary = null;
        if (input.SecondaryLabelId is not null)
        {
            var branch = input.DestinationBranchId ?? t.branch; await DemandBranch(t, branch, ct);
            if (input.Action == "Transfer" && branch == t.branch) throw new ArgumentException("Choose a different destination branch.");
            secondary = await Label(input.SecondaryLabelId.Value, t.company, branch, ct);
            if (secondary.Kind != "Product") throw new ArgumentException("The secondary label must identify a product.");
        }
        return (primary, secondary);
    }
    private async Task<string> SourceHash(ScanLabel primary, ScanLabel? secondary, DateOnly day, CancellationToken ct)
    {
        async Task<object> Stamp(ScanLabel l)
        {
            object target = l.Kind switch
            {
                "Product" => (object?)await db.StockItems.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == l.CompanyId && x.BranchId == l.BranchId && x.Id == l.StockItemId, ct),
                "Animal" => await db.Animals.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == l.CompanyId && x.BranchId == l.BranchId && x.Id == l.AnimalId, ct),
                "Asset" => await db.FixedAssets.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == l.CompanyId && x.BranchId == l.BranchId && x.Id == l.FixedAssetId, ct),
                "PurchaseOrder" => await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == l.CompanyId && x.BranchId == l.BranchId && x.Id == l.PurchaseOrderId, ct),
                _ => throw new ArgumentException("This scan context is read-only.")
            } ?? throw new KeyNotFoundException("Target unavailable in this branch.");
            var carryingValue = l.StockItemId is null ? (decimal?)null : await db.InventoryCostBalances.AsNoTracking().Where(x => x.CompanyId == l.CompanyId && x.BranchId == l.BranchId && x.StockItemId == l.StockItemId).Select(x => (decimal?)x.Value).SingleOrDefaultAsync(ct);
            return new { l.Id, l.BranchId, l.ValueHash, l.RowVersion, target, carryingValue };
        }
        var policy = await CompanyBusinessPolicyService.Configured(db, primary.CompanyId, ct, atUtc: Now);
        return Hash(Json(new { day, policyId = policy?.Id, policyVersion = policy?.Version, primary = await Stamp(primary), secondary = secondary is null ? null : await Stamp(secondary) }));
    }
    private async Task<ScanImpact> Impact(ScanDraftInput input, ScanLabel primary, ScanLabel? secondary, CancellationToken ct)
    {
        var t = await tenant.GetAsync(ct);
        var policy = await CompanyBusinessPolicyService.Configured(db, t.company, ct, atUtc: Now);
        decimal? amount = null; var precision = 3; string custodian = "", location = ""; decimal? weight = null;
        if (input.Action is "AdjustStock" or "Transfer" or "Feed" or "ReceiveOrder")
        {
            if (policy is null) throw new ScanWorkflowException("Approve a company business policy before financial scan actions.");
            if (input.Action == "ReceiveOrder")
            {
                var order = await db.PurchaseOrders.AsNoTracking().SingleAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Id == primary.PurchaseOrderId, ct);
                if (order.Status is not ("Approved" or "PartiallyReceived") || input.Quantity > order.Quantity - order.ReceivedQuantity) throw new ScanWorkflowException("The purchase order is not receivable for this quantity.");
                precision = policy.CurrencyMinorUnits;
                amount = order.ReceivedQuantity + input.Quantity == order.Quantity ? order.Amount - order.ReceivedAmount : decimal.Round(order.UnitPrice * (order.ReceivedQuantity + input.Quantity), precision, MidpointRounding.AwayFromZero) - order.ReceivedAmount;
            }
            else
            {
                var label = input.Action == "Feed" ? secondary! : primary;
                var item = await db.StockItems.AsNoTracking().SingleAsync(x => x.CompanyId == t.company && x.BranchId == label.BranchId && x.Id == label.StockItemId, ct);
                var balance = await db.InventoryCostBalances.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == label.BranchId && x.StockItemId == item.Id, ct);
                if (balance is null && item.QuantityOnHand != 0) throw new ScanWorkflowException("This stock item needs an approved opening inventory valuation before transactions can be posted.");
                balance ??= new InventoryCostBalance();
                if (input.Action == "AdjustStock" && input.Quantity > 0)
                {
                    var unit = item.QuantityOnHand == 0 ? item.UnitCost : balance.Value / item.QuantityOnHand;
                    amount = unit == 0 ? 0 : BusinessInput.Total(input.Quantity, unit, "Adjustment value");
                }
                else amount = InventoryValuation.Remove(item, balance, Math.Abs(input.Quantity)); // Detached copies: preview has no writes.
                if (input.Action == "Transfer")
                {
                    var destination = await db.StockItems.AsNoTracking().SingleAsync(x => x.CompanyId == t.company && x.BranchId == secondary!.BranchId && x.Id == secondary.StockItemId, ct);
                    if (!string.Equals(item.SKU.Trim(), destination.SKU.Trim(), StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(item.UnitOfMeasure) || item.UnitOfMeasure != destination.UnitOfMeasure)
                        throw new ArgumentException("Source and destination products must have the same SKU and reviewed stock unit.");
                }
            }
        }
        if (primary.AnimalId is not null) weight = await db.Animals.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Id == primary.AnimalId).Select(x => x.CurrentWeightKg).SingleAsync(ct);
        if (primary.FixedAssetId is not null)
        {
            var asset = await db.FixedAssets.AsNoTracking().SingleAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Id == primary.FixedAssetId, ct);
            if (asset.Status != "Active") throw new ScanWorkflowException("Only an active asset can change custody.");
            custodian = asset.Custodian; location = asset.Location;
        }
        return new(amount, policy?.CurrencyCode ?? "", precision, custodian, location, weight);
    }
    private static string Consequence(string action) => action switch
    {
        "AdjustStock" => "Confirmation changes stock and posts the existing stock-adjustment journal.",
        "Transfer" => "Confirmation transfers stock between the selected branches and posts both existing branch journals.",
        "Feed" => "Confirmation consumes the selected feed stock and posts the existing feed expense journal.",
        "ReceiveOrder" => "Confirmation receives the selected quantity against the approved purchase order and posts its existing receipt and journal.",
        "Weight" => "Confirmation records the entered animal weight. No diagnosis or treatment is generated.",
        "Health" => "Confirmation records the entered health event. No diagnosis, dose or treatment recommendation is generated.",
        "Custody" => "Confirmation records the stated asset custody change. No acquisition, depreciation or disposal is posted.", _ => ""
    };
    public async Task<ScanDraftView> Draft(ScanDraftInput input, CancellationToken ct = default)
    {
        input = Normalize(input); var t = await Access(ct); await DemandAction(input.Action, ct);
        var payload = Json(input); var hash = Hash(payload);
        return await Write(async () =>
        {
            await DemandAction(input.Action, ct);
            var replay = await db.ScanIntents.AsNoTracking().SingleOrDefaultAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.OwnerUserId == t.user && x.RequestKey == input.RequestKey, ct);
            if (replay is not null)
            { if (replay.PayloadHash != hash) throw new ScanWorkflowException("This request key was already used for a different draft."); return ToView(replay); }
            if (await db.ScanIntents.CountAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.OwnerUserId == t.user && x.CreatedUtc > Now.AddMinutes(-1), ct) >= 20)
                throw new ScanWorkflowException("Too many scan drafts. Wait one minute before trying again.");
            var sources = await Sources(input, t, ct);
            var day = (await CompanyBusinessDate.Current(db, t.company, ct, clock)).Date;
            if (input.Date is not null) FarmBusinessRules.RecordedDate(input.Date.Value, day);
            if (input.NextDueDate is not null && (input.NextDueDate < input.Date || input.NextDueDate > input.Date!.Value.AddYears(10))) throw new ArgumentException("Choose a valid health event and a follow-up date from the event date through ten years later.");
            var review = new ScanReview(await Preview(sources.primary, ct), sources.secondary is null ? null : await Preview(sources.secondary, ct), input, day, Consequence(input.Action), await Impact(input, sources.primary, sources.secondary, ct));
            if (review.Primary.Target.State == "Sold") throw new ScanWorkflowException("Sold animals cannot receive new scan entries.");
            var row = new ScanIntent { CompanyId = t.company, BranchId = t.branch, OwnerUserId = t.user, RequestKey = input.RequestKey, Action = input.Action,
                ScanLabelId = input.LabelId, SecondaryBranchId = sources.secondary?.BranchId, SecondaryLabelId = sources.secondary?.Id,
                PayloadJson = payload, PayloadHash = hash, SourceHash = await SourceHash(sources.primary, sources.secondary, day, ct), ReviewJson = Json(review),
                BusinessReference = "SCAN-" + Guid.NewGuid().ToString("N").ToUpperInvariant(), CreatedUtc = Now, ExpiresUtc = Now.AddMinutes(10) };
            db.ScanIntents.Add(row); Audit(t, "DraftCreated", row.BusinessReference); await db.SaveChangesAsync(ct); return ToView(row);
        }, ct);
    }
    private ScanDraftView ToView(ScanIntent row) => new(row.Id, row.Status == "Pending" && row.ExpiresUtc <= Now ? "Expired" : row.Status, row.BusinessReference, row.ExpiresUtc,
        JsonSerializer.Deserialize<ScanReview>(row.ReviewJson) ?? throw new ScanWorkflowException("Scan draft unavailable."));
    public async Task<ScanDraftView> Detail(long id, CancellationToken ct = default)
    {
        var t = await Access(ct); var row = await Owned(id, t, ct); await DemandAction(row.Action, ct);
        if (row.SecondaryBranchId is not null) await DemandBranch(t, row.SecondaryBranchId.Value, ct);
        return ToView(row);
    }
    private async Task<ScanIntent> Owned(long id, (int company, int branch, string user) t, CancellationToken ct) =>
        await db.ScanIntents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == t.company && x.BranchId == t.branch && x.OwnerUserId == t.user, ct)
        ?? throw new KeyNotFoundException("Scan draft unavailable for this user and branch.");
    public async Task<object> History(int page = 1, CancellationToken ct = default, string? q = null, string? status = null)
    {
        var t = await Access(ct); if (page is < 1 or > 10000) throw new ArgumentException("Choose a valid search and page.");
        q = q?.Trim() ?? ""; status = status?.Trim() ?? "";
        if (q.Length > 80 || status is not ("" or "Pending" or "Committed" or "Cancelled" or "Expired")) throw new ArgumentException("Choose a valid search and page.");
        // History reveals only own opaque references and statuses. Opening content rechecks all action permissions.
        var query = db.ScanIntents.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch && x.OwnerUserId == t.user);
        var now = Now;
        if (q.Length > 0) query = query.Where(x => x.BusinessReference.Contains(q));
        if (status == "Expired") query = query.Where(x => x.Status == "Pending" && x.ExpiresUtc <= now);
        else if (status == "Pending") query = query.Where(x => x.Status == "Pending" && x.ExpiresUtc > now);
        else if (status.Length > 0) query = query.Where(x => x.Status == status);
        return new { page, total = await query.CountAsync(ct), rows = await query.OrderByDescending(x => x.Id).Skip((page - 1) * 20).Take(20).Select(x => new { x.Id, x.Action, Status = x.Status == "Pending" && x.ExpiresUtc <= now ? "Expired" : x.Status, x.BusinessReference, x.ExpiresUtc }).ToListAsync(ct) };
    }
    public async Task Cancel(long id, CancellationToken ct = default)
    {
        var t = await Access(ct);
        await Write(async () =>
        {
            var row = await Owned(id, t, ct);
            if (row.Status == "Cancelled") return 0;
            if (row.Status != "Pending") throw new ScanWorkflowException("A completed scan cannot be cancelled.");
            var tracked = db.ChangeTracker.Entries<ScanIntent>().SingleOrDefault(x => x.Entity.Id == row.Id); if (tracked is not null) tracked.State = EntityState.Detached;
            db.ScanIntents.Attach(row); row.Status = "Cancelled"; row.CompletedUtc = Now;
            Audit(t, "DraftCancelled", row.BusinessReference); return 0;
        }, ct);
    }
    public async Task<ScanDraftView> Confirm(long id, bool confirmReviewed, CancellationToken ct = default)
    {
        if (!confirmReviewed) throw new ArgumentException("Review the draft and explicitly confirm the operation.");
        db.ChangeTracker.Clear(); // This request owns its unit of work; never reuse stale tracked preview entities.
        var t = await Access(ct); var initial = await Owned(id, t, ct); await DemandAction(initial.Action, ct);
        if (initial.SecondaryBranchId is not null) await DemandBranch(t, initial.SecondaryBranchId.Value, ct);
        if (initial.Status == "Committed") return ToView(initial);
        var input = JsonSerializer.Deserialize<ScanDraftInput>(initial.PayloadJson) ?? throw new ScanWorkflowException("Scan draft unavailable.");
        var sources = await Sources(input, t, ct);
        if (db.PendingScanGuard is not null) throw new ScanWorkflowException("A scan confirmation is already running.");
        var consumed = false; var previous = db.ScanWorkflowWrite;
        db.PendingScanGuard = async (action, token) =>
        {
            if (consumed || action != initial.Action || db.Database.CurrentTransaction is null) throw new ScanWorkflowException("The scan transaction does not match the reviewed action.");
            var freshScope = await Access(token); if (freshScope != t) throw new UnauthorizedAccessException("The active scan context changed.");
            await DemandAction(action, token);
            var row = await Owned(id, t, token);
            if (row.Status != "Pending" || row.ExpiresUtc <= Now) throw new ScanWorkflowException("This scan draft is no longer pending or has expired. Prepare a new draft.");
            if (Hash(row.PayloadJson) != row.PayloadHash || row.PayloadHash != initial.PayloadHash) throw new ScanWorkflowException("The scan draft changed. Prepare a new draft.");
            var current = await Sources(input, t, token); var day = (await CompanyBusinessDate.Current(db, t.company, token, clock)).Date;
            if (await SourceHash(current.primary, current.secondary, day, token) != row.SourceHash) throw new ScanWorkflowException("The source changed after preview. Scan again and review a new draft.");
            var tracked = db.ChangeTracker.Entries<ScanIntent>().SingleOrDefault(x => x.Entity.Id == row.Id); if (tracked is not null) tracked.State = EntityState.Detached;
            db.ScanIntents.Attach(row); db.ScanWorkflowWrite = true; row.Status = "Committed"; row.CompletedUtc = Now;
            Audit(t, "Confirmed." + action, row.BusinessReference); consumed = true;
        };
        try
        {
            switch (input.Action)
            {
                case "AdjustStock": await farm.MoveStock(sources.primary.StockItemId!.Value, input.Quantity, initial.BusinessReference, ct); break;
                case "Transfer": await transfers.Post(sources.primary.StockItemId!.Value, input.DestinationBranchId!.Value, sources.secondary!.StockItemId!.Value, input.Quantity, initial.BusinessReference, input.Notes, ct); break;
                case "Weight": await business.RecordWeight(new WeightEntryInput { AnimalId = sources.primary.AnimalId!.Value, Reference = initial.BusinessReference, WeighedOn = input.Date!.Value, WeightKg = input.Quantity, Notes = input.Notes }, ct); break;
                case "Health": await health.RecordHealth(sources.primary.AnimalId!.Value, initial.BusinessReference, input.EventType, input.Date!.Value, input.NextDueDate, input.Notes, ct); break;
                case "Feed": await health.ConsumeFeed(sources.primary.AnimalId!.Value, sources.secondary!.StockItemId!.Value, input.Quantity, initial.BusinessReference, input.Date!.Value, ct); break;
                case "ReceiveOrder": await operations.ReceiveOrder(sources.primary.PurchaseOrderId!.Value, input.Quantity, initial.BusinessReference, ct); break;
                case "Custody":
                    var asset = await db.FixedAssets.AsNoTracking().SingleAsync(x => x.Id == sources.primary.FixedAssetId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
                    await assets.ChangeCustody(new FixedAssetCustodyInput { Id = asset.Id, RowVersion = asset.RowVersion, EffectiveDate = input.Date!.Value, Custodian = input.Custodian, Location = input.Location, Reason = input.Notes }, ct); break;
            }
            if (!consumed) throw new ScanWorkflowException("The operation did not consume the reviewed scan draft.");
            return ToView(await Owned(id, t, ct));
        }
        catch { db.ChangeTracker.Clear(); throw; }
        finally { db.PendingScanGuard = null; db.ScanWorkflowWrite = previous; }
    }
}
public sealed class ScanOption { public long Id { get; set; } public string Title { get; set; } = ""; }

public sealed class ScanWorkflowException(string message) : InvalidOperationException(message);
