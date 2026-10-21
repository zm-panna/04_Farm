using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed partial class AppDbContext
{
 public DbSet<OpeningBalance> OpeningBalances=>Set<OpeningBalance>();
 public DbSet<OpeningStockValue> OpeningStockValues=>Set<OpeningStockValue>();
 public DbSet<OpeningAnimalValue> OpeningAnimalValues=>Set<OpeningAnimalValue>();
 private void CheckOpeningImmutability()
 {
  ChangeTracker.DetectChanges();
  if(ChangeTracker.Entries().Any(x=>(x.Entity is OpeningBalance or OpeningStockValue or OpeningAnimalValue)&&(x.State is EntityState.Modified or EntityState.Deleted)))
   throw new InvalidOperationException("Posted opening balances and their itemized source values are immutable.");
  var openings=ChangeTracker.Entries<OpeningBalance>().Where(x=>x.State==EntityState.Added).Select(x=>x.Entity).ToArray();
  if(ChangeTracker.Entries<OpeningStockValue>().Any(x=>x.State==EntityState.Added&&!openings.Any(o=>o.Stocks.Contains(x.Entity)))||ChangeTracker.Entries<OpeningAnimalValue>().Any(x=>x.State==EntityState.Added&&!openings.Any(o=>o.Animals.Contains(x.Entity))))
   throw new InvalidOperationException("Opening source rows must be posted with their new opening balance.");
 }
 private static void ConfigureOpening(ModelBuilder b)
 {
  b.Entity<OpeningBalance>().HasIndex(x=>new{x.CompanyId,x.BranchId}).IsUnique();
  b.Entity<OpeningBalance>().HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();
  b.Entity<OpeningBalance>().HasAlternateKey(x=>new{x.CompanyId,x.BranchId,x.Id});
  b.Entity<OpeningBalance>().Property(x=>x.Reference).HasMaxLength(60);
  b.Entity<OpeningBalance>().Property(x=>x.ReviewNote).HasMaxLength(1000);
  b.Entity<OpeningBalance>().HasOne(x=>x.Voucher).WithMany().HasForeignKey(x=>x.VoucherId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<OpeningBalance>().HasMany(x=>x.Stocks).WithOne().HasForeignKey(x=>new{x.CompanyId,x.BranchId,x.OpeningBalanceId}).HasPrincipalKey(x=>new{x.CompanyId,x.BranchId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  b.Entity<OpeningBalance>().HasMany(x=>x.Animals).WithOne().HasForeignKey(x=>new{x.CompanyId,x.BranchId,x.OpeningBalanceId}).HasPrincipalKey(x=>new{x.CompanyId,x.BranchId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  b.Entity<OpeningStockValue>().HasIndex(x=>new{x.CompanyId,x.BranchId,x.StockItemId}).IsUnique();
  b.Entity<OpeningAnimalValue>().HasIndex(x=>new{x.CompanyId,x.BranchId,x.AnimalId}).IsUnique();
 }
}
