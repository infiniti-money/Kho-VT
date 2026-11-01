using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HisaWarehouse.Services;

/// <summary>
/// Local, read-only reconciliation between the exact saved KHO.xlsx stock
/// snapshot and Bravo's A/B/C/L export. Never changes HISA stock or Excel.
/// </summary>
public static class BravoStockComparison
{
    public sealed record Difference(string Sku, string Name, string Unit,
        double? Kho, double? Bravo)
    {
        public double? Delta => Kho.HasValue && Bravo.HasValue ? Bravo.Value - Kho.Value : null;
        public string Status => !Kho.HasValue ? "Chỉ có trong Bravo"
            : !Bravo.HasValue ? "Thiếu trong Bravo"
            : Bravo.Value < 0 ? (Math.Abs(Delta!.Value) < 0.000001 ? "Khớp (tồn âm Bravo)" : "Lệch (tồn âm Bravo)")
            : Math.Abs(Delta!.Value) < 0.000001 ? "Khớp" : "Lệch";
        public bool Matches => Kho.HasValue && Bravo.HasValue &&
            Math.Abs(Kho.Value - Bravo.Value) < 0.000001;
    }

    public sealed record Result(string PrimaryPath, string BravoPath, DateTime PrimarySaved,
        DateTime BravoSaved, IReadOnlyList<Difference> Rows)
    {
        public int MatchCount => Rows.Count(row => row.Matches);
        public int MismatchCount => Rows.Count(row => row.Kho.HasValue && row.Bravo.HasValue && !row.Matches);
        public int MissingBravoCount => Rows.Count(row => !row.Bravo.HasValue);
        public int OnlyBravoCount => Rows.Count(row => !row.Kho.HasValue);
        public int NegativeBravoCount => Rows.Count(row => row.Bravo < 0);
    }

    public static Result Read(InventoryState state)
    {
        var primary = state.PrimaryExcelPath?.Trim();
        if (string.IsNullOrWhiteSpace(primary) || !File.Exists(primary))
            throw new FileNotFoundException("An chưa được cấu hình file KHO.xlsx chính. Vào Dữ liệu Excel để chọn.");
        var bravo = ReferenceMaterialLookup.Resolve(state.ReferencePath, primary);
        var first = PrimaryWorkbookReader.ReadStocks(primary);
        var second = BravoStockReader.Read(bravo);
        var codes = first.Keys.Concat(second.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(sku => sku, StringComparer.OrdinalIgnoreCase);
        var rows = codes.Select(sku =>
        {
            first.TryGetValue(sku, out var kho);
            second.TryGetValue(sku, out var refStock);
            return new Difference(sku, kho?.Name ?? refStock?.Name ?? "",
                kho?.Unit ?? refStock?.Unit ?? "", kho?.Current, refStock?.Quantity);
        }).ToList();
        return new Result(primary, bravo, File.GetLastWriteTime(primary),
            File.GetLastWriteTime(bravo), rows);
    }

    public static string BuildSummary(Result result, string? question = null, int maxRows = 25)
    {
        var matches = Regex.Match(question ?? "",
            @"(?:mã|sku)\s+(?:hàng\s+|vật\s+tư\s+|sản\s+phẩm\s+)?(?<sku>[\p{L}\p{N}._-]{2,64})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var requestedSku = matches.Success ? matches.Groups["sku"].Value.TrimEnd('.', '!', '?') : null;
        var vi = CultureInfo.GetCultureInfo("vi-VN");
        string N(double? x) => x.HasValue ? x.Value.ToString("#,##0.##", vi) : "không có";
        var output = new StringBuilder();
        output.AppendLine("ĐỐI CHIẾU TỒN HISA KHO.xlsx ↔ BRAVO (CHỈ ĐỌC)");
        output.AppendLine($"KHO.xlsx lưu {result.PrimarySaved:dd/MM/yyyy HH:mm}; Bravo lưu {result.BravoSaved:dd/MM/yyyy HH:mm}.");
        output.AppendLine("Bravo: cột A mã hàng, B tên, C đơn vị, L tồn còn lại (giữ nguyên cả số âm).");
        output.AppendLine("Chênh lệch = tồn Bravo − tồn KHO.xlsx; không tự động ghi/cộng tồn.");
        output.AppendLine($"{result.Rows.Count:N0} mã: khớp {result.MatchCount:N0}, lệch {result.MismatchCount:N0}, " +
            $"thiếu Bravo {result.MissingBravoCount:N0}, chỉ có Bravo {result.OnlyBravoCount:N0}, " +
            $"tồn âm Bravo {result.NegativeBravoCount:N0}.");

        IEnumerable<Difference> listed = result.Rows;
        if (!string.IsNullOrWhiteSpace(requestedSku))
        {
            listed = listed.Where(row => row.Sku.Equals(requestedSku, StringComparison.OrdinalIgnoreCase));
            if (!listed.Any()) output.AppendLine($"Mã {requestedSku} không có trong cả hai file.");
        }
        else
        {
            listed = listed.Where(row => !row.Matches || row.Bravo < 0)
                .OrderBy(row => row.Bravo < 0 ? 0 : 1)
                .ThenBy(row => row.Status, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.Sku, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(0, maxRows));
        }
        foreach (var row in listed)
        {
            output.AppendLine($"- {row.Sku} | {Sanitize(row.Name)} | {Sanitize(row.Unit)} | " +
                $"KHO={N(row.Kho)} | Bravo={N(row.Bravo)} | " +
                $"lệch={N(row.Delta)} | {row.Status}");
        }
        if (string.IsNullOrWhiteSpace(requestedSku))
            output.AppendLine("Chỉ hiển thị một phần mã cần chú ý; bảng Đối chiếu hai file Excel hiển thị toàn bộ.");
        return output.ToString();
    }

    public static bool IsComparisonQuestion(string question) =>
        Regex.IsMatch(question ?? "",
            @"\bbravo\b|đối\s*chiếu|so\s*sánh\s*tồn|tồn\s*hai\s*file|tồn\s*2\s*file",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string Sanitize(string text) =>
        text.Replace('\n', ' ').Replace('\r', ' ').Replace('|', '/');
}
