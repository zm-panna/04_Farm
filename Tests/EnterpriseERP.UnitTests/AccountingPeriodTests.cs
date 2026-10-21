using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class AccountingPeriodTests
{
    private static Voucher Journal(string reference, DateTimeOffset? date = null)
    {
        var result = BusinessInput.Voucher(1, 11, reference, ("1000", 10m, 0m), ("3000", 0m, 10m));
        if (date.HasValue) result.Date = date.Value;
        return result;
    }
    [Fact] public async Task CloseBlocksBackdatedPostingButAllowsCurrentPosting()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Accounts.Close");
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await new AccountingPeriodService(f.Db, f.Tenant, f.Permissions).Close(yesterday, "Reconciled and approved", default);
        var old = Journal("BACKDATED", DateTimeOffset.UtcNow.AddDays(-2)); f.Db.Vouchers.Add(old);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); f.Db.Vouchers.Add(Journal("CURRENT")); await f.Db.SaveChangesAsync();
        Assert.Single(await f.Db.Vouchers.ToListAsync());
    }
    [Fact] public async Task PostedVoucherAndLinesCannotBeEditedOrExtended()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var voucher = Journal("LOCKED"); f.Db.Vouchers.Add(voucher); await f.Db.SaveChangesAsync();
        voucher.Reference = "EDITED"; await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); var line = await f.Db.VoucherLines.FirstAsync(); line.Debit += 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); f.Db.VoucherLines.Add(new VoucherLine { VoucherId = voucher.Id, AccountCode = "1000", Debit = 1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }
    [Fact] public async Task ClosingRequiresPermissionAndCannotMoveBackward()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var service = new AccountingPeriodService(f.Db, f.Tenant, f.Permissions);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.Close(yesterday, "Reviewed", default));
        await f.GrantAsync("Accounts.Close"); await service.Close(yesterday, "Reviewed", default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.Close(yesterday.AddDays(-1), "Try reopen", default));
        await Assert.ThrowsAsync<ArgumentException>(() => service.Close(yesterday.AddDays(1), "Current day", default));
    }
    [Fact] public async Task UnbalancedPostedVoucherRejectedAtDatabaseUnitOfWorkBoundary()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        f.Db.Vouchers.Add(new Voucher { CompanyId = 1, BranchId = 11, Reference = "INVALID", Posted = true, Lines = new() { new() { AccountCode = "1000", Debit = 10 }, new() { AccountCode = "3000", Credit = 9 } } });
        await Assert.ThrowsAsync<ArgumentException>(() => f.Db.SaveChangesAsync()); Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
}
