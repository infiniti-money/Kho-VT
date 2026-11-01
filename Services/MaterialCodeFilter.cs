namespace HisaWarehouse.Services;

/// <summary>
/// Exact, case-insensitive matching for a pasted list of material codes.
/// Separators: comma or any whitespace (space/tab/newline); repeated entries
/// are harmless. Never fuzzy-match partial SKUs such as VT1 against VT10.
/// </summary>
public static class MaterialCodeFilter
{
    public static HashSet<string> Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return input.Split(
                new[] { ',', ' ', '\t', '\r', '\n', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool Matches(HashSet<string> codes, string? sku) =>
        codes.Count == 0 || (sku is not null && codes.Contains(sku.Trim()));
}
