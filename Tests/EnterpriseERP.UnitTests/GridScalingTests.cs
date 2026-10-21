using System.Collections.Concurrent;
using EnterpriseERP.Domain;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace EnterpriseERP.UnitTests;

public sealed class GridScalingTests
{
 private static GridRequest R(int start=0,int length=25,string search="",int order=0,bool desc=false,string status="",string language="en")=>new(7,start,length,search,order,desc,status,language);
 private static GridPage Page(string name="row")=>new(1,1,[new(1,"REF",name,"Active","","","/products")]);
 private sealed class Store:IGridCacheStore
 {
  public readonly ConcurrentDictionary<string,string> Values=new(); public int Reads,Writes,Leases;
  public Task<string?> Read(string key,CancellationToken ct){Interlocked.Increment(ref Reads);return Task.FromResult(Values.GetValueOrDefault(key));}
  public Task Write(string key,string value,TimeSpan ttl,CancellationToken ct){Interlocked.Increment(ref Writes);Values[key]=value;return Task.CompletedTask;}
  public Task<bool> TryLease(string key,string owner,CancellationToken ct){Interlocked.Increment(ref Leases);return Task.FromResult(true);}
  public Task Release(string key,string owner,CancellationToken ct)=>Task.CompletedTask;
 }
 private static GridService Service(SecurityTestScope f,Store store,bool enabled=true)=>new(f.Db,f.Permissions,new(f.Db,store,new(){Enabled=enabled}));
 [Theory]
 [InlineData("draw=-1")][InlineData("draw=<script>")][InlineData("length=-1")][InlineData("length=101")][InlineData("length=0")][InlineData("start=2000001")]
 [InlineData("order[0][column]=4")][InlineData("order[0][dir]=DROP")][InlineData("order[1][column]=0")][InlineData("search[regex]=true")]
 [InlineData("draw=1&draw=2")][InlineData("columns[1][search][value]=x")]
 public void RejectsUnboundedOrUnsupportedProtocol(string query)=>Assert.Throws<ArgumentException>(()=>GridRequest.Parse(new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query))));
 [Fact]public void ParsesIntegerDrawAndBoundedLiteralSearch()
 {var r=GridRequest.Parse(new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery("draw=12&start=100&length=50&search[value]=%25_%27&order[0][column]=2&order[0][dir]=asc&language=ar")));Assert.Equal(12,r.Draw);Assert.Equal("%_'",r.Search);Assert.Equal("ar",r.Locale);Assert.False(r.Descending);Assert.DoesNotContain("12",r.CacheIdentity("products"));}
 [Fact]public void KeysSeparateEverySecurityAndLanguageBoundaryAndHideRawIdentifiers()
 {var a=GridCache.Key("test",1,11,"employee@example.invalid","en","epoch1","secret search");foreach(var b in new[]{GridCache.Key("test",2,11,"employee@example.invalid","en","epoch1","secret search"),GridCache.Key("test",1,12,"employee@example.invalid","en","epoch1","secret search"),GridCache.Key("test",1,11,"other","en","epoch1","secret search"),GridCache.Key("test",1,11,"employee@example.invalid","bn","epoch1","secret search"),GridCache.Key("test",1,11,"employee@example.invalid","en","epoch2","secret search")})Assert.NotEqual(a,b);Assert.DoesNotContain("employee@",a);Assert.DoesNotContain("secret search",a);}
 [Fact]public async Task RealRelationalPagesCountAndFilterOnlySelectedTenantAndBranch()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.View");
  for(var i=0;i<135;i++)f.Db.StockItems.Add(new(){CompanyId=1,BranchId=11,SKU=$"P{i:000}",Name=i<32?"Match product":"Other product",UnitOfMeasure="KG"});
  f.Db.StockItems.AddRange(new StockItem{CompanyId=1,BranchId=12,SKU="SECRET1",Name="Match other branch",UnitOfMeasure="KG"},new StockItem{CompanyId=2,BranchId=21,SKU="SECRET2",Name="Match other company",UnitOfMeasure="KG"});await f.Db.SaveChangesAsync();
  var service=Service(f,new(),false);var all=await service.Get("products",R(100,25));Assert.Equal(135,all.RecordsTotal);Assert.Equal(25,all.Data.Count);Assert.Equal("P100",all.Data[0].Reference);
  var match=await service.Get("products",R(25,25,"Match"));Assert.Equal(135,match.RecordsTotal);Assert.Equal(32,match.RecordsFiltered);Assert.Equal(7,match.Data.Count);Assert.DoesNotContain(match.Data,x=>x.Reference.StartsWith("SECRET"));
  Assert.Empty((await service.Get("products",R(search:"%_'"))).Data);
 }
 [Fact]public async Task EveryRegisteredQueryExecutesInSqliteAndTranslatesSqlServerPaging()
 {
  await using var f=await SecurityTestScope.CreateAsync();
  await using var sql=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=localhost;Database=synthetic;Integrated Security=true;TrustServerCertificate=false").Options);
  foreach(var d in GridCatalog.All)
  {
   var actual=GridService.Sort(GridService.Filter(GridCatalog.Query(f.Db,d.Key,(1,11,"operator-a")),R(search:"a")),R()).Skip(3).Take(2);Assert.Empty(await actual.ToListAsync());
   var query=GridService.Sort(GridService.Filter(GridCatalog.Query(sql,d.Key,(1,11,"operator-a")),R(search:"search' value")),R()).Skip(3).Take(2).ToQueryString();
   Assert.Contains("OFFSET",query);Assert.Contains("FETCH NEXT",query);Assert.Contains("CompanyId",query);Assert.Contains("@",query);
  }
 }
 [Fact]public async Task EveryConfiguredGridPermissionIsCheckedBeforeAnyCacheOrRows()
 {await using var f=await SecurityTestScope.CreateAsync();var store=new Store();foreach(var d in GridCatalog.All)await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(f,store).Get(d.Key,R()));Assert.Equal(0,store.Reads);}
 [Fact]public async Task CacheHitThenCommittedMutationChangesSqlGenerationAndPermissionRevocationStillDenies()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.View");var row=new StockItem{CompanyId=1,BranchId=11,SKU="A",Name="Before",UnitOfMeasure="KG"};f.Db.StockItems.Add(row);await f.Db.SaveChangesAsync();var store=new Store();var service=Service(f,store);
  Assert.Equal("Before",(await service.Get("products",R())).Data.Single().Description);await service.Get("products",R());Assert.Equal(1,store.Writes);
  row.Name="After";await f.Db.SaveChangesAsync();Assert.Equal("After",(await service.Get("products",R())).Data.Single().Description);Assert.Equal(2,store.Writes);
  f.Db.UserPermissions.Remove(await f.Db.UserPermissions.SingleAsync());await f.Db.SaveChangesAsync();var reads=store.Reads;await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.Get("products",R()));Assert.Equal(reads,store.Reads);
 }
 [Fact]public async Task RollbackDoesNotInvalidateCommittedGenerationOrExposeUncommittedData()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.View");var row=new StockItem{CompanyId=1,BranchId=11,SKU="A",Name="Committed",UnitOfMeasure="KG"};f.Db.StockItems.Add(row);await f.Db.SaveChangesAsync();var before=await f.Db.ScaleCacheEpochs.AsNoTracking().Where(x=>x.CompanyId==1&&x.ScopeBranchId==11).Select(x=>x.Token).SingleAsync();
  await using(var tx=await f.Db.Database.BeginTransactionAsync()){row.Name="Rolled back";await f.Db.SaveChangesAsync();Assert.NotEqual(before,await f.Db.ScaleCacheEpochs.AsNoTracking().Where(x=>x.CompanyId==1&&x.ScopeBranchId==11).Select(x=>x.Token).SingleAsync());await tx.RollbackAsync();}f.Db.ChangeTracker.Clear();
  Assert.Equal(before,await f.Db.ScaleCacheEpochs.AsNoTracking().Where(x=>x.CompanyId==1&&x.ScopeBranchId==11).Select(x=>x.Token).SingleAsync());Assert.Equal("Committed",(await Service(f,new()).Get("products",R())).Data.Single().Description);
 }
 [Fact]public async Task FinancialRegistersBypassCacheAndNeverExposeMoneyInQuantityColumn()
 {await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("*");var store=new Store();foreach(var name in new[]{"documents","vouchers","payments","payroll-runs","assets","ecommerce-orders"}){Assert.False(GridCatalog.Find(name).Cacheable);await Service(f,store).Get(name,R());}Assert.Equal(0,store.Reads);}
 [Fact]public async Task MalformedCacheFallsBackToSqlAndIsReplaced()
 {await using var f=await SecurityTestScope.CreateAsync();var store=new Store();var cache=new GridCache(f.Db,store,new(){Enabled=true});var calls=0;Task<GridPage> Read(){calls++;return Task.FromResult(Page());}await cache.Get(1,11,"operator-a","en","test",Read,default);foreach(var key in store.Values.Keys)store.Values[key]="not json";await cache.Get(1,11,"operator-a","en","test",Read,default);Assert.Equal(2,calls);}
 [Fact]public async Task AConcurrentGenerationChangePreventsCachingAnObsoletePage()
 {await using var f=await SecurityTestScope.CreateAsync();var store=new Store();var cache=new GridCache(f.Db,store,new(){Enabled=true});await cache.Get(1,11,"operator-a","en","test",async()=>{f.Db.StockItems.Add(new(){CompanyId=1,BranchId=11,SKU="NEW",Name="New",UnitOfMeasure="KG"});await f.Db.SaveChangesAsync();return Page();},default);Assert.Equal(0,store.Writes);}
 [Theory][InlineData("null")][InlineData("{}")][InlineData("{\"RecordsTotal\":-1,\"RecordsFiltered\":0,\"Data\":[]}")]
 public async Task StructurallyInvalidCachePayloadUsesDatabase(string payload)
 {await using var f=await SecurityTestScope.CreateAsync();var store=new Store();var cache=new GridCache(f.Db,store,new(){Enabled=true});int calls=0;Task<GridPage> Read(){calls++;return Task.FromResult(Page());}await cache.Get(1,11,"operator-a","en","test",Read,default);foreach(var key in store.Values.Keys)store.Values[key]=payload;await cache.Get(1,11,"operator-a","en","test",Read,default);Assert.Equal(2,calls);}
 [Fact]public async Task LocalStampedeControlSharesOneFillAcrossSeparateDbContexts()
 {
  var path=Path.Combine(Path.GetTempPath(),"farm-grid-demo-"+Guid.NewGuid().ToString("N")+".db");
  try
  {
   var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source="+path).Options;
   await using(var init=new AppDbContext(options)){await init.Database.EnsureCreatedAsync();init.Companies.Add(new(){Id=1,Name="Synthetic"});await init.SaveChangesAsync();}
   var store=new Store();int calls=0;
   async Task Run(){await using var db=new AppDbContext(options);var cache=new GridCache(db,store,new(){Enabled=true});await cache.Get(1,11,"synthetic","en","same",async()=>{Interlocked.Increment(ref calls);await Task.Delay(150);return Page();},default);}
   await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Run()));Assert.Equal(1,calls);Assert.Equal(1,store.Writes);
  }finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
 }
 [Fact]public void CacheConfigurationIsBoundedAndProductionRequiresTls()
 {IConfiguration Config(params (string,string)[] values)=>new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(x=>"Scaling:Cache:"+x.Item1,x=>(string?)x.Item2)).Build();Assert.False(GridCacheOptions.Read(Config(),false).Enabled);Assert.Throws<InvalidOperationException>(()=>GridCacheOptions.Read(Config(("Enabled","true"),("Connection","localhost:6379")),false));Assert.Throws<InvalidOperationException>(()=>GridCacheOptions.Read(Config(("TtlSeconds","999")),true));Assert.Throws<InvalidOperationException>(()=>GridCacheOptions.Read(Config(("KeyPrefix","invalid space")),true));}
}

public sealed class RedisAvailableFactAttribute:FactAttribute
{public RedisAvailableFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FARM_TEST_REDIS")))Skip="Requires explicitly configured disposable local Redis; no live service is contacted by default.";}}
public sealed class RealRedisGridTests
{
 [RedisAvailableFact]public async Task RealRedisRoundTripTtlLeaseOwnerAndPayloadBound()
 {
  await using var store=new RedisGridCacheStore(new(){Enabled=true,Connection=Environment.GetEnvironmentVariable("FARM_TEST_REDIS")!,TimeoutMilliseconds=1000});var key="farm-test:"+Guid.NewGuid().ToString("N");
  Assert.Null(await store.Read(key,default));await store.Write(key,"synthetic",TimeSpan.FromMilliseconds(300),default);Assert.Equal("synthetic",await store.Read(key,default));
  Assert.True(await store.TryLease(key,"owner",default));Assert.False(await store.TryLease(key,"other",default));await store.Release(key,"other",default);Assert.False(await store.TryLease(key,"other",default));await store.Release(key,"owner",default);Assert.True(await store.TryLease(key,"other",default));await store.Release(key,"other",default);
  await Task.Delay(450);Assert.Null(await store.Read(key,default));await store.Write(key,new string('x',65537),TimeSpan.FromSeconds(1),default);Assert.Null(await store.Read(key,default));
 }
 [RedisAvailableFact]public async Task RealCacheServiceHitInvalidationAndUnavailableServerUseSql()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Inventory.View");var row=new StockItem{CompanyId=1,BranchId=11,SKU="REAL",Name="Before",UnitOfMeasure="KG"};f.Db.StockItems.Add(row);await f.Db.SaveChangesAsync();
  var options=new GridCacheOptions{Enabled=true,Connection=Environment.GetEnvironmentVariable("FARM_TEST_REDIS")!,KeyPrefix="farm-test:"+Guid.NewGuid().ToString("N"),TimeoutMilliseconds=1000};await using var store=new RedisGridCacheStore(options);var cache=new GridCache(f.Db,store,options);int calls=0;
  async Task<GridPage> Read(){calls++;var name=await f.Db.StockItems.AsNoTracking().Select(x=>x.Name).SingleAsync();return new(1,1,[new(1,"REAL",name,"","","","/products")]);}
  Assert.Equal("Before",(await cache.Get(1,11,"operator-a","en","same",Read,default)).Data.Single().Description);await cache.Get(1,11,"operator-a","en","same",Read,default);Assert.Equal(1,calls);
  row.Name="After";await f.Db.SaveChangesAsync();Assert.Equal("After",(await cache.Get(1,11,"operator-a","en","same",Read,default)).Data.Single().Description);Assert.Equal(2,calls);
  await using var down=new RedisGridCacheStore(new(){Enabled=true,Connection="127.0.0.1:1",TimeoutMilliseconds=50});var fallback=new GridCache(f.Db,down,options);Assert.Equal("After",(await fallback.Get(1,11,"operator-a","en","down",Read,default)).Data.Single().Description);Assert.Equal(3,calls);
 }
}
