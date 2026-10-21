namespace EnterpriseERP.Web;

/// <summary>
/// Bounded barcode-field aliases for decoder representations of the same retail code.
/// Never apply these aliases to SKU identifiers. Invalid checksums retain exact-only matching.
/// </summary>
public static class ScanRetailBarcode
{
    public static string[] Equivalents(string input)
    {
        var values = new HashSet<string>(StringComparer.Ordinal) { input };
        if (input.Any(c => c is < '0' or > '9')) return [input];
        string? upca = input.Length switch
        {
            12 when ValidCheckDigit(input) => input,
            13 when input[0] == '0' && ValidCheckDigit(input) => input[1..],
            8 when input[0] is '0' or '1' => ExpandUpce(input),
            _ => null
        };
        if (upca == null || !ValidCheckDigit(upca)) return [input];
        values.Add(upca);
        values.Add("0" + upca);
        if (upca[0] is '0' or '1')
        {
            // Enumerate the four zero-suppression layouts, retaining only exact round-trips.
            string[] candidates =
            [
                upca[..3] + upca.Substring(8, 3) + upca[3] + upca[11],
                upca[..4] + upca.Substring(9, 2) + "3" + upca[11],
                upca[..5] + upca[10] + "4" + upca[11],
                upca[..6] + upca[10] + upca[11]
            ];
            foreach (var candidate in candidates)
                if (ExpandUpce(candidate) == upca) values.Add(candidate);
        }
        return values.Order(StringComparer.Ordinal).ToArray();
    }

    public static bool ValidCheckDigit(string value)
    {
        if (value.Length is not (8 or 12 or 13) || value.Any(c => c is < '0' or > '9')) return false;
        var sum = value[^1] - '0';
        for (var i = value.Length - 2; i >= 0; i--)
            sum += (value[i] - '0') * ((value.Length - 2 - i) % 2 == 0 ? 3 : 1);
        return sum % 10 == 0;
    }

    private static string? ExpandUpce(string value)
    {
        if (value.Length != 8 || value[0] is not ('0' or '1')) return null;
        return value[6] switch
        {
            '0' or '1' or '2' => value[..3] + value[6] + "0000" + value.Substring(3, 3) + value[7],
            '3' => value[..4] + "00000" + value.Substring(4, 2) + value[7],
            '4' => value[..5] + "00000" + value[5] + value[7],
            _ => value[..6] + "0000" + value[6] + value[7]
        };
    }
}
