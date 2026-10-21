using EnterpriseERP.Domain;
using Microsoft.EntityFrameworkCore;
namespace EnterpriseERP.Web;
// Operational entry points: no unrestricted SQL, require tenant & explicit permission at each call.
public sealed class FarmHealthFeedService(AppDbContext db,TenantContext tenant,PermissionService permissions, TimeProvider? clock = null)
{
 public async Task<List<AnimalHealthRecord>> Health(int animalId,CancellationToken ct)
 {
  await permissions.Demand("AnimalHealth.View",ct); var t=await tenant.GetAsync(ct);
  return await db.AnimalHealthRecords.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.AnimalId==animalId).OrderByDescending(x=>x.EventDate).ToListAsync(ct);
 }
 public async Task<AnimalHealthRecord> RecordHealth(int animalId,string reference,string eventType,DateOnly eventDate,DateOnly? nextDue,string details,CancellationToken ct)
 {
  await permissions.Demand("AnimalHealth.Create",ct); var t=await tenant.GetAsync(ct);
  reference=BusinessInput.Reference(reference);details=FarmBusinessRules.Text(details,"Details",4000,false);
  FarmBusinessRules.RecordedDate(eventDate,(await CompanyBusinessDate.Current(db,t.company,ct,clock)).Date);
  if(!new[]{"Vaccination","Deworming","Treatment","VetVisit","Observation"}.Contains(eventType) || nextDue.HasValue && (nextDue.Value < eventDate || nextDue.Value > eventDate.AddYears(10)))
   throw new ArgumentException("Choose a valid health event and a follow-up date from the event date through ten years later.");
  await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        await db.ValidatePendingScan("Health", ct);
  if(!await db.Animals.AsNoTracking().AnyAsync(x=>x.Id==animalId&&x.CompanyId==t.company&&x.BranchId==t.branch&&!x.Sold,ct)) throw new InvalidOperationException("Animal unavailable");
  if(await db.AnimalHealthRecords.AnyAsync(x=>x.CompanyId==t.company&&x.Reference==reference,ct)) throw new InvalidOperationException("Health reference already exists.");
  var r=new AnimalHealthRecord{CompanyId=t.company,BranchId=t.branch,AnimalId=animalId,Reference=reference,EventType=eventType,EventDate=eventDate,NextDueDate=nextDue,Details=details,RecordedBy=t.user};
  db.AnimalHealthRecords.Add(r);
  db.Audits.Add(new AuditEntry{CompanyId=t.company,ActorId=t.user,Action="AnimalHealth.Record",Reference=reference});
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return r;
 }
 public async Task<AnimalHealthRecord> CompleteFollowUp(long recordId,DateOnly completedDate,CancellationToken ct=default)
 {
  await permissions.Demand("AnimalHealth.Create",ct);var t=await tenant.GetAsync(ct);
  FarmBusinessRules.RecordedDate(completedDate,(await CompanyBusinessDate.Current(db,t.company,ct,clock)).Date);
  await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
  var record=await db.AnimalHealthRecords.SingleOrDefaultAsync(x=>x.Id==recordId&&x.CompanyId==t.company&&x.BranchId==t.branch,ct)
   ??throw new InvalidOperationException("Health record unavailable in this company and branch.");
  if(!record.NextDueDate.HasValue)throw new InvalidOperationException("This health record has no scheduled follow-up.");
  if(record.FollowUpCompletedDate.HasValue)throw new InvalidOperationException("This follow-up is already recorded as completed.");
  if(completedDate<record.EventDate)throw new ArgumentException("Completion date cannot precede the original health event.");
  record.FollowUpCompletedDate=completedDate;
  db.Audits.Add(new AuditEntry{CompanyId=t.company,ActorId=t.user,Action="AnimalHealth.FollowUpComplete",Reference=record.Reference+"/"+completedDate.ToString("yyyy-MM-dd")});
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return record;
 }
 public async Task<List<AnimalFeedRecord>> FeedHistory(int animalId,CancellationToken ct)
 {
  await permissions.Demand("FeedManagement.View",ct);var t=await tenant.GetAsync(ct);
  return await db.AnimalFeedRecords.AsNoTracking().Where(x=>x.CompanyId==t.company&&x.BranchId==t.branch&&x.AnimalId==animalId).OrderByDescending(x=>x.FeedDate).ToListAsync(ct);
 }
 public async Task<AnimalFeedRecord> ConsumeFeed(int animalId,int stockItemId,decimal quantity,string reference,DateOnly date,CancellationToken ct)
 {
  await permissions.Demand("FeedManagement.Create",ct); await permissions.Demand("Inventory.Move",ct);
  var t=await tenant.GetAsync(ct);
  BusinessInput.Decimal(quantity,"Feed quantity",true);reference=BusinessInput.Reference(reference);
  var businessDay=await CompanyBusinessDate.Current(db,t.company,ct,clock);
  if(date>businessDay.Date||date<new DateOnly(1900,1,1)) throw new ArgumentException("Feed date cannot be in the future");
  await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        await db.ValidatePendingScan("Feed", ct);
  if(await db.AnimalFeedRecords.AnyAsync(x=>x.CompanyId==t.company&&x.Reference==reference,ct)) throw new InvalidOperationException("Duplicate feed reference");
  var animal=await db.Animals.SingleOrDefaultAsync(x=>x.Id==animalId&&x.CompanyId==t.company&&x.BranchId==t.branch&&!x.Sold,ct)??throw new InvalidOperationException("Animal unavailable");
  var stock=await db.StockItems.SingleOrDefaultAsync(x=>x.Id==stockItemId&&x.CompanyId==t.company&&x.BranchId==t.branch,ct)??throw new InvalidOperationException("Stock item unavailable");
  if(stock.QuantityOnHand<quantity)throw new InvalidOperationException("Insufficient feed stock");
  var balance=await InventoryValuation.Load(db,stock,ct);var originalUnitCost=stock.UnitCost;
  var amount=InventoryValuation.Remove(stock,balance,quantity);
  var feed=new AnimalFeedRecord{CompanyId=t.company,BranchId=t.branch,AnimalId=animal.Id,StockItemId=stock.Id,Quantity=quantity,UnitCost=originalUnitCost,CostAmount=amount,Reference=reference.Trim(),FeedDate=date,RecordedBy=t.user};
  db.AnimalFeedRecords.Add(feed);
  db.StockMovements.Add(new StockMovement{CompanyId=t.company,BranchId=t.branch,StockItemId=stock.Id,QuantityDelta=-quantity,Reference="FEED:"+reference.Trim()});
  // Dr animal feed expense / Cr inventory; explicit mapping must be reviewed against customer's chart of accounts.

  if(amount>0)
  {
   var voucher=BusinessInput.Voucher(t.company,t.branch,"FEED:"+reference,("5100",amount,0),("1300",0,amount));
   voucher.Date=CompanyBusinessDate.AccountingDate(date);db.Vouchers.Add(voucher);
  }
  db.Audits.Add(new AuditEntry{CompanyId=t.company,ActorId=t.user,Action="FeedConsumed",Reference=reference.Trim()});
  await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return feed;
 }
}
