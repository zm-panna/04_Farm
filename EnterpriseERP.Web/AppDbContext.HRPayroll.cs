using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed partial class AppDbContext
{
 public DbSet<HRPayrollStaff> HRPayrollStaff => Set<HRPayrollStaff>();
 public DbSet<HRPayrollCompensation> HRPayrollCompensations => Set<HRPayrollCompensation>();
 public DbSet<HRPayrollAttendance> HRPayrollAttendance => Set<HRPayrollAttendance>();
 public DbSet<HRPayrollLeave> HRPayrollLeaves => Set<HRPayrollLeave>();
 public DbSet<HRPayrollPolicy> HRPayrollPolicies => Set<HRPayrollPolicy>();
 public DbSet<HRPayrollRun> HRPayrollRuns => Set<HRPayrollRun>();
 public DbSet<HRPayrollLine> HRPayrollLines => Set<HRPayrollLine>();
 private static void ConfigureHRPayroll(ModelBuilder b)
 {
  b.Entity<HRPayrollStaff>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
  b.Entity<HRPayrollStaff>().HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
  b.Entity<HRPayrollStaff>().HasIndex(x => new { x.CompanyId, x.IdentityUserId }).IsUnique().HasFilter("[IdentityUserId] IS NOT NULL");
  b.Entity<HRPayrollStaff>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.IdentityUserId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<HRPayrollStaff>().Property(x => x.Code).HasMaxLength(40);
  b.Entity<HRPayrollStaff>().Property(x => x.Name).HasMaxLength(160);
  b.Entity<HRPayrollCompensation>().HasAlternateKey(x=>new{x.CompanyId,x.BranchId,x.StaffId,x.Id});
  b.Entity<HRPayrollCompensation>().HasIndex(x=>new{x.CompanyId,x.BranchId,x.StaffId,x.Version}).IsUnique();
  b.Entity<HRPayrollCompensation>().HasIndex(x=>new{x.CompanyId,x.BranchId,x.StaffId,x.EffectiveFrom});
  b.Entity<HRPayrollCompensation>().Property(x=>x.ReviewNote).HasMaxLength(1000);
  b.Entity<HRPayrollCompensation>().HasOne<HRPayrollStaff>().WithMany().HasForeignKey(x=>new{x.CompanyId,x.BranchId,x.StaffId}).HasPrincipalKey(x=>new{x.CompanyId,x.BranchId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  b.Entity<HRPayrollLine>().HasOne<HRPayrollCompensation>().WithMany().HasForeignKey(x=>new{x.CompanyId,x.BranchId,x.StaffId,x.CompensationId}).HasPrincipalKey(x=>new{x.CompanyId,x.BranchId,x.StaffId,x.Id}).OnDelete(DeleteBehavior.Restrict);
  b.Entity<HRPayrollAttendance>().HasIndex(x => new { x.CompanyId, x.BranchId, x.StaffId, x.Date }).IsUnique();
  b.Entity<HRPayrollAttendance>().HasOne<HRPayrollStaff>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.StaffId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
  b.Entity<HRPayrollLeave>().HasOne<HRPayrollStaff>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.StaffId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
  b.Entity<HRPayrollLeave>().HasIndex(x => new { x.CompanyId, x.BranchId, x.StaffId, x.From, x.Through });
  b.Entity<HRPayrollLeave>().Property(x => x.Reason).HasMaxLength(1000);
  b.Entity<HRPayrollLeave>().Property(x => x.ReviewNote).HasMaxLength(1000);
  b.Entity<HRPayrollPolicy>().HasOne<CompanyBusinessPolicy>().WithMany().HasForeignKey(x => new { x.CompanyId, x.CompanyBusinessPolicyId }).HasPrincipalKey(x => new { x.CompanyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
  b.Entity<HRPayrollPolicy>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
  b.Entity<HRPayrollPolicy>().Property(x => x.JurisdictionAndBasis).HasMaxLength(2000);
  b.Entity<HRPayrollPolicy>().Property(x => x.ReviewNote).HasMaxLength(1000);
  b.Entity<HRPayrollRun>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
  b.Entity<HRPayrollRun>().HasOne<HRPayrollPolicy>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.PolicyId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
  b.Entity<HRPayrollRun>().HasIndex(x => new { x.CompanyId, x.BranchId, x.PeriodStart }).IsUnique();
  b.Entity<HRPayrollRun>().HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
  b.Entity<HRPayrollRun>().Property(x => x.Reference).HasMaxLength(60);
  b.Entity<HRPayrollRun>().Property(x => x.ReviewNote).HasMaxLength(1000);
  b.Entity<HRPayrollRun>().HasMany(x => x.Lines).WithOne().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.RunId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
  b.Entity<HRPayrollLine>().HasIndex(x => new { x.CompanyId, x.BranchId, x.RunId, x.StaffId }).IsUnique();
  b.Entity<HRPayrollLine>().HasOne<HRPayrollStaff>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.StaffId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
  foreach (var type in new[] { typeof(HRPayrollStaff), typeof(HRPayrollAttendance), typeof(HRPayrollLeave), typeof(HRPayrollPolicy), typeof(HRPayrollRun) })
   b.Entity(type).Property<string>("Revision").HasMaxLength(32).IsConcurrencyToken();
 }
 private async Task ValidateHRPayrollWritesAsync(CancellationToken ct)
 {
  ChangeTracker.DetectChanges();
  if(ChangeTracker.Entries<HRPayrollCompensation>().Any(x=>x.State is EntityState.Modified or EntityState.Deleted))throw new InvalidOperationException("Reviewed compensation history is immutable; append a new effective-dated version.");
  foreach(var entry in ChangeTracker.Entries<HRPayrollPolicy>().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
   {
    if(entry.State==EntityState.Deleted||entry.OriginalValues.GetValue<bool>(nameof(HRPayrollPolicy.Reviewed))) throw new InvalidOperationException("Reviewed payroll policies are immutable; create a new policy.");
    if(!entry.Entity.Reviewed||string.IsNullOrWhiteSpace(entry.Entity.ReviewedBy)||entry.Entity.ReviewedBy==entry.Entity.CreatedBy||entry.Properties.Any(p=>p.IsModified&&p.Metadata.Name is not ("Reviewed" or "ReviewedBy" or "ReviewNote" or "Revision")))throw new InvalidOperationException("Payroll policy proposals may only receive an independent review; create a new proposal to correct their values.");
   }
  foreach(var entry in ChangeTracker.Entries<HRPayrollRun>().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
  {
   var status=entry.OriginalValues.GetValue<string>(nameof(HRPayrollRun.Status));
   if(status=="Posted") throw new InvalidOperationException("Posted payroll is immutable.");
   if(status=="Draft"&&(entry.State==EntityState.Deleted||entry.Entity.Status is not ("Draft" or "Approved")))throw new InvalidOperationException("Draft payroll must be independently approved before posting.");
   if(status=="Draft"&&entry.Entity.Status=="Approved"&&(string.IsNullOrWhiteSpace(entry.Entity.ApprovedBy)||entry.Entity.ApprovedBy==entry.Entity.CreatedBy||entry.Entity.ApprovedUtc==null||string.IsNullOrWhiteSpace(entry.Entity.ReviewNote)))throw new InvalidOperationException("Payroll needs a recorded independent review before approval.");
   if(status=="Approved" && (entry.State==EntityState.Deleted || entry.Entity.Status!="Posted" || entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not ("Status" or "Revision" or "VoucherId" or "PostedBy" or "PostedUtc" or "PostingDate"))))
    throw new InvalidOperationException("Approved payroll can only be posted without changing its reviewed values.");
  }
  foreach(var entry in ChangeTracker.Entries<HRPayrollLine>().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
  {
   var originalRunId=entry.State==EntityState.Added?entry.Entity.RunId:entry.OriginalValues.GetValue<long>(nameof(HRPayrollLine.RunId));
   var parent=ChangeTracker.Entries<HRPayrollRun>().FirstOrDefault(x => (entry.State==EntityState.Added&&x.Entity.Lines.Contains(entry.Entity)) || (originalRunId!=0&&x.Entity.Id==originalRunId));
   if(parent?.State==EntityState.Modified&&parent.Entity.Status=="Approved")throw new InvalidOperationException("Payroll amounts cannot be rewritten during approval.");
   var status=parent?.State==EntityState.Added ? "Draft" : await HRPayrollRuns.AsNoTracking().Where(x => x.Id==originalRunId).Select(x=>x.Status).SingleOrDefaultAsync(ct);
   if(status!="Draft") throw new InvalidOperationException("Approved payroll lines are immutable.");
  }
 }
}
