using System.Globalization;
using Microsoft.Extensions.Primitives;
namespace EnterpriseERP.Web;

public sealed record GridRequest(int Draw,int Start,int Length,string Search,int OrderColumn,bool Descending,string Status,string Locale)
{
 public const int MaxStart=2_000_000;
 public static string Language(string? value)=>value is "bn" or "ar" or "banglish"?value:"en";
 public static GridRequest Parse(IQueryCollection query)
 {
  string One(string name,string fallback="")
  {if(!query.TryGetValue(name,out var values))return fallback;if(values.Count!=1||values[0] is null)throw new ArgumentException("Each table parameter must have one value.");return values[0]!;}
  int Number(string name,int fallback,int min,int max)
  {var raw=One(name,fallback.ToString(CultureInfo.InvariantCulture));if(!int.TryParse(raw,NumberStyles.None,CultureInfo.InvariantCulture,out var n)||n<min||n>max)throw new ArgumentException("Invalid bounded table paging or ordering parameter.");return n;}
  var draw=Number("draw",0,0,int.MaxValue);var start=Number("start",0,0,MaxStart);var length=Number("length",25,1,100);var column=Number("order[0][column]",0,0,3);
  var direction=One("order[0][dir]","desc");if(direction is not ("asc" or "desc"))throw new ArgumentException("Table ordering must use an allowed column and direction.");
  if(query.Keys.Any(k=>k.StartsWith("order[",StringComparison.Ordinal)&&k is not ("order[0][column]" or "order[0][dir]" or "order[0][name]")))throw new ArgumentException("Only one table ordering column is supported.");
  if(query.Any(p=>p.Key.EndsWith("[regex]",StringComparison.Ordinal)&&p.Value.Any(v=>v!="false"&&v!="")))throw new ArgumentException("Regular expression table searches are not supported.");
  if(query.Any(p=>p.Key.StartsWith("columns[",StringComparison.Ordinal)&&p.Key.EndsWith("[search][value]",StringComparison.Ordinal)&&p.Value.Any(v=>!string.IsNullOrEmpty(v))))throw new ArgumentException("Use the bounded global search and status filter.");
  var search=One("search[value]").Trim();var status=One("status").Trim();
  if(search.Length>120||status.Length>40||search.Any(char.IsControl)||status.Any(char.IsControl))throw new ArgumentException("Table search or status is too long or contains control characters.");
  return new(draw,start,length,search,column,direction=="desc",status,Language(One("language","en")));
 }
 public string CacheIdentity(string list)=>$"{list}\n{Start}\n{Length}\n{Search}\n{OrderColumn}\n{Descending}\n{Status}";
}
public sealed class GridRecord
{
 public long Id {get;set;}
 public string Reference {get;set;}="";
 public string Description {get;set;}="";
 public string Status {get;set;}="";
 public DateOnly? Day {get;set;}
 public DateTimeOffset? Instant {get;set;}
 public decimal? Value {get;set;}
 public string Unit {get;set;}="";
 public long RelatedId {get;set;}
}
public sealed record GridRow(long Id,string Reference,string Description,string Status,string Date,string Value,string Url);
// Draw is deliberately NOT cached: each response echoes only the current parsed integer.
public sealed record GridPage(int RecordsTotal,int RecordsFiltered,IReadOnlyList<GridRow> Data);
public sealed record GridDescriptor(string Key,string Title,string Permission,string Home,string? Detail,bool Cacheable=false,bool CompanyWide=false);
