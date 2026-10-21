using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed partial class AppDbContext
{
    public DbSet<EcommerceCustomer> EcommerceCustomers => Set<EcommerceCustomer>();
    public DbSet<EcommerceProduct> EcommerceProducts => Set<EcommerceProduct>();
    public DbSet<EcommerceCart> EcommerceCarts => Set<EcommerceCart>();
    public DbSet<EcommerceCartLine> EcommerceCartLines => Set<EcommerceCartLine>();
    public DbSet<EcommerceOrder> EcommerceOrders => Set<EcommerceOrder>();
    public DbSet<EcommerceOrderLine> EcommerceOrderLines => Set<EcommerceOrderLine>();
    public DbSet<EcommerceStockReservation> EcommerceStockReservations => Set<EcommerceStockReservation>();
    public DbSet<EcommerceOrderEvent> EcommerceOrderEvents => Set<EcommerceOrderEvent>();
    public DbSet<EcommerceFulfillment> EcommerceFulfillments => Set<EcommerceFulfillment>();
    public DbSet<EcommerceReturn> EcommerceReturns => Set<EcommerceReturn>();
    private void ConfigureEcommerce(ModelBuilder b)
    {
        string C(string column) => Database.IsSqlite() ? $"CAST([{column}] AS REAL)" : $"[{column}]";
        var scoped = new[] { typeof(EcommerceCustomer), typeof(EcommerceProduct), typeof(EcommerceCart), typeof(EcommerceOrder), typeof(EcommerceOrderLine), typeof(EcommerceFulfillment), typeof(StockDocumentReturn), typeof(ProductSale) };
        foreach (var type in scoped) b.Entity(type).HasAlternateKey("CompanyId", "BranchId", "Id");
        b.Entity<EcommerceCustomer>().HasIndex(x => new { x.CompanyId, x.BranchId, x.UserId }).IsUnique();
        b.Entity<EcommerceCustomer>().HasIndex(x => new { x.CompanyId, x.BranchId, x.TradingPartyId }).IsUnique();
        b.Entity<EcommerceCustomer>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceCustomer>().HasOne<TradingParty>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.TradingPartyId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceProduct>().HasIndex(x => new { x.CompanyId, x.BranchId, x.StockItemId }).IsUnique();
        b.Entity<EcommerceProduct>().Property(x => x.RevisionToken).IsConcurrencyToken().HasMaxLength(32);
        b.Entity<EcommerceProduct>().Property(x => x.Description).HasMaxLength(2000);
        b.Entity<EcommerceProduct>().ToTable(t => t.HasCheckConstraint("CK_EcommerceProduct_Price", $"{C("NetUnitPrice")} > 0"));
        b.Entity<EcommerceCart>().HasIndex(x => new { x.CompanyId, x.BranchId, x.EcommerceCustomerId }).IsUnique();
        b.Entity<EcommerceCart>().Property(x => x.RevisionToken).IsConcurrencyToken().HasMaxLength(32);
        b.Entity<EcommerceCart>().HasOne<EcommerceCustomer>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceCustomerId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceCartLine>().HasIndex(x => new { x.EcommerceCartId, x.EcommerceProductId }).IsUnique();
        b.Entity<EcommerceCartLine>().HasOne<EcommerceCart>().WithMany(x => x.Lines).HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceCartId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceCartLine>().HasOne<EcommerceProduct>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceProductId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceOrder>().HasIndex(x => new { x.CompanyId, x.BranchId, x.CheckoutToken }).IsUnique();
        b.Entity<EcommerceOrder>().HasIndex(x => new { x.CompanyId, x.BranchId, x.Reference }).IsUnique();
        b.Entity<EcommerceOrder>().HasIndex(x => new { x.CompanyId, x.BranchId, x.EcommerceCustomerId, x.Id });
        b.Entity<EcommerceOrder>().Property(x => x.StateToken).IsConcurrencyToken().HasMaxLength(32);
        b.Entity<EcommerceOrder>().Property(x => x.CheckoutToken).HasMaxLength(32);
        b.Entity<EcommerceOrder>().HasOne<EcommerceCustomer>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceCustomerId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceOrder>().ToTable(t => { t.HasCheckConstraint("CK_EcommerceOrder_Status", "[Status] IN ('Submitted','Approved','Fulfilled','Cancelled')"); t.HasCheckConstraint("CK_EcommerceOrder_Payment", "[PaymentStatus] = 'PendingUnverified'"); t.HasCheckConstraint("CK_EcommerceOrder_Delivery", "[DeliveryMethod] = 'BranchCollection'"); });
        b.Entity<EcommerceOrderLine>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId, x.StockItemId, x.Id });
        b.Entity<EcommerceOrderLine>().HasOne<EcommerceOrder>().WithMany(x => x.Lines).HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceOrderLine>().HasOne<EcommerceProduct>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceProductId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceOrderEvent>().HasOne<EcommerceOrder>().WithMany(x => x.Events).HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceOrderEvent>().Property(x => x.Note).HasMaxLength(1000);
        b.Entity<EcommerceStockReservation>().HasIndex(x => x.EcommerceOrderLineId).IsUnique();
        b.Entity<EcommerceStockReservation>().Property(x => x.Status).IsConcurrencyToken();
        b.Entity<EcommerceStockReservation>().HasOne<EcommerceOrder>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceStockReservation>().HasOne<EcommerceOrderLine>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId, x.StockItemId, x.EcommerceOrderLineId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.EcommerceOrderId, x.StockItemId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceStockReservation>().ToTable(t => { t.HasCheckConstraint("CK_EcommerceReservation_Quantity", $"{C("Quantity")} > 0"); t.HasCheckConstraint("CK_EcommerceReservation_Status", "[Status] IN ('Reserved','Released','Consumed')"); });
        b.Entity<EcommerceFulfillment>().HasIndex(x => x.EcommerceOrderLineId).IsUnique();
        b.Entity<EcommerceFulfillment>().HasIndex(x => x.FinanceDocumentId).IsUnique();
        b.Entity<EcommerceFulfillment>().HasIndex(x => x.ProductSaleId).IsUnique();
        b.Entity<EcommerceFulfillment>().HasOne<EcommerceOrderLine>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceOrderLineId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceFulfillment>().HasOne<ProductSale>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.ProductSaleId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceReturn>().HasIndex(x => new { x.CompanyId, x.BranchId, x.RequestToken }).IsUnique();
        b.Entity<EcommerceReturn>().HasIndex(x => x.StockDocumentReturnId).IsUnique();
        b.Entity<EcommerceReturn>().HasOne<EcommerceOrderLine>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.EcommerceOrderLineId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceReturn>().HasOne<StockDocumentReturn>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.StockDocumentReturnId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<EcommerceReturn>().Property(x => x.Reason).HasMaxLength(1000);
        foreach (var type in new[] { typeof(EcommerceCartLine), typeof(EcommerceOrderLine), typeof(EcommerceOrder) })
            b.Entity(type).ToTable(t => t.HasCheckConstraint("CK_" + type.Name + "_Amounts", $"{C("NetAmount")} > 0 AND {C("TaxAmount")} >= 0 AND {C("GrossAmount")} > 0"));
        foreach (var type in new[] { typeof(EcommerceCartLine), typeof(EcommerceOrderLine), typeof(EcommerceReturn) })
            b.Entity(type).ToTable(t => t.HasCheckConstraint("CK_" + type.Name + "_Quantity", $"{C("Quantity")} > 0"));
        // Policy records are immutable company-scoped approvals; their alternate key is configured here too.
        b.Entity<CompanyBusinessPolicy>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        b.Entity<TaxPolicy>().HasAlternateKey(x => new { x.CompanyId, x.Id });
        foreach (var type in new[] { typeof(EcommerceProduct), typeof(EcommerceCartLine), typeof(EcommerceOrder) })
            b.Entity(type).HasOne(typeof(CompanyBusinessPolicy)).WithMany().HasForeignKey("CompanyId", "CompanyPolicyId").HasPrincipalKey("CompanyId", "Id").OnDelete(DeleteBehavior.Restrict);
        foreach (var type in new[] { typeof(EcommerceProduct), typeof(EcommerceCartLine), typeof(EcommerceOrderLine) })
            b.Entity(type).HasOne(typeof(TaxPolicy)).WithMany().HasForeignKey("CompanyId", "TaxPolicyId").HasPrincipalKey("CompanyId", "Id").OnDelete(DeleteBehavior.Restrict);
    }
    private void ValidateEcommerceWrites()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is EcommerceOrderLine or EcommerceOrderEvent or EcommerceFulfillment or EcommerceReturn)
                throw new InvalidOperationException("Ecommerce snapshots, fulfillment links and audit events are immutable.");
            if (entry.Entity is EcommerceOrder)
            {
                if (entry.State == EntityState.Deleted || entry.Properties.Any(x => x.IsModified && x.Metadata.Name is not ("Status" or "StateToken")))
                    throw new InvalidOperationException("An ecommerce order snapshot cannot be changed or deleted.");
            }
            if (entry.Entity is EcommerceStockReservation && (entry.State == EntityState.Deleted || entry.Properties.Any(x => x.IsModified && x.Metadata.Name != "Status")))
                throw new InvalidOperationException("Reservation identities and quantities are immutable.");
            if (entry.Entity is EcommerceCustomer && (entry.State == EntityState.Deleted || entry.Properties.Any(x => x.IsModified && x.Metadata.Name != "Active")))
                throw new InvalidOperationException("A customer identity link cannot be reassigned.");
        }
    }
}
