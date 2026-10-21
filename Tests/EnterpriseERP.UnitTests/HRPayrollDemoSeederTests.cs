using System.Reflection;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class HRPayrollDemoSeederTests
{
 [Fact] public async Task SyntheticHelperCreatesAllSixTablesAndExactlyOneBalancedAccrualInsideOuterTransaction()
 {
  await using var f=await SecurityTestScope.CreateAsync();var policy=await CompanyBusinessPolicyTestData.SeedAsync(f);var tax=await CompanyBusinessPolicyTestData.SeedTaxAsync(f);var company=await f.Db.Companies.SingleAsync(x=>x.Id==1);company.Slug="demo-farm-1";var maker=await f.Db.Users.SingleAsync(x=>x.Id=="operator-a");maker.EmailConfirmed=true;await f.Db.SaveChangesAsync();var reviewer=await f.Db.Users.SingleAsync(x=>x.Id=="approver-a");var branch=await f.Db.Branches.SingleAsync(x=>x.Id==11);var now=DateTimeOffset.UtcNow;var today=DateOnly.FromDateTime(now.UtcDateTime);
  await using var tx=await f.Db.Database.BeginTransactionAsync();var method=typeof(EnterpriseDemoSeeder).GetMethod("SeedHRPayroll",BindingFlags.Static|BindingFlags.NonPublic)!;await (Task)method.Invoke(null,new object[]{f.Db,company,branch,maker,reviewer,policy,tax,1,now,today,CancellationToken.None})!;
  Assert.Equal(1,await f.Db.HRPayrollStaff.CountAsync());Assert.Equal(1,await f.Db.HRPayrollCompensations.CountAsync());Assert.Equal(1,await f.Db.HRPayrollAttendance.CountAsync());Assert.Equal(1,await f.Db.HRPayrollLeaves.CountAsync());Assert.Equal(1,await f.Db.HRPayrollPolicies.CountAsync());Assert.Equal(1,await f.Db.HRPayrollRuns.CountAsync());Assert.Equal(1,await f.Db.HRPayrollLines.CountAsync());var voucher=await f.Db.Vouchers.Include(x=>x.Lines).SingleAsync();Assert.Equal(5,voucher.Lines.Count);Assert.Equal(voucher.Lines.Sum(x=>x.Debit),voucher.Lines.Sum(x=>x.Credit));Assert.Equal("Posted",(await f.Db.HRPayrollRuns.SingleAsync()).Status);Assert.Contains("DEMO FICTIONAL",(await f.Db.HRPayrollPolicies.SingleAsync()).JurisdictionAndBasis);Assert.NotEqual((await f.Db.HRPayrollRuns.SingleAsync()).CreatedBy,(await f.Db.HRPayrollRuns.SingleAsync()).ApprovedBy);Assert.Empty(await f.Db.FinancePayments.ToListAsync());await tx.RollbackAsync();f.Db.ChangeTracker.Clear();Assert.Empty(await f.Db.HRPayrollRuns.ToListAsync());Assert.Empty(await f.Db.Vouchers.ToListAsync());
 }
}
