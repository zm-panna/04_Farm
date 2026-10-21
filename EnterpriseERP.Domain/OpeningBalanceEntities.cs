namespace EnterpriseERP.Domain;

public sealed class OpeningBalance
{
 public long Id {get;set;}
 public int CompanyId {get;set;}
 public int BranchId {get;set;}
 public string Reference {get;set;}="";
 public DateOnly OpeningDate {get;set;}
 public string ReviewNote {get;set;}="";
 public string RecordedBy {get;set;}="";
 public DateTimeOffset CreatedUtc {get;set;}=DateTimeOffset.UtcNow;
 public long VoucherId {get;set;}
 public Voucher Voucher {get;set;}=null!;
 public List<OpeningStockValue> Stocks {get;set;}=new();
 public List<OpeningAnimalValue> Animals {get;set;}=new();
}
public sealed class OpeningStockValue
{
 public long Id {get;set;}
 public int CompanyId {get;set;}
 public int BranchId {get;set;}
 public long OpeningBalanceId {get;set;}
 public int StockItemId {get;set;}
 public decimal Quantity {get;set;}
 public decimal Value {get;set;}
}
public sealed class OpeningAnimalValue
{
 public long Id {get;set;}
 public int CompanyId {get;set;}
 public int BranchId {get;set;}
 public long OpeningBalanceId {get;set;}
 public int AnimalId {get;set;}
 public decimal Value {get;set;}
}
