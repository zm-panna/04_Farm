namespace EnterpriseERP.Web;
/// <summary>Allowlisted display labels; account identifiers and decimal values stay invariant.</summary>
public static class ReportLanguage
{
    public static string Resolve(string? requested, string? preference = null)
    {
        if (!string.IsNullOrWhiteSpace(requested) && requested is not ("en" or "bn" or "banglish" or "ar"))
            throw new ArgumentException("Report language must be en, bn, ar or banglish.");
        return requested is "en" or "bn" or "banglish" or "ar" ? requested : preference is "en" or "bn" or "banglish" or "ar" ? preference : "en";
    }
    private static readonly Dictionary<string,(string Bn,string Banglish,string Ar)> Labels = new()
    {
        ["Farm Management Summary"]=("খামার ব্যবস্থাপনার সারসংক্ষেপ","Khamar byabosthaponar sarongkhep","ملخص إدارة المزرعة"),
        ["Trial Balance"]=("রেওয়ামিল","Rewamil","ميزان المراجعة"), ["Account"]=("হিসাব","Hisab","الحساب"),
        ["Debit"]=("ডেবিট","Debit","مدين"), ["Credit"]=("ক্রেডিট","Credit","دائن"),
        ["Metric"]=("সূচক","Suchok","المؤشر"), ["Value"]=("মান","Man","القيمة"),
        ["Animals"]=("সক্রিয় প্রাণী","Sokriyo prani","الحيوانات النشطة"), ["Stock value"]=("মজুদের মূল্য","Mojuder mullo","قيمة المخزون"),
        ["Product sales (gross before returns)"]=("পণ্য বিক্রয় (ফেরতের আগে মোট)","Ponno bikroy (feroter age mot)","مبيعات المنتجات (الإجمالي قبل المرتجعات)"),
        ["Product purchases (gross before returns)"]=("পণ্য ক্রয় (ফেরতের আগে মোট)","Ponno kroy (feroter age mot)","مشتريات المنتجات (الإجمالي قبل المرتجعات)"),
        ["Milk litres"]=("দুধের পরিমাণ (লিটার)","Dudher poriman (litre)","كمية الحليب (لتر)"),
        ["Generated UTC"]=("তৈরির সময় (ইউটিসি)","Toirir shomoy (UTC)","وقت الإنشاء (UTC)"),
        ["All-time selected branch totals"]=("নির্বাচিত শাখার সর্বকালের মোট হিসাব","Nirbachito shakhar shorbokaler mot hisab","إجماليات الفرع المحدد لجميع الفترات")
    };
    public static string Text(string key, string language) => language == "en" || !Labels.TryGetValue(key, out var value)
        ? key : language == "bn" ? value.Bn : language == "ar" ? value.Ar : value.Banglish;
}
