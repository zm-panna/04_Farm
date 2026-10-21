using System.Security.Claims;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class AdministrationTests
{
    private static AdministrationService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions, f.Users,
        f.Accessor, f.Services.GetRequiredService<IDataProtectionProvider>());
    private static string Password() => "Staff-" + Guid.NewGuid().ToString("N") + "aA8!";
    private static StaffCreateInput Staff(int branch = 11) => new()
    { Email = "new-staff@example.invalid", Password = Password(), BranchId = branch, Permissions = ["Livestock.View", "Reports.View"] };

    [Fact]
    public async Task OrdinaryUserCannotProvisionStaff()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).CreateStaff(Staff()));
        Assert.Equal(3, await f.Db.Users.CountAsync());
    }

    [Fact]
    public async Task TenantAdministratorCreatesSecureUserOnlyInAuthorizedBranch()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        var input = Staff(); var id = await Service(f).CreateStaff(input);
        var user = await f.Users.FindByIdAsync(id);
        Assert.NotNull(user);
        Assert.True(await f.Users.CheckPasswordAsync(user!, input.Password));
        Assert.NotEqual(input.Password, user!.PasswordHash);
        Assert.True(user.LockoutEnabled);
        Assert.False(user.EmailConfirmed);
        Assert.Empty(await f.Users.GetRolesAsync(user));
        Assert.Empty(await f.Users.GetClaimsAsync(user));
        var companyAccess = await f.Db.UserCompanyAccess.Where(x => x.UserId == id).SingleAsync();
        var branchAccess = await f.Db.UserBranchAccess.Where(x => x.UserId == id).SingleAsync();
        Assert.Equal(1, companyAccess.CompanyId); Assert.Equal(1, branchAccess.CompanyId); Assert.Equal(11, branchAccess.BranchId);
        Assert.Equal(new[] { "Livestock.View", "Reports.View" }, await f.Db.UserPermissions.Where(x => x.UserId == id).OrderBy(x => x.Permission).Select(x => x.Permission).ToArrayAsync());
        Assert.Equal("StaffAccountCreated", (await f.Db.Audits.SingleAsync()).Action);
    }

    [Theory]
    [InlineData("*")][InlineData("Permissions.Manage")][InlineData("Subscription.Pay")][InlineData("PlatformAdmin")][InlineData("platform.billing")]
    public async Task ProtectedPrivilegeInjectionIsRejectedAtomically(string permission)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        var input = Staff(); input.Permissions = [permission];
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).CreateStaff(input));
        Assert.Equal(3, await f.Db.Users.CountAsync()); Assert.Empty(await f.Db.Audits.ToListAsync());
    }

    [Theory]
    [InlineData(12)][InlineData(21)]
    public async Task StaffCannotBeAssignedToUnauthorizedOrOtherTenantBranch(int branch)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).CreateStaff(Staff(branch)));
        Assert.Equal(3, await f.Db.Users.CountAsync());
    }

    [Fact]
    public async Task InvalidIdentityPasswordRollsBackAndExistingAccountIsNeverReset()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        var input = Staff(); input.Password = new string('a', 16);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CreateStaff(input));
        Assert.Equal(3, await f.Db.Users.CountAsync()); Assert.Empty(await f.Db.Audits.ToListAsync());
        input = Staff(); input.Email = "operator-b@example.invalid";
        var existing = await f.Users.FindByEmailAsync(input.Email); var hash = existing!.PasswordHash;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).CreateStaff(input));
        Assert.Equal(hash, (await f.Users.FindByEmailAsync(input.Email))!.PasswordHash);
        Assert.False(await f.Db.UserCompanyAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 1));
    }

    [Fact]
    public async Task ExistingAccountRequiresExactLookupConfirmationAndPreservesOtherTenantAccess()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        var service = Service(f);
        var candidate = await service.LookupExisting(new StaffLookupInput { Email = "operator-b@example.invalid", BranchId = 11 });
        Assert.Equal("operator-b@example.invalid", candidate.Email);
        Assert.False(await f.Db.UserCompanyAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 1));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AttachExisting(new StaffAttachInput { ConfirmationToken = candidate.ConfirmationToken }));
        var input = new StaffAttachInput { ConfirmationToken = candidate.ConfirmationToken, Confirm = true };
        Assert.Equal("operator-b", await service.AttachExisting(input));
        Assert.Equal("operator-b", await service.AttachExisting(input)); // Benign replay does not duplicate grants/audit.
        Assert.Equal(2, await f.Db.UserCompanyAccess.CountAsync(x => x.UserId == "operator-b"));
        Assert.True(await f.Db.UserBranchAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 1 && x.BranchId == 11));
        Assert.True(await f.Db.UserBranchAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 2 && x.BranchId == 21));
        Assert.Empty(await f.Db.UserPermissions.Where(x => x.UserId == "operator-b").ToListAsync());
        Assert.Single(await f.Db.Audits.ToListAsync());
    }

    [Fact]
    public async Task LookupTokenRejectsTamperingAndOtherAdministrator()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage"); await f.GrantAsync("Permissions.Manage", "approver-a");
        var service = Service(f);
        var candidate = await service.LookupExisting(new StaffLookupInput { Email = "operator-b@example.invalid", BranchId = 11 });
        await Assert.ThrowsAsync<ArgumentException>(() => service.AttachExisting(new StaffAttachInput { ConfirmationToken = "tampered", Confirm = true }));
        f.ActAs("approver-a");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AttachExisting(new StaffAttachInput { ConfirmationToken = candidate.ConfirmationToken, Confirm = true }));
        Assert.False(await f.Db.UserCompanyAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 1));
    }

    [Fact]
    public async Task AccessRevokedAfterLookupPreventsAttachment()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        var service = Service(f);
        var candidate = await service.LookupExisting(new StaffLookupInput { Email = "operator-b@example.invalid", BranchId = 11 });
        f.Db.UserPermissions.Remove(await f.Db.UserPermissions.SingleAsync()); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.AttachExisting(new StaffAttachInput { ConfirmationToken = candidate.ConfirmationToken, Confirm = true }));
        Assert.False(await f.Db.UserCompanyAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 1));
    }

    [Fact]
    public async Task TenantWildcardAndSpoofedPlatformRoleCannotCreateUnrelatedCompany()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim(ClaimTypes.Role, "PlatformAdmin"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).CreateCompany(new CompanyCreateInput { CompanyName = "Other", FarmName = "Farm", BranchName = "Branch" }));
        Assert.Equal(2, await f.Db.Companies.CountAsync());
    }

    [Fact]
    public async Task PersistedPlatformAdministratorCreatesOwnNewTenantAndBranchScope()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("PlatformAdmin"))).Succeeded);
        Assert.True((await f.Users.AddToRoleAsync((await f.Users.FindByIdAsync("operator-a"))!, "PlatformAdmin")).Succeeded);
        var service = Service(f);
        var companyId = await service.CreateCompany(new CompanyCreateInput { CompanyName = "New Company", FarmName = "New Farm", BranchName = "New Main" });
        var branch = await f.Db.Branches.SingleAsync(x => x.CompanyId == companyId);
        Assert.True(await f.Db.UserCompanyAccess.AnyAsync(x => x.CompanyId == companyId && x.UserId == "operator-a"));
        Assert.True(await f.Db.UserBranchAccess.AnyAsync(x => x.CompanyId == companyId && x.BranchId == branch.Id && x.UserId == "operator-a"));
        Assert.Equal("*", (await f.Db.UserPermissions.SingleAsync(x => x.CompanyId == companyId)).Permission);
        Assert.Equal(3, await f.Db.Users.CountAsync());
        Assert.False(await f.Db.UserCompanyAccess.AnyAsync(x => x.CompanyId == companyId && x.UserId != "operator-a"));
        var otherBranchId = await service.CreateBranch(new BranchCreateInput { FarmId = 1, BranchName = "A new branch" });
        Assert.True(await f.Db.UserBranchAccess.AnyAsync(x => x.CompanyId == 1 && x.BranchId == otherBranchId && x.UserId == "operator-a"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateBranch(new BranchCreateInput { FarmId = 2, BranchName = "Foreign farm attempt" }));
    }
    [Fact]
    public async Task RevokingBranchImmediatelyInvalidatesStaleTenantClaimsAndPreservesIdentityHistory()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        f.Db.Audits.Add(new AuditEntry { CompanyId = 1, ActorId = "approver-a", Action = "HistoricalActivity", Reference = "keep" });
        await f.Db.SaveChangesAsync();
        f.ActAs("approver-a"); Assert.Equal(1, (await f.Tenant.GetAsync()).company);
        f.ActAs();
        Assert.True(await Service(f).RevokeStaffAccess(new StaffRevokeInput { UserId = "approver-a", BranchId = 11, Confirm = true }));
        Assert.NotNull(await f.Users.FindByIdAsync("approver-a"));
        Assert.True(await f.Db.Audits.AnyAsync(x => x.Action == "HistoricalActivity"));
        f.ActAs("approver-a");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Tenant.GetAsync());
    }

    [Fact]
    public async Task CompanyRevocationPreservesOtherTenantsAndClearsOnlyCurrentTenantGrants()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        f.Db.UserCompanyAccess.Add(new UserCompanyAccess { CompanyId = 1, UserId = "operator-b" });
        f.Db.UserBranchAccess.Add(new UserBranchAccess { CompanyId = 1, BranchId = 11, UserId = "operator-b" });
        await f.GrantAsync("Livestock.View", "operator-b", 1);
        await f.GrantAsync("Reports.View", "operator-b", 2);
        Assert.True(await Service(f).RevokeStaffAccess(new StaffRevokeInput { UserId = "operator-b", BranchId = 11, RemoveCompanyAccess = true, Confirm = true }));
        Assert.False(await f.Db.UserCompanyAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 1));
        Assert.False(await f.Db.UserBranchAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 1));
        Assert.False(await f.Db.UserPermissions.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 1));
        Assert.True(await f.Db.UserCompanyAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 2));
        Assert.True(await f.Db.UserBranchAccess.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 2 && x.BranchId == 21));
        Assert.True(await f.Db.UserPermissions.AnyAsync(x => x.UserId == "operator-b" && x.CompanyId == 2 && x.Permission == "Reports.View"));
        f.ActAs("operator-b", 2, 21); Assert.Equal(2, (await f.Tenant.GetAsync()).company);
    }

    [Fact]
    public async Task CompanyRevocationCannotReachBranchesOutsideAdministratorScope()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Permissions.Manage");
        f.Db.UserBranchAccess.Add(new UserBranchAccess { CompanyId = 1, BranchId = 12, UserId = "approver-a" });
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).RevokeStaffAccess(new StaffRevokeInput { UserId = "approver-a", BranchId = 11, RemoveCompanyAccess = true, Confirm = true }));
        Assert.Equal(2, await f.Db.UserBranchAccess.CountAsync(x => x.UserId == "approver-a"));
    }

    [Fact]
    public async Task SelfOrPlatformAdministratorRevocationIsBlocked()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Permissions.Manage");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).RevokeStaffAccess(new StaffRevokeInput { UserId = "operator-a", BranchId = 11, RemoveCompanyAccess = true, Confirm = true }));
        var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("PlatformAdmin"))).Succeeded);
        Assert.True((await f.Users.AddToRoleAsync((await f.Users.FindByIdAsync("approver-a"))!, "PlatformAdmin")).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).RevokeStaffAccess(new StaffRevokeInput { UserId = "approver-a", BranchId = 11, Confirm = true }));
        Assert.True(await f.Db.UserBranchAccess.AnyAsync(x => x.UserId == "operator-a" && x.BranchId == 11));
        Assert.True(await f.Db.UserBranchAccess.AnyAsync(x => x.UserId == "approver-a" && x.BranchId == 11));
    }

    [Fact]
    public async Task UnconfirmedRevocationHasNoEffect()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Permissions.Manage");
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).RevokeStaffAccess(new StaffRevokeInput { UserId = "approver-a", BranchId = 11 }));
        Assert.True(await f.Db.UserBranchAccess.AnyAsync(x => x.UserId == "approver-a" && x.BranchId == 11));
        Assert.Empty(await f.Db.Audits.ToListAsync());
    }

}
