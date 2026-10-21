using ClosedXML.Excel;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Globalization;
namespace EnterpriseERP.Web;
public sealed class ReportExportService(FinanceService finance,ReportService reports,IConfiguration configuration,IWebHostEnvironment environment)
{
 private static readonly Lazy<bool> Fonts = new(() =>
 {
  foreach(var family in new[]{"Bengali","Arabic"})
  foreach(var name in new[]{"Regular","Bold"})
  {
   using var stream=typeof(ReportExportService).Assembly.GetManifestResourceStream($"EnterpriseERP.Web.Assets.Fonts.NotoSans{family}-{name}.ttf")
       ?? throw new InvalidOperationException("The packaged report font is missing.");
   FontManager.RegisterFont(stream);
  }
  return true;
 });
 public async Task<byte[]> TrialBalanceExcel(DateTimeOffset through,CancellationToken ct,string language="en")
 {
  language=ReportLanguage.Resolve(language);var items=await finance.TrialBalance(through,ct);using var book=new XLWorkbook();var s=book.Worksheets.Add(ReportLanguage.Text("Trial Balance",language));
  s.Cell(1,1).Value=ReportLanguage.Text("Account",language);s.Cell(1,2).Value=ReportLanguage.Text("Debit",language);s.Cell(1,3).Value=ReportLanguage.Text("Credit",language);
  for(int i=0;i<items.Count;i++){s.Cell(i+2,1).Value=items[i].Account;s.Cell(i+2,2).Value=items[i].Debit;s.Cell(i+2,3).Value=items[i].Credit;}
  s.Range(1,1,1,3).Style.Font.Bold=true;s.Columns(1,3).Width=22;s.Columns(2,3).Style.NumberFormat.Format="0.000";
  if(language=="bn")s.Style.Font.FontName="Noto Sans Bengali";
  if(language=="ar"){s.Style.Font.FontName="Noto Sans Arabic";s.RightToLeft=true;}
  using var stream=new MemoryStream();book.SaveAs(stream);return stream.ToArray();
 }
 public async Task<byte[]> SummaryPdf(CancellationToken ct,string language="en")
 {
  language=ReportLanguage.Resolve(language);var s=await reports.Summary(ct);
  var configured=configuration["Reporting:QuestPdfLicense"];
  if(environment.IsDevelopment()&&string.IsNullOrWhiteSpace(configured))configured="Community";
  if(!Enum.TryParse<LicenseType>(configured,true,out var license)||!Enum.IsDefined(license))
      throw new InvalidOperationException("Configure Reporting:QuestPdfLicense after reviewing QuestPDF license eligibility. PDF output is not enabled for production by default.");
  QuestPDF.Settings.License=license;_ = Fonts.Value;
  string Label(string key)=>ReportLanguage.Text(key,language);
  string Amount(decimal value)=>value.ToString("N3",CultureInfo.InvariantCulture);
  return Document.Create(doc=>doc.Page(page=>
  {
   page.Margin(35);if(language=="ar")page.ContentFromRightToLeft();
   page.DefaultTextStyle(style=>style.FontFamily(language=="ar"?"Noto Sans Arabic":"Noto Sans Bengali","Lato").FontSize(12));
   page.Header().Text(Label("Farm Management Summary")).FontSize(20).Bold();
   page.Content().PaddingTop(18).Column(c=>
   {
    c.Spacing(8);c.Item().Text(Label("All-time selected branch totals")).FontSize(10);
    c.Item().Text($"{Label("Animals")}: {s.AnimalCount}");c.Item().Text($"{Label("Stock value")}: {Amount(s.StockValue)}");
    c.Item().Text($"{Label("Product sales (gross before returns)")}: {Amount(s.Sales)}");c.Item().Text($"{Label("Product purchases (gross before returns)")}: {Amount(s.Purchases)}");
    c.Item().Text($"{Label("Milk litres")}: {Amount(s.MilkLitres)}");
    c.Item().Text($"{Label("Generated UTC")}: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}").FontSize(10);
   });
  })).GeneratePdf();
 }
}
