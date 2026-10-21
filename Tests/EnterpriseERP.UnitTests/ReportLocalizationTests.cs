using System.Text;
using ClosedXML.Excel;
using EnterpriseERP.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;
namespace EnterpriseERP.UnitTests;
public sealed class ReportLocalizationTests
{
 [Theory][InlineData("en","Metric")][InlineData("bn","সূচক")][InlineData("banglish","Suchok")][InlineData("ar","المؤشر")]
 public async Task CsvHasUtf8BomAndTranslatedLabels(string language,string heading)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Reports.View");
  var bytes=await new ReportService(f.Db,f.Tenant,f.Permissions).Csv(default,language);
  Assert.Equal(new byte[]{239,187,191},bytes.Take(3));Assert.Contains(heading,Encoding.UTF8.GetString(bytes));
  Assert.Contains(",0\r\n",Encoding.UTF8.GetString(bytes));
 }
 [Theory][InlineData("en","Account")][InlineData("bn","হিসাব")][InlineData("banglish","Hisab")][InlineData("ar","الحساب")]
 public async Task ExcelRetainsMachineReadableNumbersAndLocalizedHeading(string language,string heading)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Accounts.View");
  f.Db.Vouchers.Add(BusinessInput.Voucher(1,11,"REPORT-1",("1000",12.345m,0m),("3000",0m,12.345m)));await f.Db.SaveChangesAsync();
  var export=Export(f);var bytes=await export.TrialBalanceExcel(DateTimeOffset.UtcNow.AddMinutes(1),default,language);
  using var book=new XLWorkbook(new MemoryStream(bytes));var sheet=book.Worksheets.First();
  Assert.Equal(heading,sheet.Cell(1,1).GetString());Assert.Equal(language=="ar",sheet.RightToLeft);Assert.Equal(12.345,sheet.Cell(2,2).GetDouble(),3);
 }
 [Theory][InlineData("en")][InlineData("bn")][InlineData("banglish")][InlineData("ar")]
 public async Task PdfRendersAllSupportedLanguagesWithPackagedFont(string language)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Reports.View");
  var bytes=await Export(f).SummaryPdf(default,language);
  Assert.StartsWith("%PDF",Encoding.ASCII.GetString(bytes.Take(4).ToArray()));Assert.True(bytes.Length>3000);
 }
 [Fact] public async Task PdfProductionRequiresExplicitLicense()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Reports.View");
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Export(f,"Production").SummaryPdf(default));
 }
 [Theory][InlineData("999")][InlineData("UndefinedTier")]
 public async Task UndefinedPdfLicenseCannotPassEnumParsing(string value)
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Reports.View");
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Export(f,"Production",value).SummaryPdf(default));
 }
 [Theory][InlineData("../../other")][InlineData("xx")][InlineData("<script>")]
 public void InvalidLanguageRejected(string language)=>Assert.Throws<ArgumentException>(()=>ReportLanguage.Resolve(language));
 [Fact] public void InvalidPreferenceFallsBackToEnglish()=>Assert.Equal("en",ReportLanguage.Resolve(null,"tampered"));
 [Fact] public void ExplicitLanguageOverridesCookie()=>Assert.Equal("bn",ReportLanguage.Resolve("bn","en"));
 [Fact] public async Task ExportCannotBypassScopedPermission()
 {
  await using var f=await SecurityTestScope.CreateAsync();
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Export(f).SummaryPdf(default,"bn"));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Export(f).TrialBalanceExcel(DateTimeOffset.UtcNow,default,"banglish"));
 }
 [Fact] public async Task MissingInventoryOpeningValueCannotBeReportedAsZero()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Reports.View");
  f.Db.StockItems.Add(new EnterpriseERP.Domain.StockItem{CompanyId=1,BranchId=11,SKU="LEGACY",Name="Unreviewed stock",QuantityOnHand=4,UnitCost=25});await f.Db.SaveChangesAsync();
  await Assert.ThrowsAsync<InvalidOperationException>(()=>new ReportService(f.Db,f.Tenant,f.Permissions).Summary());
  await Assert.ThrowsAsync<InvalidOperationException>(()=>Export(f).SummaryPdf(default,"bn"));
 }
 [Fact] public async Task MissingValuationInOtherBranchDoesNotBlockCurrentReport()
 {
  await using var f=await SecurityTestScope.CreateAsync();await f.GrantAsync("Reports.View");
  f.Db.StockItems.Add(new EnterpriseERP.Domain.StockItem{CompanyId=1,BranchId=12,SKU="LEGACY",Name="Other stock",QuantityOnHand=4,UnitCost=25});await f.Db.SaveChangesAsync();
  Assert.Equal(0,(await new ReportService(f.Db,f.Tenant,f.Permissions).Summary()).StockValue);
 }
 private static ReportExportService Export(SecurityTestScope f,string environment="Development",string? license=null)=>new(
  new FinanceService(f.Db,f.Tenant,f.Permissions),new ReportService(f.Db,f.Tenant,f.Permissions),
  new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Reporting:QuestPdfLicense",license}}).Build(),new EnvironmentStub{EnvironmentName=environment});
 private sealed class EnvironmentStub:IWebHostEnvironment
 {
  public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Tests";
  public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
  public string WebRootPath{get;set;}=".";public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
 }
}
