using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using EnterpriseERP.Web.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using ZXing;

namespace EnterpriseERP.UnitTests;
public sealed class ScanWorkflowTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Instant = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Instant; }
    private static ScanService Service(SecurityTestScope f, TimeProvider? clock = null) => new(f.Db, f.Tenant, f.Permissions,
        new(f.Db, f.Tenant, f.Permissions, clock), new(f.Db, f.Tenant, f.Permissions, clock), new(f.Db, f.Tenant, f.Permissions, clock),
        new(f.Db, f.Tenant, f.Permissions, clock), new(f.Db, f.Tenant, f.Permissions, clock), new(f.Db, f.Tenant, f.Permissions), clock);
    private static async Task<(StockItem item, Animal animal)> Seed(SecurityTestScope f)
    {
        await f.GrantAsync("*"); await f.GrantAsync("*", "approver-a"); await CompanyBusinessPolicyTestData.SeedAsync(f);
        var item = await new FarmService(f.Db, f.Tenant, f.Permissions).AddStock("FEED", "Synthetic feed", 2, default, "KG");
        await new FarmService(f.Db, f.Tenant, f.Permissions).MoveStock(item.Id, 10, "OPEN-SCAN", default);
        var animal = await new FarmService(f.Db, f.Tenant, f.Permissions).AddAnimal("AN-SCAN", "Synthetic breed", 0, default);
        f.Db.ChangeTracker.Clear(); return (item, animal);
    }
    private static Task<ScanLabelView> Label(SecurityTestScope f, string kind, long id, string format = "Internal", string value = "") => Service(f).CreateLabel(new() { Kind = kind, TargetId = id, Format = format, Value = value });
    private static ScanDraftInput Input(long id, string action, decimal quantity = 1) => new() { RequestKey = Guid.NewGuid().ToString("N"), LabelId = id, Action = action, Quantity = quantity, Notes = "Synthetic operator-reviewed scan entry" };
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka")).DateTime);
    private static async Task<decimal> Quantity(SecurityTestScope f, int id) => await f.Db.StockItems.AsNoTracking().Where(x => x.Id == id).Select(x => x.QuantityOnHand).SingleAsync();

    [Fact]
    public async Task ResolveAndDraftAreReadOnlyForStockAndMoneyUntilExplicitConfirmation()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var seed = await Seed(f); var label = await Label(f, "Product", seed.item.Id);
        var vouchers = await f.Db.Vouchers.CountAsync(); var moves = await f.Db.StockMovements.CountAsync();
        var preview = await Service(f).Resolve("Product", label.Value); Assert.Equal(seed.item.Id, preview.Target.Id); Assert.Equal(10, preview.Target.Available);
        var input = Input(label.Id, "AdjustStock", -2); var draft = await Service(f).Draft(input);
        Assert.Equal(4, draft.Review.Impact.JournalAmount); Assert.Equal("BDT", draft.Review.Impact.CurrencyCode);
        Assert.Equal(10, await Quantity(f, seed.item.Id)); Assert.Equal(vouchers, await f.Db.Vouchers.CountAsync()); Assert.Equal(moves, await f.Db.StockMovements.CountAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => Service(f).Confirm(draft.Id, false));
        var committed = await Service(f).Confirm(draft.Id, true); Assert.Equal("Committed", committed.Status); Assert.Equal(8, await Quantity(f, seed.item.Id));
        var replay = await Service(f).Confirm(draft.Id, true); Assert.Equal(System.Text.Json.JsonSerializer.Serialize(committed), System.Text.Json.JsonSerializer.Serialize(replay)); Assert.Equal(vouchers + 1, await f.Db.Vouchers.CountAsync());
        Assert.Equal(moves + 1, await f.Db.StockMovements.CountAsync());
        var journal = await f.Db.Vouchers.Include(x => x.Lines).SingleAsync(x => x.Reference == "ADJ-" + committed.BusinessReference);
        Assert.Equal(4, journal.Lines.Sum(x => x.Debit)); Assert.Equal(journal.Lines.Sum(x => x.Debit), journal.Lines.Sum(x => x.Credit));
        Assert.Single(await f.Db.Audits.Where(x => x.Action == "Scanning.Confirmed.AdjustStock").ToListAsync());
    }
    [Fact]
    public async Task DraftSubmissionIdempotencyRejectsChangedPayload()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var seed = await Seed(f); var label = await Label(f,"Product",seed.item.Id);
        var input = Input(label.Id,"AdjustStock",1); var first = await Service(f).Draft(input); var second = await Service(f).Draft(input);
        Assert.Equal(first.Id, second.Id); input.Quantity = 2; await Assert.ThrowsAsync<ScanWorkflowException>(() => Service(f).Draft(input));
        Assert.Single(await f.Db.ScanIntents.ToListAsync()); Assert.Equal(10,await Quantity(f,seed.item.Id));
    }
    [Fact]
    public async Task CancellationAndExpiryCannotPostAndRemainOwned()
    {
        await using var f = await SecurityTestScope.CreateAsync(); var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id);
        var draft=await Service(f).Draft(Input(label.Id,"AdjustStock"));await Service(f).Cancel(draft.Id);await Service(f).Cancel(draft.Id);
        Assert.Equal("Cancelled",(await Service(f).Detail(draft.Id)).Status);await Assert.ThrowsAsync<ScanWorkflowException>(()=>Service(f).Confirm(draft.Id,true));
        var clock=new Clock();var expiring=await Service(f,clock).Draft(Input(label.Id,"AdjustStock"));clock.Instant=clock.Instant.AddMinutes(11);
        await Assert.ThrowsAsync<ScanWorkflowException>(()=>Service(f,clock).Confirm(expiring.Id,true));Assert.Equal(10,await Quantity(f,seed.item.Id));
    }
    [Theory]
    [InlineData("operator-b",2,21)] [InlineData("approver-a",1,11)] [InlineData("operator-a",1,12)]
    public async Task GuessedDraftIdsCannotCrossUserCompanyOrBranch(string user,int company,int branch)
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);await f.GrantAsync("*","operator-b",2);
        f.Db.UserBranchAccess.Add(new(){UserId="operator-a",CompanyId=1,BranchId=12});await f.Db.SaveChangesAsync();
        var label=await Label(f,"Product",seed.item.Id);var draft=await Service(f).Draft(Input(label.Id,"AdjustStock"));f.ActAs(user,company,branch);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service(f).Detail(draft.Id));await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service(f).Confirm(draft.Id,true));await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service(f).Cancel(draft.Id));
        Assert.Equal(10,await Quantity(f,seed.item.Id));
    }
    [Theory]
    [InlineData("Product")] [InlineData("Animal")]
    public async Task ScanUseAloneCannotExposeTargetsOrMapLabels(string kind)
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var label=await Label(f,kind,kind=="Product"?seed.item.Id:seed.animal.Id);
        await f.Db.UserPermissions.Where(x=>x.UserId=="operator-a").ExecuteDeleteAsync();await f.GrantAsync("Scanning.Use");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Resolve(kind,label.Value));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).CreateLabel(new(){Kind=kind,TargetId=1}));
    }
    [Fact]
    public async Task ProductViewWithoutMoveSeesNoActionAndCannotDraft()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id);
        await f.Db.UserPermissions.Where(x=>x.UserId=="operator-a").ExecuteDeleteAsync();await f.GrantAsync("Scanning.Use");await f.GrantAsync("Inventory.View");
        Assert.Empty((await Service(f).Resolve("Product",label.Value)).Actions);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Draft(Input(label.Id,"AdjustStock")));
    }
    [Fact]
    public async Task RevocationAfterDraftBlocksCommitAndPreventsValueReuse()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id,"Code128","FEED-SCAN-01");
        var draft=await Service(f).Draft(Input(label.Id,"AdjustStock"));await Service(f).RevokeLabel(label.Id,"Damaged synthetic label");
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service(f).Resolve("Product",label.Value));await Assert.ThrowsAsync<ScanWorkflowException>(()=>Service(f).Confirm(draft.Id,true));
        await Assert.ThrowsAsync<ScanWorkflowException>(()=>Label(f,"Product",seed.item.Id,"Code128",label.Value));await Assert.ThrowsAsync<ScanWorkflowException>(()=>Service(f).PrintLabel(label.Id));
        Assert.Equal(10,await Quantity(f,seed.item.Id));Assert.Equal("Pending",(await Service(f).Detail(draft.Id)).Status);
    }
    [Theory]
    [InlineData("stock")] [InlineData("permission")] [InlineData("balance")]
    public async Task ChangesBetweenPreviewAndCommitFailClosed(string change)
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id);var draft=await Service(f).Draft(Input(label.Id,"AdjustStock"));
        if(change=="stock")await new FarmService(f.Db,f.Tenant,f.Permissions).MoveStock(seed.item.Id,1,"OTHER",default);
        if(change=="permission"){await f.Db.UserPermissions.Where(x=>x.UserId=="operator-a").ExecuteDeleteAsync();await f.GrantAsync("Scanning.Use");await f.GrantAsync("Inventory.View");}
        if(change=="balance")await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE InventoryCostBalances SET Value={21m} WHERE StockItemId={seed.item.Id}");
        var before=await f.Db.Vouchers.CountAsync();await Assert.ThrowsAnyAsync<Exception>(()=>Service(f).Confirm(draft.Id,true));
        Assert.Equal(before,await f.Db.Vouchers.CountAsync());Assert.Equal("Pending",(await f.Db.ScanIntents.AsNoTracking().SingleAsync()).Status);
    }
    [Fact]
    public async Task InjectedFailureRollsBackDraftConsumptionJournalBalanceAndAudit()
    {
        var failure=new FailScanSave();await using var f=await SecurityTestScope.CreateAsync(failure);var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id);var draft=await Service(f).Draft(Input(label.Id,"AdjustStock",-1));
        var vouchers=await f.Db.Vouchers.CountAsync();var moves=await f.Db.StockMovements.CountAsync();failure.Enabled=true;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Confirm(draft.Id,true));failure.Enabled=false;
        Assert.Equal(10,await Quantity(f,seed.item.Id));Assert.Equal(vouchers,await f.Db.Vouchers.CountAsync());Assert.Equal(moves,await f.Db.StockMovements.CountAsync());
        Assert.Equal("Pending",(await Service(f).Detail(draft.Id)).Status);Assert.False(await f.Db.Audits.AnyAsync(x=>x.Action=="Scanning.Confirmed.AdjustStock"));
        Assert.Equal("Committed",(await Service(f).Confirm(draft.Id,true)).Status);
    }
    [Theory]
    [InlineData("Weight")] [InlineData("Health")] [InlineData("Feed")]
    public async Task AnimalEntriesUseExistingDomainServicesAndReplayOnce(string action)
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var animal=await Label(f,"Animal",seed.animal.Id);var product=await Label(f,"Product",seed.item.Id);
        var input=Input(animal.Id,action,action=="Weight"?123.456m:action=="Feed"?2:0);input.Date=Today;
        if(action=="Health"){input.EventType="Observation";input.NextDueDate=Today.AddDays(5);}if(action=="Feed")input.SecondaryLabelId=product.Id;
        var draft=await Service(f).Draft(input);Assert.Equal(10,await Quantity(f,seed.item.Id));Assert.Empty(await f.Db.AnimalWeightRecords.ToListAsync());Assert.Empty(await f.Db.AnimalHealthRecords.ToListAsync());Assert.Empty(await f.Db.AnimalFeedRecords.ToListAsync());
        await Service(f).Confirm(draft.Id,true);await Service(f).Confirm(draft.Id,true);
        if(action=="Weight"){Assert.Single(await f.Db.AnimalWeightRecords.ToListAsync());Assert.Equal(123.456m,(await f.Db.Animals.AsNoTracking().SingleAsync()).CurrentWeightKg);}
        if(action=="Health"){var row=await f.Db.AnimalHealthRecords.SingleAsync();Assert.Equal("Observation",row.EventType);Assert.Equal(Today.AddDays(5),row.NextDueDate);}
        if(action=="Feed"){Assert.Single(await f.Db.AnimalFeedRecords.ToListAsync());Assert.Equal(8,await Quantity(f,seed.item.Id));Assert.Equal(4,(await f.Db.AnimalFeedRecords.SingleAsync()).CostAmount);}
    }
    [Fact]
    public async Task FeedDraftRejectsRevokedSecondaryAndReservationExhaustion()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var animal=await Label(f,"Animal",seed.animal.Id);var product=await Label(f,"Product",seed.item.Id);
        var input=Input(animal.Id,"Feed",2);input.Date=Today;input.SecondaryLabelId=product.Id;var draft=await Service(f).Draft(input);
        await Service(f).RevokeLabel(product.Id,"Old feed label");await Assert.ThrowsAsync<ScanWorkflowException>(()=>Service(f).Confirm(draft.Id,true));Assert.Empty(await f.Db.AnimalFeedRecords.ToListAsync());
        var fresh=await Label(f,"Product",seed.item.Id);input.SecondaryLabelId=fresh.Id;input.RequestKey=Guid.NewGuid().ToString("N");
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE StockItems SET ReservedQuantity={9m} WHERE Id={seed.item.Id}");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>Service(f).Draft(input));Assert.Equal(10,await Quantity(f,seed.item.Id));
    }
    [Fact]
    public async Task TransferUsesTwoAuthorizedLabelsAndConservesBalances()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);f.Db.UserBranchAccess.Add(new(){UserId="operator-a",CompanyId=1,BranchId=12});await f.Db.SaveChangesAsync();
        var source=await Label(f,"Product",seed.item.Id);f.ActAs(branch:12);var dest=await new FarmService(f.Db,f.Tenant,f.Permissions).AddStock("FEED","Destination feed",2,default,"KG");var destination=await Label(f,"Product",dest.Id);f.ActAs();
        Assert.Equal(12,(await Service(f).Resolve("Product",destination.Value,12)).BranchId);await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service(f).Resolve("Product",destination.Value));
        var input=Input(source.Id,"Transfer",3);input.DestinationBranchId=12;input.SecondaryLabelId=destination.Id;var draft=await Service(f).Draft(input);
        Assert.Equal(6,draft.Review.Impact.JournalAmount);await Service(f).Confirm(draft.Id,true);await Service(f).Confirm(draft.Id,true);
        Assert.Equal(7,await Quantity(f,seed.item.Id));Assert.Equal(3,await Quantity(f,dest.Id));Assert.Single(await f.Db.StockTransfers.ToListAsync());
        foreach(var v in await f.Db.Vouchers.Include(x=>x.Lines).ToListAsync())Assert.Equal(v.Lines.Sum(x=>x.Debit),v.Lines.Sum(x=>x.Credit));
    }
    [Fact]
    public async Task DestinationMembershipRevokedAfterDraftPreventsTransfer()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);f.Db.UserBranchAccess.Add(new(){UserId="operator-a",CompanyId=1,BranchId=12});await f.Db.SaveChangesAsync();
        var source=await Label(f,"Product",seed.item.Id);f.ActAs(branch:12);var dest=await new FarmService(f.Db,f.Tenant,f.Permissions).AddStock("FEED","Destination",2,default,"KG");var destination=await Label(f,"Product",dest.Id);f.ActAs();
        var input=Input(source.Id,"Transfer",3);input.DestinationBranchId=12;input.SecondaryLabelId=destination.Id;var draft=await Service(f).Draft(input);
        await f.Db.UserBranchAccess.Where(x=>x.UserId=="operator-a"&&x.BranchId==12).ExecuteDeleteAsync();await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f).Confirm(draft.Id,true));Assert.Empty(await f.Db.StockTransfers.ToListAsync());
    }
    [Fact]
    public async Task PurchaseReceivingPostsExactlyOnceAndDocumentLookupHasNoMutation()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var ops=new IntegratedOperations(f.Db,f.Tenant,f.Permissions);
        var party=new TradingParty{CompanyId=1,BranchId=11,Kind="Supplier",Code="SUP-SCAN",Name="Synthetic supplier"};f.Db.TradingParties.Add(party);await f.Db.SaveChangesAsync();
        var order=await ops.CreateOrder(party.Id,seed.item.Id,3,2,"PO-SCAN",default);await ops.ApproveOrder(order.Id,default);var label=await Label(f,"PurchaseOrder",order.Id);
        var draft=await Service(f).Draft(Input(label.Id,"ReceiveOrder",2));Assert.Equal(4,draft.Review.Impact.JournalAmount);await Service(f).Confirm(draft.Id,true);await Service(f).Confirm(draft.Id,true);
        Assert.Equal(12,await Quantity(f,seed.item.Id));Assert.Single(await f.Db.PurchaseOrderReceipts.ToListAsync());
        var document=await f.Db.FinanceDocuments.AsNoTracking().SingleAsync(x=>x.Reference=="PUR-"+draft.BusinessReference);var docLabel=await Label(f,"Document",document.Id);var count=await f.Db.Vouchers.CountAsync();
        var preview=await Service(f).Resolve("Document",docLabel.Value);Assert.Empty(preview.Actions);Assert.Equal(document.Reference,preview.Target.Title);Assert.Equal(count,await f.Db.Vouchers.CountAsync());
    }
    [Fact]
    public async Task AssetCustodyUsesReviewedCurrentAssetAndHasNoMoneyPosting()
    {
        await using var f=await SecurityTestScope.CreateAsync();await Seed(f);var assets=new FixedAssetService(f.Db,f.Tenant,f.Permissions);
        var a=await assets.Create(new(){AssetCode="SCAN-PUMP",Name="Synthetic pump",Category="Equipment",SourceReference="AS-SCAN",AcquisitionDate=new(2025,1,1),Cost=1000,AcquisitionSettlement="Cash",DepreciationMethod="None",ResidualValue=1000,DepreciationStart=new(2025,1,1),PolicyReason="Synthetic fixture only",Custodian="Operator A",Location="Shed A"});
        f.ActAs("approver-a");a=await assets.ApproveAcquisition(new(){Id=a.Id,RowVersion=a.RowVersion,ConfirmReviewed=true,ReviewNote="Synthetic independent review"});f.ActAs();a=await assets.PostAcquisition(new(){Id=a.Id,RowVersion=a.RowVersion,ConfirmReviewed=true});
        var label=await Label(f,"Asset",a.Id);var input=Input(label.Id,"Custody",0);input.Date=Today;input.Custodian="Operator B";input.Location="Shed B";var draft=await Service(f).Draft(input);var vouchers=await f.Db.Vouchers.CountAsync();
        Assert.Equal("Operator A",draft.Review.Impact.PreviousCustodian);await Service(f).Confirm(draft.Id,true);await Service(f).Confirm(draft.Id,true);
        Assert.Equal("Operator B",(await f.Db.FixedAssets.AsNoTracking().SingleAsync()).Custodian);Assert.Equal(2,await f.Db.FixedAssetCustodies.CountAsync());Assert.Equal(vouchers,await f.Db.Vouchers.CountAsync());
    }
    [Fact]
    public async Task SourceContextMismatchForeignTargetsAndUnsupportedUrlsFailClosed()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var animal=await Label(f,"Animal",seed.animal.Id);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service(f).Resolve("Product",animal.Value));await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Draft(Input(animal.Id,"AdjustStock")));
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Resolve("Product","https://example.invalid/"));await Assert.ThrowsAsync<KeyNotFoundException>(()=>Label(f,"Product",999999));
        await f.GrantAsync("*","operator-b",2);f.ActAs("operator-b",2,21);await Assert.ThrowsAsync<KeyNotFoundException>(()=>Label(f,"Animal",seed.animal.Id));
    }
    [Theory]
    [InlineData("EAN13","4006381333931")] [InlineData("EAN8","96385074")] [InlineData("UPCA","012345678905")] [InlineData("UPCE","04252614")] [InlineData("Code128","FEED-CODE128")] [InlineData("SKU","FEED")]
    public async Task ExactConfiguredProductFormatsResolve(string format,string value)
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id,format,value);
        Assert.Equal(label.Id,(await Service(f).Resolve("Product",value)).LabelId);
        if(format=="UPCA")Assert.Equal(label.Id,(await Service(f).Resolve("Product","0"+value)).LabelId);
        if(format=="UPCE")Assert.Equal(label.Id,(await Service(f).Resolve("Product","042100005264")).LabelId);
    }
    [Theory]
    [InlineData("EAN13","4006381333932")] [InlineData("EAN8","96385075")] [InlineData("UPCA","012345678906")] [InlineData("UPCE","04252615")] [InlineData("SKU","feed")]
    public async Task InvalidChecksumsAndNonExactSkuMappingsAreRejected(string format,string value)
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);await Assert.ThrowsAsync<ArgumentException>(()=>Label(f,"Product",seed.item.Id,format,value));Assert.Empty(await f.Db.ScanLabels.ToListAsync());
    }
    [Fact]
    public async Task BarcodeAliasesCannotCollideAndSkuZerosAreNeverStripped()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);await Label(f,"Product",seed.item.Id,"UPCA","012345678905");
        await Assert.ThrowsAsync<ScanWorkflowException>(()=>Label(f,"Product",seed.item.Id,"EAN13","0012345678905"));
        var numeric=await new FarmService(f.Db,f.Tenant,f.Permissions).AddStock("036000291452","Numeric SKU",0,default,"EA");await Label(f,"Product",numeric.Id,"SKU","036000291452");
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>Service(f).Resolve("Product","0036000291452"));
    }
    [Fact]
    public async Task DirectEntityMutationCannotChangeMappingsOrCompletedHistory()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id);var draft=await Service(f).Draft(Input(label.Id,"AdjustStock"));
        f.Db.ChangeTracker.Clear();var l=await f.Db.ScanLabels.SingleAsync();l.Value="FORGED";await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());f.Db.ChangeTracker.Clear();
        var d=await f.Db.ScanIntents.SingleAsync();d.Status="Committed";d.CompletedUtc=DateTimeOffset.UtcNow;await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Db.SaveChangesAsync());f.Db.ChangeTracker.Clear();
        Assert.Equal("Pending",(await Service(f).Detail(draft.Id)).Status);
    }
    [Fact]
    public async Task ScopedLabelForeignKeysRejectCrossBranchTargetsEvenWithRawSql()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(()=>f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ScanLabels SET BranchId={12} WHERE Id={label.Id}"));
    }
    [Fact]
    public async Task LabelsAndTargetsAreBoundedAndTenantFilteredBeforePaging()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);for(var i=0;i<25;i++)await Label(f,"Product",seed.item.Id);
        var options=System.Text.Json.JsonSerializer.Serialize(await Service(f).Options("Animal",seed.animal.Tag));using var found=System.Text.Json.JsonDocument.Parse(options);Assert.Equal(1,found.RootElement.GetProperty("total").GetInt32());Assert.Single(found.RootElement.GetProperty("rows").EnumerateArray());
        var json=System.Text.Json.JsonSerializer.Serialize(await Service(f).Labels("Product","",2));using var doc=System.Text.Json.JsonDocument.Parse(json);Assert.Equal(25,doc.RootElement.GetProperty("total").GetInt32());Assert.Equal(5,doc.RootElement.GetProperty("rows").GetArrayLength());
        await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Labels("Product","",0));await Assert.ThrowsAsync<ArgumentException>(()=>Service(f).Options("Product",new string('a',81)));
    }
    [Theory]
    [InlineData("qr")] [InlineData("code128")]
    public void PrintedSvgContainsOnlyGeneratedGeometry(string format)
    {
        var value="FARM1-ANIMAL-"+new string('A',32);var svg=ScanLabelImage.Svg(value,format);Assert.StartsWith("<svg",svg);Assert.Contains("<path",svg);Assert.DoesNotContain(value,svg);Assert.DoesNotContain("script",svg);Assert.DoesNotContain("href",svg);
    }
    [Fact]
    public async Task SeparateRelationalContextsRacingConfirmationPersistOneOutcome()
    {
        await using var f = await SecurityTestScope.CreateSharedAsync(); var seed = await Seed(f); var label = await Label(f, "Product", seed.item.Id);
        var draft = await Service(f).Draft(Input(label.Id, "AdjustStock", -1)); var connection = f.Db.Database.GetConnectionString(); var principal = f.Http.User;
        using var start = new ManualResetEventSlim(false);
        async Task<string> Attempt()
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
            var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal } };
            var tenant = new TenantContext(db, accessor, f.Users); var permissions = new PermissionService(db, tenant);
            var service = new ScanService(db, tenant, permissions, new(db,tenant,permissions),new(db,tenant,permissions),new(db,tenant,permissions),new(db,tenant,permissions),new(db,tenant,permissions),new(db,tenant,permissions));
            start.Wait();
            try { return (await service.Confirm(draft.Id, true)).Status; }
            catch (Exception ex) when (ex is DbUpdateException or Microsoft.Data.Sqlite.SqliteException or ScanWorkflowException) { return "Conflict"; }
        }
        var a = Task.Run(Attempt); var b = Task.Run(Attempt); start.Set(); var outcomes = await Task.WhenAll(a,b);
        Assert.All(outcomes, x => Assert.Contains(x, new[] { "Committed", "Conflict" }));
        // A losing concurrent writer retries the same immutable intent, not a new business operation.
        Assert.Equal("Committed", (await Service(f).Confirm(draft.Id, true)).Status);
        Assert.Equal(9, await Quantity(f,seed.item.Id));
        Assert.Equal(1,await f.Db.StockMovements.CountAsync(x=>x.Reference=="ADJ-"+draft.BusinessReference));
        Assert.Equal(1,await f.Db.Audits.CountAsync(x=>x.Action=="Scanning.Confirmed.AdjustStock"));
    }
    [Fact]
    public async Task DraftFloodLimitDoesNotWriteBusinessData()
    {
        await using var f=await SecurityTestScope.CreateAsync();var seed=await Seed(f);var label=await Label(f,"Product",seed.item.Id);
        for(var i=0;i<20;i++)await Service(f).Draft(Input(label.Id,"AdjustStock",1));
        await Assert.ThrowsAsync<ScanWorkflowException>(()=>Service(f).Draft(Input(label.Id,"AdjustStock",1)));Assert.Equal(10,await Quantity(f,seed.item.Id));
    }
    [Fact]
    public async Task FinancialDraftRequiresConfiguredPolicyAndLeavesNoBusinessWrites()
    {
        await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");
        var stock=await new FarmService(f.Db,f.Tenant,f.Permissions).AddStock("UNCONFIGURED","Synthetic item",0,default,"KG");
        var label=await Label(f,"Product",stock.Id);
        await Assert.ThrowsAsync<ScanWorkflowException>(()=>Service(f).Draft(Input(label.Id,"AdjustStock",1)));
        Assert.Empty(await f.Db.ScanIntents.ToListAsync());Assert.Empty(await f.Db.StockMovements.ToListAsync());Assert.Empty(await f.Db.Vouchers.ToListAsync());
    }
    private sealed class FailScanSave : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        { if(Enabled&&data.Context!.ChangeTracker.Entries<ScanIntent>().Any(x=>x.Entity.Status=="Committed"&&x.State==EntityState.Modified))throw new InvalidOperationException("Injected scan save failure");return ValueTask.FromResult(result); }
    }
}
