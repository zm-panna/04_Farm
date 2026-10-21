using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class StockTransferTests
{
    private static StockTransferService Service(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static FarmService Farm(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static IntegratedOperations Ops(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static async Task<(int source, int destination)> Seed(SecurityTestScope f, bool destinationAccess = true)
    {
        await f.GrantAsync("*");
        if (destinationAccess) f.Db.UserBranchAccess.Add(new UserBranchAccess { UserId = "operator-a", CompanyId = 1, BranchId = 12 });
        var destination = new StockItem { CompanyId = 1, BranchId = 12, SKU = "FEED", Name = "Destination feed", UnitOfMeasure = "KG" };
        f.Db.StockItems.Add(destination); await f.Db.SaveChangesAsync();
        var source = await Farm(f).AddStock("FEED", "Source feed", 0, default, "KG");
        await Ops(f).Receive(source.Id, 1, 1, "p1", default);
        await Ops(f).Receive(source.Id, 2, 2, "p2", default);
        return (source.Id, destination.Id);
    }
    private static async Task<(decimal quantity, decimal value)> Balance(SecurityTestScope f, int id)
    {
        var quantity = await f.Db.StockItems.AsNoTracking().Where(x => x.Id == id).Select(x => x.QuantityOnHand).SingleAsync();
        var value = await f.Db.InventoryCostBalances.AsNoTracking().Where(x => x.StockItemId == id).Select(x => (decimal?)x.Value).SingleOrDefaultAsync() ?? 0;
        return (quantity, value);
    }

    [Fact]
    public async Task PartialThenFullTransferConservesExactValueAndBalancesEachJournal()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        var first = await Service(f).Post(ids.source, 12, ids.destination, 1, " move-1 ", "Delivery one", default);
        Assert.Equal("MOVE-1", first.Reference); Assert.Equal(1.667m, first.CostAmount);
        Assert.Equal((2m, 3.333m), await Balance(f, ids.source)); Assert.Equal((1m, 1.667m), await Balance(f, ids.destination));
        var last = await Service(f).Post(ids.source, 12, ids.destination, 2, "move-2", null, default);
        Assert.Equal(3.333m, last.CostAmount); Assert.Equal((0m, 0m), await Balance(f, ids.source)); Assert.Equal((3m, 5m), await Balance(f, ids.destination));
        var journals = await f.Db.Vouchers.AsNoTracking().Include(x => x.Lines).Where(x => x.Reference.StartsWith("XFER-")).ToListAsync();
        Assert.Equal(4, journals.Count);
        Assert.All(journals, x => { Assert.True(x.Posted); Assert.Equal(2, x.Lines.Count); Assert.Equal(x.Lines.Sum(l => l.Debit), x.Lines.Sum(l => l.Credit)); });
        Assert.Equal(0, journals.SelectMany(x => x.Lines).Where(x => x.AccountCode == "1290").Sum(x => x.Debit - x.Credit));
        var outgoing = await f.Db.StockMovements.SingleAsync(x => x.Id == first.SourceMovementId);
        var incoming = await f.Db.StockMovements.SingleAsync(x => x.Id == first.DestinationMovementId);
        Assert.Equal(-1, outgoing.QuantityDelta); Assert.Equal(1, incoming.QuantityDelta);
        Assert.Equal(first.CreatedUtc, outgoing.CreatedUtc); Assert.Equal(first.CreatedUtc, incoming.CreatedUtc);
        Assert.Equal(2, await f.Db.Audits.CountAsync(x => x.Action == "Inventory.Transfer"));
        foreach (var branch in new[] { 11, 12 })
        {
            var ledger = await f.Db.Vouchers.AsNoTracking().Include(x => x.Lines).Where(x => x.CompanyId == 1 && x.BranchId == branch).ToListAsync();
            var inventory = ledger.SelectMany(x => x.Lines).Where(x => x.AccountCode == "1300").Sum(x => x.Debit - x.Credit);
            Assert.Equal(branch == 11 ? 0 : 5, inventory);
        }
    }
    [Fact]
    public async Task DestinationBlendUsesExactValueAndHistoricalNamesRemainStable()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        f.ActAs(branch: 12); await Ops(f).Receive(ids.destination, 2, 3, "dest-receipt", default); f.ActAs();
        var transfer = await Service(f).Post(ids.source, 12, ids.destination, 1, "blend", null, default);
        Assert.Equal((3m, 7.667m), await Balance(f, ids.destination));
        Assert.Equal(2.556m, (await f.Db.StockItems.SingleAsync(x => x.Id == ids.destination)).UnitCost);
        Assert.Equal(6, transfer.DestinationValueBefore); Assert.Equal(7.667m, transfer.DestinationValueAfter);
        (await f.Db.StockItems.SingleAsync(x => x.Id == ids.source)).Name = "Renamed later";
        (await f.Db.Branches.SingleAsync(x => x.Id == 11)).Name = "Renamed branch";
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var history = await f.Db.StockTransfers.SingleAsync(); Assert.Equal("Source feed", history.SourceProductName); Assert.Equal("A main", history.SourceBranchName);
    }
    [Fact]
    public async Task ReferencesAreTrimmedCaseNormalizedAndUniqueAcrossBranches()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        await Service(f).Post(ids.source, 12, ids.destination, 1, " move ", null, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "MoVe", null, default));
        f.ActAs(branch: 12);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Post(ids.destination, 11, ids.source, 1, "move", null, default));
        Assert.Single(await f.Db.StockTransfers.ToListAsync()); Assert.Equal((2m, 3.333m), await Balance(f, ids.source));
    }
    [Fact]
    public async Task MissingDestinationGrantAndCrossCompanyTransferCannotPost()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f, false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "denied", null, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).Post(ids.source, 21, ids.destination, 1, "foreign", null, default));
        Assert.Empty(await f.Db.StockTransfers.ToListAsync()); Assert.Equal((3m, 5m), await Balance(f, ids.source));
    }
    [Fact]
    public async Task TransferPermissionAndActiveSourceGrantAreRequired()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        f.Db.UserPermissions.RemoveRange(await f.Db.UserPermissions.ToListAsync()); await f.Db.SaveChangesAsync();
        await f.GrantAsync("Inventory.Move");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "move-is-not-transfer", null, default));
        await f.GrantAsync("Inventory.Transfer");
        f.Db.UserBranchAccess.Remove(await f.Db.UserBranchAccess.SingleAsync(x => x.UserId == "operator-a" && x.BranchId == 11)); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "no-source", null, default));
    }
    [Fact]
    public async Task WrongBranchItemSameBranchAndDifferentSkuAreRejected()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Post(ids.source, 11, ids.destination, 1, "same", null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Post(ids.source, 12, ids.source, 1, "wrong-item", null, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Post(ids.destination, 12, ids.source, 1, "wrong-source", null, default));
        (await f.Db.StockItems.SingleAsync(x => x.Id == ids.destination)).SKU = "OTHER"; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "different", null, default));
        Assert.Empty(await f.Db.StockTransfers.ToListAsync());
    }
    [Theory]
    [InlineData("0")][InlineData("-1")][InlineData("0.0004")][InlineData("1000000000000000")]
    public async Task InvalidQuantityCannotChangeStock(string text)
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        var quantity = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Post(ids.source, 12, ids.destination, quantity, "bad", null, default));
        Assert.Equal((3m, 5m), await Balance(f, ids.source)); Assert.Empty(await f.Db.StockTransfers.ToListAsync());
    }
    [Fact]
    public async Task OversellAndLegacyDestinationValuationAreBlockedWithoutPartialWrites()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Post(ids.source, 12, ids.destination, 4, "oversell", null, default));
        (await f.Db.StockItems.SingleAsync(x => x.Id == ids.destination)).QuantityOnHand = 2; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "unvalued", null, default));
        Assert.Equal((3m, 5m), await Balance(f, ids.source)); Assert.Empty(await f.Db.StockTransfers.ToListAsync());
        Assert.False(await f.Db.StockMovements.AnyAsync(x => x.Reference.StartsWith("XFER-")));
    }
    [Fact]
    public async Task FailureAfterBothBalanceWritesRollsBackEverythingAndAllowsSafeRetry()
    {
        var fail = new FailTransferSave(); await using var f = await SecurityTestScope.CreateAsync(fail); var ids = await Seed(f);
        fail.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "atomic", null, default));
        Assert.Equal((3m, 5m), await Balance(f, ids.source)); Assert.Equal((0m, 0m), await Balance(f, ids.destination));
        Assert.Empty(await f.Db.StockTransfers.ToListAsync()); Assert.False(await f.Db.StockMovements.AnyAsync(x => x.Reference.StartsWith("XFER-")));
        Assert.False(await f.Db.Vouchers.AnyAsync(x => x.Reference.StartsWith("XFER-"))); Assert.False(await f.Db.Audits.AnyAsync(x => x.Action == "Inventory.Transfer"));
        fail.Enabled = false; await Service(f).Post(ids.source, 12, ids.destination, 1, "atomic", null, default); Assert.Single(await f.Db.StockTransfers.ToListAsync());
    }
    [Fact]
    public async Task PostedTransferAndMovementAreImmutableInSyncAndAsyncSave()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        var transfer = await Service(f).Post(ids.source, 12, ids.destination, 1, "immutable", null, default);
        transfer.Note = "Rewrite history"; await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); f.Db.StockTransfers.Remove(await f.Db.StockTransfers.SingleAsync()); Assert.Throws<InvalidOperationException>(() => f.Db.SaveChanges());
        f.Db.ChangeTracker.Clear(); (await f.Db.StockMovements.SingleAsync(x => x.Id == transfer.SourceMovementId)).QuantityDelta = -2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); f.Db.StockMovements.Remove(await f.Db.StockMovements.SingleAsync(x => x.Id == transfer.DestinationMovementId));
        Assert.Throws<InvalidOperationException>(() => f.Db.SaveChanges());
    }
    [Fact]
    public async Task OppositeTransferUsesNewReferenceAndPreservesOriginalHistory()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        var original = await Service(f).Post(ids.source, 12, ids.destination, 1, "original", null, default);
        f.ActAs(branch: 12);
        var correction = await Service(f).Post(ids.destination, 11, ids.source, 1, "opposite", "Correction of ORIGINAL", default);
        Assert.Equal(original.CostAmount, correction.CostAmount); Assert.Equal((3m, 5m), await Balance(f, ids.source)); Assert.Equal((0m, 0m), await Balance(f, ids.destination));
        Assert.Equal(2, await f.Db.StockTransfers.CountAsync()); Assert.Equal(4, await f.Db.StockMovements.CountAsync(x => x.Reference.StartsWith("XFER-")));
    }
    [Fact]
    public async Task ZeroValueStockStillHasPairedPhysicalTraceWithoutFakeJournals()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        var source = await Farm(f).AddStock("FREE", "Free stock", 0, default, "KG"); await Farm(f).MoveStock(source.Id, 2, "free", default);
        var destination = new StockItem { CompanyId = 1, BranchId = 12, SKU = "FREE", Name = "Free stock", UnitOfMeasure = "KG" }; f.Db.StockItems.Add(destination); await f.Db.SaveChangesAsync();
        var transfer = await Service(f).Post(source.Id, 12, destination.Id, 2, "free-move", null, default);
        Assert.Equal(0, transfer.CostAmount); Assert.Null(transfer.SourceVoucherId); Assert.Null(transfer.DestinationVoucherId);
        Assert.Equal((2m, 0m), await Balance(f, destination.Id)); Assert.NotEqual(0, transfer.SourceMovementId); Assert.NotEqual(0, transfer.DestinationMovementId);
    }
    [Fact]
    public async Task HistoryAndDetailsRequireBothBranchGrantsAndNeverLeakOtherCompanies()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        var transfer = await Service(f).Post(ids.source, 12, ids.destination, 1, "visibility", null, default);
        var controller = new StockTransfersController(f.Db, f.Tenant, f.Permissions, Service(f));
        var visible = Assert.IsType<ViewResult>(await controller.Index(null)); Assert.Single(Assert.IsType<StockTransferList>(visible.Model).Transfers);
        Assert.IsType<ViewResult>(await controller.Details(transfer.Id, default));
        f.Db.UserBranchAccess.Remove(await f.Db.UserBranchAccess.SingleAsync(x => x.UserId == "operator-a" && x.BranchId == 12)); await f.Db.SaveChangesAsync();
        var hidden = Assert.IsType<ViewResult>(await controller.Index(null)); Assert.Empty(Assert.IsType<StockTransferList>(hidden.Model).Transfers);
        Assert.IsType<NotFoundResult>(await controller.Details(transfer.Id, default));
        await f.GrantAsync("Inventory.View", "operator-b", 2); f.ActAs("operator-b", 2, 21);
        Assert.IsType<NotFoundResult>(await controller.Details(transfer.Id, default));
    }
    [Fact]
    public async Task ProductCreatePermissionDoesNotGrantEditAndEditDoesNotGrantCreate()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Inventory.Create");
        var controller = new ProductCatalogController(f.Db, f.Tenant, f.Permissions)
        {
            ControllerContext = new ControllerContext { HttpContext = f.Http },
            Url = new UrlHelper(new ActionContext(f.Http, new RouteData(), new ActionDescriptor())),
            TempData = new TempDataDictionary(f.Http, f.Services.GetRequiredService<ITempDataProvider>())
        };
        Assert.IsType<ViewResult>(await controller.Create(default));
        Assert.IsType<RedirectToActionResult>(await controller.Save(new ProductEditor { SKU = "NEW", Name = "New product", UnitOfMeasure = "KG" }, default));
        var item = await f.Db.StockItems.SingleAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Save(new ProductEditor { Id = item.Id, SKU = "NEW", Name = "Edited" }, default));
        f.Db.UserPermissions.RemoveRange(await f.Db.UserPermissions.ToListAsync()); await f.Db.SaveChangesAsync(); await f.GrantAsync("Inventory.Edit");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Create(default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controller.Save(new ProductEditor { SKU = "OTHER", Name = "Other product" }, default));
    }

    private static ProductCatalogController Catalog(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions)
    {
        ControllerContext = new ControllerContext { HttpContext = f.Http },
        Url = new UrlHelper(new ActionContext(f.Http, new RouteData(), new ActionDescriptor())),
        TempData = new TempDataDictionary(f.Http, f.Services.GetRequiredService<ITempDataProvider>())
    };
    [Fact]
    public async Task UnitIsSnapshottedImmutableAndConsistentAcrossCompanySku()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        var transfer = await Service(f).Post(ids.source, 12, ids.destination, 1, "unit-snapshot", null, default);
        Assert.Equal("KG", transfer.UnitOfMeasure);
        (await f.Db.StockItems.SingleAsync(x => x.Id == ids.source)).UnitOfMeasure = "BAG25KG";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync()); f.Db.ChangeTracker.Clear();
        f.Db.StockItems.Add(new StockItem { CompanyId = 1, BranchId = 11, SKU = "FEED", Name = "Wrong unit", UnitOfMeasure = "L" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }
    [Fact]
    public async Task UnknownAndCorruptDifferentUnitsCannotTransfer()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        // Simulate unreviewed legacy/import data without weakening the catalog's write guard.
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockItems SET UnitOfMeasure = {""} WHERE Id = {ids.source}"); f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "unknown", null, default));
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockItems SET UnitOfMeasure = {"BAG25KG"} WHERE Id = {ids.source}"); f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Post(ids.source, 12, ids.destination, 1, "different-unit", null, default));
        Assert.Empty(await f.Db.StockTransfers.ToListAsync()); Assert.Equal((3m, 5m), await Balance(f, ids.source));
    }
    [Fact]
    public async Task LegacyStockUnitNeedsExplicitReviewAndIsAuditedWithoutQuantityConversion()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var item = await Farm(f).AddStock("LEGACY-UNIT", "Legacy unit", 2, default, "KG"); await Farm(f).MoveStock(item.Id, 3, "legacy-unit", default);
        // Only a legacy/import fixture may have an unknown unit; new API products require one.
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockItems SET UnitOfMeasure = {""} WHERE Id = {item.Id}"); f.Db.ChangeTracker.Clear();
        item = await f.Db.StockItems.SingleAsync(x => x.Id == item.Id);
        var input = new ProductEditor { Id = item.Id, SKU = item.SKU, Name = item.Name, UnitOfMeasure = "kg", RowVersion = item.RowVersion.ToArray() };
        var unconfirmed = Catalog(f); Assert.IsType<ViewResult>(await unconfirmed.Save(input, default));
        Assert.Equal("", (await f.Db.StockItems.SingleAsync()).UnitOfMeasure); Assert.True(input.RequiresUnitReview);
        input.ReviewedExistingUnit = true;
        Assert.IsType<RedirectToActionResult>(await Catalog(f).Save(input, default));
        Assert.Equal("KG", (await f.Db.StockItems.SingleAsync()).UnitOfMeasure); Assert.Equal((3m, 6m), await Balance(f, item.Id));
        Assert.Contains(await f.Db.Audits.ToListAsync(), x => x.Action == "Product.UnitReviewed" && x.Reference.Contains("KG"));
        input.RowVersion = item.RowVersion.ToArray(); input.UnitOfMeasure = "L"; var change = Catalog(f); Assert.IsType<ViewResult>(await change.Save(input, default));
        Assert.False(change.ModelState.IsValid); Assert.Equal("KG", (await f.Db.StockItems.SingleAsync()).UnitOfMeasure);
    }
    [Fact]
    public async Task NewCatalogProductRequiresExplicitNormalizedUnitAndRejectsCompanyMismatch()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var ids = await Seed(f);
        var missing = Catalog(f); Assert.IsType<ViewResult>(await missing.Save(new ProductEditor { SKU = "MISSING", Name = "Missing unit" }, default));
        Assert.False(missing.ModelState.IsValid);
        f.ActAs(branch: 12);
        var conflicting = Catalog(f); Assert.IsType<ViewResult>(await conflicting.Save(new ProductEditor { SKU = "FEED", Name = "Wrong unit", UnitOfMeasure = "BAG25KG" }, default));
        Assert.False(conflicting.ModelState.IsValid);
        var fresh = Catalog(f); Assert.IsType<RedirectToActionResult>(await fresh.Save(new ProductEditor { SKU = "MILK", Name = "Milk stock", UnitOfMeasure = " l " }, default));
        Assert.Equal("L", (await f.Db.StockItems.SingleAsync(x => x.SKU == "MILK")).UnitOfMeasure);
    }

    private sealed class FailTransferSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<StockTransfer>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("Injected failure after physical balances and journals have been saved inside the transaction.");
            return ValueTask.FromResult(result);
        }
    }
}
