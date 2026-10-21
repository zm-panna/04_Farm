using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

/// <summary>Explicit trusted CLI bootstrap for a new database. Never called by web requests/startup.</summary>
public static class BootstrapAdmin
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        string Require(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            && !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException("Missing environment variable: " + name);
        var email = Require("FARM_BOOTSTRAP_EMAIL").Trim();
        var password = Require("FARM_BOOTSTRAP_PASSWORD");
        var companyName = Require("FARM_BOOTSTRAP_COMPANY").Trim();
        var branchName = Require("FARM_BOOTSTRAP_BRANCH").Trim();
        if (password.Length < 12) throw new InvalidOperationException("Password must be at least 12 characters.");

        using var strategyScope = services.CreateScope();
        var strategyDb = strategyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await strategyDb.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            if (await db.Companies.AnyAsync() || await users.Users.AnyAsync() || await db.Farms.AnyAsync() || await db.Branches.AnyAsync())
                throw new InvalidOperationException("Bootstrap only allowed on an empty database. Use authorized admin provisioning for existing databases.");
            var company = new Company { Name = companyName };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            var farm = new Farm { CompanyId = company.Id, Name = companyName + " Farm" };
            db.Farms.Add(farm);
            await db.SaveChangesAsync();
            var branch = new Branch { CompanyId = company.Id, FarmId = farm.Id, Name = branchName };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, LockoutEnabled = true };
            DemoIdentitySeeder.Ensure(await users.CreateAsync(user, password));
            // Readiness/host administration is a separate trusted role, not implied by tenant wildcard grants.
            const string platformAdmin = "PlatformAdmin";
            if (!await roles.RoleExistsAsync(platformAdmin))
                DemoIdentitySeeder.Ensure(await roles.CreateAsync(new IdentityRole(platformAdmin)));
            DemoIdentitySeeder.Ensure(await users.AddToRoleAsync(user, platformAdmin));
            db.UserCompanyAccess.Add(new UserCompanyAccess { CompanyId = company.Id, UserId = user.Id });
            db.UserBranchAccess.Add(new UserBranchAccess { CompanyId = company.Id, BranchId = branch.Id, UserId = user.Id });
            db.UserPermissions.Add(new UserPermission { CompanyId = company.Id, UserId = user.Id, Permission = "*" });
            db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = user.Id,
                Action = "FirstAdministratorProvisioned", Reference = "trusted-cli-bootstrap" });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            Console.WriteLine($"Provisioned company {company.Id}, branch {branch.Id}, and initial PlatformAdmin {email}. Protect this account and change its password after first login.");
        });
        return 0;
    }
}
