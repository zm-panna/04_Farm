using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class EnterpriseAiTests
{
    private static EnterpriseAiService Ai(SecurityTestScope f)=>new(f.Db,f.Tenant,f.Permissions);
    [Fact] public async Task BothAiAndModulePermissionsAreRequired()
    { await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("HR.View"); await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Ai(f).Facts("hr")); await f.GrantAsync("AI.Read"); Assert.Equal("hr",(await Ai(f).Facts("hr")).Module); await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Ai(f).Facts("payroll")); }
    [Theory] [InlineData("sql.execute")] [InlineData("approve")] [InlineData("payroll.post")] [InlineData("HR")] public async Task UnknownToolsCannotExecute(string tool)
    { await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("*"); await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Ai(f).Facts(tool)); Assert.Empty(await f.Db.Vouchers.ToListAsync()); }
    [Fact] public async Task FactsAreBranchScopedAndExcludePersonalOrPayData()
    { await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("AI.Read"); await f.GrantAsync("HR.View"); f.Db.HRPayrollStaff.AddRange(new HRPayrollStaff{CompanyId=1,BranchId=11,Code="A",Name="PRIVATE-NAME",DailyRate=987654,StartDate=new DateOnly(2020,1,1)},new HRPayrollStaff{CompanyId=1,BranchId=12,Code="B",Name="OTHER-BRANCH",StartDate=new DateOnly(2020,1,1)},new HRPayrollStaff{CompanyId=2,BranchId=21,Code="C",Name="OTHER-COMPANY",StartDate=new DateOnly(2020,1,1)}); await f.Db.SaveChangesAsync(); var json=await Ai(f).Serialize("hr"); Assert.Contains("\"Count\":1",json); Assert.DoesNotContain("PRIVATE",json); Assert.DoesNotContain("987654",json); Assert.DoesNotContain("OTHER",json); }
    [Fact] public async Task ReviewDraftDoesNotMutateOrPretendToExecute()
    { await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("AI.Read"); await f.GrantAsync("Payroll.View"); var before=await f.Db.Audits.CountAsync(); var draft=await Ai(f).Draft("payroll"); Assert.True(draft.RequiresHumanReview); Assert.False(draft.Executed); Assert.Contains("Deterministic",draft.Mode); Assert.Contains("different authorized person",draft.Text); Assert.Equal("/HRPayroll/register",draft.ReviewRoute); Assert.Equal(before,await f.Db.Audits.CountAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync()); }
    [Fact] public async Task CustomerViewDoesNotRevealStaffOrderSummary()
    { await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("AI.Read"); await f.GrantAsync("Ecommerce.View"); await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Ai(f).Facts("ecommerce")); }
    [Fact] public async Task UnconfiguredPolicyIsExplicitRatherThanDefaultCurrency()
    { await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("AI.Read"); await f.GrantAsync("CRM.View"); var facts=await Ai(f).Facts("crm"); Assert.Null(facts.CurrencyCode); Assert.Contains("no jurisdiction or currency",facts.PolicyStatus); }
    [Fact] public async Task ToolDispatcherDelegatesToPermissionScopedFactsOnly()
    { await using var f=await SecurityTestScope.CreateAsync(); await f.GrantAsync("AI.Read"); await f.GrantAsync("CRM.View"); var tools=new AiToolService(new ReportService(f.Db,f.Tenant,f.Permissions),f.Permissions,Ai(f)); Assert.Contains("crm",await tools.Execute("enterprise.crm",default)); await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>tools.Execute("enterprise.sql.execute",default)); }
}
