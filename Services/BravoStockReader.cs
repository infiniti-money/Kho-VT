using System.Globalization;
using System.IO;
using ClosedXML.Excel;

namespace HisaWarehouse.Services;

/// <summary>
/// Read-only snapshot of Bravo's "Tổng hợp tồn" export:
/// A = SKU, B = product name, C = unit, L = signed on-hand quantity.
/// Negative quantities are VALID (negative stock), not missing records.
/// No source workbook is ever saved, changed, or used to update HISA stock.
/// </summary>
public static class BravoStockReader
{
    public sealed record Row(string Sku, string Name, string Unit, double Quantity, string Sheet, int SourceRow);
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static IReadOnlyDictionary<string, Row> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Chưa tìm thấy file tổng hợp tồn Bravo. Hãy chọn file .xlsx ở phần Dữ liệu Excel.");
        using var snapshot = ExcelReadSnapshot.Capture(path);
        using var book = new XLWorkbook(snapshot);
        Dictionary<string, Row>? best = null;
        foreach (var sheet in book.Worksheets)
        {
            var data = ReadSheet(sheet);
            if (data.Count > 0 && (best is null || data.Count > best.Count))
                best = data;
        }
        return best is { Count: > 0 } ? best :
            throw new InvalidDataException(
                "Không tìm thấy bảng Bravo có mã hàng A, tên B, đơn vị C và tồn số (kể cả số âm) ở cột L.");
    }

    private static Dictionary<string, Row> ReadSheet(IXLWorksheet sheet)
    {
        var results = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        var first = sheet.FirstRowUsed()?.RowNumber() ?? 0;
        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (first == 0 || last < first) return results;

        // Header may be in any of the initial rows. For this known Bravo export,
        // column positions are authoritative, not ambiguous descriptive headers.
        var started = false;
        for (int row = first; row <= last; row++)
        {
            var sku = sheet.Cell(row, 1).GetString().Trim();
            var name = sheet.Cell(row, 2).GetString().Trim();
            var unit = sheet.Cell(row, 3).GetString().Trim();
            if (sku.Length == 0 || IsHeaderOrTotal(sku)) continue;
            var quantityCell = sheet.Cell(row, 12);
            if (!TrySignedNumber(quantityCell, out var quantity))
            {
                // Allow preamble before the first stock row; do not silently
                // omit an item with an invalid balance inside the actual table.
                if (started && name.Length > 0 && unit.Length > 0)
                    throw new InvalidDataException(
                        $"Bravo sheet '{sheet.Name}', dòng {row}, mã {sku}: cột L không phải số tồn hợp lệ.");
                continue;
            }
            if (name.Length == 0) // Preamble and malformed records are not SKU entries.
            {
                if (started)
                    throw new InvalidDataException($"Bravo sheet '{sheet.Name}' dòng {row}: thiếu tên vật tư cột B.");
                continue;
            }
            started = true;
            if (results.TryGetValue(sku, out var previous))
            {
                if (!previous.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    !previous.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        $"Mã {sku} trùng trong Bravo nhưng tên/đơn vị không thống nhất (dòng {previous.SourceRow} và {row}).");
                // Multiple storage locations from a Bravo summary: aggregate only
                // identical SKU/unit, including signed negatives.
                var sum = previous.Quantity + quantity;
                if (!double.IsFinite(sum))
                    throw new InvalidDataException("Tổng tồn Bravo vượt giới hạn số.");
                results[sku] = previous with { Quantity = sum };
            }
            else results.Add(sku, new Row(sku, name, unit, quantity, sheet.Name, row));
        }
        return results;
    }

    private static bool IsHeaderOrTotal(string sku)
    {
        var key = sku.Trim().ToLowerInvariant();
        return key is "mã hàng" or "mã sản phẩm" or "mã vật tư" or "sku"
            or "mã sku" or "stt" or "tổng" or "tổng cộng" or "cộng";
    }

    public static bool TrySignedNumber(IXLCell cell, out double value)
    {
        value = 0;
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var number) && double.IsFinite(number))
        {
            value = number;
            return true;
        }
        var text = cell.GetString().Trim().Replace("\u00A0", "").Replace(" ", "");
        if (text.Length == 0) return false;
        var negativeParentheses = text.StartsWith('(') && text.EndsWith(')');
        if (negativeParentheses) text = "-" + text[1..^1];
        // Prefer Vietnamese number convention for string-formatted Bravo
        // exports (1.234,5 = 1234.5); ordinary numeric Excel cells bypass it.
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, Vi, out number)
            && double.IsFinite(number))
        {
            value = number;
            return true;
        }
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out number) && double.IsFinite(number))
        {
            value = number;
            return true;
        }
        return false;
    }
}
