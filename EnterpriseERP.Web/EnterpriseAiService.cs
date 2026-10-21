using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

public sealed record EnterpriseAiFact(string Label, int Count);
public sealed record EnterpriseAiSnapshot(string Module, int CompanyId, int BranchId, DateTimeOffset GeneratedUtc,
    string? CurrencyCode, string PolicyStatus, IReadOnlyList<EnterpriseAiFact> Facts) { public int? PolicyVersion { get; init; } }
public sealed record EnterpriseAiDraft(EnterpriseAiSnapshot Snapshot, string Mode, string Text, string ReviewRoute, bool RequiresHumanReview, bool Executed) { public string Checklist { get; init; } = ""; }
/// <summary>Local allowlisted facts and review drafts. No provider transmission, mutation, model tools, or auto-approval.</summary>
public sealed class EnterpriseAiService(AppDbContext db, TenantContext tenant, PermissionService permission)
{
    public static IReadOnlyDictionary<string,(string Permission,string Route)> Modules { get; } = new Dictionary<string,(string,string)>(StringComparer.Ordinal)
    {
        ["hr"] = ("HR.View","/HRPayroll"), ["payroll"] = ("Payroll.View","/HRPayroll/register"),
        ["crm"] = ("CRM.View","/crm"), ["assets"] = ("FixedAssets.View","/fixed-assets"),
        ["ecommerce"] = ("Ecommerce.Fulfill","/ecommerce/staff/orders")
    };
    public async Task<EnterpriseAiSnapshot> Facts(string module, CancellationToken ct = default)
    {
        await permission.Demand("AI.Read",ct);
        if (!Modules.TryGetValue(module,out var spec)) throw new UnauthorizedAccessException("Unknown enterprise AI fact module.");
        await permission.Demand(spec.Permission,ct); var t=await tenant.GetAsync(ct);
        var facts = new List<EnterpriseAiFact>();
        switch(module)
        {
            case "hr":
                facts.Add(new("Active staff",await db.HRPayrollStaff.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Active,ct)));
                facts.Add(new("Pending leave requests",await db.HRPayrollLeaves.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status=="Pending",ct)));
                break;
            case "payroll":
                foreach(var status in new[]{"Draft","Approved","Posted"}) facts.Add(new(status+" payroll runs",await db.HRPayrollRuns.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status==status,ct)));
                break;
            case "crm":
                facts.Add(new("Active leads",await db.CRMLeads.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status=="Active",ct)));
                facts.Add(new("Open activities",await db.CRMActivities.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status=="Open",ct)));
                facts.Add(new("Draft quotes",await db.CRMQuotes.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status=="Draft",ct)));
                break;
            case "assets":
                foreach(var status in new[]{"Draft","Approved","Active","Disposed"}) facts.Add(new(status+" assets",await db.FixedAssets.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status==status,ct)));
                facts.Add(new("Draft depreciation runs",await db.FixedAssetDepreciationRuns.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status=="Draft",ct)));
                break;
            case "ecommerce":
                foreach(var status in new[]{"Submitted","Approved","Fulfilled","Cancelled"}) facts.Add(new(status+" orders",await db.EcommerceOrders.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Status==status,ct)));
                facts.Add(new("Unverified-payment orders",await db.EcommerceOrders.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.PaymentStatus=="PendingUnverified",ct)));
                break;
        }
        int? policyVersion=null; string? currency=null; var policyStatus="Unreviewed or unavailable; no jurisdiction or currency has been assumed.";
        try { var p=await CompanyBusinessPolicyService.Current(db,t.company,ct); currency=p.CurrencyCode; policyVersion=p.Version; policyStatus=$"Reviewed company policy v{p.Version}; statutory compliance requires separate business acceptance."; } catch(InvalidOperationException) { }
        return new(module,t.company,t.branch,DateTimeOffset.UtcNow,currency,policyStatus,facts){PolicyVersion=policyVersion};
    }
    public async Task<EnterpriseAiDraft> Draft(string module, CancellationToken ct = default)
    {
        var snapshot=await Facts(module,ct);
        var checklist=module switch
        {
            "hr"=>"Review pending leave against recorded attendance and employment dates. Confirm the employee and dates before recording an approval. This draft contains no salary or personal leave reason.",
            "payroll"=>"Review the exact payroll policy version, attendance and approved leave, pay-rate snapshots, currency precision, deductions and employer contributions. A different authorized person must approve the payroll run; posting records a liability only and makes no bank transfer.",
            "crm"=>"Review open activities and draft quotes. Check contact consent before any separate outreach. Confirm the customer's identity, quote lines, currency and acceptance evidence before converting a quote. This draft sends no message and posts no sale.",
            "assets"=>"Review acquisition cost, residual value, useful life, depreciation period and policy evidence. Reconcile accumulated depreciation and obtain the required independent approval before posting or disposal. This draft makes no accounting entries.",
            "ecommerce"=>"Review submitted orders against the confirmed server-calculated price, effective tax policy and reserved stock. Payment remains unverified until separately evidenced. Fulfillment or cancellation requires its own authorized action. This draft does not charge a payment or change stock.",
            _=>throw new UnauthorizedAccessException("Unknown review draft.")
        };
        var counts=string.Join("; ",snapshot.Facts.Select(x=>$"{x.Label}: {x.Count}"));
        return new(snapshot,"Deterministic review draft from live permission-scoped facts",$"{counts}.\n\n{checklist}\n\n{snapshot.PolicyStatus}",Modules[module].Route,true,false){Checklist=checklist};
    }
    public async Task<string> Serialize(string module,CancellationToken ct=default)=>JsonSerializer.Serialize(await Facts(module,ct));
}
