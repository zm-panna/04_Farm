using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;
public sealed partial class AppDbContext
{
    public DbSet<InventoryCostBalance> InventoryCostBalances => Set<InventoryCostBalance>();
    public DbSet<FinanceDocument> FinanceDocuments => Set<FinanceDocument>();
    public DbSet<FinancePaymentAllocation> FinancePaymentAllocations => Set<FinancePaymentAllocation>();
    public DbSet<StockDocumentReturn> StockDocumentReturns => Set<StockDocumentReturn>();
    public DbSet<TradingParty> TradingParties => Set<TradingParty>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderReceipt> PurchaseOrderReceipts => Set<PurchaseOrderReceipt>();
    public DbSet<StockReorderPolicy> StockReorderPolicies => Set<StockReorderPolicy>();
    private void ConfigureFinance(ModelBuilder b)
    {
        string C(string column) => Database.IsSqlite() ? $"CAST([{column}] AS REAL)" : $"[{column}]";
        b.Entity<TradingParty>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
        b.Entity<PurchaseOrder>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
        b.Entity<PurchaseReceipt>().HasAlternateKey(x => new { x.CompanyId, x.BranchId, x.Id });
        b.Entity<TradingParty>().HasIndex(x => new { x.CompanyId, x.BranchId, x.Code }).IsUnique();
        b.Entity<TradingParty>().Property(x => x.Code).HasMaxLength(30);
        b.Entity<TradingParty>().Property(x => x.Name).HasMaxLength(160);
        b.Entity<TradingParty>().Property(x => x.Kind).HasMaxLength(20);
        b.Entity<TradingParty>().Property(x => x.Email).HasMaxLength(200);
        b.Entity<TradingParty>().Property(x => x.Phone).HasMaxLength(40);
        b.Entity<TradingParty>().Property(x => x.Address).HasMaxLength(500);
        b.Entity<TradingParty>().ToTable(t => t.HasCheckConstraint("CK_TradingParty_Kind", "[Kind] IN ('Customer','Supplier')"));
        b.Entity<FinanceDocument>().HasOne<TradingParty>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.PartyId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseOrder>().HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
        b.Entity<PurchaseOrder>().Property(x => x.Reference).HasMaxLength(80);
        b.Entity<PurchaseOrder>().Property(x => x.Status).HasMaxLength(20);
        b.Entity<PurchaseOrder>().Property(x => x.RowVersion).IsRowVersion();
        b.Entity<PurchaseOrder>().HasOne<TradingParty>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.SupplierId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseOrder>().HasOne<StockItem>().WithMany().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseOrder>().ToTable(t => { t.HasCheckConstraint("CK_PurchaseOrder_Amounts", $"{C("Quantity")} > 0 AND {C("UnitPrice")} > 0 AND {C("Amount")} > 0 AND {C("ReceivedQuantity")} >= 0 AND {C("ReceivedQuantity")} <= {C("Quantity")} AND {C("ReceivedAmount")} >= 0 AND {C("ReceivedAmount")} <= {C("Amount")}"); t.HasCheckConstraint("CK_PurchaseOrder_Status", "[Status] IN ('Draft','Approved','PartiallyReceived','Received','Cancelled')"); });
        b.Entity<PurchaseOrderReceipt>().HasIndex(x => x.PurchaseReceiptId).IsUnique();
        b.Entity<PurchaseOrderReceipt>().HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.PurchaseOrderId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseOrderReceipt>().HasOne<PurchaseReceipt>().WithMany().HasForeignKey(x => new { x.CompanyId, x.BranchId, x.PurchaseReceiptId }).HasPrincipalKey(x => new { x.CompanyId, x.BranchId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<PurchaseOrderReceipt>().ToTable(t => t.HasCheckConstraint("CK_PurchaseOrderReceipt_Amounts", $"{C("Quantity")} > 0 AND {C("Amount")} > 0"));
        b.Entity<StockReorderPolicy>().HasIndex(x => new { x.CompanyId, x.BranchId, x.StockItemId }).IsUnique();
        b.Entity<StockReorderPolicy>().HasOne<StockItem>().WithMany().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockReorderPolicy>().ToTable(t => t.HasCheckConstraint("CK_StockReorderPolicy_Levels", $"{C("ReorderPoint")} >= 0 AND {C("TargetQuantity")} >= {C("ReorderPoint")}"));
        b.Entity<InventoryCostBalance>().ToTable(t => t.HasCheckConstraint("CK_InventoryCostBalance_Value", $"{C("Value")} >= 0"));
        b.Entity<FinanceDocument>().ToTable(t => t.HasCheckConstraint("CK_FinanceDocument_Amounts", $"{C("Amount")} > 0 AND {C("CostAmount")} >= 0"));
        b.Entity<FinancePaymentAllocation>().ToTable(t => t.HasCheckConstraint("CK_FinancePaymentAllocation_Amount", $"{C("SignedAmount")} <> 0"));
        b.Entity<StockDocumentReturn>().ToTable(t => t.HasCheckConstraint("CK_StockDocumentReturn_Amounts", $"{C("Quantity")} > 0 AND {C("Amount")} > 0 AND {C("CostAmount")} >= 0"));
        b.Entity<InventoryCostBalance>().HasIndex(x => new { x.CompanyId, x.BranchId, x.StockItemId }).IsUnique();
        b.Entity<InventoryCostBalance>().Property(x => x.Value).IsConcurrencyToken();
        b.Entity<InventoryCostBalance>().HasOne<StockItem>().WithMany().HasForeignKey(x => x.StockItemId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinanceDocument>().HasIndex(x => new { x.CompanyId, x.Kind, x.SourceId }).IsUnique();
        b.Entity<FinanceDocument>().HasIndex(x => new { x.CompanyId, x.BranchId, x.Reference }).IsUnique();
        b.Entity<FinanceDocument>().Property(x => x.Kind).HasMaxLength(20);
        b.Entity<FinanceDocument>().Property(x => x.Reference).HasMaxLength(100);
        b.Entity<FinanceDocument>().HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancePaymentAllocation>().HasIndex(x => x.FinancePaymentId).IsUnique();
        b.Entity<FinancePaymentAllocation>().HasOne<FinanceDocument>().WithMany().HasForeignKey(x => x.FinanceDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<FinancePaymentAllocation>().HasOne<FinancePayment>().WithMany().HasForeignKey(x => x.FinancePaymentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockDocumentReturn>().HasIndex(x => new { x.CompanyId, x.Reference }).IsUnique();
        b.Entity<StockDocumentReturn>().Property(x => x.Reference).HasMaxLength(80);
        b.Entity<StockDocumentReturn>().HasOne<FinanceDocument>().WithMany().HasForeignKey(x => x.FinanceDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StockDocumentReturn>().HasOne<Voucher>().WithMany().HasForeignKey(x => x.VoucherId).OnDelete(DeleteBehavior.Restrict);
        // A bank movement is matched once, and an existing match cannot be overwritten by a concurrent writer.
        b.Entity<BankStatementLine>().Property(x => x.MatchedVoucherId).IsConcurrencyToken();
        b.Entity<BankStatementLine>().HasIndex(x => new { x.CompanyId, x.BranchId, x.MatchedVoucherId }).IsUnique().HasFilter("[MatchedVoucherId] IS NOT NULL");
        b.Entity<BankStatementLine>().HasOne<Voucher>().WithMany().HasForeignKey(x => x.MatchedVoucherId).OnDelete(DeleteBehavior.Restrict);
    }
}
