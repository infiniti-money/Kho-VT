using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Strict read-only projection of HISA's "Phiếu yêu cầu" worksheet.
/// The sheet is optional for older workbooks. Never infer stock movements
/// from material requests and never change the workbook during import.
/// </summary>
public static class MaterialRequestWorkbookReader
{
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly string[] Headers =
    [
        "ID phiếu", "Số phiếu", "Ngày", "Loại yêu cầu", "Cần trước", "Bộ phận",
        "Kho", "Người yêu cầu", "Người nhận / nguồn", "Trạng thái",
        "Mã hàng", "Tên vật tư", "ĐVT", "Số lượng yêu cầu", "Ghi chú"
    ];

    /// <returns>Null when the optional worksheet is absent; otherwise the
    /// validated request collection, including an empty worksheet.</returns>
    public static IReadOnlyList<MaterialRequest>? Read(string workbookPath)
    {
        using var zip = new ZipArchive(ExcelReadSnapshot.Capture(workbookPath), ZipArchiveMode.Read);
        var book = Load(zip, "xl/workbook.xml");
        var sheet = book.Descendants(M + "sheet").SingleOrDefault(x =>
            (string?)x.Attribute("name") == MaterialRequestWorkbookWriter.SheetName);
        if (sheet is null) return null;

        var relations = Load(zip, "xl/_rels/workbook.xml.rels");
        var id = (string?)sheet.Attribute(R + "id")
            ?? throw new InvalidDataException("Sheet Phiếu yêu cầu thiếu quan hệ.");
        var relation = relations.Descendants(P + "Relationship")
            .SingleOrDefault(x => (string?)x.Attribute("Id") == id)
            ?? throw new InvalidDataException("Không tìm thấy dữ liệu sheet Phiếu yêu cầu.");
        var target = (string?)relation.Attribute("Target") ?? "";
        if (!target.StartsWith("worksheets/", StringComparison.Ordinal) ||
            target.Contains("..", StringComparison.Ordinal) ||
            !target.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Đường dẫn sheet Phiếu yêu cầu không hợp lệ.");
        var xml = Load(zip, "xl/" + target);
        var strings = ReadSharedStrings(zip);
        var data = xml.Root?.Element(M + "sheetData")
            ?? throw new InvalidDataException("Phiếu yêu cầu thiếu dữ liệu.");
        var rows = data.Elements(M + "row").ToArray();
        var header = rows.SingleOrDefault(x => (int?)x.Attribute("r") == 1)
            ?? throw new InvalidDataException("Phiếu yêu cầu thiếu hàng tiêu đề.");
        for (int index = 0; index < Headers.Length; index++)
            if (Value(header, 1, index + 1, strings) != Headers[index])
                throw new InvalidDataException($"Phiếu yêu cầu có tiêu đề cột {index + 1} khác cấu trúc HISA.");

        var requests = new Dictionary<string, MaterialRequest>(StringComparer.Ordinal);
        var numbers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int previousRow = 1;
        foreach (var row in rows.Where(x => (int?)x.Attribute("r") != 1))
        {
            int rowNumber = (int?)row.Attribute("r") ?? 0;
            if (rowNumber <= previousRow)
                throw new InvalidDataException("Các dòng Phiếu yêu cầu bị trùng hoặc không theo thứ tự.");
            previousRow = rowNumber;
            if (row.Elements(M + "c").Any(x => x.Element(M + "f") is not null))
                throw new InvalidDataException($"Phiếu yêu cầu dòng {rowNumber} có công thức; cần kiểm tra thủ công.");
            var fields = Enumerable.Range(1, Headers.Length)
                .Select(column => Value(row, rowNumber, column, strings)).ToArray();
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            var idValue = fields[0].Trim();
            var docNo = fields[1].Trim();
            if (idValue.Length == 0 || docNo.Length == 0 || idValue.Length > 150 || docNo.Length > 100)
                throw new InvalidDataException($"Phiếu yêu cầu dòng {rowNumber} thiếu ID hoặc số phiếu.");
            if (fields[3] is not ("Yêu cầu cấp vật tư" or "Yêu cầu nhập vật tư" or "Yêu cầu cấp vật tư dự phòng"))
                throw new InvalidDataException($"Loại phiếu yêu cầu ở dòng {rowNumber} không hợp lệ.");
            if (!TryDate(fields[2], out var date) ||
                (fields[4].Length > 0 && !TryDate(fields[4], out _)))
                throw new InvalidDataException($"Ngày của phiếu yêu cầu dòng {rowNumber} không hợp lệ.");
            var neededBy = fields[4].Length == 0 ? (DateOnly?)null :
                ParseDate(fields[4]);
            if (!double.TryParse(fields[13], NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity) ||
                !double.IsFinite(quantity) || quantity <= 0 ||
                string.IsNullOrWhiteSpace(fields[11]) || string.IsNullOrWhiteSpace(fields[12]))
                throw new InvalidDataException($"Vật tư hoặc số lượng ở Phiếu yêu cầu dòng {rowNumber} không hợp lệ.");

            var parsed = new MaterialRequest
            {
                Id = idValue, DocNo = docNo, Date = date, Type = fields[3],
                NeededBy = neededBy, Department = fields[5], Warehouse = fields[6],
                Requester = fields[7], Recipient = fields[8], Status = fields[9],
                Note = fields[14]
            };
            if (numbers.TryGetValue(docNo, out var existingId) && existingId != idValue)
                throw new InvalidDataException("Hai phiếu yêu cầu khác ID có cùng số phiếu: " + docNo);
            numbers[docNo] = idValue;
            if (!requests.TryGetValue(idValue, out var request))
            {
                request = parsed;
                requests.Add(idValue, request);
            }
            else if (request.DocNo != parsed.DocNo || request.Date != parsed.Date ||
                request.Type != parsed.Type || request.NeededBy != parsed.NeededBy ||
                request.Department != parsed.Department || request.Warehouse != parsed.Warehouse ||
                request.Requester != parsed.Requester || request.Recipient != parsed.Recipient ||
                request.Status != parsed.Status || request.Note != parsed.Note)
                throw new InvalidDataException($"Phiếu {docNo} có thông tin khác nhau giữa các dòng vật tư.");
            request.Lines.Add(new MovementLine
            {
                Sku = fields[10], ItemName = fields[11], Unit = fields[12], Quantity = quantity
            });
        }
        return requests.Values.ToArray();
    }

    private static string Value(XElement row, int rowIndex, int column, IReadOnlyList<string> strings)
    {
        string address = ((char)('A' + column - 1)).ToString() + rowIndex;
        var cell = row.Elements(M + "c").FirstOrDefault(x => (string?)x.Attribute("r") == address);
        if (cell is null) return "";
        var type = (string?)cell.Attribute("t") ?? "";
        if (type == "inlineStr")
            return string.Concat(cell.Element(M + "is")?.Descendants(M + "t").Select(x => x.Value)
                ?? Enumerable.Empty<string>()).Trim();
        var value = (string?)cell.Element(M + "v") ?? "";
        if (type == "s")
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
                index < 0 || index >= strings.Count)
                throw new InvalidDataException($"Chỉ số shared string không hợp lệ tại {address}.");
            return strings[index].Trim();
        }
        return value.Trim();
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        if (zip.GetEntry("xl/sharedStrings.xml") is null) return [];
        return Load(zip, "xl/sharedStrings.xml").Descendants(M + "si")
            .Select(si => string.Concat(si.Descendants(M + "t").Select(x => x.Value))).ToList();
    }

    private static bool TryDate(string value, out DateOnly date)
    {
        if (DateOnly.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) &&
            serial > 0 && serial < 2958465)
        {
            try
            {
                date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
                return true;
            }
            catch (ArgumentException) { }
        }
        date = default;
        return false;
    }

    private static DateOnly ParseDate(string value) =>
        TryDate(value, out var date) ? date :
        throw new InvalidDataException("Ngày yêu cầu không hợp lệ.");

    private static XDocument Load(ZipArchive zip, string path)
    {
        using var stream = (zip.GetEntry(path) ??
            throw new InvalidDataException("Thiếu thành phần Excel " + path)).Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }
}
