namespace EnterpriseERP.Web;

/// <summary>A stock unit describes existing quantities. It never converts them.</summary>
public static class StockUnitPolicy
{
    public static string Normalize(string? unit)
    {
        var value = BusinessInput.Text(unit, "Stock unit", 30).ToUpperInvariant();
        if (!value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
            throw new ArgumentException("Use a stock unit code containing only letters, numbers or hyphens, such as KG, L, EA or BAG25KG.");
        return value;
    }
}
