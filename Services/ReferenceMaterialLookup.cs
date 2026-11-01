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

    public static IReadOnlyDictionary<string, Material> Read(string path) =>
        BravoStockReader.Read(path).Values.ToDictionary(
            row => row.Sku,
            row => new Material(row.Sku, row.Name, row.Unit),
            StringComparer.OrdinalIgnoreCase);
}
