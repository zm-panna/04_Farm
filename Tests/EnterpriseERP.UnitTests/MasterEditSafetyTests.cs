using System.ComponentModel.DataAnnotations;
using System.Globalization;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseERP.UnitTests;

public sealed class MasterEditSafetyTests
{
    private static FarmService Farm(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions);
    private static ProductCatalogController Catalog(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions)
    {
        ControllerContext = new ControllerContext { HttpContext = f.Http },
        Url = new UrlHelper(new ActionContext(f.Http, new RouteData(), new ActionDescriptor())),
        TempData = new TempDataDictionary(f.Http, f.Services.GetRequiredService<ITempDataProvider>())
    };
    private static AnimalRegistryController Registry(SecurityTestScope f) => new(f.Db, f.Tenant, f.Permissions, Farm(f));
    private static async Task<ProductEditor> Edit(SecurityTestScope f, int id)
        => Assert.IsType<ProductEditor>(Assert.IsType<ViewResult>(await Catalog(f).Edit(id, default)).Model);
    private static async Task<StockItem> SeedProduct(SecurityTestScope f)
    {
        await f.GrantAsync("*");
        var item = new StockItem { CompanyId = 1, BranchId = 11, SKU = "ORIGINAL", Name = "Original name", UnitOfMeasure = "KG", QuantityOnHand = 1.25m, UnitCost = 2.5m };
        f.Db.StockItems.Add(item); await f.Db.SaveChangesAsync();
        f.Db.InventoryCostBalances.Add(new InventoryCostBalance { CompanyId = 1, BranchId = 11, StockItemId = item.Id, Value = 3.125m });
        f.Db.StockMovements.Add(new StockMovement { CompanyId = 1, BranchId = 11, StockItemId = item.Id, QuantityDelta = 1.25m, Reference = "INITIAL" });
        await f.Db.SaveChangesAsync(); return item;
    }
    private static void Validate(Controller controller, object input)
    {
        var errors = new List<ValidationResult>(); Validator.TryValidateObject(input, new ValidationContext(input), errors, true);
        foreach (var error in errors) controller.ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? "", error.ErrorMessage!);
    }
    private static async Task AssertProductUnchanged(SecurityTestScope f, string sku = "ORIGINAL", string name = "Original name", int audits = 0)
    {
        var row = await f.Db.StockItems.AsNoTracking().SingleAsync();
        Assert.Equal(sku, row.SKU); Assert.Equal(name, row.Name); Assert.Equal("KG", row.UnitOfMeasure);
        Assert.Equal(1.25m, row.QuantityOnHand); Assert.Equal(2.5m, row.UnitCost);
        Assert.Equal(3.125m, (await f.Db.InventoryCostBalances.AsNoTracking().SingleAsync()).Value);
        Assert.Equal(1.25m, (await f.Db.StockMovements.AsNoTracking().SingleAsync()).QuantityDelta);
        Assert.Equal(audits, await f.Db.Audits.CountAsync());
    }

    [Fact]
    public async Task TwoEditSnapshotsRejectStaleMetadataWithoutStockValueUnitOrAuditChanges()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var item = await SeedProduct(f);
        var first = await Edit(f, item.Id); var stale = await Edit(f, item.Id);
        Assert.NotNull(first.RowVersion); Assert.NotEmpty(first.RowVersion!); Assert.Equal(first.RowVersion, stale.RowVersion);
        first.SKU = "FIRST-SAVED"; first.Name = "First user's mixed CASE";
        f.Db.ChangeTracker.Clear(); Assert.IsType<RedirectToActionResult>(await Catalog(f).Save(first, default));
        var savedToken = (await f.Db.StockItems.AsNoTracking().SingleAsync()).RowVersion;
        Assert.False(savedToken.SequenceEqual(stale.RowVersion!));
        stale.SKU = "STALE-OVERWRITE"; stale.Name = "Stale overwrite"; stale.QuantityOnHand = 999; stale.UnitCost = 999;
        f.Db.ChangeTracker.Clear(); var controller = Catalog(f);
        var result = Assert.IsType<ViewResult>(await controller.Save(stale, default));
        Assert.Equal(409, result.StatusCode); Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState.Values.SelectMany(x => x.Errors), x => x.ErrorMessage.Contains("Reload before saving"));
        await AssertProductUnchanged(f, first.SKU, first.Name, 1);
        Assert.Equal(savedToken, (await f.Db.StockItems.AsNoTracking().SingleAsync()).RowVersion);
        Assert.Empty(f.Db.ChangeTracker.Entries<AuditEntry>());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("incorrect")]
    public async Task ExistingProductNeedsItsOriginalNonemptyToken(string token)
    {
        await using var f = await SecurityTestScope.CreateAsync(); var item = await SeedProduct(f); var input = await Edit(f, item.Id);
        input.RowVersion = token == "missing" ? null : token == "empty" ? [] : [1, 2, 3];
        input.SKU = "BAD-TOKEN"; input.Name = "Should not save"; var controller = Catalog(f);
        Assert.Equal(409, Assert.IsType<ViewResult>(await controller.Save(input, default)).StatusCode);
        await AssertProductUnchanged(f); Assert.Empty(f.Db.ChangeTracker.Entries<AuditEntry>());
    }

    [Fact]
    public async Task RepeatedSuccessfulPostCannotCreateAnotherUpdateAudit()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var item = await SeedProduct(f); var input = await Edit(f, item.Id);
        input.Name = "Saved once"; Assert.IsType<RedirectToActionResult>(await Catalog(f).Save(input, default));
        f.Db.ChangeTracker.Clear(); Assert.Equal(409, Assert.IsType<ViewResult>(await Catalog(f).Save(input, default)).StatusCode);
        await AssertProductUnchanged(f, name: "Saved once", audits: 1);
    }

    [Fact]
    public async Task ProviderConcurrencyConflictRollsBackMetadataAndQueuedAudits()
    {
        var race = new ChangeProductTokenBeforeSave(); await using var f = await SecurityTestScope.CreateAsync(race);
        var item = await SeedProduct(f); var input = await Edit(f, item.Id); input.Name = "Losing update";
        race.Enabled = true; var controller = Catalog(f);
        Assert.Equal(409, Assert.IsType<ViewResult>(await controller.Save(input, default)).StatusCode);
        Assert.True(race.ChangedToken); await AssertProductUnchanged(f); Assert.Empty(f.Db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("10.001")]
    [InlineData("1000000000.125")]
    [InlineData("999999999999999.999")]
    public async Task NewAnimalAcceptsExactServiceSupportedCostAndPostsMatchingAcquisition(string text)
    {
        var amount = decimal.Parse(text, CultureInfo.InvariantCulture);
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Livestock.Create");
        var input = new AnimalEditor { Tag = " amount-1 ", Breed = " Mixed CASE breed ", PurchaseCost = amount, CurrentWeightKg = 123.456m };
        var controller = Registry(f); Validate(controller, input); Assert.True(controller.ModelState.IsValid);
        Assert.IsType<RedirectToActionResult>(await controller.Save(input));
        var animal = await f.Db.Animals.AsNoTracking().SingleAsync(); Assert.Equal(amount, animal.PurchaseCost);
        Assert.Equal("AMOUNT-1", animal.Tag); Assert.Equal("Mixed CASE breed", animal.Breed); Assert.Equal(123.456m, animal.CurrentWeightKg);
        Assert.Equal(amount, (await f.Db.FinanceDocuments.AsNoTracking().SingleAsync()).Amount);
        var lines = await f.Db.VoucherLines.AsNoTracking().ToListAsync();
        Assert.Equal(amount, lines.Sum(x => x.Debit)); Assert.Equal(amount, lines.Sum(x => x.Credit));
    }

    [Theory]
    [InlineData("-0.001")]
    [InlineData("10.0001")]
    [InlineData("1000000000000000")]
    public async Task NewAnimalRejectsInvalidCostWithoutAcquisitionOrAudit(string text)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Livestock.Create");
        var controller = Registry(f); var input = new AnimalEditor { Tag = "BAD-AMOUNT", Breed = "Cattle", PurchaseCost = decimal.Parse(text, CultureInfo.InvariantCulture) };
        Validate(controller, input); Assert.IsType<ViewResult>(await controller.Save(input)); Assert.False(controller.ModelState.IsValid);
        Assert.Empty(await f.Db.Animals.ToListAsync()); Assert.Empty(await f.Db.FinanceDocuments.ToListAsync());
        Assert.Empty(await f.Db.Vouchers.ToListAsync()); Assert.Empty(await f.Db.Audits.ToListAsync());
    }

    [Fact]
    public async Task MetadataEditAcceptsExistingCostAboveOldUiCapWithoutChangingAcquisition()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var animal = await Farm(f).AddAnimal("LARGE-COST", "Original breed", 1000000000.125m, default, 123.456m);
        var input = Assert.IsType<AnimalEditor>(Assert.IsType<ViewResult>(await Registry(f).Edit(animal.Id)).Model);
        input.Tag = "updated-tag"; input.Breed = "Updated mixed CASE";
        var controller = Registry(f); Validate(controller, input); Assert.True(controller.ModelState.IsValid);
        Assert.IsType<RedirectToActionResult>(await controller.Save(input));
        var saved = await f.Db.Animals.AsNoTracking().SingleAsync(); Assert.Equal("UPDATED-TAG", saved.Tag); Assert.Equal(input.Breed, saved.Breed);
        Assert.Equal(1000000000.125m, saved.PurchaseCost); Assert.Equal(123.456m, saved.CurrentWeightKg);
        Assert.Equal(1000000000.125m, (await f.Db.FinanceDocuments.SingleAsync()).Amount);
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "Animal.Edit").ToListAsync());
    }

    [Fact]
    public async Task ExpandedCostRangeDoesNotAllowRewritingAcquisitionOrCurrentWeight()
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("*");
        var animal = await Farm(f).AddAnimal("LOCKED", "Original breed", 1000000000.125m, default, 123.456m);
        var auditCount = await f.Db.Audits.CountAsync(); var originalToken = animal.RowVersion.ToArray();
        foreach (var changedCost in new[] { 10.001m, 1000000000.126m })
        {
            var input = new AnimalEditor { Id = animal.Id, Tag = "CHANGED", Breed = "Changed breed", PurchaseCost = changedCost, CurrentWeightKg = 123.456m, RowVersion = originalToken };
            var controller = Registry(f); Validate(controller, input); Assert.True(controller.ModelState.IsValid);
            Assert.IsType<ViewResult>(await controller.Save(input)); Assert.Contains("locked", controller.ModelState[nameof(input.PurchaseCost)]!.Errors.Single().ErrorMessage);
        }
        var weightController = Registry(f);
        Assert.IsType<ViewResult>(await weightController.Save(new AnimalEditor { Id = animal.Id, Tag = animal.Tag, Breed = animal.Breed, PurchaseCost = animal.PurchaseCost, CurrentWeightKg = 124, RowVersion = originalToken }));
        var saved = await f.Db.Animals.AsNoTracking().SingleAsync(); Assert.Equal(1000000000.125m, saved.PurchaseCost); Assert.Equal(123.456m, saved.CurrentWeightKg);
        Assert.Equal("LOCKED", saved.Tag); Assert.Equal("Original breed", saved.Breed); Assert.Equal(originalToken, saved.RowVersion);
        Assert.Equal(auditCount, await f.Db.Audits.CountAsync()); Assert.Equal(1000000000.125m, (await f.Db.FinanceDocuments.SingleAsync()).Amount);
    }

    [Theory]
    [InlineData("sku")]
    [InlineData("name")]
    public async Task ProductUiAndApiRejectEmbeddedControlCharacters(string field)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Inventory.Create");
        var input = new ProductEditor { SKU = field == "sku" ? "BAD\tSKU" : "VALID-SKU", Name = field == "name" ? "Bad\nname" : "Valid name", UnitOfMeasure = "KG" };
        var controller = Catalog(f); Assert.IsType<ViewResult>(await controller.Save(input, default)); Assert.False(controller.ModelState.IsValid);
        await Assert.ThrowsAsync<ArgumentException>(() => Farm(f).AddStock(input.SKU, input.Name, 0, default, "KG"));
        Assert.Empty(await f.Db.StockItems.ToListAsync()); Assert.Empty(await f.Db.Audits.ToListAsync());
    }

    [Theory]
    [InlineData("sku")]
    [InlineData("name")]
    public async Task ProductEditRejectsControlCharactersWithoutChangingExistingMetadata(string field)
    {
        await using var f = await SecurityTestScope.CreateAsync(); var item = await SeedProduct(f); var input = await Edit(f, item.Id);
        if (field == "sku") input.SKU = "BAD\tSKU"; else input.Name = "Bad\nname";
        var controller = Catalog(f); Assert.IsType<ViewResult>(await controller.Save(input, default)); Assert.False(controller.ModelState.IsValid);
        await AssertProductUnchanged(f);
    }

    [Theory]
    [InlineData("tag")]
    [InlineData("breed")]
    public async Task AnimalEditRejectsControlCharactersWithoutChangingExistingMetadata(string field)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Livestock.Edit");
        var animal = new Animal { CompanyId = 1, BranchId = 11, Tag = "SAFE-TAG", Breed = "Safe breed", PurchaseCost = 10.001m, CurrentWeightKg = 123.456m };
        f.Db.Animals.Add(animal); await f.Db.SaveChangesAsync();
        var input = new AnimalEditor { Id = animal.Id, Tag = field == "tag" ? "BAD\tTAG" : animal.Tag, Breed = field == "breed" ? "Bad\nbreed" : animal.Breed, PurchaseCost = animal.PurchaseCost, CurrentWeightKg = animal.CurrentWeightKg, RowVersion = animal.RowVersion.ToArray() };
        var controller = Registry(f); Assert.IsType<ViewResult>(await controller.Save(input)); Assert.False(controller.ModelState.IsValid);
        var saved = await f.Db.Animals.AsNoTracking().SingleAsync(); Assert.Equal("SAFE-TAG", saved.Tag); Assert.Equal("Safe breed", saved.Breed);
        Assert.Equal(10.001m, saved.PurchaseCost); Assert.Equal(123.456m, saved.CurrentWeightKg); Assert.Empty(await f.Db.Audits.ToListAsync());
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(61, 10)]
    [InlineData(10, 1)]
    [InlineData(10, 161)]
    public async Task ProductUiAndApiRejectSameTextLengthViolations(int skuLength, int nameLength)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Inventory.Create");
        var input = new ProductEditor { SKU = new string('S', skuLength), Name = new string('n', nameLength), UnitOfMeasure = "KG" };
        var controller = Catalog(f); Validate(controller, input); Assert.IsType<ViewResult>(await controller.Save(input, default));
        await Assert.ThrowsAsync<ArgumentException>(() => Farm(f).AddStock(input.SKU, input.Name, 0, default, "KG"));
        Assert.Empty(await f.Db.StockItems.ToListAsync()); Assert.Empty(await f.Db.Audits.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductUiAndApiAcceptMaximumTextLengthsAndPreserveNameCase(bool viaApi)
    {
        await using var f = await SecurityTestScope.CreateAsync(); await f.GrantAsync("Inventory.Create");
        var sku = new string('s', 60); var name = "Mixed CASE " + new string('n', 149);
        if (viaApi) await Farm(f).AddStock(sku, name, 0, default, "kg");
        else
        {
            var input = new ProductEditor { SKU = sku, Name = name, UnitOfMeasure = "kg" }; var controller = Catalog(f);
            Validate(controller, input); Assert.True(controller.ModelState.IsValid); Assert.IsType<RedirectToActionResult>(await controller.Save(input, default));
        }
        var item = await f.Db.StockItems.AsNoTracking().SingleAsync(); Assert.Equal(sku.ToUpperInvariant(), item.SKU); Assert.Equal(name, item.Name); Assert.Equal("KG", item.UnitOfMeasure);
    }

    private sealed class ChangeProductTokenBeforeSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public bool ChangedToken { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && !ChangedToken && eventData.Context!.ChangeTracker.Entries<StockItem>().Any(x => x.State == EntityState.Modified))
            {
                var entry = eventData.Context.ChangeTracker.Entries<StockItem>().Single(x => x.State == EntityState.Modified);
                var replacement = Guid.NewGuid().ToByteArray();
                await eventData.Context.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockItems SET RowVersion = {replacement} WHERE Id = {entry.Entity.Id}", cancellationToken);
                ChangedToken = true;
            }
            return result;
        }
    }
}
