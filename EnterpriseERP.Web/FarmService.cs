using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
using System.Data;
namespace EnterpriseERP.Web;
public sealed class FarmService(AppDbContext db, TenantContext tenant, PermissionService permissions, TimeProvider? clock = null)
{
    public async Task<List<Animal>> Animals(CancellationToken ct)
    {
        await permissions.Demand("Livestock.View", ct); var t = await tenant.GetAsync(ct);
        return await db.Animals.AsNoTracking().Where(x => x.CompanyId == t.company && x.BranchId == t.branch).OrderBy(x => x.Tag).ToListAsync(ct);
    }
    public async Task<Animal> AddAnimal(string tag, string breed, decimal cost, CancellationToken ct, decimal? weight = null)
    {
        await permissions.Demand("Livestock.Create", ct); var t = await tenant.GetAsync(ct);
        tag = BusinessInput.Text(tag, "Tag", 70).ToUpperInvariant(); breed = BusinessInput.Text(breed, "Breed", 100);
        if (tag.Length < 2 || breed.Length < 2) throw new ArgumentException("Tag and breed must contain at least 2 characters.");
        BusinessInput.Amount(cost, "Purchase cost", false);
        if (weight.HasValue) BusinessInput.Decimal(weight.Value, "Weight", true, maximum: 100000);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, cost, ct, positive: false, atUtc: businessDay.UtcInstant);
        if (await db.Animals.AnyAsync(x => x.CompanyId == t.company && x.Tag == tag, ct)) throw new InvalidOperationException("Animal tag already exists.");
        var animal = new Animal { CompanyId = t.company, BranchId = t.branch, Tag = tag, Breed = breed, PurchaseCost = cost, CurrentWeightKg = weight };
        db.Animals.Add(animal); await db.SaveChangesAsync(ct);
        if (cost > 0)
        {
            var voucher = BusinessInput.Voucher(t.company, t.branch, "ANIMAL-ACQ-" + animal.Id, ("1400", cost, 0), ("2100", 0, cost));
            voucher.Date = businessDay.PostingDate;
            db.Vouchers.Add(voucher); await db.SaveChangesAsync(ct);
            db.FinanceDocuments.Add(new FinanceDocument { CompanyId = t.company, BranchId = t.branch, Kind = "AnimalPurchase", SourceId = animal.Id, Reference = voucher.Reference, Amount = cost, CostAmount = cost, VoucherId = voucher.Id });
        }
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Animal.Acquire", Reference = tag });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return animal;
    }
    public async Task<StockItem> AddStock(string sku, string name, decimal unitCost, CancellationToken ct, string? unitOfMeasure = null)
    {
        await permissions.Demand("Inventory.Create", ct); var t = await tenant.GetAsync(ct);
        sku = BusinessInput.Text(sku, "SKU", 60).ToUpperInvariant(); name = BusinessInput.Text(name, "Name", 160);
        if (sku.Length < 2 || name.Length < 2) throw new ArgumentException("Enter a SKU of 2–60 characters and product name of 2–160 characters.");
        BusinessInput.Amount(unitCost, "Unit cost", false);
        var unit = StockUnitPolicy.Normalize(unitOfMeasure);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var item = new StockItem { CompanyId = t.company, BranchId = t.branch, SKU = sku, Name = name, UnitCost = unitCost, UnitOfMeasure = unit };
        db.StockItems.Add(item);
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Product.Create", Reference = sku });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Product.UnitAssigned", Reference = $"{sku}; unit {unit}" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return item;
    }
    public async Task<StockItem> MoveStock(int itemId, decimal delta, string reference, CancellationToken ct)
    {
        await permissions.Demand("Inventory.Move", ct); var t = await tenant.GetAsync(ct);
        BusinessInput.Decimal(delta, "Movement", allowNegative: true); if (delta == 0) throw new ArgumentException("Movement must not be zero.");
        reference = BusinessInput.Reference(reference);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await db.ValidatePendingScan("AdjustStock", ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        if (await db.StockMovements.AnyAsync(x => x.CompanyId == t.company && x.Reference == "ADJ-" + reference, ct)) throw new InvalidOperationException("Duplicate adjustment reference.");
        var item = await db.StockItems.SingleAsync(x => x.Id == itemId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        var value = await InventoryValuation.Load(db, item, ct);
        decimal amount;
        if (delta > 0)
        {
            var exactUnitCost = item.QuantityOnHand == 0 ? item.UnitCost : value.Value / item.QuantityOnHand;
            amount = exactUnitCost == 0 ? 0 : BusinessInput.Total(delta, exactUnitCost, "Adjustment value");
            InventoryValuation.Add(item, value, delta, amount);
        }
        else amount = InventoryValuation.Remove(item, value, -delta);
        db.StockMovements.Add(new StockMovement { CompanyId = t.company, BranchId = t.branch, StockItemId = item.Id, QuantityDelta = delta, Reference = "ADJ-" + reference });
        if (amount > 0)
        {
            var voucher = delta > 0
            ? BusinessInput.Voucher(t.company, t.branch, "ADJ-" + reference, ("1300", amount, 0), ("3900", 0, amount))
            : BusinessInput.Voucher(t.company, t.branch, "ADJ-" + reference, ("6100", amount, 0), ("1300", 0, amount));
            voucher.Date = businessDay.PostingDate;
            db.Vouchers.Add(voucher);
        }
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Inventory.Adjust", Reference = reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return item;
    }
    public async Task<MilkYield> RecordMilk(int animalId, DateOnly date, string session, decimal litres, CancellationToken ct)
    {
        await permissions.Demand("Dairy.Create", ct); var t = await tenant.GetAsync(ct);
        BusinessInput.Decimal(litres, "Litres", true, maximum: 100000);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        if (session is not ("Morning" or "Evening") || date > businessDay.Date || date < new DateOnly(2000, 1, 1)) throw new ArgumentException("Invalid milk session or date.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Animals.AnyAsync(a => a.Id == animalId && a.CompanyId == t.company && a.BranchId == t.branch && !a.Sold, ct)) throw new InvalidOperationException("Animal unavailable.");
        if (await db.MilkYields.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.AnimalId == animalId && x.Date == date && x.Session == session, ct)) throw new InvalidOperationException("A milk entry already exists for this animal, day and session.");
        var milk = new MilkYield { CompanyId = t.company, BranchId = t.branch, AnimalId = animalId, Date = date, Session = session, Litres = litres };
        db.MilkYields.Add(milk);
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Dairy.Milk.Record", Reference = $"{animalId}/{date:yyyy-MM-dd}/{session}" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return milk;
    }
    public async Task<Voucher> PostVoucher(string reference, List<VoucherLine> lines, CancellationToken ct)
    {
        await permissions.Demand("Accounts.Post", ct); var t = await tenant.GetAsync(ct);
        reference = BusinessInput.Reference(reference); BusinessInput.ValidateLines(lines);
        if (lines.Any(x => x.AccountCode is "1100" or "1300" or "1400" or "2100")) throw new ArgumentException("Use linked operational documents for receivables, inventory, livestock assets and payables.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        foreach (var line in lines.Where(x => x.AccountCode == "1000"))
        {
            // Actual cash must be representable; noncash book corrections retain the internal three-decimal contract.
            await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, line.Debit, ct, positive: false, atUtc: businessDay.UtcInstant);
            await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, line.Credit, ct, positive: false, atUtc: businessDay.UtcInstant);
        }
        var voucher = new Voucher { CompanyId = t.company, BranchId = t.branch, Reference = "JRN-" + reference, Date = businessDay.PostingDate, Posted = true, Lines = lines.Select(x => new VoucherLine { AccountCode = x.AccountCode, Debit = x.Debit, Credit = x.Credit }).ToList() };
        db.Vouchers.Add(voucher);
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Finance.Journal.Post", Reference = voucher.Reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return voucher;
    }
    public async Task<AnimalSale> SellAnimal(int animalId, decimal amount, string reference, CancellationToken ct, int? partyId = null)
    {
        await permissions.Demand("Livestock.Sell", ct); var t = await tenant.GetAsync(ct);
        BusinessInput.Amount(amount); reference = BusinessInput.Reference(reference);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var businessDay = await CompanyBusinessDate.Current(db, t.company, ct, clock);
        await CompanyBusinessPolicyService.RequireConfiguredMoney(db, t.company, amount, ct, atUtc: businessDay.UtcInstant);
        if (partyId.HasValue && !await db.TradingParties.AnyAsync(x => x.Id == partyId && x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == "Customer" && x.Active, ct)) throw new ArgumentException("Select an active customer in this branch.");
        if (await db.AnimalSales.AnyAsync(x => x.CompanyId == t.company && x.Reference == reference, ct)) throw new InvalidOperationException("Duplicate animal sale reference.");
        var animal = await db.Animals.SingleAsync(x => x.Id == animalId && x.CompanyId == t.company && x.BranchId == t.branch, ct);
        if (animal.Sold) throw new InvalidOperationException("Already sold.");
        // Historical paid-cost animals require an acquisition journal/import first; otherwise a sale would invent an asset credit.
        if (animal.PurchaseCost > 0 && !await db.FinanceDocuments.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.Kind == "AnimalPurchase" && x.SourceId == animal.Id, ct)
            && !await db.OpeningAnimalValues.AnyAsync(x => x.CompanyId == t.company && x.BranchId == t.branch && x.AnimalId == animal.Id && x.Value == animal.PurchaseCost, ct))
            throw new InvalidOperationException("Animal acquisition balance must be reviewed and imported before sale.");
        animal.Sold = true;
        var voucher = BusinessInput.Voucher(t.company, t.branch, "ANIMAL-SAL-" + reference, ("1100", amount, 0), ("4200", 0, amount), ("5200", animal.PurchaseCost, 0), ("1400", 0, animal.PurchaseCost));
        voucher.Date = businessDay.PostingDate;
        var sale = new AnimalSale { CompanyId = t.company, BranchId = t.branch, AnimalId = animal.Id, Amount = amount, Reference = reference, Date = businessDay.PostingDate };
        db.Vouchers.Add(voucher); db.AnimalSales.Add(sale); await db.SaveChangesAsync(ct);
        db.FinanceDocuments.Add(new FinanceDocument { CompanyId = t.company, BranchId = t.branch, Kind = "AnimalSale", PartyId = partyId, SourceId = sale.Id, Reference = voucher.Reference, Amount = amount, CostAmount = animal.PurchaseCost, VoucherId = voucher.Id });
        db.Audits.Add(new AuditEntry { CompanyId = t.company, ActorId = t.user, Action = "Animal.Sell", Reference = reference });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return sale;
    }
}
