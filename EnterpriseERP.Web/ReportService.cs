using Microsoft.EntityFrameworkCore;
using System.Text;
namespace EnterpriseERP.Web;
public sealed record FarmSummary(int AnimalCount,decimal StockValue,decimal Sales,decimal Purchases,decimal MilkLitres);
public sealed class ReportService(AppDbContext db,TenantContext context,PermissionService permission)
{
 public async Task<FarmSummary> Summary(CancellationToken ct=default)
 {
  await permission.Demand("Reports.View",ct);var t=await context.GetAsync(ct);
  if(await db.StockItems.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.QuantityOnHand!=0&&!db.InventoryCostBalances.Any(v=>v.CompanyId==x.CompanyId&&v.BranchId==x.BranchId&&v.StockItemId==x.Id),ct))
   throw new InvalidOperationException("Reports are unavailable until stock with a missing opening valuation has been reviewed and imported. No zero value has been assumed.");
  return new(await db.Animals.CountAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&!x.Sold,ct),
   (await db.InventoryCostBalances.Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch).Select(x=>x.Value).ToListAsync(ct)).Sum(),
   (await db.FinanceDocuments.Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Kind=="Sale").Select(x=>x.Amount).ToListAsync(ct)).Sum(),
   (await db.FinanceDocuments.Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Kind=="Purchase").Select(x=>x.Amount).ToListAsync(ct)).Sum(),
   (await db.MilkYields.Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch).Select(x=>x.Litres).ToListAsync(ct)).Sum());
 }
 public async Task<byte[]> Csv(CancellationToken ct=default, string language="en")
 {
  language=ReportLanguage.Resolve(language);var r=await Summary(ct);
  var rows=new (string,decimal)[]{("Animals",r.AnimalCount),("Stock value",r.StockValue),("Product sales (gross before returns)",r.Sales),("Product purchases (gross before returns)",r.Purchases),("Milk litres",r.MilkLitres)};
  var text=ReportLanguage.Text("Metric",language)+","+ReportLanguage.Text("Value",language)+"\r\n"+
    string.Join("",rows.Select(row=>ReportLanguage.Text(row.Item1,language)+","+row.Item2.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\r\n"));
  return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
 }
}
