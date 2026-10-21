using System.Data;
using EnterpriseERP.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseERP.Web;

/// <summary>
/// Typed, opt-in fixture for an isolated demo database. All business rows and Identity hashes are
/// created in one transaction. Existing business databases and Production are never seeded.
/// </summary>
public static class DemoBusinessSeeder
{
    public const string SeedVersion = "FARM-DEMO-V3";
    public const string CompletionAction = "DemoSeedCompletedV3";

    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken ct = default, bool includeEnterprise = false, int tenantCount = 5)
    {
        if (tenantCount is not (5 or 10)) throw new ArgumentOutOfRangeException(nameof(tenantCount));
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        var allowed = Environment.GetEnvironmentVariable("FARM_ALLOW_DEMO_SEED");
        var password = Environment.GetEnvironmentVariable("FARM_DEMO_PASSWORD");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("Set FARM_DEMO_PASSWORD to a unique secret of at least 12 characters; Identity password rules also apply.");

        using var strategyScope = services.CreateScope();
        var strategyDb = strategyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = strategyDb.Database.GetDbConnection();
        var provider = strategyDb.Database.ProviderName ?? "";
        var target = provider.Contains("Sqlite", StringComparison.Ordinal) ? connection.DataSource : connection.Database;
        ValidateTarget(environment.EnvironmentName, provider, target, allowed);

        // A fresh scope per retry ensures an aborted transaction cannot leak tracked entities.
        await strategyDb.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var markers = await db.Audits.Where(x => x.Action == CompletionAction && x.Reference == SeedVersion).ToListAsync(ct);
            if (markers.Count != 0)
            {
                var companies = await db.Companies.Where(x => x.Slug != null && x.Slug.StartsWith("demo-farm-")).ToListAsync(ct);
                if (markers.Count != tenantCount || markers.Select(x => x.CompanyId).Distinct().Count() != tenantCount || companies.Count != tenantCount
                    || companies.Any(c => !markers.Any(m => m.CompanyId == c.Id)))
                    throw new InvalidOperationException("Incomplete demo seed markers. Restore or recreate this isolated demo database; nothing was changed.");
                var hasEnterprise = await db.Audits.AnyAsync(x => x.Action == EnterpriseDemoSeeder.CompletionAction, ct);
                if (hasEnterprise != includeEnterprise) throw new InvalidOperationException("The requested demo mode differs from the existing fixture. Use a new empty isolated demo database; no data was changed.");
                await DemoIdentitySeeder.ValidateExistingAsync(db, users, companies, ct);
                if (includeEnterprise) await EnterpriseDemoSeeder.ValidateExistingAsync(db, users, companies, ct);
                await tx.CommitAsync(ct);
                Console.WriteLine("Demo seed already completed. No rows, passwords, grants, balances, or dates were changed.");
                return;
            }
            await RequireEmptyDatabaseAsync(db, ct);
            var now = DateTimeOffset.UtcNow;
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var companiesToCreate = Enumerable.Range(1, tenantCount).Select(i => new Company
                { Name = $"DEMO Farm {i}", Slug = $"demo-farm-{i}", PrimaryColor = "#215841" }).ToList();
            db.Companies.AddRange(companiesToCreate);
            await db.SaveChangesAsync(ct);
            var farms = companiesToCreate.Select((company, i) => new Farm
                { CompanyId = company.Id, Name = $"Demo Farm Unit {i + 1}" }).ToList();
            db.Farms.AddRange(farms);
            await db.SaveChangesAsync(ct);
            var branches = farms.Select((farm, i) => new Branch
                { CompanyId = farm.CompanyId, FarmId = farm.Id, Name = $"Demo Branch {i + 1}" }).ToList();
            var stores = farms.Select((farm, i) => new Branch
                { CompanyId = farm.CompanyId, FarmId = farm.Id, Name = $"Demo Store Branch {i + 1}" }).ToList();
            db.Branches.AddRange(branches);
            db.Branches.AddRange(stores);
            await db.SaveChangesAsync(ct);
            var accounts = await DemoIdentitySeeder.CreateAsync(db, users, roles, companiesToCreate, branches, password, ct);
            for (var i = 0; i < tenantCount; i++)
            {
                db.UserBranchAccess.Add(new UserBranchAccess { UserId = accounts[i].Id,
                    CompanyId = companiesToCreate[i].Id, BranchId = stores[i].Id });
                if (i is 1 or 3) db.UserPermissions.Add(new UserPermission { UserId = accounts[i].Id,
                    CompanyId = companiesToCreate[i].Id, Permission = "Inventory.Transfer" });
            }
            await db.SaveChangesAsync(ct);
            for (var i = 0; i < tenantCount; i++)
                await CreateTenantAsync(db, companiesToCreate[i], branches[i], stores[i], accounts[i], i + 1, now, today, ct);
            if (includeEnterprise)
            {
                await EnterpriseDemoSeeder.SeedAsync(db, users, companiesToCreate, branches, accounts, password, now, today, ct);
                await MessagingDemoSeeder.SeedAsync(db, companiesToCreate, branches, accounts, now, ct);
                await ScanningDemoSeeder.SeedAsync(db, companiesToCreate, branches, accounts, now, today, ct);
            }
            if (tenantCount == 10 && includeEnterprise) await TenRowDemoSeeder.CompleteAsync(db, roles, companiesToCreate, branches, accounts, ct);
            await AssertBusinessIntegrityAsync(db, ct);
            foreach (var company in companiesToCreate)
                db.Audits.Add(new AuditEntry { CompanyId = company.Id, ActorId = "DEMO-CLI", Action = CompletionAction,
                    Reference = SeedVersion, CreatedUtc = now });
            await db.SaveChangesAsync(ct);
            var fingerprint = await DemoSqlExporter.ComputeFixtureFingerprintAsync(db, ct);
            db.Audits.Add(new AuditEntry { CompanyId = companiesToCreate[0].Id, ActorId = "DEMO-CLI",
                Action = DemoSqlExporter.FingerprintAction, Reference = fingerprint, CreatedUtc = now });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            Console.WriteLine("Created isolated demo tenants and Identity users, with balanced vouchers and reconciled stock. Automation is inactive. Passwords were not printed.");
        });
        return 0;
    }

    public static void ValidateTarget(string environment, string provider, string target, string? allowed)
    {
        if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Demo seeding is prohibited in Production.");
        if (!string.Equals(allowed, "YES", StringComparison.Ordinal))
            throw new InvalidOperationException("Set FARM_ALLOW_DEMO_SEED=YES explicitly for an isolated demo database.");
        var sqlite = provider.Contains("Sqlite", StringComparison.Ordinal);
        if (!sqlite && !provider.Contains("SqlServer", StringComparison.Ordinal))
            throw new InvalidOperationException("Demo seeding supports only SQL Server or Development SQLite.");
        if (sqlite && !string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQLite demo seeding is Development-only.");
        var name = sqlite ? Path.GetFileNameWithoutExtension(target) : target;
        if (string.IsNullOrWhiteSpace(name) || !name.Contains("demo", StringComparison.OrdinalIgnoreCase)
            || name.Contains("prod", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use an isolated database/file whose name includes 'demo' and excludes 'prod'. No connection or data changes were attempted.");
    }

    private static async Task RequireEmptyDatabaseAsync(AppDbContext db, CancellationToken ct)
    {
        // Deliberately enumerate supported tables; no arbitrary table reflection or fabricated system rows.
        if (await db.Companies.AnyAsync(ct) || await db.Farms.AnyAsync(ct) || await db.Branches.AnyAsync(ct)
            || await db.Users.AnyAsync(ct) || await db.Roles.AnyAsync(ct) || await db.Animals.AnyAsync(ct)
            || await db.StockItems.AnyAsync(ct) || await db.StockMovements.AnyAsync(ct) || await db.MilkYields.AnyAsync(ct)
            || await db.Vouchers.AnyAsync(ct) || await db.VoucherLines.AnyAsync(ct) || await db.AnimalSales.AnyAsync(ct)
            || await db.PurchaseReceipts.AnyAsync(ct) || await db.ProductSales.AnyAsync(ct) || await db.FinancePayments.AnyAsync(ct)
            || await db.BankStatementLines.AnyAsync(ct) || await db.SubscriptionInvoices.AnyAsync(ct)
            || await db.SubscriptionPayments.AnyAsync(ct) || await db.CompanySubscriptionPolicies.AnyAsync(ct)
            || await db.SupportTickets.AnyAsync(ct) || await db.SupportTicketActivities.AnyAsync(ct)
            || await db.AnimalHealthRecords.AnyAsync(ct) || await db.AnimalFeedRecords.AnyAsync(ct)
            || await db.UserCompanyAccess.AnyAsync(ct) || await db.UserBranchAccess.AnyAsync(ct) || await db.UserPermissions.AnyAsync(ct)
            || await db.AutomationRules.AnyAsync(ct) || await db.AutomationApprovals.AnyAsync(ct)
            || await db.AutomationExecutions.AnyAsync(ct) || await db.ScheduledJobs.AnyAsync(ct) || await db.Audits.AnyAsync(ct)
            || await db.FatteningPlans.AnyAsync(ct) || await db.AnimalWeightRecords.AnyAsync(ct)
            || await db.FarmExpenses.AnyAsync(ct) || await db.BreedingCycles.AnyAsync(ct) || await db.BreedingCycleEvents.AnyAsync(ct)
            || await db.InventoryCostBalances.AnyAsync(ct) || await db.FinanceDocuments.AnyAsync(ct)
            || await db.FinancePaymentAllocations.AnyAsync(ct) || await db.StockDocumentReturns.AnyAsync(ct) || await db.Notifications.AnyAsync(ct)
            || await db.AccountingPeriodCloses.AnyAsync(ct) || await db.TradingParties.AnyAsync(ct)
            || await db.PurchaseOrders.AnyAsync(ct) || await db.PurchaseOrderReceipts.AnyAsync(ct)
            || await db.StockReorderPolicies.AnyAsync(ct) || await db.StockTransfers.AnyAsync(ct)
            || await db.OpeningBalances.AnyAsync(ct) || await db.OpeningStockValues.AnyAsync(ct) || await db.OpeningAnimalValues.AnyAsync(ct))
            throw new InvalidOperationException("Demo seed requires an empty database, or a database already completed by this seed version. Existing data was left unchanged.");
    }

    private static async Task CreateTenantAsync(AppDbContext db, Company company, Branch branch, Branch store, AppUser user,
        int number, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var companyId = company.Id;
        var branchId = branch.Id;
        var active = new Animal { CompanyId = companyId, BranchId = branchId, Tag = $"DEMO-COW-{number}",
            Breed = "Holstein Friesian", PurchaseCost = 50000 + number * 1000, CurrentWeightKg = 220 + number * 10 };
        var sold = new Animal { CompanyId = companyId, BranchId = branchId, Tag = $"DEMO-SOLD-{number}",
            Breed = "Local Cross", PurchaseCost = 42000 + number * 1000, CurrentWeightKg = 210 + number * 10, Sold = true };
        var item = new StockItem { CompanyId = companyId, BranchId = branchId, SKU = $"DEMO-FEED-{number}", UnitOfMeasure = "KG",
            Name = $"Demo Cattle Feed {number}", UnitCost = 45 + number, QuantityOnHand = 96 };
        var supplier = new TradingParty { CompanyId = companyId, BranchId = branchId, Kind = "Supplier",
            Code = $"DEMO-SUP-{number}", Name = $"Demo Supplier {number}", Active = true };
        var customer = new TradingParty { CompanyId = companyId, BranchId = branchId, Kind = "Customer",
            Code = $"DEMO-CUST-{number}", Name = $"Demo Customer {number}", Active = true };
        var destinationItem = await CreateOpeningAsync(db, company, store, user, number, item.SKU, item.Name, item.UnitCost, today, ct);
        db.TradingParties.AddRange(supplier, customer);
        db.Animals.AddRange(active, sold);
        db.StockItems.Add(item);
        await db.SaveChangesAsync(ct);

        var purchaseReference = $"DEMO-PR-{number}";
        var saleReference = $"DEMO-PS-{number}";
        var feedReference = $"DEMO-FEED-ISSUE-{number}";
        var paymentReference = $"DEMO-FP-{number}";
        var animalSaleReference = $"DEMO-AS-{number}";
        var purchaseAmount = 100 * item.UnitCost;
        var saleUnitPrice = 65 + number;
        var saleAmount = 2 * saleUnitPrice;
        var animalSaleAmount = 65000 + number * 100;
        var activeAcquisition = VoucherFor(companyId, branchId, "ANIMAL-ACQ-" + active.Id, now.AddDays(-2),
            ("1400", active.PurchaseCost, 0), ("2100", 0, active.PurchaseCost));
        var soldAcquisition = VoucherFor(companyId, branchId, "ANIMAL-ACQ-" + sold.Id, now.AddDays(-2),
            ("1400", sold.PurchaseCost, 0), ("2100", 0, sold.PurchaseCost));
        var purchase = VoucherFor(companyId, branchId, "PUR-" + purchaseReference, now.AddDays(-1),
            ("1300", purchaseAmount, 0), ("2100", 0, purchaseAmount));
        var sale = VoucherFor(companyId, branchId, "SAL-" + saleReference, now,
            ("1100", saleAmount, 0), ("4100", 0, saleAmount), ("5100", 2 * item.UnitCost, 0), ("1300", 0, 2 * item.UnitCost));
        var feed = VoucherFor(companyId, branchId, "FEED:" + feedReference, now,
            ("5100", item.UnitCost, 0), ("1300", 0, item.UnitCost));
        var payment = VoucherFor(companyId, branchId, "PAY-" + paymentReference, now,
            ("1000", saleAmount, 0), ("1100", 0, saleAmount));
        var animalSale = VoucherFor(companyId, branchId, "ANIMAL-SAL-" + animalSaleReference, now,
            ("1100", animalSaleAmount, 0), ("4200", 0, animalSaleAmount),
            ("5200", sold.PurchaseCost, 0), ("1400", 0, sold.PurchaseCost));
        var expenseReference = $"DEMO-EXP-{number}";
        var expense = VoucherFor(companyId, branchId, "EXP:" + expenseReference, now,
            ("6200", 100, 0), ("1000", 0, 100));
        var returnReference = $"DEMO-PUR-RETURN-{number}";
        var purchaseReturn = VoucherFor(companyId, branchId, "RET-" + returnReference, now,
            ("2100", item.UnitCost, 0), ("1300", 0, item.UnitCost));
        db.Vouchers.AddRange(activeAcquisition, soldAcquisition, purchase, sale, feed, payment, animalSale, expense, purchaseReturn);
        await db.SaveChangesAsync(ct);
        var receipt = new PurchaseReceipt { CompanyId = companyId, BranchId = branchId, StockItemId = item.Id,
            Reference = purchaseReference, Quantity = 100, UnitPrice = item.UnitCost, VoucherId = purchase.Id, CreatedUtc = now.AddDays(-1) };
        db.PurchaseReceipts.Add(receipt);
        var order = new PurchaseOrder { CompanyId = companyId, BranchId = branchId,
            Reference = $"DEMO-PO-{number}", SupplierId = supplier.Id, StockItemId = item.Id,
            Quantity = 100, UnitPrice = item.UnitCost, Amount = purchaseAmount,
            ReceivedQuantity = 100, ReceivedAmount = purchaseAmount, Status = "Received",
            CreatedBy = user.Id, ApprovedBy = user.Id, CreatedUtc = now.AddDays(-2), ApprovedUtc = now.AddDays(-1) };
        db.PurchaseOrders.Add(order);
        var productSale = new ProductSale { CompanyId = companyId, BranchId = branchId, StockItemId = item.Id,
            Reference = saleReference, Quantity = 2, UnitPrice = saleUnitPrice, UnitCost = item.UnitCost, VoucherId = sale.Id, CreatedUtc = now };
        db.ProductSales.Add(productSale);
        db.StockMovements.AddRange(
            Movement(companyId, branchId, item.Id, 100, "PUR-" + purchaseReference, now.AddDays(-1)),
            Movement(companyId, branchId, item.Id, -2, "SAL-" + saleReference, now),
            Movement(companyId, branchId, item.Id, -1, "FEED:" + feedReference, now),
            Movement(companyId, branchId, item.Id, -1, "RET-" + returnReference, now));
        db.InventoryCostBalances.Add(new InventoryCostBalance { CompanyId = companyId, BranchId = branchId,
            StockItemId = item.Id, Value = item.QuantityOnHand * item.UnitCost });
        db.StockReorderPolicies.Add(new StockReorderPolicy { CompanyId = companyId, BranchId = branchId,
            StockItemId = item.Id, ReorderPoint = 5, TargetQuantity = 10 });
        db.FatteningPlans.Add(new FatteningPlan { CompanyId = companyId, BranchId = branchId, AnimalId = active.Id,
            Reference = $"DEMO-FAT-{number}", StartDate = today, TargetDate = today.AddDays(90),
            InitialWeightKg = active.CurrentWeightKg!.Value, TargetWeightKg = active.CurrentWeightKg.Value + 60,
            ExpectedSaleAmount = 80000 + number * 1000, RecordedBy = user.Id, CreatedUtc = now });
        db.AnimalWeightRecords.Add(new AnimalWeightRecord { CompanyId = companyId, BranchId = branchId, AnimalId = active.Id,
            Reference = $"PLAN:DEMO-FAT-{number}", WeighedOn = today, WeightKg = active.CurrentWeightKg!.Value,
            Notes = "Demo plan starting weight.", RecordedBy = user.Id, CreatedUtc = now });
        db.FarmExpenses.Add(new FarmExpense { CompanyId = companyId, BranchId = branchId, AnimalId = active.Id,
            Reference = expenseReference, ExpenseDate = today, Category = "Veterinary", Amount = 100,
            Description = "Demo routine farm observation cost.", VoucherId = expense.Id, RecordedBy = user.Id, CreatedUtc = now });
        var cycle = new BreedingCycle { CompanyId = companyId, BranchId = branchId, AnimalId = active.Id,
            Reference = $"DEMO-BREED-{number}", ServiceDate = today, ExpectedCalvingDate = today.AddDays(283),
            Method = "Natural", SireOrSemen = $"DEMO-SIRE-{number}", Status = "Bred", IsClosed = false,
            LastEventDate = today, RecordedBy = user.Id, CreatedUtc = now };
        cycle.Events.Add(new BreedingCycleEvent { CompanyId = companyId, BranchId = branchId,
            Reference = $"DEMO-BREED-EVENT-{number}", Outcome = "Bred", EventDate = today,
            Notes = "Demo breeding event; sample dates only.", RecordedBy = user.Id, CreatedUtc = now });
        db.BreedingCycles.Add(cycle);
        db.AnimalFeedRecords.Add(new AnimalFeedRecord { CompanyId = companyId, BranchId = branchId, AnimalId = active.Id,
            StockItemId = item.Id, Reference = feedReference, Quantity = 1, UnitCost = item.UnitCost, CostAmount = item.UnitCost, FeedDate = today, RecordedBy = user.Id, CreatedUtc = now });
        db.AnimalHealthRecords.Add(new AnimalHealthRecord { CompanyId = companyId, BranchId = branchId, AnimalId = active.Id,
            Reference = $"DEMO-HEALTH-{number}", EventType = "Observation", EventDate = today, NextDueDate = today.AddDays(30),
            Details = "Demo routine observation only; not veterinary advice.", RecordedBy = user.Id, CreatedUtc = now });
        db.MilkYields.Add(new MilkYield { CompanyId = companyId, BranchId = branchId, AnimalId = active.Id,
            Date = today, Session = "Morning", Litres = 5 + number });
        var animalSaleRecord = new AnimalSale { CompanyId = companyId, BranchId = branchId, AnimalId = sold.Id,
            Amount = animalSaleAmount, Reference = animalSaleReference, Date = now };
        db.AnimalSales.Add(animalSaleRecord);
        var paymentRecord = new FinancePayment { CompanyId = companyId, BranchId = branchId, Reference = paymentReference,
            PartyType = "Customer", Amount = saleAmount, VoucherId = payment.Id, CreatedUtc = now };
        db.FinancePayments.Add(paymentRecord);
        db.BankStatementLines.Add(new BankStatementLine { CompanyId = companyId, BranchId = branchId,
            ExternalReference = $"DEMO-BANK-{number}", Amount = saleAmount, ValueDate = today, MatchedVoucherId = payment.Id });

        var invoice = new SubscriptionInvoice { CompanyId = companyId, Reference = $"DEMO-SI-{number}",
            DueDate = today.AddDays(30), Amount = 1000, Paid = 1000 };
        db.SubscriptionInvoices.Add(invoice);
        var ticket = new SupportTicket { CompanyId = companyId, BranchId = branchId, TicketNumber = $"DEMO-T-{number}",
            Subject = "Demo feed delivery question", Description = "Sample issue for reviewing the customer care workflow.",
            ContactName = $"Demo Customer {number}", Priority = "Normal", Status = "Open", CreatedBy = user.Id,
            CreatedUtc = now, UpdatedUtc = now };
        db.SupportTickets.Add(ticket);
        db.AutomationRules.Add(new AutomationRule { CompanyId = companyId, Name = $"Demo Low Stock Review {number}",
            Trigger = "Manual", Action = "Review", Active = false });
        db.CompanySubscriptionPolicies.Add(new CompanySubscriptionPolicy { CompanyId = companyId,
            NoticeDays = 1, AlertDays = 15, SuspendDays = 30, GraceDays = 0 });
        await db.SaveChangesAsync(ct);
        db.SubscriptionPayments.Add(new SubscriptionPayment { CompanyId = companyId, InvoiceId = invoice.Id,
            Reference = $"DEMO-SP-{number}", Amount = 1000, RecordedUtc = now });
        db.SupportTicketActivities.Add(new SupportTicketActivity { CompanyId = companyId, TicketId = ticket.Id,
            ActorId = user.Id, Action = "Created", Note = "Created as part of the isolated demo fixture.", CreatedUtc = now });
        await db.SaveChangesAsync(ct);
        FinanceDocument Document(string kind, long sourceId, Voucher voucher, decimal amount, decimal cost) =>
            new() { CompanyId = companyId, BranchId = branchId, Kind = kind, SourceId = sourceId,
                PartyId = kind is "Purchase" or "AnimalPurchase" ? supplier.Id : customer.Id,
                Reference = voucher.Reference, Amount = amount, CostAmount = cost, VoucherId = voucher.Id, CreatedUtc = voucher.Date };
        var purchaseDocument = Document("Purchase", receipt.Id, purchase, purchaseAmount, purchaseAmount);
        var saleDocument = Document("Sale", productSale.Id, sale, saleAmount, 2 * item.UnitCost);
        db.FinanceDocuments.AddRange(purchaseDocument, saleDocument,
            Document("AnimalPurchase", active.Id, activeAcquisition, active.PurchaseCost, active.PurchaseCost),
            Document("AnimalPurchase", sold.Id, soldAcquisition, sold.PurchaseCost, sold.PurchaseCost),
            Document("AnimalSale", animalSaleRecord.Id, animalSale, animalSaleAmount, sold.PurchaseCost));
        await db.SaveChangesAsync(ct);
        db.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt { CompanyId = companyId, BranchId = branchId,
            PurchaseOrderId = order.Id, PurchaseReceiptId = receipt.Id, Quantity = 100, Amount = purchaseAmount });
        db.FinancePaymentAllocations.Add(new FinancePaymentAllocation { CompanyId = companyId, BranchId = branchId,
            FinanceDocumentId = saleDocument.Id, FinancePaymentId = paymentRecord.Id, SignedAmount = saleAmount });
        db.StockDocumentReturns.Add(new StockDocumentReturn { CompanyId = companyId, BranchId = branchId,
            FinanceDocumentId = purchaseDocument.Id, Reference = returnReference, Quantity = 1, Amount = item.UnitCost,
            CostAmount = item.UnitCost, VoucherId = purchaseReturn.Id, CreatedUtc = now });
        await db.SaveChangesAsync(ct);
        await CreateTransferAsync(db, company.Id, branch, store, item, destinationItem, 5,
            $"DEMO-TRANSFER-OUT-{number}", "Demo stock transfer to the store branch.", user.Id, now, ct);
        await CreateTransferAsync(db, company.Id, store, branch, destinationItem, item, 5,
            $"DEMO-TRANSFER-BACK-{number}", $"Demo opposite-direction return of DEMO-TRANSFER-OUT-{number}.", user.Id, now, ct);
    }

    private static async Task<StockItem> CreateOpeningAsync(AppDbContext db, Company company, Branch branch,
        AppUser user, int number, string sku, string productName, decimal unitCost, DateOnly today, CancellationToken ct)
    {
        // The new branch has no prior books. Master records begin at zero; only this explicit opening values them.
        var item = new StockItem { CompanyId = company.Id, BranchId = branch.Id, SKU = sku, Name = productName, UnitOfMeasure = "KG" };
        var animal = new Animal { CompanyId = company.Id, BranchId = branch.Id, Tag = $"DEMO-OPEN-ANIMAL-{number}",
            Breed = "Local Cross", CurrentWeightKg = 180 + number * 5 };
        db.StockItems.Add(item); db.Animals.Add(animal);
        await db.SaveChangesAsync(ct);
        var date = today.AddDays(-3);
        var at = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var reference = $"DEMO-OPEN-{number}";
        const decimal cash = 10000m;
        const decimal quantity = 10m;
        var stockValue = quantity * unitCost;
        var animalValue = 25000m + number * 1000m;
        var voucher = VoucherFor(company.Id, branch.Id, $"OPEN-{branch.Id}-{reference}", at,
            ("1000", cash, 0), ("1300", stockValue, 0), ("1400", animalValue, 0),
            ("3000", 0, cash + stockValue + animalValue));
        var opening = new OpeningBalance { CompanyId = company.Id, BranchId = branch.Id, Reference = reference,
            OpeningDate = date, ReviewNote = "Synthetic demo opening statement with itemized stock and livestock; no real customer balances.",
            RecordedBy = user.Id, CreatedUtc = at, Voucher = voucher };
        opening.Stocks.Add(new OpeningStockValue { CompanyId = company.Id, BranchId = branch.Id,
            StockItemId = item.Id, Quantity = quantity, Value = stockValue });
        opening.Animals.Add(new OpeningAnimalValue { CompanyId = company.Id, BranchId = branch.Id,
            AnimalId = animal.Id, Value = animalValue });
        item.QuantityOnHand = quantity; item.UnitCost = unitCost; animal.PurchaseCost = animalValue;
        db.InventoryCostBalances.Add(new InventoryCostBalance { CompanyId = company.Id, BranchId = branch.Id,
            StockItemId = item.Id, Value = stockValue });
        db.StockMovements.Add(Movement(company.Id, branch.Id, item.Id, quantity, voucher.Reference, at));
        db.OpeningBalances.Add(opening);
        await db.SaveChangesAsync(ct);
        return item;
    }

    private static async Task CreateTransferAsync(AppDbContext db, int company, Branch sourceBranch, Branch destinationBranch,
        StockItem source, StockItem destination, decimal quantity, string reference, string note, string actor,
        DateTimeOffset at, CancellationToken ct)
    {
        reference = StockTransferService.NormalizeReference(reference);
        var sourceValue = await InventoryValuation.Load(db, source, ct);
        var destinationValue = await InventoryValuation.Load(db, destination, ct);
        var transfer = new StockTransfer { CompanyId = company, SourceBranchId = sourceBranch.Id,
            DestinationBranchId = destinationBranch.Id, SourceStockItemId = source.Id, DestinationStockItemId = destination.Id,
            Reference = reference, Note = note, SourceBranchName = sourceBranch.Name, DestinationBranchName = destinationBranch.Name,
            SourceSku = source.SKU, DestinationSku = destination.SKU, UnitOfMeasure = "KG", SourceProductName = source.Name, DestinationProductName = destination.Name,
            Quantity = quantity, SourceQuantityBefore = source.QuantityOnHand, DestinationQuantityBefore = destination.QuantityOnHand,
            SourceValueBefore = sourceValue.Value, DestinationValueBefore = destinationValue.Value, CreatedBy = actor, CreatedUtc = at };
        transfer.CostAmount = InventoryValuation.Remove(source, sourceValue, quantity);
        InventoryValuation.Add(destination, destinationValue, quantity, transfer.CostAmount);
        transfer.SourceQuantityAfter = source.QuantityOnHand; transfer.DestinationQuantityAfter = destination.QuantityOnHand;
        transfer.SourceValueAfter = sourceValue.Value; transfer.DestinationValueAfter = destinationValue.Value;
        var outgoing = Movement(company, sourceBranch.Id, source.Id, -quantity, StockTransferService.OutReference(reference), at);
        var incoming = Movement(company, destinationBranch.Id, destination.Id, quantity, StockTransferService.InReference(reference), at);
        var sourceVoucher = VoucherFor(company, sourceBranch.Id, outgoing.Reference, at,
            ("1290", transfer.CostAmount, 0), ("1300", 0, transfer.CostAmount));
        var destinationVoucher = VoucherFor(company, destinationBranch.Id, incoming.Reference, at,
            ("1300", transfer.CostAmount, 0), ("1290", 0, transfer.CostAmount));
        db.StockMovements.AddRange(outgoing, incoming); db.Vouchers.AddRange(sourceVoucher, destinationVoucher);
        await db.SaveChangesAsync(ct);
        transfer.SourceMovementId = outgoing.Id; transfer.DestinationMovementId = incoming.Id;
        transfer.SourceVoucherId = sourceVoucher.Id; transfer.DestinationVoucherId = destinationVoucher.Id;
        db.StockTransfers.Add(transfer);
        await db.SaveChangesAsync(ct);
    }

    private static StockMovement Movement(int company, int branch, int item, decimal delta, string reference, DateTimeOffset at) =>
        new() { CompanyId = company, BranchId = branch, StockItemId = item, QuantityDelta = delta, Reference = reference, CreatedUtc = at };

    private static Voucher VoucherFor(int company, int branch, string reference, DateTimeOffset at,
        params (string account, decimal debit, decimal credit)[] lines) => new()
        {
            CompanyId = company, BranchId = branch, Reference = reference, Date = at, Posted = true,
            Lines = lines.Select(x => new VoucherLine { AccountCode = x.account, Debit = x.debit, Credit = x.credit }).ToList()
        };

    private static async Task AssertBusinessIntegrityAsync(AppDbContext db, CancellationToken ct)
    {
        var vouchers = await db.Vouchers.Include(x => x.Lines).ToListAsync(ct);
        if (vouchers.Any(v => !v.Posted || v.Lines.Count < 2 || v.Lines.Sum(x => x.Debit) <= 0
            || v.Lines.Sum(x => x.Debit) != v.Lines.Sum(x => x.Credit)
            || v.Lines.Any(x => x.Debit < 0 || x.Credit < 0 || (x.Debit > 0 && x.Credit > 0))))
            throw new InvalidOperationException("Demo voucher integrity check failed; seed rolled back.");
        var stock = await db.StockItems.ToListAsync(ct);
        var movements = await db.StockMovements.ToListAsync(ct);
        if (stock.Any(item => item.QuantityOnHand < 0 || item.QuantityOnHand != movements
            .Where(m => m.CompanyId == item.CompanyId && m.BranchId == item.BranchId && m.StockItemId == item.Id).Sum(m => m.QuantityDelta)))
            throw new InvalidOperationException("Demo stock reconciliation failed; seed rolled back.");
        // The fixture has exactly one stock item per branch; its value can be reconciled to branch GL.
        var values = await db.InventoryCostBalances.ToListAsync(ct);
        if (stock.Any(item => values.Single(x => x.StockItemId == item.Id).Value != vouchers
            .Where(v => v.CompanyId == item.CompanyId && v.BranchId == item.BranchId).SelectMany(v => v.Lines)
            .Where(l => l.AccountCode == "1300").Sum(l => l.Debit - l.Credit)))
            throw new InvalidOperationException("Demo inventory valuation does not match the general ledger; seed rolled back.");
        var statements = await db.BankStatementLines.ToListAsync(ct);
        if (statements.Any(line => line.Amount != vouchers.Single(v => v.Id == line.MatchedVoucherId).Lines
            .Where(l => l.AccountCode == "1000").Sum(l => l.Debit - l.Credit)))
            throw new InvalidOperationException("Demo bank reconciliation failed; seed rolled back.");
        var invoices = await db.SubscriptionInvoices.ToListAsync(ct);
        var subscriptionPayments = await db.SubscriptionPayments.ToListAsync(ct);
        if (invoices.Any(invoice => invoice.Paid > invoice.Amount || invoice.Paid != subscriptionPayments
            .Where(p => p.CompanyId == invoice.CompanyId && p.InvoiceId == invoice.Id).Sum(p => p.Amount)))
            throw new InvalidOperationException("Demo subscription payment reconciliation failed; seed rolled back.");
        var animals = await db.Animals.ToListAsync(ct);
        var sales = await db.AnimalSales.ToListAsync(ct);
        if (animals.Any(a => a.Sold != sales.Any(s => s.CompanyId == a.CompanyId && s.BranchId == a.BranchId && s.AnimalId == a.Id)))
            throw new InvalidOperationException("Demo animal sale integrity check failed; seed rolled back.");
        foreach (var branch in await db.Branches.ToListAsync(ct))
        {
            var lines = vouchers.Where(v => v.CompanyId == branch.CompanyId && v.BranchId == branch.Id).SelectMany(v => v.Lines).ToList();
            var livestockValue = animals.Where(a => a.CompanyId == branch.CompanyId && a.BranchId == branch.Id && !a.Sold).Sum(a => a.PurchaseCost);
            if (livestockValue != lines.Where(l => l.AccountCode == "1400").Sum(l => l.Debit - l.Credit))
                throw new InvalidOperationException("Demo livestock opening/acquisition values do not match the general ledger; seed rolled back.");
            if (lines.Where(l => l.AccountCode == "1290").Sum(l => l.Debit - l.Credit) != 0)
                throw new InvalidOperationException("Demo round-trip transfer clearing did not settle; seed rolled back.");
        }
    }
}
