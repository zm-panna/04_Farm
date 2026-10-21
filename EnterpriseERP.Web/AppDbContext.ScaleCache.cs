using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

// SQL-backed invalidation generations are updated in the SAME SaveChanges transaction as data.
// Redis eviction is never the correctness boundary: rollback preserves the old generation and
// a committed mutation makes all old generation keys unreachable, including after an outage.
public sealed class ScaleCacheEpoch
{
 public int CompanyId { get; set; }
 public int ScopeBranchId { get; set; } // zero means company-wide data; never a client input
 public string Token { get; set; } = "";
}
public sealed partial class AppDbContext
{
 public DbSet<ScaleCacheEpoch> ScaleCacheEpochs => Set<ScaleCacheEpoch>();
 private void ConfigureScaleCache(ModelBuilder b)
 {
  b.Entity<ScaleCacheEpoch>().HasKey(x=>new{x.CompanyId,x.ScopeBranchId});
  b.Entity<ScaleCacheEpoch>().Property(x=>x.Token).HasMaxLength(32).IsRequired();
  b.Entity<ScaleCacheEpoch>().HasOne<Company>().WithMany().HasForeignKey(x=>x.CompanyId).OnDelete(DeleteBehavior.Restrict);
 }
 private void ConfigureScaleIndexes(ModelBuilder b)
 {
  // Default grid order is stable (scope, Id). Avoid duplicate indexes when an alternate key already covers it.
  foreach(var entity in b.Model.GetEntityTypes().Where(e=>e.ClrType.Namespace==typeof(Company).Namespace && e.FindProperty("CompanyId") is not null && e.FindProperty("Id") is not null).ToArray())
  {
   string[] fields=entity.FindProperty("BranchId") is not null?["CompanyId","BranchId","Id"]:["CompanyId","Id"];
   if(!entity.GetKeys().Any(k=>k.Properties.Select(p=>p.Name).SequenceEqual(fields)) && !entity.GetIndexes().Any(i=>i.Properties.Select(p=>p.Name).SequenceEqual(fields)))b.Entity(entity.ClrType).HasIndex(fields);
  }
 }
 private async Task StampCacheEpochs(CancellationToken ct)
 {
  ChangeTracker.DetectChanges();
  var entries=ChangeTracker.Entries().Where(e=>e.Entity is not ScaleCacheEpoch&&e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
  var scopes=new HashSet<(int Company,int Branch)>();
  var auditCompanies=new HashSet<int>();
  foreach(var e in entries)
  {
   if(e.Metadata.FindProperty("CompanyId") is null)continue;
   var company=(int)(e.State==EntityState.Deleted?e.Property("CompanyId").OriginalValue!:e.Property("CompanyId").CurrentValue!);
   if(company<=0)continue;
   if(e.Entity is AuditEntry){auditCompanies.Add(company);continue;}
   var branch=e.Metadata.FindProperty("BranchId") is null?0:(int)(e.State==EntityState.Deleted?e.Property("BranchId").OriginalValue!:e.Property("BranchId").CurrentValue!);
   scopes.Add((company,Math.Max(0,branch)));
   // Reparenting is not a supported UI operation, but must invalidate the old scope too.
   if(e.State==EntityState.Modified)
   {
    var oldCompany=(int)e.Property("CompanyId").OriginalValue!;
    var oldBranch=e.Metadata.FindProperty("BranchId") is null?0:(int)e.Property("BranchId").OriginalValue!;
    if(oldCompany>0)scopes.Add((oldCompany,Math.Max(0,oldBranch)));
   }
  }
  foreach(var c in auditCompanies)if(!scopes.Any(x=>x.Company==c))scopes.Add((c,0));
  // Stable locking order reduces cross-scope deadlocks; no Redis call happens in this transaction.
  foreach(var scope in scopes.OrderBy(x=>x.Company).ThenBy(x=>x.Branch))
  {
   var row=ScaleCacheEpochs.Local.FirstOrDefault(x=>x.CompanyId==scope.Company&&x.ScopeBranchId==scope.Branch)
      ??await ScaleCacheEpochs.SingleOrDefaultAsync(x=>x.CompanyId==scope.Company&&x.ScopeBranchId==scope.Branch,ct);
   if(row is null){row=new(){CompanyId=scope.Company,ScopeBranchId=scope.Branch};ScaleCacheEpochs.Add(row);}
   row.Token=Guid.NewGuid().ToString("N");
  }
 }
}
