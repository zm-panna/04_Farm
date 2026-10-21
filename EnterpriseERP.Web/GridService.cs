using System.Globalization;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed class GridService(AppDbContext db,PermissionService permission,GridCache cache)
{
 public static IQueryable<GridRecord> Filter(IQueryable<GridRecord> source,GridRequest request)
 {
  if(request.Search.Length>0)source=source.Where(x=>x.Reference.Contains(request.Search)||x.Description.Contains(request.Search)||x.Status.Contains(request.Search));
  if(request.Status.Length>0)source=source.Where(x=>x.Status==request.Status);
  return source;
 }
 public static IOrderedQueryable<GridRecord> Sort(IQueryable<GridRecord> source,GridRequest request)=> (request.OrderColumn,request.Descending) switch
 {
  (0,false)=>source.OrderBy(x=>x.Id),(0,true)=>source.OrderByDescending(x=>x.Id),
  (1,false)=>source.OrderBy(x=>x.Reference).ThenBy(x=>x.Id),(1,true)=>source.OrderByDescending(x=>x.Reference).ThenBy(x=>x.Id),
  (2,false)=>source.OrderBy(x=>x.Description).ThenBy(x=>x.Id),(2,true)=>source.OrderByDescending(x=>x.Description).ThenBy(x=>x.Id),
  (3,false)=>source.OrderBy(x=>x.Status).ThenBy(x=>x.Id),(3,true)=>source.OrderByDescending(x=>x.Status).ThenBy(x=>x.Id),
  // Mixed business-date/UTC grids deliberately use stable ID ordering rather than a client sort.
  _=>throw new ArgumentException("Sorting is supported for ID, reference, description and status only.")
 };
 public async Task<GridPage> Get(string key,GridRequest request,CancellationToken ct=default)
 {
  var descriptor=GridCatalog.Find(key);var t=await permission.DemandScope(descriptor.Permission,ct);
  // Authorization and scope are always read from SQL BEFORE any cache access.
  async Task<GridPage> Read()
  {
   var source=GridCatalog.Query(db,key,t);var total=await source.CountAsync(ct);var filtered=Filter(source,request);var count=await filtered.CountAsync(ct);
   var rows=await Sort(filtered,request).Skip(request.Start).Take(request.Length).ToListAsync(ct);
   return new(total,count,rows.Select(x=>new GridRow(x.Id,x.Reference,x.Description,x.Status,
    x.Day?.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)??x.Instant?.ToString("u",CultureInfo.InvariantCulture)??"",
    x.Value.HasValue?x.Value.Value.ToString("0.###",CultureInfo.InvariantCulture)+(x.Unit.Length>0?" "+x.Unit:""):"",
    descriptor.Detail?.Replace("{id}",x.Id.ToString(CultureInfo.InvariantCulture),StringComparison.Ordinal)??descriptor.Home)).ToArray());
  }
  return descriptor.Cacheable?await cache.Get(t.company,t.branch,t.user,request.Locale,request.CacheIdentity(key),Read,ct):await Read();
 }
}
