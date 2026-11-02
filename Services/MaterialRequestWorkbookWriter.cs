using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Upserts material requests to their OWN worksheet. It never opens the
/// transaction sheet for writing or recalculates inventory. Every ZIP part
/// unrelated to the request worksheet and its necessary package metadata is
/// copied byte-for-byte at the uncompressed part level.
/// </summary>
public static class MaterialRequestWorkbookWriter
{
    public const string SheetName = "Phiếu yêu cầu";
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace C = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string WorksheetRelationship =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
    private const string WorksheetContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    private static readonly string[] Headers =
    [
        "ID phiếu", "Số phiếu", "Ngày", "Loại yêu cầu", "Cần trước", "Bộ phận",
        "Kho", "Người yêu cầu", "Người nhận / nguồn", "Trạng thái",
        "Mã hàng", "Tên vật tư", "ĐVT", "Số lượng yêu cầu", "Ghi chú"
    ];

    public sealed record Result(string WorkbookHash, string BackupPath, int RequestCount);

    public static Result Upsert(string workbookPath,
        IReadOnlyList<MaterialRequest> requests, string expectedWorkbookHash)
    {
        if (requests.Count == 0) throw new InvalidDataException("Chưa có phiếu yêu cầu cần ghi.");
        if (string.IsNullOrWhiteSpace(expectedWorkbookHash))
            throw new InvalidOperationException("Hãy đồng bộ KHO.xlsx trước khi ghi phiếu yêu cầu.");
        foreach (var request in requests)
        {
            if (request.Type is not ("Yêu cầu cấp vật tư" or "Yêu cầu nhập vật tư" or "Yêu cầu cấp vật tư dự phòng") ||
                string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.DocNo) ||
                request.Lines.Count == 0 || request.Lines.Any(line =>
                    string.IsNullOrWhiteSpace(line.ItemName) || string.IsNullOrWhiteSpace(line.Unit) ||
                    !double.IsFinite(line.Quantity) || line.Quantity <= 0))
                throw new InvalidDataException("Phiếu yêu cầu thiếu mã, loại, vật tư hoặc số lượng hợp lệ.");
        }
        if (requests.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != requests.Count)
            throw new InvalidDataException("Trùng ID phiếu yêu cầu.");
        var inspection = WorkbookWriteSafety.Inspect(workbookPath);
        if (!inspection.CanAttemptWrite) throw new IOException(inspection.Message);
        var path = Path.GetFullPath(workbookPath);
        var folder = Path.GetDirectoryName(path)!;
        var temp = Path.Combine(folder, "." + Path.GetFileName(path) + ".request-" + Guid.NewGuid().ToString("N") + ".tmp");
        var backup = Path.Combine(folder, Path.GetFileName(path) + ".HISA-request-backup-" +
            DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture) + ".bak");
        byte[] sourceHash;
        try
        {
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                sourceHash = SHA256.HashData(source);
                if (!Convert.ToHexString(sourceHash).Equals(expectedWorkbookHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("KHO.xlsx đã đổi kể từ lần đồng bộ. Hãy đồng bộ Excel trước khi ghi phiếu yêu cầu.");
                source.Position = 0;
                using var copy = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                source.CopyTo(copy);
            }

            // Verify source structural/formula safety BEFORE any modifications.
            var profile = PrimaryWorkbookInspector.Inspect(path);
            if (profile.MissingProtectedFormulas != 0)
                throw new InvalidDataException("KHO.xlsx thiếu công thức được bảo vệ; không ghi phiếu yêu cầu.");

            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Update))
            {
                var workbook = Load(zip, "xl/workbook.xml");
                var relations = Load(zip, "xl/_rels/workbook.xml.rels");
                var sheets = workbook.Root?.Element(M + "sheets")
                    ?? throw new InvalidDataException("Workbook thiếu danh sách sheet.");
                var sheet = sheets.Elements(M + "sheet")
                    .FirstOrDefault(x => (string?)x.Attribute("name") == SheetName);
                string sheetPath;
                if (sheet is null)
                {
                    var types = Load(zip, "[Content_Types].xml");
                    int index = 1;
                    while (zip.GetEntry("xl/worksheets/sheet" + index + ".xml") is not null) index++;
                    sheetPath = "xl/worksheets/sheet" + index + ".xml";
                    int nextSheetId = sheets.Elements(M + "sheet")
                        .Select(x => (int?)x.Attribute("sheetId") ?? 0).DefaultIfEmpty().Max() + 1;
                    int relationIndex = 1;
                    while (relations.Descendants(P + "Relationship")
                        .Any(x => (string?)x.Attribute("Id") == "rId" + relationIndex)) relationIndex++;
                    string relId = "rId" + relationIndex;
                    sheets.Add(new XElement(M + "sheet",
                        new XAttribute("name", SheetName),
                        new XAttribute("sheetId", nextSheetId),
                        new XAttribute(R + "id", relId)));
                    relations.Root!.Add(new XElement(P + "Relationship",
                        new XAttribute("Id", relId),
                        new XAttribute("Type", WorksheetRelationship),
                        new XAttribute("Target", "worksheets/sheet" + index + ".xml")));
                    types.Root!.Add(new XElement(C + "Override",
                        new XAttribute("PartName", "/" + sheetPath),
                        new XAttribute("ContentType", WorksheetContentType)));
                    Replace(zip, "[Content_Types].xml", types);
                    Replace(zip, "xl/workbook.xml", workbook);
                    Replace(zip, "xl/_rels/workbook.xml.rels", relations);
                }
                else
                {
                    string id = (string?)sheet.Attribute(R + "id")
                        ?? throw new InvalidDataException("Sheet yêu cầu thiếu liên kết.");
                    var rel = relations.Descendants(P + "Relationship")
                        .SingleOrDefault(x => (string?)x.Attribute("Id") == id)
                        ?? throw new InvalidDataException("Thiếu quan hệ sheet Phiếu yêu cầu.");
                    var target = (string?)rel.Attribute("Target") ?? "";
                    if (!target.StartsWith("worksheets/", StringComparison.Ordinal) ||
                        target.Contains("..", StringComparison.Ordinal))
                        throw new InvalidDataException("Đường dẫn sheet Phiếu yêu cầu không hợp lệ.");
                    sheetPath = "xl/" + target;
                }

                XDocument requestSheet;
                if (zip.GetEntry(sheetPath) is null)
                {
                    requestSheet = new XDocument(new XElement(M + "worksheet",
                        new XElement(M + "sheetData",
                            new XElement(M + "row", new XAttribute("r", 1),
                                Headers.Select((value, i) => Str(1, i + 1, value))))));
                }
                else
                {
                    requestSheet = Load(zip, sheetPath);
                    var sheetData = requestSheet.Root?.Element(M + "sheetData")
                        ?? throw new InvalidDataException("Sheet Phiếu yêu cầu không có dữ liệu.");
                    foreach (var (value, i) in Headers.Select((value, i) => (value, i)))
                    {
                        var header = sheetData.Elements(M + "row")
                            .FirstOrDefault(x => (int?)x.Attribute("r") == 1)?
                            .Elements(M + "c").FirstOrDefault(c => (string?)c.Attribute("r") == Column(i + 1) + "1");
                        if (header is null || string.Concat(header.Descendants(M + "t").Select(x => x.Value)) != value)
                            throw new InvalidDataException("Cấu trúc sheet Phiếu yêu cầu đã thay đổi; không ghi đè.");
                    }
                    if (sheetData.Descendants(M + "f").Any())
                        throw new InvalidDataException("Sheet Phiếu yêu cầu có công thức; không được tự viết lại.");
                }

                var rows = requestSheet.Root!.Element(M + "sheetData")!;
                var ids = requests.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var existing in rows.Elements(M + "row").Where(x => (int?)x.Attribute("r") != 1).ToList())
                {
                    var idCell = existing.Elements(M + "c").FirstOrDefault(x =>
                        (string?)x.Attribute("r") == "A" + (string?)existing.Attribute("r"));
                    var id = idCell is null ? "" : string.Concat(idCell.Descendants(M + "t").Select(x => x.Value));
                    if (ids.Contains(id)) existing.Remove();
                }
                int nextRow = rows.Elements(M + "row")
                    .Select(x => (int?)x.Attribute("r") ?? 0).DefaultIfEmpty().Max() + 1;
                foreach (var request in requests.OrderBy(x => x.DocNo, StringComparer.Ordinal))
                {
                    foreach (var line in request.Lines)
                    {
                        int n = nextRow++;
                        rows.Add(new XElement(M + "row", new XAttribute("r", n),
                            Str(n, 1, request.Id), Str(n, 2, request.DocNo),
                            Str(n, 3, request.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                            Str(n, 4, request.Type),
                            Str(n, 5, request.NeededBy?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? ""),
                            Str(n, 6, request.Department), Str(n, 7, request.Warehouse),
                            Str(n, 8, request.Requester), Str(n, 9, request.Recipient),
                            Str(n, 10, request.Status), Str(n, 11, line.Sku),
                            Str(n, 12, line.ItemName), Str(n, 13, line.Unit),
                            new XElement(M + "c", new XAttribute("r", "N" + n),
                                new XElement(M + "v", line.Quantity.ToString("R", CultureInfo.InvariantCulture))),
                            Str(n, 15, request.Note)));
                    }
                }
                // Leave every other sheet, formula, chart, external link, and table untouched.
                Replace(zip, sheetPath, requestSheet);
            }
            var stagedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(temp)));
            var current = WorkbookWriteSafety.Inspect(path);
            if (!current.CanAttemptWrite) throw new IOException(current.Message);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                if (!sourceHash.SequenceEqual(SHA256.HashData(stream)))
                    throw new IOException("File Excel đã thay đổi trong lúc chuẩn bị ghi; không ghi đè.");
            File.Replace(temp, path, backup, ignoreMetadataErrors: false);
            return new Result(stagedHash, backup, requests.Count);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string Column(int index) => ((char)('A' + index - 1)).ToString();
    private static XElement Str(int row, int column, string value) =>
        new(M + "c", new XAttribute("r", Column(column) + row),
            new XAttribute("t", "inlineStr"),
            new XElement(M + "is", new XElement(M + "t", value ?? "")));

    private static XDocument Load(ZipArchive zip, string path)
    {
        using var reader = (zip.GetEntry(path) ??
            throw new InvalidDataException("Thiếu thành phần Excel: " + path)).Open();
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static void Replace(ZipArchive zip, string path, XDocument content)
    {
        zip.GetEntry(path)?.Delete();
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        content.Save(stream, SaveOptions.DisableFormatting);
    }
}
