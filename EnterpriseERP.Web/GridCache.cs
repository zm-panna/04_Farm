using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
namespace EnterpriseERP.Web;

public sealed class GridCacheOptions
{
 public bool Enabled {get;set;}
 public string Connection {get;set;}="";
 public string KeyPrefix {get;set;}="farm:grid:v1";
 public int TtlSeconds {get;set;}=20;
 public int TimeoutMilliseconds {get;set;}=400;
 public static GridCacheOptions Read(IConfiguration configuration,bool development)
 {
  var o=configuration.GetSection("Scaling:Cache").Get<GridCacheOptions>()??new();
  if(o.TtlSeconds is <1 or >120||o.TimeoutMilliseconds is <50 or >1000||o.KeyPrefix.Length is <3 or >64||o.KeyPrefix.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ':' and not '-' and not '_'))throw new InvalidOperationException("Invalid bounded cache settings.");
  if(o.Enabled)
  {
   if(string.IsNullOrWhiteSpace(o.Connection))throw new InvalidOperationException("Provide the enabled Redis cache connection through secure configuration.");
   var parsed=ConfigurationOptions.Parse(o.Connection);
   if(!development&&!parsed.Ssl)throw new InvalidOperationException("Production Redis caching requires TLS. Configure a trusted Redis endpoint securely.");
  }
  return o;
 }
}
public interface IGridCacheStore
{
 Task<string?> Read(string key,CancellationToken ct);
 Task Write(string key,string value,TimeSpan ttl,CancellationToken ct);
 Task<bool> TryLease(string key,string owner,CancellationToken ct);
 Task Release(string key,string owner,CancellationToken ct);
}
public sealed class RedisGridCacheStore(GridCacheOptions options):IGridCacheStore,IAsyncDisposable
{
 private readonly object sync=new(); private Task<ConnectionMultiplexer>? connecting;
 private DateTimeOffset unavailableUntil;
 private async Task<IDatabase?> Database(CancellationToken ct)
 {
  if(!options.Enabled||DateTimeOffset.UtcNow<unavailableUntil)return null;
  try
  {
   Task<ConnectionMultiplexer> task;
   lock(sync)
   {
    if(connecting is null||connecting.IsFaulted||connecting.IsCanceled)
    {
     var c=ConfigurationOptions.Parse(options.Connection);c.AbortOnConnectFail=false;c.ConnectRetry=0;
     c.ConnectTimeout=options.TimeoutMilliseconds;c.AsyncTimeout=options.TimeoutMilliseconds;c.SyncTimeout=options.TimeoutMilliseconds;
     c.ClientName="FarmERP-bounded-grid-cache";connecting=ConnectionMultiplexer.ConnectAsync(c);
    }
    task=connecting;
   }
   var connection=await task.WaitAsync(TimeSpan.FromMilliseconds(options.TimeoutMilliseconds),ct);
   if(!connection.IsConnected){unavailableUntil=DateTimeOffset.UtcNow.AddSeconds(2);return null;}
   return connection.GetDatabase();
  }
  catch(Exception e) when(IsCacheFailure(e,ct)){unavailableUntil=DateTimeOffset.UtcNow.AddSeconds(2);return null;}
 }
 private static bool IsCacheFailure(Exception e,CancellationToken ct)=>!ct.IsCancellationRequested&&(e is RedisException or TimeoutException or InvalidOperationException or System.Net.Sockets.SocketException);
 public async Task<string?> Read(string key,CancellationToken ct)
 {try{var db=await Database(ct);return db is null?null:(string?)await db.StringGetAsync(key).WaitAsync(TimeSpan.FromMilliseconds(options.TimeoutMilliseconds),ct);}catch(Exception e)when(IsCacheFailure(e,ct)){unavailableUntil=DateTimeOffset.UtcNow.AddSeconds(2);return null;}}
 public async Task Write(string key,string value,TimeSpan ttl,CancellationToken ct)
 {if(Encoding.UTF8.GetByteCount(value)>65536)return;try{var db=await Database(ct);if(db!=null)await db.StringSetAsync(key,value,ttl).WaitAsync(TimeSpan.FromMilliseconds(options.TimeoutMilliseconds),ct);}catch(Exception e)when(IsCacheFailure(e,ct)){unavailableUntil=DateTimeOffset.UtcNow.AddSeconds(2);}}
 public async Task<bool> TryLease(string key,string owner,CancellationToken ct)
 {try{var db=await Database(ct);return db!=null&&await db.StringSetAsync(key+":lease",owner,TimeSpan.FromSeconds(5),When.NotExists).WaitAsync(TimeSpan.FromMilliseconds(options.TimeoutMilliseconds),ct);}catch(Exception e)when(IsCacheFailure(e,ct)){return false;}}
 public async Task Release(string key,string owner,CancellationToken ct)
 {try{var db=await Database(ct);if(db!=null)await db.ScriptEvaluateAsync("if redis.call('get',KEYS[1]) == ARGV[1] then return redis.call('del',KEYS[1]) else return 0 end",[key+":lease"],[owner]).WaitAsync(TimeSpan.FromMilliseconds(options.TimeoutMilliseconds),ct);}catch(Exception e)when(IsCacheFailure(e,ct)){} }
 public async ValueTask DisposeAsync(){if(connecting is {IsCompletedSuccessfully:true})await connecting.Result.DisposeAsync();}
}
public sealed class GridCache(AppDbContext db,IGridCacheStore store,GridCacheOptions options)
{
 private static readonly SemaphoreSlim[] stripes=Enumerable.Range(0,128).Select(_=>new SemaphoreSlim(1,1)).ToArray();
 public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
 public static string Key(string prefix,int company,int branch,string user,string language,string generation,string request)=>$"{prefix}:c{company}:b{branch}:u{Hash(user)}:l{GridRequest.Language(language)}:g{Hash(generation)}:q{Hash(request)}";
 private async Task<string> Generation(int company,int branch,CancellationToken ct)
 {
  var rows=await db.ScaleCacheEpochs.AsNoTracking().Where(x=>x.CompanyId==company&&(x.ScopeBranchId==0||x.ScopeBranchId==branch)).OrderBy(x=>x.ScopeBranchId).Select(x=>x.ScopeBranchId+":"+x.Token).ToListAsync(ct);
  return string.Join("|",rows);
 }
 public async Task<GridPage> Get(int company,int branch,string user,string language,string request,Func<Task<GridPage>> factory,CancellationToken ct)
 {
  if(!options.Enabled)return await factory();
  var generation=await Generation(company,branch,ct);var key=Key(options.KeyPrefix,company,branch,user,language,generation,request);
  static GridPage? Decode(string? value)
  {
   if(value is null||value.Length>65536)return null;
   try
   {
    var page=JsonSerializer.Deserialize<GridPage>(value);
    if(page is null||page.RecordsTotal<0||page.RecordsFiltered<0||page.RecordsFiltered>page.RecordsTotal||page.Data is null||page.Data.Count>100)return null;
    if(page.Data.Any(x=>x is null||x.Id<=0||x.Reference is null||x.Description is null||x.Status is null||x.Date is null||x.Value is null||x.Url is null))return null;
    return page;
   }catch(JsonException){return null;}
  }
  if(Decode(await store.Read(key,ct)) is { } hit)return hit;
  var gate=stripes[Convert.FromHexString(Hash(key))[0]%stripes.Length];
  if(!await gate.WaitAsync(TimeSpan.FromSeconds(2),ct))return await factory();
  try
  {
   if(Decode(await store.Read(key,ct)) is { } second)return second;
   var owner=Guid.NewGuid().ToString("N");var lease=await store.TryLease(key,owner,ct);
   if(!lease)
   {
    // Wait only briefly for another node. Outage/long query falls back to SQL, never stale data.
    for(var i=0;i<2;i++){await Task.Delay(50,ct);if(Decode(await store.Read(key,ct)) is { } ready)return ready;}
    return await factory();
   }
   try
   {
    var page=await factory();
    if(generation==await Generation(company,branch,ct))await store.Write(key,JsonSerializer.Serialize(page),TimeSpan.FromSeconds(options.TtlSeconds),ct);
    return page;
   }
   finally{await store.Release(key,owner,ct);}
  }
  finally{gate.Release();}
 }
}
