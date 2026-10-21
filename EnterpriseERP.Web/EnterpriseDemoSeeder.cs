using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

/// <summary>Optional fictional enterprise examples. Runs only inside the guarded pristine demo transaction.</summary>
public static partial class EnterpriseDemoSeeder
{
    public const string CompletionAction = "EnterpriseDemoCompletedV1";
    public const string SeedVersion = "FARM-ENTERPRISE-DEMO-V1";
    internal static async Task SeedAsync(AppDbContext db, UserManager<AppUser> users, IReadOnlyList<Company> companies,
        IReadOnlyList<Branch> branches, IReadOnlyList<AppUser> makers, string password, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var examples = new[] { ("BD","","BDT","Asia/Dhaka"), ("US","NY","USD","America/New_York"), ("AE","DEMO","AED","Asia/Dubai"), ("SA","DEMO","SAR","Asia/Riyadh"), ("US","CA","USD","America/Los_Angeles") };
        for(var i=0;i<companies.Count;i++)
        {
            var company=companies[i]; var branch=branches[i]; var maker=makers[i]; var n=i+1; var example=examples[i % examples.Length];
            var email=$"farmreviewer{n}@example.invalid";
            var reviewer=new AppUser{UserName=email,Email=email,EmailConfirmed=true,LockoutEnabled=true};
            DemoIdentitySeeder.Ensure(await users.CreateAsync(reviewer,password));
            db.UserCompanyAccess.Add(new UserCompanyAccess{CompanyId=company.Id,UserId=reviewer.Id});
            db.UserBranchAccess.Add(new UserBranchAccess{CompanyId=company.Id,BranchId=branch.Id,UserId=reviewer.Id});
            db.UserPermissions.Add(new UserPermission{CompanyId=company.Id,UserId=reviewer.Id,Permission="*"});
            var existing=(await db.UserPermissions.Where(x=>x.CompanyId==company.Id&&x.UserId==maker.Id).Select(x=>x.Permission).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
            foreach(var grant in PermissionService.GrantablePermissions.Where(x=>x.StartsWith("HR.")||x.StartsWith("Payroll.")||x.StartsWith("Employee.")||x.StartsWith("CRM.")||x.StartsWith("FixedAssets.")||x.StartsWith("Ecommerce.")||x.StartsWith("BusinessPolicy.")||x.StartsWith("TaxPolicy.")||x=="AI.Read"))
                if(!existing.Contains(grant)) db.UserPermissions.Add(new UserPermission{CompanyId=company.Id,UserId=maker.Id,Permission=grant});
            var policy=new CompanyBusinessPolicy{CompanyId=company.Id,Version=1,CountryCode=example.Item1,RegionCode=example.Item2,CurrencyCode=example.Item3,CurrencyMinorUnits=2,TimeZoneId=example.Item4,
                EffectiveFrom=new DateOnly(2000,1,1),Status="Approved",CreatedBy=maker.Id,ApprovedBy=reviewer.Id,ApprovedUtc=now,ExistingLedgerCurrencyConfirmed=true,
                ReviewBasis="DEMO FICTIONAL policy and simulated maker/checker review. Not a real legal, tax, payroll or accounting approval; never copy into a customer company."};
            db.CompanyCurrencyLocks.Add(new CompanyCurrencyLock{CompanyId=company.Id,CurrencyCode=policy.CurrencyCode,CurrencyMinorUnits=2,ConfirmedBy=reviewer.Id,ConfirmedUtc=now});
            db.CompanyBusinessPolicies.Add(policy);await db.SaveChangesAsync(ct);
            var tax=new TaxPolicy{CompanyId=company.Id,CompanyBusinessPolicyId=policy.Id,Code=$"DEMO-TAX-{n}",Version=1,Name="DEMO FICTIONAL exclusive sales tax",CurrencyCode=policy.CurrencyCode,CountryCode=policy.CountryCode,RegionCode=policy.RegionCode,
                TaxTreatment="Standard",RatePercent=5m,TaxLiabilityAccount="2200",EffectiveFrom=new DateOnly(2000,1,1),Status="Approved",CreatedBy=maker.Id,ApprovedBy=reviewer.Id,ApprovedUtc=now,
                Reason="DEMO FICTIONAL 5% rate chosen only to exercise arithmetic. It is not the legal rate in this country/state; independent real policy review is required."};
            db.TaxPolicies.Add(tax);await db.SaveChangesAsync(ct);
            await SeedHRPayroll(db,company,branch,maker,reviewer,policy,tax,n,now,today,ct);
            await SeedCRM(db,company,branch,maker,reviewer,policy,tax,n,now,today,ct);
            await SeedFixedAssets(db,company,branch,maker,reviewer,policy,tax,n,now,today,ct);
            await SeedEcommerce(db,company,branch,maker,reviewer,policy,tax,n,now,today,ct);
            db.Audits.Add(new AuditEntry{CompanyId=company.Id,ActorId="DEMO-CLI",Action=CompletionAction,Reference=SeedVersion,CreatedUtc=now});
            await db.SaveChangesAsync(ct);
        }
    }
    internal static async Task ValidateExistingAsync(AppDbContext db,UserManager<AppUser> users,IReadOnlyList<Company> companies,CancellationToken ct)
    {
        if(await db.Audits.CountAsync(x=>x.Action==CompletionAction&&x.Reference==SeedVersion,ct)!=companies.Count) throw new InvalidOperationException("Enterprise demo markers are incomplete. Recreate the isolated demo; nothing was changed.");
        for(var i=1;i<=companies.Count;i++)
        {
            var company=companies.Single(x=>x.Slug==$"demo-farm-{i}");
            var reviewer=await users.FindByEmailAsync($"farmreviewer{i}@example.invalid")??throw new InvalidOperationException("Synthetic reviewer missing. Seed does not repair accounts or privileges.");
            if(!await db.UserCompanyAccess.AnyAsync(x=>x.CompanyId==company.Id&&x.UserId==reviewer.Id,ct)) throw new InvalidOperationException("Synthetic reviewer access changed. Seed does not restore privileges.");
        }
    }
}
