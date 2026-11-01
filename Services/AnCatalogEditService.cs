using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Explicitly approved edits to the two non-formula catalog input columns.
/// Only B (name) and E (minimum stock) are supported. An never edits quantity,
/// current stock, protected formulas or another worksheet.
/// A confirmed user request is required before Apply.
/// Workbook parts outside the selected catalog sheet and recalculation metadata
/// remain unchanged after decompression.
/// </summary>
public static class AnCatalogEditService
{
    public enum Field { Name, Minimum }
    public sealed record Command(string Sku, Field Target, string Value);
    public sealed record Plan(
        string Path, string OriginalHash, int Row, string Sku, Field Target,
        string OldValue, string NewValue, double? NumericValue)
    {
        public string Summary =>
            $"An đề xuất sửa file KHO.xlsx:\n" +
            $"Mã vật tư: {Sku}\n" +
            $"Ô thay đổi: Danh mục!{(Target == Field.Name ? "B" : "E")}{Row}\n" +
            $"Giá trị cũ: {OldValue}\n" +
            $"Giá trị mới: {NewValue}\n\n" +
            "Bạn xác nhận thì An mới ghi vào ô nhập liệu này. " +
            "Tất cả công thức, sheet khác và mẫu Phiếu in phải được giữ nguyên. " +
            "HISA sao lưu nguyên file trước khi thay đổi.";
    }
    public sealed record Result(string BackupPath, string WorkbookHash);
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly Regex CommandPattern = new(
        @"(?:đổi|sửa|chỉnh|cập nhật)\s+(?:lại\s+)?(?<field>tên|tồn\s+tối\s+thiểu)\s+(?:vật\s+tư\s+)?mã\s+(?<sku>[\p{L}\p{N}._-]{1,64})\s+(?:thành|là|bằng)\s+(?<value>[^\r\n]{1,180})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryParseCommand(string text, out Command? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var match = CommandPattern.Match(text.Trim());
        if (!match.Success) return false;
        var name = match.Groups["field"].Value.Trim();
        var field = name.Equals("tên", StringComparison.OrdinalIgnoreCase)
            ? Field.Name : Field.Minimum;
        command = new Command(match.Groups["sku"].Value.Trim(), field,
            match.Groups["value"].Value.Trim().TrimEnd('.', '!', ' '));
        return command.Value.Length > 0;
    }

    public static Plan Prepare(string path, string lastSyncedHash, Command command)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
            !Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new FileNotFoundException("An cần tệp KHO.xlsx chính đã chọn trong HISA.", path);
        if (string.IsNullOrWhiteSpace(lastSyncedHash))
            throw new InvalidOperationException("Trước khi cho An sửa Excel, bạn phải đồng bộ KHO.xlsx với HISA.");
        var status = WorkbookWriteSafety.Inspect(path);
        if (!status.CanAttemptWrite) throw new IOException(status.Message);
        var hash = PrimaryWorkbookReconciler.Fingerprint(path);
        if (!hash.Equals(lastSyncedHash, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Excel đã đổi ngoài HISA. Hãy lưu, tính lại và đồng bộ Excel trước khi An sửa.");
        var profile = PrimaryWorkbookInspector.Inspect(path);
        if (profile.MissingProtectedFormulas > 0)
            throw new InvalidDataException("Excel bị thiếu công thức, không cho phép An sửa.");
        var stocks = PrimaryWorkbookReader.ReadStocks(path);
        if (!stocks.TryGetValue(command.Sku, out var stock))
            throw new InvalidDataException("Không tìm thấy mã vật tư chính xác " + command.Sku + " trong Danh mục.");
        double? number = null;
        string value = command.Value;
        if (command.Target == Field.Minimum)
        {
            var sanitized = value.Replace(" ", "");
            if (!double.TryParse(sanitized, NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.GetCultureInfo("vi-VN"), out var d) ||
                !double.IsFinite(d) || d < 0 || d > 1_000_000_000)
                throw new ArgumentException("Tồn tối thiểu phải là số không âm hợp lệ.");
            number = d;
            value = d.ToString("0.####", CultureInfo.GetCultureInfo("vi-VN"));
        }
        else if (value.Length is < 1 or > 150)
            throw new ArgumentException("Tên vật tư phải dài từ 1 đến 150 ký tự.");
        var oldValue = command.Target == Field.Name ? stock.Name :
            stock.Minimum.ToString("0.####", CultureInfo.GetCultureInfo("vi-VN"));
        if (oldValue == value)
            throw new InvalidOperationException("Giá trị này đã đúng; An không cần sửa Excel.");
        return new Plan(Path.GetFullPath(path), hash, stock.Row, stock.Sku,
            command.Target, oldValue, value, number);
    }

    public static Result Apply(Plan plan)
    {
        var status = WorkbookWriteSafety.Inspect(plan.Path);
        if (!status.CanAttemptWrite) throw new IOException(status.Message);
        var dir = Path.GetDirectoryName(plan.Path)!;
        var staged = Path.Combine(dir, ".HISA-An-" + Guid.NewGuid().ToString("N") + ".tmp");
        var backup = Path.Combine(dir, Path.GetFileName(plan.Path) +
            ".An-backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + ".bak");
        var stagedHash = "";
        try
        {
            using (var source = new FileStream(plan.Path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var hash = Convert.ToHexString(SHA256.HashData(source));
                if (!hash.Equals(plan.OriginalHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("KHO.xlsx đã đổi sau khi xem trước. An hủy sửa file.");
                source.Position = 0;
                using var zip = new ZipArchive(source, ZipArchiveMode.Read, true);
                var sheetPath = ResolveCatalogPart(zip);
                var catalog = Load(zip, sheetPath);
                var cell = catalog.Descendants(M + "c").FirstOrDefault(c =>
                    (string?)c.Attribute("r") ==
                    (plan.Target == Field.Name ? "B" : "E") + plan.Row);
                if (cell is null || cell.Element(M + "f") is not null)
                    throw new InvalidDataException("Ô đích không tồn tại hoặc có công thức. An không được ghi đè.");
                var skuCell = catalog.Descendants(M + "c").FirstOrDefault(c =>
                    (string?)c.Attribute("r") == "A" + plan.Row);
                var strings = zip.GetEntry("xl/sharedStrings.xml") is null
                    ? Array.Empty<string>()
                    : Load(zip, "xl/sharedStrings.xml").Descendants(M + "si")
                        .Select(si => string.Concat(si.Descendants(M + "t").Select(t => t.Value))).ToArray();
                if (!ReadCell(skuCell, strings).Equals(plan.Sku, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Mã hàng tại dòng nguồn đã đổi. An không tự đoán lại ô.");

                cell.Element(M + "v")?.Remove();
                cell.Element(M + "is")?.Remove();
                if (plan.Target == Field.Name)
                {
                    cell.SetAttributeValue("t", "inlineStr");
                    cell.Add(new XElement(M + "is", new XElement(M + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"), plan.NewValue)));
                }
                else
                {
                    cell.SetAttributeValue("t", null);
                    cell.Add(new XElement(M + "v",
                        plan.NumericValue!.Value.ToString("R", CultureInfo.InvariantCulture)));
                }

                var metadata = Load(zip, "xl/workbook.xml");
                var calc = metadata.Descendants(M + "calcPr").FirstOrDefault();
                if (calc is null) { calc = new XElement(M + "calcPr"); metadata.Root!.Add(calc); }
                calc.SetAttributeValue("calcMode", "auto");
                calc.SetAttributeValue("fullCalcOnLoad", "1");
                calc.SetAttributeValue("forceFullCalc", "1");

                using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var resultZip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var entry in zip.Entries)
                    {
                        var dest = resultZip.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                        dest.LastWriteTime = entry.LastWriteTime;
                        dest.ExternalAttributes = entry.ExternalAttributes;
                        using var dst = dest.Open();
                        if (entry.FullName == sheetPath) catalog.Save(dst, SaveOptions.DisableFormatting);
                        else if (entry.FullName == "xl/workbook.xml")
                            metadata.Save(dst, SaveOptions.DisableFormatting);
                        else { using var src = entry.Open(); src.CopyTo(dst); }
                    }
                }
                using var check = ZipFile.OpenRead(staged);
                if (check.Entries.Count != zip.Entries.Count)
                    throw new InvalidDataException("File Excel tạm thiếu thành phần; không ghi.");
                foreach (var entry in zip.Entries)
                {
                    var corresponding = check.GetEntry(entry.FullName)
                        ?? throw new InvalidDataException("File Excel tạm thiếu " + entry.FullName);
                    if (entry.FullName == sheetPath || entry.FullName == "xl/workbook.xml") continue;
                    using var before = entry.Open();
                    using var after = corresponding.Open();
                    if (!SHA256.HashData(before).SequenceEqual(SHA256.HashData(after)))
                        throw new InvalidDataException("An đã thay đổi thành phần ngoài phạm vi. Hủy sửa.");
                }
                stagedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(staged)));
            }
            status = WorkbookWriteSafety.Inspect(plan.Path);
            if (!status.CanAttemptWrite) throw new IOException(status.Message);
            using (var guard = new FileStream(plan.Path, FileMode.Open, FileAccess.Read, FileShare.None))
                if (!Convert.ToHexString(SHA256.HashData(guard)).Equals(
                    plan.OriginalHash, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("KHO.xlsx thay đổi trước khi ghi. An hủy thao tác.");

            File.Replace(staged, plan.Path, backup, ignoreMetadataErrors: false);
            return new Result(backup, stagedHash);
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }

    private static string ResolveCatalogPart(ZipArchive zip)
    {
        var book = Load(zip, "xl/workbook.xml");
        var sheet = book.Descendants(M + "sheet").SingleOrDefault(s =>
            (string?)s.Attribute("name") == "Danh mục")
            ?? throw new InvalidDataException("Excel không có sheet Danh mục.");
        var id = (string?)sheet.Attribute(R + "id");
        var relationships = Load(zip, "xl/_rels/workbook.xml.rels");
        var entry = relationships.Descendants(P + "Relationship")
            .SingleOrDefault(e => (string?)e.Attribute("Id") == id)
            ?? throw new InvalidDataException("Không tìm được sheet Danh mục.");
        var target = (string?)entry.Attribute("Target") ?? "";
        var relative = new Uri(new Uri("https://local/xl/workbook.xml"),
            target).AbsolutePath.TrimStart('/');
        if (!relative.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase) ||
            zip.GetEntry(relative) is null)
            throw new InvalidDataException("Sheet Danh mục có đường dẫn không hợp lệ.");
        return relative;
    }

    private static XDocument Load(ZipArchive zip, string path)
    {
        using var stream = (zip.GetEntry(path) ??
            throw new InvalidDataException("Excel thiếu " + path)).Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string ReadCell(XElement? cell, IReadOnlyList<string> strings)
    {
        if (cell is null) return "";
        if ((string?)cell.Attribute("t") == "inlineStr")
            return string.Concat(cell.Descendants(M + "t").Select(t => t.Value));
        var v = (string?)cell.Element(M + "v") ?? "";
        if ((string?)cell.Attribute("t") != "s") return v;
        if (!int.TryParse(v, out var n) || n < 0 || n >= strings.Count)
            throw new InvalidDataException("SharedStrings mã vật tư không hợp lệ.");
        return strings[n];
    }
}
