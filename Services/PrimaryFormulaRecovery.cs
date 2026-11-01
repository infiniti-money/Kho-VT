using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Explicit, audited restoration of ONLY missing protected formula cells from
/// a user-selected and structurally verified .xlsx or HISA .bak backup.
/// Never guesses formula expressions from neighboring rows.
/// </summary>
public static class PrimaryFormulaRecovery
{
    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/package/2006/relationships";
    public sealed record FormulaCell(string Sheet, string Address);
    public sealed record Plan(string TargetPath, string VerifiedBackupPath,
        string TargetHash, string BackupHash, IReadOnlyList<FormulaCell> Missing)
    {
        public string Summary => $"Phát hiện {Missing.Count:N0} ô công thức thiếu.\n" +
            "Khôi phục CHỈ công thức tại: " +
            string.Join(", ", Missing.Take(12).Select(c => c.Sheet + "!" + c.Address)) +
            (Missing.Count > 12 ? $" và {Missing.Count - 12} ô khác" : "") +
            "\nNguồn: " + VerifiedBackupPath +
            "\nHISA sẽ tạo thêm bản sao lưu trước khi sửa và buộc Excel tính lại khi mở. " +
            "Không tự chỉnh những ô có công thức khác mẫu.";
    }

    public static Plan Inspect(string target, string verifiedBackup)
    {
        target = Path.GetFullPath(target);
        verifiedBackup = Path.GetFullPath(verifiedBackup);
        if (target.Equals(verifiedBackup, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Phải chọn một bản sao lưu KHÁC file kho chính.");
        if (!Path.GetExtension(target).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            !(Path.GetExtension(verifiedBackup).Equals(".bak", StringComparison.OrdinalIgnoreCase) ||
              Path.GetExtension(verifiedBackup).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Chỉ nhận KHO.xlsx và bản sao lưu Excel .xlsx/.bak.");
        if (!File.Exists(target) || !File.Exists(verifiedBackup))
            throw new FileNotFoundException("Không tìm thấy file kho hoặc bản sao lưu.");

        var profile = PrimaryWorkbookInspector.Inspect(target);
        using var source = ZipFile.OpenRead(verifiedBackup);
        using var destination = ZipFile.OpenRead(target);
        var missing = FindMissing(destination, source);
        if (profile.MissingProtectedFormulas != missing.Count)
            throw new InvalidDataException("Số công thức thiếu không khớp kết quả kiểm tra, dừng để bảo vệ Excel.");
        return new Plan(target, verifiedBackup, FileHash(target), FileHash(verifiedBackup), missing);
    }

    public static string Restore(Plan plan)
    {
        if (plan.Missing.Count == 0)
            throw new InvalidOperationException("Không có công thức bị thiếu để khôi phục.");
        var status = WorkbookWriteSafety.Inspect(plan.TargetPath);
        if (!status.CanAttemptWrite) throw new IOException(status.Message);
        if (FileHash(plan.TargetPath) != plan.TargetHash || FileHash(plan.VerifiedBackupPath) != plan.BackupHash)
            throw new IOException("File kho hoặc bản sao lưu đã thay đổi sau khi duyệt. Hãy kiểm tra lại.");

        string staged = Path.Combine(Path.GetDirectoryName(plan.TargetPath)!,
            "." + Path.GetFileName(plan.TargetPath) + ".formula-" + Guid.NewGuid().ToString("N") + ".tmp");
        string backupPath = Path.Combine(Path.GetDirectoryName(plan.TargetPath)!,
            Path.GetFileName(plan.TargetPath) + ".HISA-formula-backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") +
            "-" + Guid.NewGuid().ToString("N")[..6] + ".bak");
        try
        {
            using (var locked = new FileStream(plan.TargetPath, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var original = new ZipArchive(locked, ZipArchiveMode.Read, leaveOpen: true))
            using (var donor = ZipFile.OpenRead(plan.VerifiedBackupPath))
            {
                if (FileHash(locked) != plan.TargetHash || FileHash(plan.VerifiedBackupPath) != plan.BackupHash)
                    throw new IOException("Nguồn đã thay đổi trước khi dựng bản khôi phục.");
                var now = FindMissing(original, donor);
                if (!now.SequenceEqual(plan.Missing))
                    throw new IOException("Danh sách công thức thiếu đã thay đổi; hủy khôi phục.");

                var changed = new Dictionary<string, XDocument>(StringComparer.Ordinal);
                foreach (var sheet in new[] { "Danh mục", "Nhập Xuất" })
                {
                    var name = SheetPart(original, sheet);
                    var doc = Load(original, name);
                    var sourceDoc = Load(donor, SheetPart(donor, sheet));
                    var donorCells = Cells(sourceDoc);
                    foreach (var missing in now.Where(c => c.Sheet == sheet))
                    {
                        var cell = Cells(doc).GetValueOrDefault(missing.Address)
                            ?? throw new InvalidDataException("Ô khôi phục bị thiếu: " + missing.Address);
                        var donorFormula = donorCells[missing.Address].Element(M + "f")
                            ?? throw new InvalidDataException("Nguồn không còn công thức tại " + missing.Address);
                        // Preserve source formula attributes, including shared-formula si,
                        // but NEVER copy cached result or input contents.
                        cell.AddFirst(new XElement(donorFormula));
                        cell.Element(M + "v")?.Remove();
                    }
                    if (now.Any(c => c.Sheet == sheet)) changed[name] = doc;
                }
                var metadata = Load(original, "xl/workbook.xml");
                var calc = metadata.Descendants(M + "calcPr").FirstOrDefault();
                if (calc is null) { calc = new XElement(M + "calcPr"); metadata.Root!.Add(calc); }
                calc.SetAttributeValue("calcMode", "auto");
                calc.SetAttributeValue("fullCalcOnLoad", "1");
                calc.SetAttributeValue("forceFullCalc", "1");
                changed["xl/workbook.xml"] = metadata;

                using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var stagedZip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var part in original.Entries)
                    {
                        var entry = stagedZip.CreateEntry(part.FullName, CompressionLevel.Optimal);
                        entry.LastWriteTime = part.LastWriteTime;
                        entry.ExternalAttributes = part.ExternalAttributes;
                        using var dst = entry.Open();
                        if (changed.TryGetValue(part.FullName, out var xml))
                            xml.Save(dst, SaveOptions.DisableFormatting);
                        else { using var src = part.Open(); src.CopyTo(dst); }
                    }
                }
                // Every unrelated OOXML part must remain unchanged byte-for-byte.
                using var stagedZipRead = ZipFile.OpenRead(staged);
                if (stagedZipRead.Entries.Count != original.Entries.Count)
                    throw new InvalidDataException("Bản khôi phục thiếu thành phần Excel.");
                foreach (var part in original.Entries)
                {
                    var newer = stagedZipRead.GetEntry(part.FullName)
                        ?? throw new InvalidDataException("Bản khôi phục thiếu " + part.FullName);
                    if (changed.ContainsKey(part.FullName)) continue;
                    using var a = part.Open();
                    using var b = newer.Open();
                    if (!SHA256.HashData(a).SequenceEqual(SHA256.HashData(b)))
                        throw new InvalidDataException("Đã thay đổi sheet/metadata không thuộc phạm vi: " + part.FullName);
                }
            }
            status = WorkbookWriteSafety.Inspect(plan.TargetPath);
            if (!status.CanAttemptWrite) throw new IOException(status.Message);
            using (var locked = new FileStream(plan.TargetPath, FileMode.Open, FileAccess.Read, FileShare.None))
                if (FileHash(locked) != plan.TargetHash)
                    throw new IOException("File Excel đã thay đổi trước lúc khôi phục; hủy thao tác.");
            if (FileHash(plan.VerifiedBackupPath) != plan.BackupHash)
                throw new IOException("Bản sao lưu đã thay đổi; hủy thao tác.");
            File.Replace(staged, plan.TargetPath, backupPath, ignoreMetadataErrors: false);
            return backupPath;
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }

    private static List<FormulaCell> FindMissing(ZipArchive target, ZipArchive source)
    {
        var missing = new List<FormulaCell>();
        foreach (var (sheet, start, last, cols) in new[]
        {
            ("Danh mục", 4, 900, new[] { "F", "G", "I", "J", "K" }),
            ("Nhập Xuất", 5, PrimaryWorkbookTableExpander.LastRow(Load(target, SheetPart(target, "Nhập Xuất"))),
                new[] { "E", "F", "H", "I", "J" })
        })
        {
            var current = Cells(Load(target, SheetPart(target, sheet)));
            var donor = Cells(Load(source, SheetPart(source, sheet)));
            var catalog = sheet == "Danh mục";
            for (var row = start; row <= last; row++)
            {
                if (catalog)
                {
                    var currentSku = Text(current.GetValueOrDefault("A" + row));
                    var donorSku = Text(donor.GetValueOrDefault("A" + row));
                    if (currentSku != donorSku)
                        throw new InvalidDataException($"Danh mục dòng {row} khác bản sao lưu; không thể xác minh cùng một mẫu.");
                }
                foreach (var col in cols)
                {
                    var addr = col + row;
                    var cur = current.GetValueOrDefault(addr);
                    var trusted = donor.GetValueOrDefault(addr);
                    var currentFormula = cur?.Element(M + "f");
                    var sourceFormula = trusted?.Element(M + "f")
                        ?? throw new InvalidDataException("Bản sao lưu không có công thức chuẩn tại " + sheet + "!" + addr);
                    if (currentFormula is null)
                    {
                        if (cur is null)
                            throw new InvalidDataException($"Ô {sheet}!{addr} không tồn tại; không tự tạo lại ô hay định dạng.");
                        missing.Add(new FormulaCell(sheet, addr));
                    }
                    else if (currentFormula.Value != sourceFormula.Value ||
                             (string?)currentFormula.Attribute("t") != (string?)sourceFormula.Attribute("t") ||
                             (string?)currentFormula.Attribute("si") != (string?)sourceFormula.Attribute("si"))
                        throw new InvalidDataException($"Công thức hiện có tại {sheet}!{addr} khác bản sao lưu; không sửa chồng.");
                }
            }
        }
        return missing;
    }

    private static string SheetPart(ZipArchive zip, string sheet)
    {
        var doc = Load(zip, "xl/workbook.xml");
        var s = doc.Descendants(M + "sheet").SingleOrDefault(x => (string?)x.Attribute("name") == sheet)
            ?? throw new InvalidDataException("Thiếu sheet " + sheet);
        var id = (string?)s.Attribute(R + "id");
        var rel = Load(zip, "xl/_rels/workbook.xml.rels").Descendants(P + "Relationship")
            .SingleOrDefault(x => (string?)x.Attribute("Id") == id)
            ?? throw new InvalidDataException("Mất liên kết sheet " + sheet);
        var target = (string?)rel.Attribute("Target") ?? "";
        var name = new Uri(new Uri("https://local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
        if (!name.StartsWith("xl/worksheets/", StringComparison.Ordinal) || zip.GetEntry(name) is null)
            throw new InvalidDataException("Đường dẫn sheet không hợp lệ.");
        return name;
    }

    private static Dictionary<string, XElement> Cells(XDocument doc) =>
        doc.Descendants(M + "sheetData").Elements(M + "row").Elements(M + "c")
            .Where(c => c.Attribute("r") is not null)
            .ToDictionary(c => (string)c.Attribute("r")!, StringComparer.Ordinal);

    private static string Text(XElement? cell) => cell is null ? "" :
        (string?)cell.Attribute("t") == "inlineStr"
            ? string.Concat(cell.Descendants(M + "t").Select(x => x.Value))
            : (string?)cell.Element(M + "v") ?? "";

    private static XDocument Load(ZipArchive zip, string name)
    {
        using var stream = (zip.GetEntry(name) ?? throw new InvalidDataException("Thiếu " + name)).Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static string FileHash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return FileHash(stream);
    }
    private static string FileHash(Stream stream)
    {
        var originalPosition = stream.Position;
        stream.Position = 0;
        var result = Convert.ToHexString(SHA256.HashData(stream));
        stream.Position = originalPosition;
        return result;
    }
}
