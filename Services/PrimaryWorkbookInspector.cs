using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Strict, read-only profile for the user's real KHO.xlsx layout.
/// Inspect only the two business worksheets; NEVER rewrite the source workbook.
/// A cached formula result is not proof that Excel has recalculated the file.
/// </summary>
public static class PrimaryWorkbookInspector
{
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";

    public sealed record Report(
        int CatalogItems,
        int TypedTransactions,
        int MissingTransactionQuantities,
        int IncompleteTransactions,
        int FreeTransactionRows,
        int MinimumFormulaCells,
        int QuantityFormulaCells,
        int MissingProtectedFormulas,
        int ExternalLinkParts,
        string CatalogSheet,
        string TransactionsSheet)
    {
        public bool SafeToPrepareWrite => MissingProtectedFormulas == 0 &&
            IncompleteTransactions == 0 && MissingTransactionQuantities == 0 && FreeTransactionRows > 0;

        public string Summary =>
            $"{CatalogItems:N0} mã vật tư · {TypedTransactions:N0} dòng có loại Nhập/Xuất · " +
            $"{FreeTransactionRows:N0} dòng còn trống (đến dòng 965). " +
            $"Có {MinimumFormulaCells:N0} công thức tồn tối thiểu và " +
            $"{QuantityFormulaCells:N0} công thức số lượng trong các cột nhập tay. " +
            (IncompleteTransactions > 0 ? $"{IncompleteTransactions:N0} dòng có mã hàng nhưng thiếu loại Nhập/Xuất. " : "") +
            (MissingTransactionQuantities > 0 ? $"{MissingTransactionQuantities:N0} giao dịch thiếu số lượng hợp lệ. " : "") +
            (MissingProtectedFormulas > 0 ? $"{MissingProtectedFormulas:N0} ô công thức tính toán bị thiếu. " : "") +
            (ExternalLinkParts > 0 ? $"{ExternalLinkParts:N0} liên kết workbook ngoài cần giữ nguyên. " : "") +
            "Mới kiểm tra, KHÔNG ghi vào file.";
    }

    public static Report Inspect(string path)
    {
        // Work on a stable in-memory copy and release the original file handle
        // before parsing any XML or returning to the UI.
        using var zip = new ZipArchive(ExcelReadSnapshot.Capture(path), ZipArchiveMode.Read);
        var strings = ReadStrings(zip);
        var catalog = ReadSheet(zip, "Danh mục");
        var transactions = ReadSheet(zip, "Nhập Xuất");
        VerifyHeaders(catalog, strings, 3, new[] {
            ("A", "Mã hàng"), ("B", "Tên hàng"), ("C", "Đơn vị"),
            ("D", "Tồn đầu"), ("E", "Tồn tối thiểu"), ("F", "Tồn hiện tại")
        });
        VerifyHeaders(transactions, strings, 4, new[] {
            ("A", "Ngày"), ("B", "In"), ("C", "Loại"),
            ("D", "Mã hàng"), ("E", "Tên hàng"), ("F", "Đơn vị"),
            ("G", "Số lượng"), ("H", "Tồn trước"), ("I", "Tồn sau"),
            ("J", "Cảnh báo"), ("K", "Giao/nhận"), ("L", "Ghi chú")
        });

        var catRows = CellsByRow(catalog);
        var txRows = CellsByRow(transactions);
        var active = 0;
        var minimumFormulas = 0;
        var quantityFormulas = 0;
        var valid = 0;
        var incomplete = 0;
        var missingQuantity = 0;
        var vacant = 0;
        var missing = 0;

        for (var row = 4; row <= 900; row++)
        {
            var cells = catRows.GetValueOrDefault(row);
            if (!string.IsNullOrWhiteSpace(Text(Get(cells, "A"), strings))) active++;
            if (HasFormula(Get(cells, "E"))) minimumFormulas++;
            foreach (var column in new[] { "F", "G", "I", "J", "K" })
                if (!HasFormula(Get(cells, column))) missing++;
        }

        for (var row = 5; row <= 965; row++)
        {
            var cells = txRows.GetValueOrDefault(row);
            var sku = Text(Get(cells, "D"), strings).Trim();
            var type = Text(Get(cells, "C"), strings).Trim();
            if (!string.IsNullOrWhiteSpace(sku))
            {
                if (type is "Nhập" or "Xuất")
                {
                    valid++;
                    var quantity = Text(Get(cells, "G"), strings).Trim();
                    if (!double.TryParse(quantity, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) ||
                        !double.IsFinite(numeric) || numeric <= 0)
                        missingQuantity++;
                }
                else incomplete++;
            }
            if (HasFormula(Get(cells, "G"))) quantityFormulas++;
            // Append only within the existing Excel table/formula range.
            // Rows with any business data or a formula in an input cell are unavailable.
            if (new[] { "A", "B", "C", "D", "G", "K", "L" }.All(col =>
                string.IsNullOrWhiteSpace(Text(Get(cells, col), strings)) &&
                !HasFormula(Get(cells, col))))
                vacant++;
            foreach (var col in new[] { "E", "F", "H", "I", "J" })
                if (!HasFormula(Get(cells, col))) missing++;
        }

        return new Report(active, valid, missingQuantity, incomplete, vacant,
            minimumFormulas, quantityFormulas, missing,
            zip.Entries.Count(entry => entry.FullName.StartsWith("xl/externalLinks/externalLink", StringComparison.OrdinalIgnoreCase) &&
                entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)),
            "Danh mục", "Nhập Xuất");
    }

    private static XDocument ReadSheet(ZipArchive zip, string name)
    {
        var workbook = ReadXml(zip, "xl/workbook.xml");
        var sheet = workbook.Descendants(M + "sheet")
            .FirstOrDefault(el => string.Equals((string?)el.Attribute("name"), name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException($"KHO.xlsx thiếu sheet '{name}'. Không cho phép tự đổi tên sheet.");
        var link = (string?)sheet.Attribute(R + "id");
        var relationships = ReadXml(zip, "xl/_rels/workbook.xml.rels");
        var relationship = relationships.Descendants(P + "Relationship")
            .FirstOrDefault(el => (string?)el.Attribute("Id") == link)
            ?? throw new InvalidDataException($"Không tìm thấy quan hệ của sheet {name}.");
        var target = (string?)relationship.Attribute("Target") ?? "";
        var normalized = new Uri(new Uri("https://local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
        if (!normalized.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Đường dẫn dữ liệu của sheet {name} không hợp lệ.");
        return ReadXml(zip, normalized);
    }

    private static XDocument ReadXml(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"Thiếu thành phần {name} trong KHO.xlsx.");
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string[] ReadStrings(ZipArchive zip)
    {
        if (zip.GetEntry("xl/sharedStrings.xml") is null) return [];
        return ReadXml(zip, "xl/sharedStrings.xml").Descendants(M + "si")
            .Select(si => string.Concat(si.Descendants(M + "t").Select(x => x.Value))).ToArray();
    }

    private static Dictionary<int, Dictionary<string, XElement>> CellsByRow(XDocument xml)
    {
        return xml.Descendants(M + "sheetData").Elements(M + "row")
            .ToDictionary(row => (int?)row.Attribute("r") ?? 0,
                row => row.Elements(M + "c").Where(cell => cell.Attribute("r") is not null)
                    .ToDictionary(cell => new string(((string)cell.Attribute("r")!).TakeWhile(char.IsLetter).ToArray()),
                        cell => cell, StringComparer.OrdinalIgnoreCase));
    }

    private static XElement? Get(Dictionary<string, XElement>? row, string column) =>
        row is not null && row.TryGetValue(column, out var cell) ? cell : null;

    private static bool HasFormula(XElement? cell) => cell?.Element(M + "f") is not null;

    private static string Text(XElement? cell, string[] strings)
    {
        if (cell is null) return "";
        if ((string?)cell.Attribute("t") == "inlineStr")
            return string.Concat(cell.Descendants(M + "t").Select(t => t.Value));
        var raw = (string?)cell.Element(M + "v") ?? "";
        if ((string?)cell.Attribute("t") == "s" &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
            index >= 0 && index < strings.Length)
            return strings[index];
        return raw;
    }

    private static void VerifyHeaders(XDocument document, string[] strings,
        int headerRow, (string Column, string Expected)[] expected)
    {
        var row = CellsByRow(document).GetValueOrDefault(headerRow);
        foreach (var (column, header) in expected)
        {
            var actual = Text(Get(row, column), strings).Trim();
            if (!actual.Equals(header, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Sai cột {column}{headerRow}: yêu cầu '{header}', thực tế '{actual}'. Dừng kiểm tra để bảo vệ workbook.");
        }
    }
}
