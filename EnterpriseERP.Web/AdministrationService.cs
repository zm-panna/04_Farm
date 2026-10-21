using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

public sealed class StaffCreateInput
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 12), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Range(1, int.MaxValue)] public int BranchId { get; set; }
    public string[] Permissions { get; set; } = [];
}
public sealed class StaffEmailVerificationInput
{
    [Required, StringLength(450)] public string UserId { get; set; } = "";
    [Range(1, int.MaxValue)] public int BranchId { get; set; }
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(128)] public string ConcurrencyStamp { get; set; } = "";
    [Required, StringLength(40)] public string Method { get; set; } = "";
    [Required, StringLength(48, MinimumLength = 5)] public string EvidenceReference { get; set; } = "";
    [Required, StringLength(1024), DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
    public bool Confirm { get; set; }
}
public sealed class StaffLookupInput
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Range(1, int.MaxValue)] public int BranchId { get; set; }
}
public sealed class StaffAttachInput
{
    [Required, StringLength(8192)] public string ConfirmationToken { get; set; } = "";
    public bool Confirm { get; set; }
}
public sealed class StaffRevokeInput
{
    [Required, StringLength(450)] public string UserId { get; set; } = "";
    [Range(1, int.MaxValue)] public int BranchId { get; set; }
    public bool RemoveCompanyAccess { get; set; }
    public bool Confirm { get; set; }
}
public sealed class CompanyCreateInput
{
    [Required, StringLength(120)] public string CompanyName { get; set; } = "";
    [Required, StringLength(120)] public string FarmName { get; set; } = "";
    [Required, StringLength(120)] public string BranchName { get; set; } = "";
}
public sealed class BranchCreateInput
{
    public int? FarmId { get; set; }
    [StringLength(120)] public string? NewFarmName { get; set; }
    [Required, StringLength(120)] public string BranchName { get; set; } = "";
}
public sealed record StaffRow(string UserId, string Email, string BranchName, int BranchId, bool EmailConfirmed = false, string ConcurrencyStamp = "");
public sealed record ExistingStaffCandidate(string Email, string BranchName, string ConfirmationToken);
public sealed class AdministrationPage
{
    public string CompanyName { get; set; } = "";
    public int CompanyId { get; set; }
    public bool IsPlatformAdmin { get; set; }
    public bool CanVerifyEmailOwnership { get; set; }
    public List<Branch> Branches { get; set; } = new();
    public List<Farm> Farms { get; set; } = new();
    public List<StaffRow> Staff { get; set; } = new();
    public StaffCreateInput CreateStaff { get; set; } = new() { Permissions = ["Livestock.View"] };
    public StaffLookupInput Lookup { get; set; } = new();
    public CompanyCreateInput NewCompany { get; set; } = new();
    public BranchCreateInput NewBranch { get; set; } = new();
    public ExistingStaffCandidate? Candidate { get; set; }
}

/// <summary>Explicit tenant-scoped staff provisioning; platform creation is separately authorized.</summary>
public sealed class AdministrationService(AppDbContext db, TenantContext tenant, PermissionService permissions,
    UserManager<AppUser> users, IHttpContextAccessor accessor, IDataProtectionProvider protection)
{
    private readonly IDataProtector lookupProtector = protection.CreateProtector("FarmERP.Administration.ExistingStaff.v1");
    private sealed record LookupProof(string ActorId, int CompanyId, int BranchId, string TargetId, string Email, long ExpiresUtcTicks);

    public async Task<AdministrationPage> Page(CancellationToken ct = default)
    {
        await permissions.Demand("Permissions.Manage", ct);
        var t = await tenant.GetAsync(ct);
        var branches = await AuthorizedBranches(t.company, t.user).OrderBy(x => x.Name).ToListAsync(ct);
        var branchIds = branches.Select(x => x.Id).ToArray();
        return new AdministrationPage
        {
            CompanyId = t.company,
            CompanyName = (await db.Companies.AsNoTracking().SingleAsync(x => x.Id == t.company, ct)).Name,
            IsPlatformAdmin = await IsPlatformAdministrator(),
            CanVerifyEmailOwnership = await db.UserPermissions.AsNoTracking().AnyAsync(x => x.CompanyId == t.company && x.UserId == t.user && (x.Permission == "Identity.VerifyEmailOwnership" || x.Permission == "*"), ct),
            Branches = branches,
            Farms = await db.Farms.AsNoTracking().Where(x => x.CompanyId == t.company).OrderBy(x => x.Name).ToListAsync(ct),
            Staff = await (from grant in db.UserBranchAccess.AsNoTracking()
                join user in db.Users.AsNoTracking() on grant.UserId equals user.Id
                join branch in db.Branches.AsNoTracking() on grant.BranchId equals branch.Id
                where grant.CompanyId == t.company && branch.CompanyId == t.company && branchIds.Contains(branch.Id)
                    && db.UserCompanyAccess.Any(x => x.UserId == user.Id && x.CompanyId == t.company)
                orderby user.Email, branch.Name
                select new StaffRow(user.Id, user.Email ?? user.UserName ?? user.Id, branch.Name, branch.Id, user.EmailConfirmed, user.ConcurrencyStamp ?? "")).ToListAsync(ct)
        };
    }

    public Task<string> CreateStaff(StaffCreateInput input, CancellationToken ct = default) => Atomic(async () =>
    {
        await permissions.Demand("Permissions.Manage", ct);
        var t = await tenant.GetAsync(ct);
        await RequireAuthorizedBranch(t.company, t.user, input.BranchId, ct);
        var email = Email(input.Email);
        if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length is < 12 or > 128)
            throw new ArgumentException("Enter a password of 12–128 characters. Identity complexity rules also apply.");
        var grants = input.Permissions?.Distinct(StringComparer.Ordinal).ToArray() ?? [];
        if (grants.Any(x => string.IsNullOrWhiteSpace(x) || !PermissionService.GrantablePermissions.Contains(x)))
            throw new ArgumentException("Only listed operational permissions can be assigned. Administrator, wildcard and platform privileges are protected.");
        if (await users.FindByEmailAsync(email) != null)
            throw new InvalidOperationException("An account with this email already exists. Use the exact-email lookup and confirmation below; its password will not be changed.");
        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = false, LockoutEnabled = true };
        Ensure(await users.CreateAsync(user, input.Password));
        db.UserCompanyAccess.Add(new UserCompanyAccess { UserId = user.Id, CompanyId = t.company });
        db.UserBranchAccess.Add(new UserBranchAccess { UserId = user.Id, CompanyId = t.company, BranchId = input.BranchId });
        foreach (var grant in grants)
            db.UserPermissions.Add(new UserPermission { UserId = user.Id, CompanyId = t.company, Permission = grant });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "StaffAccountCreated", Reference = user.Id });
        await db.SaveChangesAsync(ct);
        return user.Id;
    }, ct);

    // A bounded, explicit manual attestation. This app neither sends an email nor claims SMTP verification.
    public async Task<bool> VerifyStaffEmailOwnership(StaffEmailVerificationInput input, CancellationToken ct = default)
    {
        await permissions.Demand("Permissions.Manage", ct);
        await permissions.Demand("Identity.VerifyEmailOwnership", ct);
        var t = await tenant.GetAsync(ct);
        if (!input.Confirm || input.Method is not ("ObservedMailboxControl" or "ExternalEmailChallenge"))
            throw new ArgumentException("Choose the completed out-of-band email ownership check and explicitly confirm it. No email is sent by this application.");
        var reference = input.EvidenceReference?.Trim() ?? "";
        if (reference.Length is < 5 or > 48 || reference.Any(char.IsControl))
            throw new ArgumentException("Enter a non-sensitive verification reference of 5–48 characters. Do not enter passwords, identity documents or mailbox contents.");
        var expectedEmail = Email(input.Email);
        if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp.Length > 128)
            throw new ArgumentException("Refresh the staff list before verifying this account.");
        var actor = await users.FindByIdAsync(t.user);
        if (actor == null || await users.IsLockedOutAsync(actor) || string.IsNullOrEmpty(input.CurrentPassword) || input.CurrentPassword.Length > 1024)
            throw new UnauthorizedAccessException("Current administrator password confirmation is required.");
        // Password failure accounting deliberately happens outside the business transaction so rollback cannot undo lockout.
        if (!await users.CheckPasswordAsync(actor, input.CurrentPassword))
        {
            if (!await users.GetLockoutEnabledAsync(actor)) Ensure(await users.SetLockoutEnabledAsync(actor, true));
            Ensure(await users.AccessFailedAsync(actor));
            throw new ArgumentException("Current administrator password confirmation failed.");
        }
        if (await users.GetAccessFailedCountAsync(actor) > 0) Ensure(await users.ResetAccessFailedCountAsync(actor));
        return await Atomic(async () =>
        {
            await permissions.Demand("Permissions.Manage", ct);
            await permissions.Demand("Identity.VerifyEmailOwnership", ct);
            await RequireAuthorizedBranch(t.company, t.user, input.BranchId, ct);
            if (string.IsNullOrWhiteSpace(input.UserId) || input.UserId == t.user)
                throw new InvalidOperationException("You cannot verify your own email ownership here.");
            var target = await users.FindByIdAsync(input.UserId)
                ?? throw new InvalidOperationException("This account is not eligible for scoped offline email verification.");
            var companies = await db.UserCompanyAccess.Where(x => x.UserId == target.Id).Select(x => x.CompanyId).ToListAsync(ct);
            var branches = await db.UserBranchAccess.Where(x => x.UserId == target.Id).ToListAsync(ct);
            var allowed = await AuthorizedBranches(t.company, t.user).Select(x => x.Id).ToListAsync(ct);
            if (companies.Count != 1 || companies[0] != t.company || branches.Count == 0
                || !branches.Any(x => x.CompanyId == t.company && x.BranchId == input.BranchId)
                || branches.Any(x => x.CompanyId != t.company || !allowed.Contains(x.BranchId))
                || !await db.Audits.AnyAsync(x => x.CompanyId == t.company && x.Action == "StaffAccountCreated" && x.Reference == target.Id, ct))
                throw new InvalidOperationException("Only accounts created in this company, with no other company or unauthorized branch access, can use this review.");
            if (await users.IsInRoleAsync(target, "PlatformAdmin")
                || await db.UserPermissions.AnyAsync(x => x.UserId == target.Id && (x.Permission == "*" || x.Permission == "Permissions.Manage"), ct))
                throw new InvalidOperationException("Privileged identities require the separately authorized account-recovery process.");
            if (target.EmailConfirmed || await users.IsLockedOutAsync(target)
                || users.NormalizeEmail(target.Email) != users.NormalizeEmail(expectedEmail)
                || !string.Equals(target.ConcurrencyStamp, input.ConcurrencyStamp, StringComparison.Ordinal))
                throw new InvalidOperationException("The account changed, is locked or already verified. Refresh and review its current state.");
            var emailHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(users.NormalizeEmail(expectedEmail)!)));
            // Audits.Reference is nvarchar(200) on SQL Server. Compact field keys are documented;
            // retain the complete external reference and full email hash without truncating either.
            var auditReference = JsonSerializer.Serialize(new { u = target.Id, b = input.BranchId, e = emailHash,
                m = input.Method == "ObservedMailboxControl" ? "M" : "E", r = reference },
                new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            if (auditReference.Length > 200)
                throw new ArgumentException("Use a shorter verification reference that fits the bounded audit record; no reference will be truncated.");
            target.EmailConfirmed = true;
            Ensure(await users.UpdateAsync(target));
            Ensure(await users.UpdateSecurityStampAsync(target));
            db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "StaffEmailOwnershipVerifiedOffline", Reference = auditReference });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task<ExistingStaffCandidate> LookupExisting(StaffLookupInput input, CancellationToken ct = default)
    {
        await permissions.Demand("Permissions.Manage", ct);
        var t = await tenant.GetAsync(ct);
        var branch = await RequireAuthorizedBranch(t.company, t.user, input.BranchId, ct);
        var email = Email(input.Email);
        var user = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("No existing account matches that exact email. Create a new staff account if appropriate.");
        if (string.IsNullOrWhiteSpace(user.Email)) throw new InvalidOperationException("The account has no verified lookup email.");
        // Show only the exact requested account and target branch. Other tenant memberships are never exposed.
        var proof = new LookupProof(t.user, t.company, branch.Id, user.Id, user.Email, DateTimeOffset.UtcNow.AddMinutes(10).UtcTicks);
        return new ExistingStaffCandidate(user.Email, branch.Name, lookupProtector.Protect(JsonSerializer.Serialize(proof)));
    }

    public Task<string> AttachExisting(StaffAttachInput input, CancellationToken ct = default) => Atomic(async () =>
    {
        await permissions.Demand("Permissions.Manage", ct);
        var t = await tenant.GetAsync(ct);
        if (!input.Confirm || string.IsNullOrWhiteSpace(input.ConfirmationToken) || input.ConfirmationToken.Length > 8192)
            throw new ArgumentException("Review the exact account and branch, then explicitly confirm access.");
        LookupProof proof;
        try { proof = JsonSerializer.Deserialize<LookupProof>(lookupProtector.Unprotect(input.ConfirmationToken)) ?? throw new JsonException(); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        { throw new ArgumentException("Lookup confirmation is invalid. Look up the email again."); }
        if (proof.ActorId != t.user || proof.CompanyId != t.company || proof.ExpiresUtcTicks < DateTimeOffset.UtcNow.UtcTicks)
            throw new UnauthorizedAccessException("Lookup expired or belongs to a different administrator/company. Look up the email again.");
        await RequireAuthorizedBranch(t.company, t.user, proof.BranchId, ct);
        var user = await users.FindByIdAsync(proof.TargetId);
        if (user == null || users.NormalizeEmail(user.Email) != users.NormalizeEmail(proof.Email))
            throw new InvalidOperationException("The account changed after lookup. Look up the email again.");
        var changed = false;
        if (!await db.UserCompanyAccess.AnyAsync(x => x.UserId == user.Id && x.CompanyId == t.company, ct))
        { db.UserCompanyAccess.Add(new UserCompanyAccess { UserId = user.Id, CompanyId = t.company }); changed = true; }
        if (!await db.UserBranchAccess.AnyAsync(x => x.UserId == user.Id && x.CompanyId == t.company && x.BranchId == proof.BranchId, ct))
        { db.UserBranchAccess.Add(new UserBranchAccess { UserId = user.Id, CompanyId = t.company, BranchId = proof.BranchId }); changed = true; }
        if (changed)
        {
            db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "StaffBranchAccessGranted", Reference = user.Id + ":" + proof.BranchId });
            await db.SaveChangesAsync(ct);
        }
        return user.Id;
    }, ct);

    public Task<bool> RevokeStaffAccess(StaffRevokeInput input, CancellationToken ct = default) => Atomic(async () =>
    {
        await permissions.Demand("Permissions.Manage", ct);
        var t = await tenant.GetAsync(ct);
        if (!input.Confirm) throw new ArgumentException("Explicitly confirm the account and access to revoke.");
        await RequireAuthorizedBranch(t.company, t.user, input.BranchId, ct);
        if (string.IsNullOrWhiteSpace(input.UserId) || input.UserId == t.user)
            throw new InvalidOperationException("You cannot revoke your own access here. Use another authorized administrator or the protected recovery process.");
        var user = await users.FindByIdAsync(input.UserId) ?? throw new InvalidOperationException("Account is unavailable.");
        if (await users.IsInRoleAsync(user, "PlatformAdmin"))
            throw new InvalidOperationException("Platform administrator access is protected. Use the separate authorized recovery process.");
        var selectedGrant = await db.UserBranchAccess.SingleOrDefaultAsync(x => x.CompanyId == t.company
            && x.BranchId == input.BranchId && x.UserId == user.Id, ct);
        if (selectedGrant == null) return false;
        var targetGrants = await db.UserBranchAccess.Where(x => x.CompanyId == t.company && x.UserId == user.Id).ToListAsync(ct);
        if (input.RemoveCompanyAccess)
        {
            var allowed = await AuthorizedBranches(t.company, t.user).Select(x => x.Id).ToListAsync(ct);
            if (targetGrants.Any(x => !allowed.Contains(x.BranchId)))
                throw new UnauthorizedAccessException("You cannot revoke company-wide access while this account has branches outside your own authorized scope.");
        }
        // Always retain a real, scoped administrator. A role name or stale cookie is not sufficient.
        var administratorIds = await db.UserPermissions.Where(p => p.CompanyId == t.company
            && (p.Permission == "*" || p.Permission == "Permissions.Manage")
            && db.Users.Any(u => u.Id == p.UserId)
            && db.UserCompanyAccess.Any(a => a.CompanyId == t.company && a.UserId == p.UserId))
            .Select(p => p.UserId).Distinct().ToListAsync(ct);
        var administratorBranches = await db.UserBranchAccess.Where(a => a.CompanyId == t.company
            && administratorIds.Contains(a.UserId) && db.Branches.Any(b => b.CompanyId == t.company && b.Id == a.BranchId)).ToListAsync(ct);
        if (!administratorBranches.Any(a => a.UserId != user.Id
            || (!input.RemoveCompanyAccess && a.BranchId != input.BranchId)))
            throw new InvalidOperationException("The last authorized administrator cannot be removed.");
        if (input.RemoveCompanyAccess)
        {
            db.UserBranchAccess.RemoveRange(targetGrants);
            db.UserCompanyAccess.RemoveRange(await db.UserCompanyAccess.Where(x => x.CompanyId == t.company && x.UserId == user.Id).ToListAsync(ct));
            db.UserPermissions.RemoveRange(await db.UserPermissions.Where(x => x.CompanyId == t.company && x.UserId == user.Id).ToListAsync(ct));
        }
        else db.UserBranchAccess.Remove(selectedGrant);
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user,
            Action = input.RemoveCompanyAccess ? "StaffCompanyAccessRevoked" : "StaffBranchAccessRevoked",
            Reference = user.Id + ":" + input.BranchId });
        await db.SaveChangesAsync(ct);
        // TenantContext rechecks membership on every request, immediately invalidating old selection claims.
        return true;
    }, ct);

    public Task<int> CreateCompany(CompanyCreateInput input, CancellationToken ct = default) => Atomic(async () =>
    {
        var actor = await RequirePlatformAdministrator();
        var companyName = Name(input.CompanyName, "Company");
        var farmName = Name(input.FarmName, "Farm");
        var branchName = Name(input.BranchName, "Branch");
        if (await db.Companies.AnyAsync(x => x.Name == companyName, ct)) throw new InvalidOperationException("That company name already exists.");
        var company = new Company { Name = companyName };
        db.Companies.Add(company); await db.SaveChangesAsync(ct);
        var farm = new Farm { CompanyId = company.Id, Name = farmName };
        db.Farms.Add(farm); await db.SaveChangesAsync(ct);
        var branch = new Branch { CompanyId = company.Id, FarmId = farm.Id, Name = branchName };
        db.Branches.Add(branch); await db.SaveChangesAsync(ct);
        db.UserCompanyAccess.Add(new UserCompanyAccess { CompanyId = company.Id, UserId = actor.Id });
        db.UserBranchAccess.Add(new UserBranchAccess { CompanyId = company.Id, BranchId = branch.Id, UserId = actor.Id });
        db.UserPermissions.Add(new UserPermission { CompanyId = company.Id, UserId = actor.Id, Permission = "*" });
        db.CompanySubscriptionPolicies.Add(new CompanySubscriptionPolicy { CompanyId = company.Id });
        db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = actor.Id, Action = "CompanyProvisioned", Reference = branch.Id.ToString() });
        await db.SaveChangesAsync(ct);
        return company.Id;
    }, ct);

    public Task<int> CreateBranch(BranchCreateInput input, CancellationToken ct = default) => Atomic(async () =>
    {
        var actor = await RequirePlatformAdministrator();
        var t = await tenant.GetAsync(ct); // The selected existing company must still be in this administrator's scope.
        var branchName = Name(input.BranchName, "Branch");
        if (await db.Branches.AnyAsync(x => x.CompanyId == t.company && x.Name == branchName, ct))
            throw new InvalidOperationException("That branch name already exists in this company.");
        Farm farm;
        if (input.FarmId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(input.NewFarmName)) throw new ArgumentException("Choose an existing farm or enter a new farm name, not both.");
            farm = await db.Farms.SingleOrDefaultAsync(x => x.Id == input.FarmId && x.CompanyId == t.company, ct)
                ?? throw new UnauthorizedAccessException("Farm is outside the selected company.");
        }
        else
        {
            var name = Name(input.NewFarmName, "New farm");
            if (await db.Farms.AnyAsync(x => x.CompanyId == t.company && x.Name == name, ct))
                throw new InvalidOperationException("That farm already exists. Select it from the existing-farm list.");
            farm = new Farm { CompanyId = t.company, Name = name };
            db.Farms.Add(farm); await db.SaveChangesAsync(ct);
        }
        var branch = new Branch { CompanyId = t.company, FarmId = farm.Id, Name = branchName };
        db.Branches.Add(branch); await db.SaveChangesAsync(ct);
        db.UserBranchAccess.Add(new UserBranchAccess { CompanyId = t.company, BranchId = branch.Id, UserId = actor.Id });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = actor.Id, Action = "BranchProvisioned", Reference = branch.Id.ToString() });
        await db.SaveChangesAsync(ct);
        return branch.Id;
    }, ct);

    private IQueryable<Branch> AuthorizedBranches(int company, string user) => db.Branches.AsNoTracking().Where(b => b.CompanyId == company
        && db.UserBranchAccess.Any(a => a.UserId == user && a.CompanyId == company && a.BranchId == b.Id));
    private async Task<Branch> RequireAuthorizedBranch(int company, string user, int branch, CancellationToken ct) =>
        await AuthorizedBranches(company, user).SingleOrDefaultAsync(b => b.Id == branch, ct)
        ?? throw new UnauthorizedAccessException("You may provision staff only in branches you are authorized to use in this company.");
    private async Task<bool> IsPlatformAdministrator()
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return false;
        var user = await users.GetUserAsync(principal);
        return user != null && await users.IsInRoleAsync(user, "PlatformAdmin");
    }
    private async Task<AppUser> RequirePlatformAdministrator()
    {
        if (!await IsPlatformAdministrator()) throw new UnauthorizedAccessException("Only a platform administrator may create companies, farms or branches.");
        return (await users.GetUserAsync(accessor.HttpContext!.User))!;
    }
    private static string Email(string? email)
    {
        var value = email?.Trim() ?? "";
        if (value.Length > 256 || !new EmailAddressAttribute().IsValid(value)) throw new ArgumentException("Enter a valid email address.");
        return value;
    }
    private static string Name(string? value, string label)
    {
        var result = value?.Trim() ?? "";
        if (result.Length is < 1 or > 120) throw new ArgumentException(label + " name must contain 1–120 characters.");
        return result;
    }
    private static void Ensure(IdentityResult result)
    { if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description))); }
    private async Task<T> Atomic<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { var result = await work(); await tx.CommitAsync(ct); return result; }
        catch { await tx.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
    }
}
