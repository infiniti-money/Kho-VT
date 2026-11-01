using System.IO;
using ClosedXML.Excel;

namespace HisaWarehouse.Services;

/// <summary>
/// Read-only, on-demand exact SKU lookup from the user's configured separate
/// reconciliation workbook. Does not require a stock quantity column; it
/// reads product codes and names only and never uses quantities as stock.
/// </summary>
public static class ReferenceMaterialLookup
{
    public sealed record Material(string Sku, string Name, string Unit);

    public static string Resolve(string input, string? primaryWorkbook)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FileNotFoundException("Chưa chọn file danh mục đối chiếu trong Dữ liệu Excel và sao lưu.");
        string path = input.Trim();
        if (Directory.Exists(path))
            path = Directory.EnumerateFiles(path, "*.xlsx", SearchOption.TopDirectoryOnly)
                .Where(name => !Path.GetFileName(name).StartsWith("~$", StringComparison.Ordinal))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                ?? throw new FileNotFoundException("Thư mục đối chiếu không có file Excel .xlsx.");
        if (!File.Exists(path) || !Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Không đọc được file danh mục đối chiếu .xlsx.", path);
        if (!string.IsNullOrWhiteSpace(primaryWorkbook) &&
            Path.GetFullPath(path).Equals(Path.GetFullPath(primaryWorkbook), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("File đối chiếu phải khác KHO.xlsx chính.");
        return path;
    }

    public static IReadOnlyDictionary<string, Material> Read(string path)
    {
        using var snapshot = ExcelReadSnapshot.Capture(path);
        using var workbook = new XLWorkbook(snapshot);
        foreach (var sheet in workbook.Worksheets)
        {
            var first = sheet.FirstRowUsed()?.RowNumber() ?? 0;
            var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
            if (first == 0) continue;
            for (var header = first; header <= Math.Min(last, first + 25); header++)
            {
                var count = sheet.Row(header).LastCellUsed()?.Address.ColumnNumber ?? 0;
                var columns = Enumerable.Range(1, count)
                    .Select(i => (name: sheet.Cell(header, i).GetString().Trim(), index: i)).ToList();
                int Index(params string[] names) => columns.FirstOrDefault(c =>
                    names.Any(n => n.Equals(c.name, StringComparison.OrdinalIgnoreCase))).index;
                int skuCol = Index("Mã hàng", "Mã SKU", "SKU", "Mã vật tư", "Mã sản phẩm");
                int nameCol = Index("Tên hàng", "Tên vật tư", "Tên sản phẩm");
                int unitCol = Index("Đơn vị tính", "Đơn vị", "ĐVT");
                if (skuCol == 0 || nameCol == 0) continue;
                var results = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
                for (int row = header + 1; row <= last; row++)
                {
                    var sku = sheet.Cell(row, skuCol).GetString().Trim();
                    var name = sheet.Cell(row, nameCol).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name)) continue;
                    var unit = unitCol > 0 ? sheet.Cell(row, unitCol).GetString().Trim() : "";
                    if (results.TryGetValue(sku, out var previous) &&
                        (!previous.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) ||
                         !previous.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException($"Mã {sku} bị trùng với tên hoặc đơn vị khác nhau trong file đối chiếu.");
                    results[sku] = new Material(sku, name, unit);
                }
                if (results.Count > 0) return results;
            }
        }
        throw new InvalidDataException("Không tìm thấy mã hàng và tên vật tư trong file đối chiếu.");
    }
}
