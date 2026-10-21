using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed class AppUser : IdentityUser { }
public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
 public DbSet<InAppNotification> Notifications => Set<InAppNotification>();
 public DbSet<Company> Companies => Set<Company>(); public DbSet<Farm> Farms => Set<Farm>(); public DbSet<Branch> Branches => Set<Branch>();
 public DbSet<Animal> Animals => Set<Animal>(); public DbSet<StockItem> StockItems => Set<StockItem>(); public DbSet<StockMovement> StockMovements => Set<StockMovement>();
 public DbSet<MilkYield> MilkYields => Set<MilkYield>(); public DbSet<Voucher> Vouchers => Set<Voucher>(); public DbSet<VoucherLine> VoucherLines => Set<VoucherLine>();
 public DbSet<AnimalSale> AnimalSales => Set<AnimalSale>(); public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>(); public DbSet<AuditEntry> Audits => Set<AuditEntry>();
 public DbSet<UserCompanyAccess> UserCompanyAccess => Set<UserCompanyAccess>(); public DbSet<UserBranchAccess> UserBranchAccess => Set<UserBranchAccess>();
 public DbSet<PurchaseReceipt> PurchaseReceipts=>Set<PurchaseReceipt>();
 public DbSet<ProductSale> ProductSales=>Set<ProductSale>();
 public DbSet<UserPermission> UserPermissions=>Set<UserPermission>();
 public DbSet<SubscriptionInvoice> SubscriptionInvoices=>Set<SubscriptionInvoice>();
 public DbSet<SubscriptionPayment> SubscriptionPayments=>Set<SubscriptionPayment>();
 public DbSet<AutomationExecution> AutomationExecutions=>Set<AutomationExecution>();
 public DbSet<FinancePayment> FinancePayments=>Set<FinancePayment>();
 public DbSet<BankStatementLine> BankStatementLines=>Set<BankStatementLine>();
 public DbSet<AutomationApproval> AutomationApprovals=>Set<AutomationApproval>();
 public DbSet<CompanySubscriptionPolicy> CompanySubscriptionPolicies=>Set<CompanySubscriptionPolicy>();
 public DbSet<ScheduledJob> ScheduledJobs=>Set<ScheduledJob>();
 public DbSet<SupportTicket> SupportTickets=>Set<SupportTicket>();
 public DbSet<SupportTicketActivity> SupportTicketActivities=>Set<SupportTicketActivity>();
 public DbSet<AnimalHealthRecord> AnimalHealthRecords=>Set<AnimalHealthRecord>();
 public DbSet<AnimalFeedRecord> AnimalFeedRecords=>Set<AnimalFeedRecord>();
 protected override void OnModelCreating(ModelBuilder b)
 {
  base.OnModelCreating(b);
  b.Entity<AccountingPeriodClose>().HasIndex(x => new { x.CompanyId, x.BranchId }).IsUnique();
  b.Entity<AccountingPeriodClose>().Property(x => x.ClosedThrough).IsConcurrencyToken();
  b.Entity<AccountingPeriodClose>().Property(x => x.Reason).HasMaxLength(500);
  b.Entity<InAppNotification>().HasIndex(x => new { x.CompanyId, x.BranchId, x.ExecutionKey }).IsUnique();
  b.Entity<InAppNotification>().Property(x => x.Message).HasMaxLength(1000);
  b.Entity<AnimalHealthRecord>().HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();
  b.Entity<AnimalFeedRecord>().HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();
  b.Entity<AnimalFeedRecord>().HasIndex(x=>new{x.CompanyId,x.AnimalId,x.FeedDate});
  b.Entity<SupportTicket>().HasIndex(x=>new{x.CompanyId,x.TicketNumber}).IsUnique();
  b.Entity<SupportTicket>().Property(x=>x.RowVersion).IsRowVersion();
  b.Entity<SupportTicketActivity>().HasIndex(x=>new{x.CompanyId,x.TicketId,x.CreatedUtc});
  b.Entity<FinancePayment>().HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();
  b.Entity<BankStatementLine>().HasIndex(x=>new{x.CompanyId,x.BranchId,x.ExternalReference}).IsUnique();
  b.Entity<CompanySubscriptionPolicy>().HasIndex(x=>x.CompanyId).IsUnique();
  b.Entity<ScheduledJob>().HasIndex(x=>new{x.CompanyId,x.ExecutionKey}).IsUnique();
  b.Entity<PurchaseReceipt>().HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();
  b.Entity<ProductSale>().HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();
  b.Entity<SubscriptionInvoice>().HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();
  b.Entity<SubscriptionPayment>().HasIndex(x=>new{x.CompanyId,x.Reference}).IsUnique();
  b.Entity<AutomationExecution>().HasIndex(x=>new{x.CompanyId,x.ExecutionKey}).IsUnique();
  b.Entity<UserPermission>().HasIndex(x=>new{x.UserId,x.CompanyId,x.Permission}).IsUnique();
  b.Entity<StockMovement>().HasIndex(x=>new{x.CompanyId,x.Reference,x.StockItemId}).IsUnique();
  b.Entity<Animal>().HasIndex(x=>new {x.CompanyId,x.Tag}).IsUnique();
  b.Entity<Animal>().Property(x=>x.RowVersion).IsRowVersion();
  b.Entity<StockItem>().HasIndex(x=>new {x.CompanyId,x.BranchId,x.SKU}).IsUnique();
  b.Entity<StockItem>().Property(x=>x.RowVersion).IsRowVersion();

  b.Entity<MilkYield>().HasIndex(x=>new {x.CompanyId,x.AnimalId,x.Date,x.Session}).IsUnique();
  b.Entity<AnimalSale>().HasIndex(x=>new {x.CompanyId,x.AnimalId}).IsUnique();
  b.Entity<AnimalSale>().HasIndex(x=>new {x.CompanyId,x.Reference}).IsUnique();
  b.Entity<Voucher>().HasIndex(x=>new {x.CompanyId,x.Reference}).IsUnique();
  b.Entity<UserCompanyAccess>().HasIndex(x=>new{x.UserId,x.CompanyId}).IsUnique();
  b.Entity<UserBranchAccess>().HasIndex(x=>new{x.UserId,x.CompanyId,x.BranchId}).IsUnique();
  ConfigureFarmBusiness(b);
  ConfigureFinance(b);
  ConfigureOpening(b);
  ConfigureStockTransfers(b);
  ConfigureBusinessPolicy(b);
  ConfigureHRPayroll(b);
  ConfigureCRM(b);
  ConfigureFixedAssets(b);
  ConfigureEcommerce(b);
  ConfigureScaleCache(b);
  ConfigureMessaging(b);
  ConfigureScanning(b);
  ConfigureIntegrity(b);
  ConfigureScaleIndexes(b);
  foreach(var e in b.Model.GetEntityTypes()) foreach(var p in e.GetProperties()) if(p.ClrType==typeof(decimal)||p.ClrType==typeof(decimal?)) {p.SetPrecision(18);p.SetScale(3);}
 }
}
