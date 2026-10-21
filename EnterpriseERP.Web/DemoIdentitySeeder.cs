using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

/// <summary>Identity portion of the explicit, atomic demo seed. Never runs on application startup.</summary>
public static class DemoIdentitySeeder
{
    public static readonly string[] RoleNames = ["FarmAdmin", "Manager", "Accounts", "Inventory", "Viewer"];

    // Kept for compatibility with the old CLI switch. Business data and Identity now share one transaction.
    public static Task<int> RunAsync(IServiceProvider services) => DemoBusinessSeeder.RunAsync(services);

    internal static async Task<IReadOnlyList<AppUser>> CreateAsync(
        AppDbContext db, UserManager<AppUser> users, RoleManager<IdentityRole> roles,
        IReadOnlyList<Company> companies, IReadOnlyList<Branch> branches, string password,
        CancellationToken ct)
    {
        var created = new List<AppUser>();
        for (var i = 0; i < companies.Count; i++)
        {
            var role = RoleNames[i % RoleNames.Length];
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole(role)));
            var email = $"farmdemo{i + 1}@example.invalid";
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, LockoutEnabled = true };
            Ensure(await users.CreateAsync(user, password));
            Ensure(await users.AddToRoleAsync(user, role));
            var company = companies[i];
            var branch = branches[i];
            db.UserCompanyAccess.Add(new UserCompanyAccess { UserId = user.Id, CompanyId = company.Id });
            db.UserBranchAccess.Add(new UserBranchAccess { UserId = user.Id, CompanyId = company.Id, BranchId = branch.Id });
            foreach (var permission in Permissions(i % RoleNames.Length))
                db.UserPermissions.Add(new UserPermission { UserId = user.Id, CompanyId = company.Id, Permission = permission });
            created.Add(user);
        }
        await db.SaveChangesAsync(ct);
        return created;
    }

    internal static async Task ValidateExistingAsync(AppDbContext db, UserManager<AppUser> users,
        IReadOnlyList<Company> companies, CancellationToken ct)
    {
        for (var i = 0; i < companies.Count; i++)
        {
            var company = companies.SingleOrDefault(x => x.Slug == $"demo-farm-{i + 1}")
                ?? throw new InvalidOperationException("Demo seed marker exists, but its company is missing. Restore the demo database; no data was changed.");
            var user = await users.FindByEmailAsync($"farmdemo{i + 1}@example.invalid")
                ?? throw new InvalidOperationException("Demo seed marker exists, but a demo account is missing. Restore the demo database; no data was changed.");
            if (!await db.UserCompanyAccess.AnyAsync(x => x.CompanyId == company.Id && x.UserId == user.Id, ct)
                || !await db.UserBranchAccess.AnyAsync(x => x.CompanyId == company.Id && x.UserId == user.Id, ct)
                || !await users.IsInRoleAsync(user, RoleNames[i % RoleNames.Length]))
                throw new InvalidOperationException("Demo account access was changed. Rerunning the seed does not restore privileges or reset passwords.");
        }
    }

    private static string[] Permissions(int index) => index switch
    {
        0 => ["*"], // Tenant-scoped demo administrator, never a PlatformAdmin.
        1 => ["Livestock.View", "Livestock.Create", "Livestock.Sell", "Dairy.View", "Dairy.Create",
              "AnimalHealth.View", "AnimalHealth.Create", "FeedManagement.View", "FeedManagement.Create",
              "Inventory.View", "Inventory.Move", "Purchase.View", "Sales.View", "Fattening.View", "Fattening.Create", "Breeding.View", "Breeding.Create", "Breeding.Update", "Expenses.View", "Reports.View", "CustomerCare.View", "CustomerCare.Create", "CustomerCare.Update"],
        2 => ["Accounts.View", "Accounts.Post", "Accounts.Reconcile", "Purchase.View", "Sales.View", "Expenses.View", "Expenses.Create", "Reports.View"],
        3 => ["Inventory.View", "Inventory.Create", "Inventory.Move", "Purchase.View", "Purchase.Post", "Sales.View", "Sales.Post"],
        _ => ["Livestock.View", "Dairy.View", "AnimalHealth.View", "FeedManagement.View", "Inventory.View", "Purchase.View", "Sales.View", "Fattening.View", "Breeding.View", "Expenses.View", "Reports.View", "CustomerCare.View"]
    };

    internal static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
