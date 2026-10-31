using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Read-only projection of the original KHO.xlsx. No workbook is saved or normalized here.
/// Cached values in the formula column F are authoritative only after Excel has recalculated.
/// </summary>
public static class PrimaryWorkbookReader
{
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    public sealed record Stock(string Sku, string Name, string Unit, double Current, double Minimum, int Row);
    public sealed record Transaction(DateTime? Date, string Type, string Sku, double Quantity);

    public static IReadOnlyDictionary<string, Stock> ReadStocks(string path)
    {
        using var zip = OpenWorkbook(path);
        var strings = ReadStrings(zip);
        var doc = ReadSheet(zip, "Danh mục");
        var result = new Dictionary<string, Stock>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in doc.Descendants(MainNs + "sheetData").Elements(MainNs + "row"))
        {
            var index = (int?)row.Attribute("r") ?? 0;
            if (index < 4 || index > 900) continue;
            var sku = Text(Cell(row, "A" + index), strings).Trim();
            if (string.IsNullOrWhiteSpace(sku)) continue;
            if (result.ContainsKey(sku))
                throw new InvalidDataException($"Mã vật tư {sku} xuất hiện nhiều lần trong Danh mục.");
            var currentCell = Cell(row, "F" + index);
            if (!TryNumber(currentCell, strings, out var current))
                throw new InvalidDataException($"Ô F{index} không có kết quả tồn hiện tại hợp lệ. Hãy mở file trong Microsoft Excel, tính toán và lưu lại trước khi đối chiếu.");
            TryNumber(Cell(row, "E" + index), strings, out var minimum);
            result.Add(sku, new Stock(sku, Text(Cell(row, "B" + index), strings),
                Text(Cell(row, "C" + index), strings), current, minimum, index));
        }
        if (result.Count == 0) throw new InvalidDataException("Danh mục của file kho chính không có mã vật tư.");
        return result;
    }

    public static IReadOnlyList<Transaction> ReadTransactions(string path)
    {
        using var zip = OpenWorkbook(path);
        var strings = ReadStrings(zip);
        var doc = ReadSheet(zip, "Nhập Xuất");
        var records = new List<Transaction>();
        foreach (var row in doc.Descendants(MainNs + "sheetData").Elements(MainNs + "row"))
        {
            var index = (int?)row.Attribute("r") ?? 0;
            if (index < 5 || index > 965) continue;
            var sku = Text(Cell(row, "D" + index), strings).Trim();
            var type = Text(Cell(row, "C" + index), strings).Trim();
            if (string.IsNullOrEmpty(sku) || (type != "Nhập" && type != "Xuất")) continue;
            if (!TryNumber(Cell(row, "G" + index), strings, out var quantity) || quantity < 0)
                throw new InvalidDataException($"Số lượng không hợp lệ ở Nhập Xuất!G{index}.");
            DateTime? date = null;
            if (TryNumber(Cell(row, "A" + index), strings, out var oa) && oa > 0)
            {
                try { date = DateTime.FromOADate(oa); }
                catch (ArgumentException) { /* Preserve undated history without inventing a date. */ }
            }
            records.Add(new Transaction(date, type, sku, quantity));
        }
        return records;
    }

    private static ZipArchive OpenWorkbook(string path)
    {
        if (!File.Exists(path) || !Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("Hãy chọn đúng file KHO.xlsx có định dạng .xlsx.", path);
        return ZipFile.OpenRead(path);
    }

    private static XDocument ReadSheet(ZipArchive zip, string sheetName)
    {
        var workbook = Document(zip, "xl/workbook.xml");
        var sheet = workbook.Descendants(MainNs + "sheet")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"File Excel không có sheet {sheetName}.");
        var id = (string?)sheet.Attribute(RelNs + "id");
        var relationships = Document(zip, "xl/_rels/workbook.xml.rels");
        var rel = relationships.Descendants(PkgRelNs + "Relationship")
            .FirstOrDefault(x => (string?)x.Attribute("Id") == id)
            ?? throw new InvalidDataException($"Không tìm thấy dữ liệu sheet {sheetName}.");
        var target = (string?)rel.Attribute("Target") ?? "";
        var entry = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : "xl/" + target.Replace("../", "", StringComparison.Ordinal);
        return Document(zip, entry.Replace('\\', '/'));
    }

    private static XDocument Document(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException($"Thiếu thành phần {path} trong Excel.");
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static List<string> ReadStrings(ZipArchive zip)
    {
        if (zip.GetEntry("xl/sharedStrings.xml") is null) return [];
        var doc = Document(zip, "xl/sharedStrings.xml");
        return doc.Descendants(MainNs + "si")
            .Select(si => string.Concat(si.Descendants(MainNs + "t").Select(t => t.Value))).ToList();
    }

    private static XElement? Cell(XElement row, string address) =>
        row.Elements(MainNs + "c").FirstOrDefault(c => (string?)c.Attribute("r") == address);

    private static string Text(XElement? cell, IReadOnlyList<string> strings)
    {
        if (cell is null) return "";
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr") return string.Concat(cell.Descendants(MainNs + "t").Select(x => x.Value));
        var raw = (string?)cell.Element(MainNs + "v") ?? "";
        if (type == "s" && int.TryParse(raw, out var index) && index >= 0 && index < strings.Count)
            return strings[index];
        return raw;
    }

    private static bool TryNumber(XElement? cell, IReadOnlyList<string> strings, out double value) =>
        double.TryParse(Text(cell, strings), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value);
}
