using System.ComponentModel.DataAnnotations;
using System.Data;
using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
public sealed class OpeningLedgerInput
{
 public string AccountCode {get;set;}="";
 public decimal Debit {get;set;}
 public decimal Credit {get;set;}
}
public sealed class OpeningStockInput
{
 public int StockItemId {get;set;}
 public decimal Quantity {get;set;}
 public decimal Value {get;set;}
}
public sealed class OpeningAnimalInput
{
 public int AnimalId {get;set;}
 public decimal Value {get;set;}
}
public sealed class OpeningBalanceInput
{
 [Required,StringLength(60)] public string Reference {get;set;}="";
 public DateOnly OpeningDate {get;set;}
 [Required,StringLength(1000)] public string ReviewNote {get;set;}="";
 public bool ConfirmReviewed {get;set;}
 public List<OpeningLedgerInput> Ledger {get;set;}=new();
 public List<OpeningStockInput> Stocks {get;set;}=new();
 public List<OpeningAnimalInput> Animals {get;set;}=new();
}
public sealed class OpeningBalancePage
{
 public OpeningBalanceInput Opening {get;set;}=new();
 public List<StockItem> StockItems {get;set;}=new();
 public List<Animal> Animals {get;set;}=new();
 public OpeningBalance? Existing {get;set;}
 public bool HasFinancialActivity {get;set;}
}
public sealed class OpeningBalanceService(AppDbContext db,TenantContext tenant,PermissionService permission, TimeProvider? clock = null)
{
 public static IReadOnlyDictionary<string,string> Accounts {get;}=new Dictionary<string,string>
 { ["1000"]="Cash / bank",["1500"]="Other current assets",["1600"]="Other fixed assets",["2500"]="Other liabilities",["3000"]="Capital",["3100"]="Retained earnings / accumulated loss" };
 private async Task<(int company,int branch,string user)> Authorized(CancellationToken ct)
 {await permission.Demand("Accounts.Opening",ct);await permission.Demand("Permissions.Manage",ct);return await tenant.GetAsync(ct);}
 private async Task<bool> HasActivity(int company,int branch,CancellationToken ct)=>
  await db.Vouchers.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.StockMovements.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.FinanceDocuments.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.FinancePayments.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.PurchaseReceipts.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.ProductSales.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.AnimalSales.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.AnimalFeedRecords.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.FarmExpenses.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct)||
  await db.BankStatementLines.AnyAsync(x=>x.CompanyId==company&&x.BranchId==branch,ct);
 public async Task<OpeningBalancePage> Page(CancellationToken ct=default)
 {
  var t=await Authorized(ct);
  return new OpeningBalancePage
  {
   Opening=new(){OpeningDate=(await CompanyBusinessDate.Current(db,t.company,ct,clock)).Date,Ledger=Accounts.Keys.Select(x=>new OpeningLedgerInput{AccountCode=x}).ToList(),Stocks=Enumerable.Range(0,5).Select(_=>new OpeningStockInput()).ToList(),Animals=Enumerable.Range(0,5).Select(_=>new OpeningAnimalInput()).ToList()},
   StockItems=await db.StockItems.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.QuantityOnHand==0).OrderBy(x=>x.SKU).ToListAsync(ct),
   Animals=await db.Animals.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&!x.Sold&&x.PurchaseCost==0).OrderBy(x=>x.Tag).ToListAsync(ct),
   Existing=await db.OpeningBalances.AsNoTracking().Include(x=>x.Voucher).ThenInclude(x=>x.Lines).Include(x=>x.Stocks).Include(x=>x.Animals).SingleOrDefaultAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch,ct),
   HasFinancialActivity=await HasActivity(t.company,t.branch,ct)
  };
 }
 public async Task<OpeningBalance> Post(OpeningBalanceInput input,CancellationToken ct=default)
 {
  var t=await Authorized(ct);var reference=BusinessInput.Reference(input.Reference,60);
  var note=BusinessInput.Text(input.ReviewNote,"Opening review note",1000);FarmBusinessRules.RecordedDate(input.OpeningDate,(await CompanyBusinessDate.Current(db,t.company,ct,clock)).Date);
  if(!input.ConfirmReviewed)throw new ArgumentException("Confirm that the opening balances and source records were reviewed before posting.");
  if(input.Ledger is null||input.Stocks is null||input.Animals is null||input.Ledger.Count>6||input.Stocks.Count>100||input.Animals.Count>100||input.Ledger.Any(x=>x is null)||input.Stocks.Any(x=>x is null)||input.Animals.Any(x=>x is null))throw new ArgumentException("Opening batch exceeds supported row limits.");
  var rows=input.Ledger.Where(x=>x.Debit!=0||x.Credit!=0).ToList();
  if(rows.Any(x=>!Accounts.ContainsKey(x.AccountCode))||rows.GroupBy(x=>x.AccountCode).Any(g=>g.Count()>1))throw new ArgumentException("Select unique approved opening accounts. Control accounts are derived from itemized stock and animal values.");
  foreach(var row in rows)
  {
   BusinessInput.Amount(row.Debit,"Debit",false);BusinessInput.Amount(row.Credit,"Credit",false);
   if((row.Debit>0)==(row.Credit>0))throw new ArgumentException("Each opening account must have a debit or a credit, not both.");
   if(row.AccountCode is "1000" or "1500" or "1600" && row.Credit>0)throw new ArgumentException("Opening asset accounts must be debit balances.");
   if(row.AccountCode is "2500" or "3000" && row.Debit>0)throw new ArgumentException("Opening liability and capital accounts must be credit balances.");
  }
  var stocks=input.Stocks.Where(x=>x.StockItemId!=0||x.Quantity!=0||x.Value!=0).ToList();
  var animals=input.Animals.Where(x=>x.AnimalId!=0||x.Value!=0).ToList();
  if(stocks.Any(x=>x.StockItemId<=0)||stocks.GroupBy(x=>x.StockItemId).Any(x=>x.Count()>1)||animals.Any(x=>x.AnimalId<=0)||animals.GroupBy(x=>x.AnimalId).Any(x=>x.Count()>1))throw new ArgumentException("Opening stock items and animals must be selected once each.");
  foreach(var stock in stocks){BusinessInput.Decimal(stock.Quantity,"Opening quantity",true,maximum:1000000000m);BusinessInput.Amount(stock.Value,"Opening stock value");}
  foreach(var animal in animals)BusinessInput.Amount(animal.Value,"Opening animal value");
  var journal=rows.Select(x=>(x.AccountCode,x.Debit,x.Credit)).ToList();
  if(stocks.Count>0)journal.Add(("1300",BusinessInput.Amount(stocks.Sum(x=>x.Value),"Opening inventory total"),0m));
  if(animals.Count>0)journal.Add(("1400",BusinessInput.Amount(animals.Sum(x=>x.Value),"Opening livestock total"),0m));
  var voucher=BusinessInput.Voucher(t.company,t.branch,"OPEN-"+t.branch+"-"+reference,journal.ToArray());
  voucher.Date=new DateTimeOffset(input.OpeningDate.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);
  await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
  try
  {
   if(await db.OpeningBalances.AnyAsync(x=>x.CompanyId==t.company&&(x.BranchId==t.branch||x.Reference==reference),ct))throw new InvalidOperationException("An opening balance already exists for this branch or reference.");
   if(await HasActivity(t.company,t.branch,ct))throw new InvalidOperationException("Opening balances require empty branch books with no previous financial or stock transactions.");
   if(await db.StockItems.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.QuantityOnHand!=0,ct)||await db.InventoryCostBalances.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.Value!=0,ct)||await db.Animals.AnyAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.PurchaseCost!=0,ct))throw new InvalidOperationException("Existing nonzero stock or livestock values require a separately reviewed migration; this opening form never infers them.");
   var closed=await db.AccountingPeriodCloses.AsNoTracking().SingleOrDefaultAsync(x=>x.CompanyId==t.company&&x.BranchId==t.branch,ct);
   if(closed!=null&&input.OpeningDate<=closed.ClosedThrough)throw new InvalidOperationException("Posting date is in a closed accounting period.");
   var opening=new OpeningBalance{CompanyId=t.company,BranchId=t.branch,Reference=reference,OpeningDate=input.OpeningDate,ReviewNote=note,RecordedBy=t.user,Voucher=voucher};
   foreach(var row in stocks)
   {
    var item=await db.StockItems.SingleOrDefaultAsync(x=>x.Id==row.StockItemId&&x.CompanyId==t.company&&x.BranchId==t.branch&&x.QuantityOnHand==0,ct)??throw new InvalidOperationException("Opening stock item must be a zero-quantity item in this branch.");
    var balance=await InventoryValuation.Load(db,item,ct);
    InventoryValuation.Add(item,balance,row.Quantity,row.Value);
    opening.Stocks.Add(new(){CompanyId=t.company,BranchId=t.branch,StockItemId=item.Id,Quantity=row.Quantity,Value=row.Value});
    db.StockMovements.Add(new(){CompanyId=t.company,BranchId=t.branch,StockItemId=item.Id,QuantityDelta=row.Quantity,Reference=voucher.Reference,CreatedUtc=voucher.Date});
   }
   foreach(var row in animals)
   {
    var animal=await db.Animals.SingleOrDefaultAsync(x=>x.Id==row.AnimalId&&x.CompanyId==t.company&&x.BranchId==t.branch&&!x.Sold&&x.PurchaseCost==0,ct)??throw new InvalidOperationException("Opening animal must be unsold with zero recorded cost in this branch.");
    animal.PurchaseCost=row.Value;
    opening.Animals.Add(new(){CompanyId=t.company,BranchId=t.branch,AnimalId=animal.Id,Value=row.Value});
   }
   db.OpeningBalances.Add(opening);db.Audits.Add(new(){CompanyId=t.company,ActorId=t.user,Action="Accounts.Opening.Post",Reference=reference});
   await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return opening;
  }
  catch{await tx.RollbackAsync(CancellationToken.None);db.ChangeTracker.Clear();throw;}
 }
}
