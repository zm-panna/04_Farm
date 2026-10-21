using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseERP.UnitTests;

/// <summary>Real Identity/SQLite regressions for the deliberately bounded offline ownership review.</summary>
public sealed class StaffEmailOwnershipTests
{
    private const string AdministratorPassword = "Synthetic-Administrator-123!";
    private const string StaffPassword = "Synthetic-Staff-Password-123!";
    private const string VerificationAction = "StaffEmailOwnershipVerifiedOffline";

    private static AdministrationService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions,
        f.Users, f.Accessor, f.Services.GetRequiredService<IDataProtectionProvider>());

    private static async Task<string> CreateStaff(SecurityTestScope f)
    {
        await f.GrantAsync("Permissions.Manage");
        await f.GrantAsync("Identity.VerifyEmailOwnership");
        var administrator = (await f.Users.FindByIdAsync("operator-a"))!;
        Assert.True((await f.Users.AddPasswordAsync(administrator, AdministratorPassword)).Succeeded);
        return await Service(f).CreateStaff(new StaffCreateInput
        {
            Email = "new-ownership-staff@example.invalid", Password = StaffPassword, BranchId = 11,
            Permissions = ["Employee.Self", "Ecommerce.View"]
        });
    }

    private static async Task<StaffEmailVerificationInput> Review(SecurityTestScope f, string id,
        string method = "ObservedMailboxControl")
    {
        var user = await f.Db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        return new StaffEmailVerificationInput
        {
            UserId = id, BranchId = 11, Email = user.Email!, ConcurrencyStamp = user.ConcurrencyStamp!,
            Method = method, EvidenceReference = "OFFLINE-REVIEW-2026-001",
            CurrentPassword = AdministratorPassword, Confirm = true
        };
    }

    private sealed record AccountState(bool EmailConfirmed, string? Email, string? NormalizedEmail,
        string? UserName, string? NormalizedUserName, string? PasswordHash, string? SecurityStamp,
        string? ConcurrencyStamp, bool LockoutEnabled, DateTimeOffset? LockoutEnd, int AccessFailedCount,
        bool TwoFactorEnabled, string Companies, string Branches, string Permissions, string Roles, string Claims);

    private static async Task<AccountState> State(SecurityTestScope f, string id)
    {
        var user = await f.Db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        var companies = await f.Db.UserCompanyAccess.AsNoTracking().Where(x => x.UserId == id)
            .OrderBy(x => x.CompanyId).Select(x => x.CompanyId).ToListAsync();
        var branches = await f.Db.UserBranchAccess.AsNoTracking().Where(x => x.UserId == id)
            .OrderBy(x => x.CompanyId).ThenBy(x => x.BranchId).Select(x => new { x.CompanyId, x.BranchId }).ToListAsync();
        var permissions = await f.Db.UserPermissions.AsNoTracking().Where(x => x.UserId == id)
            .OrderBy(x => x.CompanyId).ThenBy(x => x.Permission).Select(x => new { x.CompanyId, x.Permission }).ToListAsync();
        var roles = await f.Users.GetRolesAsync(user);
        var claims = await f.Users.GetClaimsAsync(user);
        return new(user.EmailConfirmed, user.Email, user.NormalizedEmail, user.UserName, user.NormalizedUserName,
            user.PasswordHash, user.SecurityStamp, user.ConcurrencyStamp, user.LockoutEnabled, user.LockoutEnd,
            user.AccessFailedCount, user.TwoFactorEnabled, string.Join(";", companies),
            string.Join(";", branches.Select(x => $"{x.CompanyId}:{x.BranchId}")),
            string.Join(";", permissions.Select(x => $"{x.CompanyId}:{x.Permission}")),
            string.Join(";", roles.OrderBy(x => x, StringComparer.Ordinal)),
            string.Join(";", claims.Select(x => $"{x.Type}:{x.Value}").OrderBy(x => x, StringComparer.Ordinal)));
    }

    private static async Task AssertDeniedWithoutChanges(SecurityTestScope f, StaffEmailVerificationInput input,
        string? unchangedTargetId = null)
    {
        var target = unchangedTargetId ?? input.UserId;
        var before = await State(f, target);
        var auditIds = await f.Db.Audits.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync();
        var exception = await Record.ExceptionAsync(() => Service(f).VerifyStaffEmailOwnership(input));
        Assert.NotNull(exception);
        Assert.True(exception is ArgumentException or InvalidOperationException or UnauthorizedAccessException
            or DbUpdateConcurrencyException, $"Expected a validation/authorization rejection, got {exception}");
        Assert.Equal(before, await State(f, target));
        Assert.Equal(auditIds, await f.Db.Audits.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).ToArrayAsync());
        Assert.Empty(await f.Db.Audits.Where(x => x.Action == VerificationAction).ToListAsync());
    }

    [Theory]
    [InlineData("ObservedMailboxControl")]
    [InlineData("ExternalEmailChallenge")]
    public async Task ReviewedOfflineEvidenceVerifiesOnlyTargetAndInvalidatesItsSecurityStamp(string method)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var user = (await f.Users.FindByIdAsync(id))!;
        Assert.True((await f.Users.AddClaimAsync(user, new Claim("department", "farm-operations"))).Succeeded);
        var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole("ExistingStaffRole"))).Succeeded);
        Assert.True((await f.Users.AddToRoleAsync(user, "ExistingStaffRole")).Succeeded);
        var input = await Review(f, id, method);
        var before = await State(f, id);
        var administratorBefore = await State(f, "operator-a");
        var started = DateTimeOffset.UtcNow;

        Assert.True(await Service(f).VerifyStaffEmailOwnership(input));

        var after = await State(f, id);
        Assert.True(after.EmailConfirmed);
        Assert.NotEqual(before.SecurityStamp, after.SecurityStamp);
        Assert.NotEqual(before.ConcurrencyStamp, after.ConcurrencyStamp);
        Assert.Equal(before with { EmailConfirmed = true, SecurityStamp = after.SecurityStamp,
            ConcurrencyStamp = after.ConcurrencyStamp }, after);
        Assert.Equal(administratorBefore, await State(f, "operator-a"));
        Assert.True(await f.Users.CheckPasswordAsync((await f.Users.FindByIdAsync(id))!, StaffPassword));
        var audit = await f.Db.Audits.SingleAsync(x => x.Action == VerificationAction);
        Assert.Equal(1, audit.CompanyId);
        Assert.Equal("operator-a", audit.ActorId);
        Assert.InRange(audit.CreatedUtc, started, DateTimeOffset.UtcNow);
        Assert.Contains(id, audit.Reference);
        Assert.Contains(input.EvidenceReference, audit.Reference);
        Assert.DoesNotContain(AdministratorPassword, audit.Reference);
        Assert.DoesNotContain(StaffPassword, audit.Reference);
        Assert.DoesNotContain(before.PasswordHash!, audit.Reference);
        Assert.DoesNotContain(input.Email, audit.Reference);
        using var evidence = JsonDocument.Parse(audit.Reference);
        Assert.Equal(id, evidence.RootElement.GetProperty("u").GetString());
        Assert.Equal(11, evidence.RootElement.GetProperty("b").GetInt32());
        Assert.Equal(method == "ObservedMailboxControl" ? "M" : "E", evidence.RootElement.GetProperty("m").GetString());
        Assert.Equal(input.EvidenceReference, evidence.RootElement.GetProperty("r").GetString());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(f.Users.NormalizeEmail(input.Email)!))),
            evidence.RootElement.GetProperty("e").GetString());
        Assert.Equal(5, evidence.RootElement.EnumerateObject().Count());
        Assert.Equal(200, f.Db.Model.FindEntityType(typeof(AuditEntry))!.FindProperty(nameof(AuditEntry.Reference))!.GetMaxLength());
        Assert.InRange(audit.Reference.Length, 1, 200);
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "StaffAccountCreated").ToListAsync());
        Assert.Equal(2, await f.Db.Audits.CountAsync());
    }

    [Fact]
    public async Task RepeatedVerificationDoesNotRotateStampOrDuplicateAudit()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var input = await Review(f, id);
        Assert.True(await Service(f).VerifyStaffEmailOwnership(input));
        var afterFirstReview = await State(f, id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).VerifyStaffEmailOwnership(input));
        var refreshed = await Review(f, id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).VerifyStaffEmailOwnership(refreshed));

        Assert.Equal(afterFirstReview, await State(f, id));
        Assert.Single(await f.Db.Audits.Where(x => x.Action == VerificationAction).ToListAsync());
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("missing-password")]
    [InlineData("oversized-password")]
    [InlineData("missing-confirmation")]
    [InlineData("missing-reference")]
    [InlineData("blank-reference")]
    [InlineData("oversized-reference")]
    [InlineData("escaped-reference-exceeds-storage")]
    [InlineData("control-character-reference")]
    [InlineData("unknown-method")]
    [InlineData("missing-method")]
    [InlineData("wrong-email")]
    [InlineData("stale-version")]
    [InlineData("missing-version")]
    [InlineData("missing-target")]
    [InlineData("foreign-branch")]
    [InlineData("unauthorized-branch")]
    public async Task InvalidOrForgedReviewFailsAtomically(string invalid)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var input = await Review(f, id);
        switch (invalid)
        {
            case "wrong-password": input.CurrentPassword = "Wrong-Administrator-123!"; break;
            case "missing-password": input.CurrentPassword = ""; break;
            case "oversized-password": input.CurrentPassword = new string('P', 1025); break;
            case "missing-confirmation": input.Confirm = false; break;
            case "missing-reference": input.EvidenceReference = ""; break;
            case "blank-reference": input.EvidenceReference = "   "; break;
            case "oversized-reference": input.EvidenceReference = new string('R', 49); break;
            case "escaped-reference-exceeds-storage": input.EvidenceReference = new string('"', 48); break;
            case "control-character-reference": input.EvidenceReference = "OFFLINE-REVIEW\nFORGED-LINE"; break;
            case "unknown-method": input.Method = "EmailWasTypedByAdministrator"; break;
            case "missing-method": input.Method = ""; break;
            case "wrong-email": input.Email = "another-person@example.invalid"; break;
            case "stale-version": input.ConcurrencyStamp = Guid.NewGuid().ToString(); break;
            case "missing-version": input.ConcurrencyStamp = ""; break;
            case "missing-target": input.UserId = "does-not-exist"; break;
            case "foreign-branch": input.BranchId = 21; break;
            case "unauthorized-branch": input.BranchId = 12; break;
        }
        await AssertDeniedWithoutChanges(f, input, id);
    }

    [Theory]
    [InlineData('R')]
    [InlineData('র')]
    public async Task FortyEightCharacterReviewReferenceFitsActualAuditStorageWithoutTruncation(char referenceCharacter)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var input = await Review(f, id);
        input.EvidenceReference = new string(referenceCharacter, 48);
        Assert.True(await Service(f).VerifyStaffEmailOwnership(input));
        var audit = await f.Db.Audits.SingleAsync(x => x.Action == VerificationAction);
        using var evidence = JsonDocument.Parse(audit.Reference);
        Assert.Equal(input.EvidenceReference, evidence.RootElement.GetProperty("r").GetString());
        Assert.Equal(200, f.Db.Model.FindEntityType(typeof(AuditEntry))!.FindProperty(nameof(AuditEntry.Reference))!.GetMaxLength());
        Assert.InRange(audit.Reference.Length, 1, 200);
    }

    [Theory]
    [InlineData("Permissions.Manage")]
    [InlineData("Identity.VerifyEmailOwnership")]
    public async Task BothPersistedCompanyPermissionsAreRequired(string removedPermission)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var input = await Review(f, id);
        f.Db.UserPermissions.Remove(await f.Db.UserPermissions.SingleAsync(x => x.UserId == "operator-a"
            && x.CompanyId == 1 && x.Permission == removedPermission));
        await f.Db.SaveChangesAsync();
        // Neither a forged cookie claim nor a permission in another company replaces the removed grant.
        ((ClaimsIdentity)f.Http.User.Identity!).AddClaim(new Claim("Permission", removedPermission));
        await f.GrantAsync(removedPermission, "operator-a", 2);
        await AssertDeniedWithoutChanges(f, input);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedWrongAdministratorPasswordsRemainRateLimitedOutsideRollback(bool lockoutInitiallyEnabled)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var administrator = (await f.Users.FindByIdAsync("operator-a"))!;
        Assert.True((await f.Users.SetLockoutEnabledAsync(administrator, lockoutInitiallyEnabled)).Succeeded);
        var input = await Review(f, id);
        input.CurrentPassword = "Wrong-Administrator-123!";
        for (var attempt = 1; attempt <= f.Users.Options.Lockout.MaxFailedAccessAttempts; attempt++)
        {
            await AssertDeniedWithoutChanges(f, input);
            administrator = (await f.Users.FindByIdAsync("operator-a"))!;
            Assert.True(await f.Users.GetLockoutEnabledAsync(administrator));
            if (attempt < f.Users.Options.Lockout.MaxFailedAccessAttempts)
                Assert.Equal(attempt, await f.Users.GetAccessFailedCountAsync(administrator));
        }
        Assert.True(await f.Users.IsLockedOutAsync(administrator));
        input.CurrentPassword = AdministratorPassword;
        await AssertDeniedWithoutChanges(f, input);
        Assert.True(await f.Users.IsLockedOutAsync((await f.Users.FindByIdAsync("operator-a"))!));
    }

    [Fact]
    public async Task ValidLongExistingAdministratorPasswordCanReauthenticateWithoutReplacingCredentials()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var administrator = (await f.Users.FindByIdAsync("operator-a"))!;
        var longPassword = new string('p', 180) + "A7!";
        Assert.True((await f.Users.ChangePasswordAsync(administrator, AdministratorPassword, longPassword)).Succeeded);
        var administratorBefore = await State(f, administrator.Id);
        var input = await Review(f, id);
        input.CurrentPassword = longPassword;
        Assert.True(Validator.TryValidateObject(input, new ValidationContext(input), new List<ValidationResult>(), true));

        Assert.True(await Service(f).VerifyStaffEmailOwnership(input));

        Assert.Equal(administratorBefore, await State(f, administrator.Id));
        Assert.DoesNotContain(longPassword, (await f.Db.Audits.SingleAsync(x => x.Action == VerificationAction)).Reference);
        Assert.True(await f.Users.CheckPasswordAsync((await f.Users.FindByIdAsync(administrator.Id))!, longPassword));
        Assert.True(await f.Users.CheckPasswordAsync((await f.Users.FindByIdAsync(id))!, StaffPassword));
    }

    [Fact]
    public async Task OwnershipVerificationPermissionCannotBeMintedThroughOrdinaryStaffProvisioning()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await f.GrantAsync("Permissions.Manage");
        Assert.DoesNotContain("Identity.VerifyEmailOwnership", PermissionService.GrantablePermissions);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).CreateStaff(new StaffCreateInput
        {
            Email = "privilege-attempt@example.invalid", Password = StaffPassword, BranchId = 11,
            Permissions = ["Identity.VerifyEmailOwnership"]
        }));
        Assert.Equal(3, await f.Db.Users.CountAsync());
        Assert.Empty(await f.Db.Audits.ToListAsync());
    }

    [Fact]
    public async Task AnotherTenantAdministratorCannotVerifyTheOriginalCompanysAccount()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var input = await Review(f, id);
        await f.GrantAsync("Permissions.Manage", "operator-b", 2);
        await f.GrantAsync("Identity.VerifyEmailOwnership", "operator-b", 2);
        Assert.True((await f.Users.AddPasswordAsync((await f.Users.FindByIdAsync("operator-b"))!, AdministratorPassword)).Succeeded);
        f.ActAs("operator-b", 2, 21);
        input.BranchId = 21;
        await AssertDeniedWithoutChanges(f, input);
    }

    [Theory]
    [InlineData("shared-company")]
    [InlineData("outside-branch-scope")]
    [InlineData("selected-branch-not-assigned")]
    [InlineData("missing-company-membership")]
    [InlineData("wrong-origin-company")]
    [InlineData("no-creation-audit")]
    [InlineData("locked-target")]
    [InlineData("wildcard-target")]
    [InlineData("administrator-target")]
    [InlineData("platform-target")]
    public async Task SharedUnmanagedProtectedOrUnprovenAccountsCannotBeVerified(string disallowedState)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        switch (disallowedState)
        {
            case "shared-company":
                f.Db.UserCompanyAccess.Add(new UserCompanyAccess { UserId = id, CompanyId = 2 });
                f.Db.UserBranchAccess.Add(new UserBranchAccess { UserId = id, CompanyId = 2, BranchId = 21 });
                break;
            case "outside-branch-scope":
                f.Db.UserBranchAccess.Add(new UserBranchAccess { UserId = id, CompanyId = 1, BranchId = 12 });
                break;
            case "selected-branch-not-assigned":
                f.Db.UserBranchAccess.Remove(await f.Db.UserBranchAccess.SingleAsync(x => x.UserId == id));
                break;
            case "missing-company-membership":
                f.Db.UserCompanyAccess.Remove(await f.Db.UserCompanyAccess.SingleAsync(x => x.UserId == id));
                break;
            case "wrong-origin-company":
                (await f.Db.Audits.SingleAsync(x => x.Action == "StaffAccountCreated")).CompanyId = 2;
                break;
            case "no-creation-audit":
                f.Db.Audits.Remove(await f.Db.Audits.SingleAsync(x => x.Action == "StaffAccountCreated"));
                break;
            case "locked-target":
                Assert.True((await f.Users.SetLockoutEndDateAsync((await f.Users.FindByIdAsync(id))!, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
                break;
            case "wildcard-target": await f.GrantAsync("*", id); break;
            case "administrator-target": await f.GrantAsync("Permissions.Manage", id); break;
            case "platform-target":
                var roles = f.Services.GetRequiredService<RoleManager<IdentityRole>>();
                Assert.True((await roles.CreateAsync(new IdentityRole("PlatformAdmin"))).Succeeded);
                Assert.True((await f.Users.AddToRoleAsync((await f.Users.FindByIdAsync(id))!, "PlatformAdmin")).Succeeded);
                break;
        }
        await f.Db.SaveChangesAsync();
        await AssertDeniedWithoutChanges(f, await Review(f, id));
    }

    [Fact]
    public async Task ExistingAccountAttachmentNeverCountsAsCreationProvenance()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        await CreateStaff(f);
        // A pre-existing local account has branch membership but no StaffAccountCreated record.
        await AssertDeniedWithoutChanges(f, await Review(f, "approver-a"));
        var existing = new AppUser { UserName = "external-existing@example.invalid", Email = "external-existing@example.invalid", LockoutEnabled = true };
        Assert.True((await f.Users.CreateAsync(existing, StaffPassword)).Succeeded);
        var lookup = await Service(f).LookupExisting(new StaffLookupInput { Email = existing.Email, BranchId = 11 });
        Assert.Equal(existing.Id, await Service(f).AttachExisting(new StaffAttachInput
            { ConfirmationToken = lookup.ConfirmationToken, Confirm = true }));
        Assert.Single(await f.Db.UserCompanyAccess.Where(x => x.UserId == existing.Id && x.CompanyId == 1).ToListAsync());
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "StaffBranchAccessGranted").ToListAsync());
        await AssertDeniedWithoutChanges(f, await Review(f, existing.Id));
    }

    [Fact]
    public async Task MultipleBranchesAreAllowedOnlyWhenAllAreInTheReviewersScope()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        f.Db.UserBranchAccess.AddRange(new UserBranchAccess { UserId = id, CompanyId = 1, BranchId = 12 },
            new UserBranchAccess { UserId = "operator-a", CompanyId = 1, BranchId = 12 });
        await f.Db.SaveChangesAsync();
        Assert.True(await Service(f).VerifyStaffEmailOwnership(await Review(f, id)));
        Assert.Equal(2, await f.Db.UserBranchAccess.CountAsync(x => x.UserId == id));
        Assert.Single(await f.Db.Audits.Where(x => x.Action == VerificationAction).ToListAsync());
    }

    [Fact]
    public async Task SelfVerificationIsRejectedEvenWithOriginalCreationProvenance()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        await f.GrantAsync("Permissions.Manage", id);
        await f.GrantAsync("Identity.VerifyEmailOwnership", id);
        f.ActAs(id);
        var input = await Review(f, id);
        input.CurrentPassword = StaffPassword;
        await AssertDeniedWithoutChanges(f, input);
    }

    [Fact]
    public async Task ChangedPersistedEmailOrVersionInvalidatesPreviouslyReviewedInput()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var original = await Review(f, id);
        var user = (await f.Users.FindByIdAsync(id))!;
        Assert.True((await f.Users.SetEmailAsync(user, "changed-ownership-staff@example.invalid")).Succeeded);
        await AssertDeniedWithoutChanges(f, original);
        var correctEmailStaleVersion = await Review(f, id);
        correctEmailStaleVersion.ConcurrencyStamp = original.ConcurrencyStamp;
        await AssertDeniedWithoutChanges(f, correctEmailStaleVersion);
    }

    [Fact]
    public async Task AuditFailureRollsBackConfirmationStampAndAllIdentityChanges()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var input = await Review(f, id);
        var before = await State(f, id);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectOwnershipAudit BEFORE INSERT ON Audits WHEN NEW.Action='StaffEmailOwnershipVerifiedOffline' BEGIN SELECT RAISE(ABORT,'simulated ownership audit failure'); END;");

        await Assert.ThrowsAsync<DbUpdateException>(() => Service(f).VerifyStaffEmailOwnership(input));

        Assert.Empty(f.Db.ChangeTracker.Entries());
        Assert.Equal(before, await State(f, id));
        Assert.Empty(await f.Db.Audits.Where(x => x.Action == VerificationAction).ToListAsync());
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "StaffAccountCreated").ToListAsync());
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectOwnershipAudit;");
        Assert.True(await Service(f).VerifyStaffEmailOwnership(input));
        Assert.Single(await f.Db.Audits.Where(x => x.Action == VerificationAction).ToListAsync());
    }

    [Fact]
    public async Task CreatedStaffBecomesEligibleForHrAndEcommerceOnlyAfterActualOwnershipReview()
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        await CompanyBusinessPolicyTestData.SeedAsync(f);
        await f.GrantAsync("HR.Manage");
        await f.GrantAsync("HR.IdentityLink");
        await f.GrantAsync("Ecommerce.Manage");
        var hr = new HRPayrollService(f.Db, f.Tenant, f.Permissions);
        var ecommerce = new EcommerceService(f.Db, f.Tenant, f.Permissions);
        var staffId = await hr.SaveStaff(new HRPayrollStaffInput
        {
            Code = "VERIFIED-STAFF", Name = "Synthetic ownership staff", JobTitle = "Farm operator",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)
        });
        var party = new TradingParty { CompanyId = 1, BranchId = 11, Kind = "Customer",
            Code = "VERIFIED-CUSTOMER", Name = "Synthetic ownership customer", Active = true };
        f.Db.TradingParties.Add(party);
        await f.Db.SaveChangesAsync();
        var staff = await f.Db.HRPayrollStaff.AsNoTracking().SingleAsync(x => x.Id == staffId);

        Assert.False((await f.Db.Users.AsNoTracking().SingleAsync(x => x.Id == id)).EmailConfirmed);
        Assert.DoesNotContain((await hr.IdentityForm(staffId)).users, x => x.Id == id);
        await Assert.ThrowsAsync<ArgumentException>(() => hr.LinkIdentity(staffId, id, staff.Revision, true));
        await Assert.ThrowsAsync<ArgumentException>(() => ecommerce.LinkCustomer(id, party.Id, true, default));
        Assert.Null((await f.Db.HRPayrollStaff.AsNoTracking().SingleAsync(x => x.Id == staffId)).IdentityUserId);
        Assert.Empty(await f.Db.EcommerceCustomers.ToListAsync());

        Assert.True(await Service(f).VerifyStaffEmailOwnership(await Review(f, id)));

        Assert.Contains((await hr.IdentityForm(staffId)).users, x => x.Id == id);
        staff = await f.Db.HRPayrollStaff.AsNoTracking().SingleAsync(x => x.Id == staffId);
        await hr.LinkIdentity(staffId, id, staff.Revision, true);
        var profile = await ecommerce.LinkCustomer(id, party.Id, true, default);
        Assert.Equal(id, (await f.Db.HRPayrollStaff.AsNoTracking().SingleAsync(x => x.Id == staffId)).IdentityUserId);
        Assert.Equal(id, profile.UserId);
        Assert.Equal(party.Id, profile.TradingPartyId);
        Assert.Equal("operator-a", profile.LinkedBy);
        Assert.Single(await f.Db.Audits.Where(x => x.Action == VerificationAction).ToListAsync());
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "HR.Identity.Link").ToListAsync());
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "Ecommerce.CustomerLinked").ToListAsync());
        Assert.True(await f.Users.CheckPasswordAsync((await f.Users.FindByIdAsync(id))!, StaffPassword));
    }

    [Fact]
    public void VerificationEndpointRequiresAuthenticationPostAndAntiforgery()
    {
        Assert.NotEmpty(typeof(AdministrationController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var action = typeof(AdministrationController).GetMethod(nameof(AdministrationController.VerifyEmailOwnership))!;
        Assert.NotEmpty(action.GetCustomAttributes(typeof(HttpPostAttribute), true));
        Assert.NotEmpty(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidReviewNeverRedisplaysTheCurrentAdministratorPassword(bool invalidModel)
    {
        await using var f = await SecurityTestScope.CreateAsync();
        var id = await CreateStaff(f);
        var input = await Review(f, id);
        input.CurrentPassword = "Wrong-Administrator-123!";
        var controller = new AdministrationController(Service(f))
            { ControllerContext = new ControllerContext { HttpContext = f.Http } };
        controller.ModelState.SetModelValue("VerifyEmail.CurrentPassword", input.CurrentPassword, input.CurrentPassword);
        if (invalidModel) controller.ModelState.AddModelError("VerifyEmail.EvidenceReference", "A review reference is required.");

        var result = Assert.IsType<ViewResult>(await controller.VerifyEmailOwnership(input, default));

        Assert.Equal("Index", result.ViewName);
        Assert.IsType<AdministrationPage>(result.Model);
        Assert.Equal("", input.CurrentPassword);
        Assert.False(controller.ModelState.ContainsKey("VerifyEmail.CurrentPassword"));
        Assert.False((await f.Db.Users.AsNoTracking().SingleAsync(x => x.Id == id)).EmailConfirmed);
        Assert.Empty(await f.Db.Audits.Where(x => x.Action == VerificationAction).ToListAsync());
    }
}
