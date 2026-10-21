using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EnterpriseERP.UnitTests;
public sealed class ManualFinancialAuditTests
{
 private static FarmService Farm(SecurityTestScope f) => new(f.Db,f.Tenant,f.Permissions);
 private static FinanceService Finance(SecurityTestScope f) => new(f.Db,f.Tenant,f.Permissions);
 private static List<VoucherLine> Lines() => [new(){AccountCode="1000",Debit=10},new(){AccountCode="3000",Credit=10}];
 [Fact] public async Task ManualJournalRecordsExactActorCompanyReferenceAndTime()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Accounts.Post");
  var before=DateTimeOffset.UtcNow;
  var voucher=await Farm(f).PostVoucher(" reviewed-journal ",Lines(),default);
  var audit=await f.Db.Audits.SingleAsync();
  Assert.Equal("Finance.Journal.Post",audit.Action);Assert.Equal(voucher.Reference,audit.Reference);
  Assert.Equal("JRN-reviewed-journal",audit.Reference);Assert.Equal("operator-a",audit.ActorId);Assert.Equal(1,audit.CompanyId);
  Assert.InRange(audit.CreatedUtc,before,DateTimeOffset.UtcNow);
 }
 [Fact] public async Task DuplicateJournalCannotWriteASecondAudit()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Accounts.Post");
  await Farm(f).PostVoucher("duplicate",Lines(),default);
  await Assert.ThrowsAsync<DbUpdateException>(()=>Farm(f).PostVoucher("duplicate",Lines(),default));
  f.Db.ChangeTracker.Clear();Assert.Equal(1,await f.Db.Audits.CountAsync());Assert.Equal(1,await f.Db.Vouchers.CountAsync());
 }
 [Fact] public async Task JournalStoreFailureRollsBackAuditAndLines()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Accounts.Post");
  await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_synthetic_journal BEFORE INSERT ON Vouchers WHEN NEW.Reference='JRN-fail' BEGIN SELECT RAISE(ABORT, 'synthetic fixture rejection'); END;");
  await Assert.ThrowsAsync<DbUpdateException>(()=>Farm(f).PostVoucher("fail",Lines(),default));
  f.Db.ChangeTracker.Clear();Assert.Empty(await f.Db.Audits.ToListAsync());Assert.Empty(await f.Db.Vouchers.ToListAsync());Assert.Empty(await f.Db.VoucherLines.ToListAsync());
 }
 [Fact] public async Task DeniedOrInvalidJournalCannotCreateAudit()
 {
  await using var f=await SecurityTestScope.CreateAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Farm(f).PostVoucher("denied",Lines(),default));
  await f.GrantAsync("Accounts.Post");
  await Assert.ThrowsAsync<ArgumentException>(()=>Farm(f).PostVoucher("invalid",[new(){AccountCode="1000",Debit=10},new(){AccountCode="3000",Credit=9}],default));
  Assert.Empty(await f.Db.Audits.ToListAsync());Assert.Empty(await f.Db.Vouchers.ToListAsync());
 }
 [Fact] public async Task BankLineRecordsActorBranchReferenceAndTime()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Accounts.Reconcile");var before=DateTimeOffset.UtcNow;
  var line=await Finance(f).RecordBankLine(" bank-entry ",-10,DateOnly.FromDateTime(DateTime.UtcNow),default);
  var audit=await f.Db.Audits.SingleAsync();Assert.Equal("Bank.StatementLineCreated",audit.Action);Assert.Equal("branch:11; bank-entry",audit.Reference);
  Assert.Equal("bank-entry",line.ExternalReference);Assert.Equal("operator-a",audit.ActorId);Assert.Equal(1,audit.CompanyId);Assert.InRange(audit.CreatedUtc,before,DateTimeOffset.UtcNow);
 }
 [Fact] public async Task DuplicateBankLineRollsBackItsAudit()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Accounts.Reconcile");var date=DateOnly.FromDateTime(DateTime.UtcNow);
  await Finance(f).RecordBankLine("duplicate",10,date,default);
  await Assert.ThrowsAsync<DbUpdateException>(()=>Finance(f).RecordBankLine("duplicate",10,date,default));
  f.Db.ChangeTracker.Clear();Assert.Equal(1,await f.Db.Audits.CountAsync());Assert.Equal(1,await f.Db.BankStatementLines.CountAsync());
 }
 [Fact] public async Task BankLineStoreFailureRollsBackItsAudit()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Accounts.Reconcile");
  await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reject_synthetic_bank BEFORE INSERT ON BankStatementLines WHEN NEW.ExternalReference='fail' BEGIN SELECT RAISE(ABORT, 'synthetic fixture rejection'); END;");
  await Assert.ThrowsAsync<DbUpdateException>(()=>Finance(f).RecordBankLine("fail",10,DateOnly.FromDateTime(DateTime.UtcNow),default));
  f.Db.ChangeTracker.Clear();Assert.Empty(await f.Db.Audits.ToListAsync());Assert.Empty(await f.Db.BankStatementLines.ToListAsync());
 }
 [Fact] public async Task DeniedOrInvalidBankLineCannotCreateAudit()
 {
  await using var f=await SecurityTestScope.CreateAsync();var date=DateOnly.FromDateTime(DateTime.UtcNow);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Finance(f).RecordBankLine("denied",10,date,default));await f.GrantAsync("Accounts.Reconcile");
  await Assert.ThrowsAsync<ArgumentException>(()=>Finance(f).RecordBankLine("invalid",0,date,default));Assert.Empty(await f.Db.Audits.ToListAsync());Assert.Empty(await f.Db.BankStatementLines.ToListAsync());
 }
}
