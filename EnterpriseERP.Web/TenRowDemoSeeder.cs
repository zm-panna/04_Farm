using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

// Only the explicit new minimum-row enterprise fixture calls this inside its transaction.
public static class TenRowDemoSeeder
{
    internal static async Task CompleteAsync(AppDbContext db, RoleManager<IdentityRole> roles,
        IReadOnlyList<Company> companies, IReadOnlyList<Branch> branches,
        IReadOnlyList<AppUser> users, CancellationToken ct)
    {
        if (companies.Count is not (10 or 20) || db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Requires the new minimum-row fixture transaction.");
        for (var i = 1; i <= companies.Count - 5; i++)
            DemoIdentitySeeder.Ensure(await roles.CreateAsync(new IdentityRole($"DemoExample{i}")));
        var allRoles = await db.Roles.OrderBy(x => x.Name).ToListAsync(ct);
        for (var i = 0; i < companies.Count; i++)
        {
            var company = companies[i]; var branch = branches[i]; var user = users[i];
            var rule = await db.AutomationRules.SingleAsync(x => x.CompanyId == company.Id, ct);
            var key = $"SYNTHETIC-CANCELLED-{i + 1}";
            db.AccountingPeriodCloses.Add(new AccountingPeriodClose {
                CompanyId = company.Id, BranchId = branch.Id, ClosedThrough = new DateOnly(1999, 12, 31),
                ClosedBy = user.Id, Reason = "SYNTHETIC DEMO: historical period only" });
            db.ScheduledJobs.Add(new ScheduledJob { CompanyId = company.Id, RuleId = rule.Id,
                ExecutionKey = key, DueUtc = DateTimeOffset.UtcNow, Status = "Cancelled" });
            db.AutomationExecutions.Add(new AutomationExecution { CompanyId = company.Id, RuleId = rule.Id,
                ExecutionKey = key, Result = "SYNTHETIC DEMO: cancelled without executing actions" });
            db.AutomationApprovals.Add(new AutomationApproval { CompanyId = company.Id, RuleId = rule.Id,
                RequesterId = user.Id, Status = "Rejected", PayloadHash = new string('0', 64) });
            // Descriptive claims and inert provider/token names grant no application permissions.
            db.UserClaims.Add(new IdentityUserClaim<string> { UserId = user.Id,
                ClaimType = "demo:description", ClaimValue = "Synthetic fixture account" });
            db.RoleClaims.Add(new IdentityRoleClaim<string> { RoleId = allRoles[i].Id,
                ClaimType = "demo:description", ClaimValue = "Synthetic fixture role" });
            db.UserLogins.Add(new IdentityUserLogin<string> { UserId = user.Id,
                LoginProvider = "SyntheticDemoNotConfigured", ProviderKey = $"demo-{i + 1}", ProviderDisplayName = "Inert demo example" });
            db.UserTokens.Add(new IdentityUserToken<string> { UserId = user.Id,
                LoginProvider = "SyntheticDemoNotConfigured", Name = "Sample", Value = "INERT-NOT-A-CREDENTIAL" });
        }
        await db.SaveChangesAsync(ct);
    }
}
