using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace HisaWarehouse.Services;

public static class BarcodeService
{
    private static readonly IReadOnlyDictionary<char, string> Code39 = new Dictionary<char, string>
    {
        ['0']="nnnwwnwnn", ['1']="wnnwnnnnw", ['2']="nnwwnnnnw", ['3']="wnwwnnnnn", ['4']="nnnwwnnnw",
        ['5']="wnnwwnnnn", ['6']="nnwwwnnnn", ['7']="nnnwnnwnw", ['8']="wnnwnnwnn", ['9']="nnwwnnwnn",
        ['A']="wnnnnwnnw", ['B']="nnwnnwnnw", ['C']="wnwnnwnnn", ['D']="nnnnwwnnw", ['E']="wnnnwwnnn",
        ['F']="nnwnwwnnn", ['G']="nnnnnwwnw", ['H']="wnnnnwwnn", ['I']="nnwnnwwnn", ['J']="nnnnwwwnn",
        ['K']="wnnnnnnww", ['L']="nnwnnnnww", ['M']="wnwnnnnwn", ['N']="nnnnwnnww", ['O']="wnnnwnnwn",
        ['P']="nnwnwnnwn", ['Q']="nnnnnnwwn", ['R']="wnnnnnwnw", ['S']="nnwnnnwnw", ['T']="nnnnwnwnw",
        ['U']="wwnnnnnnw", ['V']="nwwnnnnnw", ['W']="wwwnnnnnn", ['X']="nwnnwnnnw", ['Y']="wwnnwnnnn",
        ['Z']="nwwnwnnnn", ['-']="nwnnnnwnw", ['.']="wwnnnnwnn", [' ']="nwwnnwnnn", ['$']="nwnwnwnnn",
        ['/']="nwnwnnnwn", ['+']="nwnnnwnwn", ['%']="nnnwnwnwn", ['*']="nwnnwnwnn"
    };

    public static DrawingImage CreateCode39Image(string? rawValue, double barHeight = 44, double module = 2)
    {
        var value = Normalize(rawValue);
        var encoded = $"*{value}*";
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            var totalWidth = encoded.Sum(ch => Code39[Code39.ContainsKey(ch) ? ch : '-'].Sum(bit => bit == 'w' ? module * 2.3 : module) + module) + module * 8;
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, totalWidth, barHeight + 24));
            var x = module * 4;
            foreach (var ch in encoded)
            {
                var pattern = Code39.TryGetValue(ch, out var found) ? found : Code39['-'];
                for (var index = 0; index < pattern.Length; index++)
                {
                    var width = pattern[index] == 'w' ? module * 2.3 : module;
                    if (index % 2 == 0)
                        context.DrawRectangle(Brushes.Black, null, new Rect(x, 0, width, barHeight));
                    x += width;
                }
                x += module;
            }
            var text = new FormattedText(value, CultureInfo.GetCultureInfo("vi-VN"), FlowDirection.LeftToRight,
                new Typeface("Consolas"), 12, Brushes.Black, 1.25);
            context.DrawText(text, new Point(Math.Max(0, (totalWidth - text.Width) / 2), barHeight + 4));
        }
        drawing.Freeze();
        return new DrawingImage(drawing);
    }

    public static string Normalize(string? value)
    {
        var normalized = (value ?? "").Trim().ToUpperInvariant();
        if (normalized.Length == 0) return "SKU";
        return new string(normalized.Select(ch => Code39.ContainsKey(ch) && ch != '*' ? ch : '-').ToArray());
    }
}
