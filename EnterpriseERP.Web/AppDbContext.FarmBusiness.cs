using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;

public sealed partial class AppDbContext
{
    public DbSet<FatteningPlan> FatteningPlans => Set<FatteningPlan>();
    public DbSet<AnimalWeightRecord> AnimalWeightRecords => Set<AnimalWeightRecord>();
    public DbSet<FarmExpense> FarmExpenses => Set<FarmExpense>();
    public DbSet<BreedingCycle> BreedingCycles => Set<BreedingCycle>();
    public DbSet<BreedingCycleEvent> BreedingCycleEvents => Set<BreedingCycleEvent>();

    private static void ConfigureFarmBusiness(ModelBuilder b)
    {
        b.Entity<FatteningPlan>().HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
        b.Entity<FatteningPlan>().HasIndex(x => new { x.CompanyId, x.AnimalId }).IsUnique();
        b.Entity<AnimalWeightRecord>().HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
        b.Entity<AnimalWeightRecord>().HasIndex(x => new { x.CompanyId, x.AnimalId, x.WeighedOn }).IsUnique();
        b.Entity<FarmExpense>().HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
        b.Entity<FarmExpense>().HasIndex(x => new { x.CompanyId, x.BranchId, x.ExpenseDate });
        b.Entity<FarmExpense>().HasOne(x => x.Voucher).WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BreedingCycle>().HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
        b.Entity<BreedingCycle>().HasIndex(x => new { x.CompanyId, x.AnimalId }).IsUnique().HasFilter("[IsClosed] = 0");
        b.Entity<BreedingCycle>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<BreedingCycle>().HasMany(x => x.Events).WithOne().HasForeignKey(x => x.BreedingCycleId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BreedingCycleEvent>().HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
        b.Entity<BreedingCycleEvent>().HasIndex(x => new { x.CompanyId, x.BreedingCycleId, x.EventDate });
        b.Entity<BreedingCycle>().Property(x => x.Method).HasMaxLength(30);
        b.Entity<BreedingCycle>().Property(x => x.SireOrSemen).HasMaxLength(120);
        b.Entity<BreedingCycle>().Property(x => x.Status).HasMaxLength(30);
        b.Entity<BreedingCycleEvent>().Property(x => x.Outcome).HasMaxLength(30);
        b.Entity<BreedingCycleEvent>().Property(x => x.Notes).HasMaxLength(2000);
        b.Entity<FarmExpense>().Property(x => x.Category).HasMaxLength(40);
        b.Entity<FarmExpense>().Property(x => x.Description).HasMaxLength(1000);
        b.Entity<AnimalWeightRecord>().Property(x => x.Notes).HasMaxLength(1000);
        foreach (var type in new[] { typeof(FatteningPlan), typeof(AnimalWeightRecord), typeof(FarmExpense), typeof(BreedingCycle), typeof(BreedingCycleEvent) })
        {
            b.Entity(type).Property("Reference").HasMaxLength(90).IsRequired();
            b.Entity(type).Property("RecordedBy").HasMaxLength(450).IsRequired();
            if (type != typeof(BreedingCycleEvent))
                b.Entity(type).HasOne(typeof(Animal)).WithMany().HasForeignKey("AnimalId").OnDelete(DeleteBehavior.Restrict);
        }
    }
}
