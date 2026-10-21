using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EnterpriseERP.Web;

public sealed record MessagingActor(int CompanyId, int BranchId, string UserId, string SecurityStamp);

/// <summary>Never relies on cached grants or connection-time authorization for protected operations.</summary>
public sealed class MessagingAccess(AppDbContext db, IOptions<IdentityOptions> identity)
{
    public async Task<MessagingActor> DemandAsync(ClaimsPrincipal? principal, string permission, CancellationToken ct = default)
    {
        if (principal?.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException("Login required.");
        string Single(string claim)
        {
            var values = principal.FindAll(claim).ToArray();
            if (values.Length != 1 || string.IsNullOrWhiteSpace(values[0].Value)) throw new UnauthorizedAccessException("Login and select an authorized company and branch.");
            return values[0].Value;
        }
        var userId = Single(identity.Value.ClaimsIdentity.UserIdClaimType);
        var stamp = Single(identity.Value.ClaimsIdentity.SecurityStampClaimType);
        if (!int.TryParse(Single("ActiveCompanyId"), out var company) || !int.TryParse(Single("ActiveBranchId"), out var branch) || company <= 0 || branch <= 0)
            throw new UnauthorizedAccessException("Select an authorized company and branch.");
        var current = await EligibleAsync(company, branch, userId, permission, ct);
        if (current is null || !string.Equals(current.SecurityStamp, stamp, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Session or messaging access is no longer valid.");
        return current;
    }

    public async Task<MessagingActor?> EligibleAsync(int company, int branch, string userId, string permission, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var user = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.SecurityStamp, x.LockoutEnabled, x.LockoutEnd }).SingleOrDefaultAsync(ct);
        if (user is null || string.IsNullOrWhiteSpace(user.SecurityStamp) || (user.LockoutEnabled && user.LockoutEnd > now)
            || !await db.UserCompanyAccess.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.UserId == userId, ct)
            || !await db.UserBranchAccess.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.BranchId == branch && x.UserId == userId, ct)
            || !await db.Branches.AsNoTracking().AnyAsync(x => x.Id == branch && x.CompanyId == company, ct)
            || !await db.UserPermissions.AsNoTracking().AnyAsync(x => x.CompanyId == company && x.UserId == userId && (x.Permission == permission || x.Permission == "*"), ct))
            return null;
        return new MessagingActor(company, branch, userId, user.SecurityStamp!);
    }

    // Neither the client nor a caller-supplied ID can choose a group. A changed security stamp gets a different group.
    public static string Group(MessagingActor actor) => "inbox:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{actor.CompanyId}\n{actor.BranchId}\n{actor.UserId}\n{actor.SecurityStamp}")));
}
