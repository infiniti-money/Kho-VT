using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Appends confirmed Nhập / Xuất rows to the EXISTING table in KHO.xlsx.
/// Only A,C,D,G,K,L may be assigned. B and calculated E,F,H,I,J remain untouched.
/// All ZIP parts other than the "Nhập Xuất" sheet and Excel calc metadata
/// are copied unchanged. Existing formulas are never overwritten.
/// No rewriting through ClosedXML: charts, 5 other sheets, external links,
/// print layouts and existing shared formulas must survive byte-for-byte.
/// </summary>
public static class PrimaryWorkbookTransactionWriter
{
    public sealed record Input(DateOnly Date, string Type, string Sku, double Quantity,
        string GiaoNhan, string Note);
    public sealed record Result(int FirstRow, int RowCount, string BackupPath);

    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly string[] Writable = ["A", "C", "D", "G", "K", "L"];
    private static readonly string[] Calculated = ["E", "F", "H", "I", "J"];
    private const int FirstDataRow = 5;

    public static Result Append(string workbookPath, string docNo, IReadOnlyList<Input> lines)
    {
        if (string.IsNullOrWhiteSpace(docNo) || docNo.Length > 70 ||
            docNo.Any(ch => !char.IsLetterOrDigit(ch) && ch is not '-' and not '_'))
            throw new ArgumentException("Mã phiếu không hợp lệ.", nameof(docNo));
        if (lines.Count == 0) throw new InvalidDataException("Phiếu không có mặt hàng.");
        foreach (var line in lines)
        {
            if (line.Type is not ("Nhập" or "Xuất"))
                throw new InvalidDataException("File KHO.xlsx chỉ nhận loại Nhập hoặc Xuất, không ghi Điều chuyển.");
            if (string.IsNullOrWhiteSpace(line.Sku) || !double.IsFinite(line.Quantity) ||
                line.Quantity <= 0 || line.Sku.Length > 130)
                throw new InvalidDataException("Mã vật tư hoặc số lượng không hợp lệ.");
            if (line.GiaoNhan.Length > 1024 || line.Note.Length > 2048)
                throw new InvalidDataException("Thông tin giao nhận hoặc ghi chú quá dài.");
        }

        var available = WorkbookWriteSafety.Inspect(workbookPath);
        if (!available.CanAttemptWrite)
            throw new IOException(available.Message);
        var path = Path.GetFullPath(workbookPath);
        // Inspector detects deleted formulas and different source workbook layouts.
        var profile = PrimaryWorkbookInspector.Inspect(path);
        if (profile.MissingProtectedFormulas > 0)
            throw new InvalidDataException("Workbook bị thiếu công thức. Dừng ghi để bảo vệ công thức trong KHO.xlsx.");
        var knownSkus = PrimaryWorkbookReader.ReadStocks(path);
        foreach (var line in lines)
            if (!knownSkus.ContainsKey(line.Sku.Trim()))
                throw new InvalidDataException($"Mã vật tư {line.Sku} chưa có trong sheet Danh mục; không ghi phiếu.");

        string temp = Path.Combine(Path.GetDirectoryName(path)!,
            "." + Path.GetFileName(path) + ".HISA-" + Guid.NewGuid().ToString("N") + ".tmp");
        string backup = Path.Combine(Path.GetDirectoryName(path)!,
            Path.GetFileName(path) + ".HISA-backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") +
            "-" + Guid.NewGuid().ToString("N")[..6] + ".bak");
        int firstRow = 0;
        byte[] originalHash;
        try
        {
            // Exclusive access WHILE preparing the replacement. If Excel is open,
            // Windows rejects the handle; we do not write anywhere in that case.
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                originalHash = SHA256.HashData(source);
                source.Position = 0;
                using var originalZip = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
                var sheetEntry = ResolveSheet(originalZip, "Nhập Xuất");
                XDocument document = LoadXml(originalZip, sheetEntry);
                var workbookMetadata = LoadXml(originalZip, "xl/workbook.xml");
                var calc = workbookMetadata.Descendants(M + "calcPr").FirstOrDefault();
                if (calc is null)
                {
                    calc = new XElement(M + "calcPr");
                    workbookMetadata.Root!.Add(calc);
                }
                // Cached formula results may be stale after writing input values.
                // Ask Excel to recalculate the existing formulas on next opening;
                // no formulas in any worksheet are modified.
                calc.SetAttributeValue("calcMode", "auto");
                calc.SetAttributeValue("fullCalcOnLoad", "1");
                calc.SetAttributeValue("forceFullCalc", "1");
                var sheetData = document.Descendants(M + "sheetData").Single();
                var currentLast = PrimaryWorkbookTableExpander.LastRow(document);
                PrimaryWorkbookTableExpander.VerifyTableBoundary(originalZip, currentLast);
                var rows = sheetData.Elements(M + "row")
                    .ToDictionary(x => (int?)x.Attribute("r") ?? 0);
                var sharedStrings = ReadStrings(originalZip);
                var occupiedEnd = FirstDataRow - 1;
                for (int row = FirstDataRow; row <= currentLast; row++)
                {
                    if (!rows.TryGetValue(row, out var node)) continue;
                    if (Writable.Concat(["B"]).Any(col => !IsBlank(node, col + row, sharedStrings)))
                        occupiedEnd = row;
                }
                firstRow = occupiedEnd + 1;
                var targetLast = PrimaryWorkbookTableExpander.RequiredLastRow(
                    currentLast, occupiedEnd, lines.Count);
                var changedParts = PrimaryWorkbookTableExpander.Prepare(
                    originalZip, sheetEntry, document, currentLast, targetLast);
                rows = sheetData.Elements(M + "row")
                    .ToDictionary(x => (int?)x.Attribute("r") ?? 0);
                if (targetLast - (occupiedEnd + lines.Count) < PrimaryWorkbookTableExpander.MinimumSpareRows)
                    throw new InvalidDataException("Không đảm bảo được 50 dòng nhập liệu trống.");

                var token = "[HISA:" + docNo + "]";
                if (document.Descendants(M + "c").Where(c => ((string?)c.Attribute("r"))?.StartsWith("L", StringComparison.Ordinal) == true)
                    .Any(c => CellText(c, sharedStrings).Contains(token, StringComparison.Ordinal)))
                    throw new InvalidDataException("Phiếu này đã được ghi vào Excel; không được ghi trùng.");

                for (int i = 0; i < lines.Count; i++)
                {
                    int rowNumber = firstRow + i;
                    if (!rows.TryGetValue(rowNumber, out var row))
                        throw new InvalidDataException($"Không có dòng định dạng sẵn {rowNumber} trong sheet Nhập Xuất.");
                    foreach (var col in Calculated)
                        if (Cell(row, col + rowNumber)?.Element(M + "f") is null)
                            throw new InvalidDataException($"Thiếu công thức {col}{rowNumber}; dừng ghi.");
                    foreach (var col in Writable.Concat(["B"]))
                    {
                        var cell = Cell(row, col + rowNumber);
                        if (cell?.Element(M + "f") is not null || !string.IsNullOrWhiteSpace(CellText(cell, sharedStrings)))
                            throw new InvalidDataException($"Ô {col}{rowNumber} không trống hoặc có công thức; dừng ghi.");
                    }
                    var line = lines[i];
                    // Column B is the original Excel printing selector; intentionally unchanged.
                    SetNumber(row, "A" + rowNumber, line.Date.ToDateTime(TimeOnly.MinValue).ToOADate());
                    SetString(row, "C" + rowNumber, line.Type);
                    SetString(row, "D" + rowNumber, line.Sku.Trim());
                    SetNumber(row, "G" + rowNumber, line.Quantity);
                    SetString(row, "K" + rowNumber, line.GiaoNhan.Trim());
                    var note = line.Note.Trim();
                    SetString(row, "L" + rowNumber,
                        string.IsNullOrEmpty(note) ? token : note + " " + token);
                }
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                using (var resultZip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var entry in originalZip.Entries)
                    {
                        var created = resultZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                        created.LastWriteTime = entry.LastWriteTime;
                        created.ExternalAttributes = entry.ExternalAttributes;
                        using var outStream = created.Open();
                        if (entry.FullName == "xl/workbook.xml")
                            workbookMetadata.Save(outStream, SaveOptions.DisableFormatting);
                        else if (changedParts.TryGetValue(entry.FullName, out var changed))
                            changed.Save(outStream, SaveOptions.DisableFormatting);
                        else
                        {
                            using var inStream = entry.Open();
                            inStream.CopyTo(outStream);
                        }
                    }
                }
                output.Flush(true);
                }
                // No changes to source file yet; caller will receive an error if anything
                // in staged ZIP other than the target sheet was accidentally modified.
                using var staged = ZipFile.OpenRead(temp);
                var expected = originalZip.Entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
                if (staged.Entries.Count != originalZip.Entries.Count)
                    throw new InvalidDataException("Gói Excel mới thiếu thành phần. Không ghi.");
                foreach (var entry in staged.Entries)
                {
                    if (!expected.TryGetValue(entry.FullName, out var oldEntry))
                        throw new InvalidDataException("Gói Excel có thành phần lạ.");
                    if (entry.FullName == "xl/workbook.xml" || changedParts.ContainsKey(entry.FullName)) continue;
                    using var oldStream = oldEntry.Open();
                    using var newStream = entry.Open();
                    if (!SHA256.HashData(oldStream).SequenceEqual(SHA256.HashData(newStream)))
                        throw new InvalidDataException($"Thành phần {entry.FullName} bị thay đổi. Không ghi.");
                }
            }

            // Fail closed if Excel reopened or source changed between staging and commit.
            var latest = WorkbookWriteSafety.Inspect(path);
            if (!latest.CanAttemptWrite) throw new IOException(latest.Message);
            using (var finalSource = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                if (!originalHash.SequenceEqual(SHA256.HashData(finalSource)))
                    throw new IOException("File Excel bị thay đổi trong khi chuẩn bị ghi. Hủy thao tác để tránh mất dữ liệu.");

            // Atomic replacement with automatically retained original backup.
            File.Replace(temp, path, backup, ignoreMetadataErrors: false);
            return new Result(firstRow, lines.Count, backup);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static XDocument LoadXml(ZipArchive zip, string name)
    {
        using var stream = (zip.GetEntry(name) ?? throw new InvalidDataException($"Thiếu {name}")).Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string ResolveSheet(ZipArchive zip, string title)
    {
        var workbook = LoadXml(zip, "xl/workbook.xml");
        if ((string?)workbook.Root?.Element(M + "workbookPr")?.Attribute("date1904") is "1" or "true")
            throw new InvalidDataException("File dùng hệ ngày Excel 1904, chưa hỗ trợ ghi tự động.");
        var sheet = workbook.Descendants(M + "sheet")
            .SingleOrDefault(x => (string?)x.Attribute("name") == title)
            ?? throw new InvalidDataException("Không tìm thấy sheet Nhập Xuất.");
        var relId = (string?)sheet.Attribute(R + "id");
        var rels = LoadXml(zip, "xl/_rels/workbook.xml.rels");
        var rel = rels.Descendants(P + "Relationship")
            .Single(x => (string?)x.Attribute("Id") == relId);
        var target = (string?)rel.Attribute("Target") ?? "";
        var normalized = new Uri(new Uri("https://internal/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
        if (!normalized.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) ||
            zip.GetEntry(normalized) is null)
            throw new InvalidDataException("Đường dẫn sheet Nhập Xuất không hợp lệ.");
        return normalized;
    }

    private static string[] ReadStrings(ZipArchive zip) =>
        zip.GetEntry("xl/sharedStrings.xml") is null ? [] :
        LoadXml(zip, "xl/sharedStrings.xml").Descendants(M + "si")
            .Select(si => string.Concat(si.Descendants(M + "t").Select(t => t.Value))).ToArray();

    private static XElement? Cell(XElement row, string address) =>
        row.Elements(M + "c").FirstOrDefault(c => (string?)c.Attribute("r") == address);

    private static string CellText(XElement? cell, string[] strings)
    {
        if (cell is null) return "";
        if ((string?)cell.Attribute("t") == "inlineStr")
            return string.Concat(cell.Descendants(M + "t").Select(x => x.Value));
        var value = (string?)cell.Element(M + "v") ?? "";
        if ((string?)cell.Attribute("t") == "s" &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int index) &&
            index >= 0 && index < strings.Length) return strings[index];
        return value;
    }
    private static bool IsBlank(XElement row, string address, string[] strings)
    {
        var cell = Cell(row, address);
        return cell?.Element(M + "f") is null && string.IsNullOrWhiteSpace(CellText(cell, strings));
    }
    private static void SetNumber(XElement row, string address, double value)
    {
        var cell = Cell(row, address) ?? throw new InvalidDataException("Ô " + address + " không có định dạng.");
        cell.Attribute("t")?.Remove();
        cell.Element(M + "is")?.Remove();
        cell.Element(M + "v")?.Remove();
        cell.Add(new XElement(M + "v", value.ToString("R", CultureInfo.InvariantCulture)));
    }
    private static void SetString(XElement row, string address, string value)
    {
        var cell = Cell(row, address) ?? throw new InvalidDataException("Ô " + address + " không có định dạng.");
        cell.SetAttributeValue("t", "inlineStr");
        cell.Element(M + "v")?.Remove();
        cell.Element(M + "is")?.Remove();
        cell.Add(new XElement(M + "is",
            new XElement(M + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value)));
    }
}
