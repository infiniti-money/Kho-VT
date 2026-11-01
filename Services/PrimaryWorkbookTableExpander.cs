using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace HisaWarehouse.Services;

/// <summary>
/// Grows the existing Nhập Xuất table, calculated columns and their dependent
/// Excel formula ranges. Only invoked on a staged copy; the original remains
/// untouched until the transaction writer verifies and atomically replaces it.
/// </summary>
public static class PrimaryWorkbookTableExpander
{
    public const int MinimumSpareRows = 50;
    public const int GrowthRows = 200;
    public const int FirstRow = 5;
    public const int InitialLastRow = 965;

    private static readonly XNamespace M = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly Regex RefInFormula = new(
        @"(?<![A-Za-z0-9_])(?<col>\$?[A-Z]{1,3})(?<abs>\$?)(?<row>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RangeEnd = new(
        @"(?<start>\$?[A-Z]{1,3}\$?\d+):(?<col>\$?[A-Z]{1,3}\$?)(?<last>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static int LastRow(XDocument sheet)
    {
        var rows = sheet.Descendants(M + "sheetData").Elements(M + "row")
            .Select(r => (int?)r.Attribute("r") ?? 0);
        return Math.Max(InitialLastRow, rows.DefaultIfEmpty(0).Max());
    }

    public static int RequiredLastRow(int currentLast, int lastUsedRow, int newLines)
    {
        if (currentLast < InitialLastRow || lastUsedRow < FirstRow - 1 ||
            newLines < 0 || lastUsedRow > currentLast)
            throw new InvalidDataException("Vùng bảng Nhập Xuất không hợp lệ.");
        var needed = checked(lastUsedRow + newLines + MinimumSpareRows);
        if (needed > 1_048_576)
            throw new InvalidDataException("Số dòng Excel vượt giới hạn cho phép.");
        if (needed <= currentLast) return currentLast;
        var growth = checked(currentLast + GrowthRows);
        return Math.Min(1_048_576, Math.Max(needed, growth));
    }

    public static Dictionary<string, XDocument> Prepare(
        ZipArchive originalZip, string transactionSheetEntry,
        XDocument tx, int oldLastRow, int targetLastRow)
    {
        var changes = new Dictionary<string, XDocument>(StringComparer.Ordinal)
        {
            [transactionSheetEntry] = tx
        };
        if (targetLastRow == oldLastRow) return changes;
        if (targetLastRow <= oldLastRow || oldLastRow < InitialLastRow)
            throw new InvalidDataException("Dải mở rộng không hợp lệ.");
        var rowsNode = tx.Descendants(M + "sheetData").Single();
        var existing = rowsNode.Elements(M + "row")
            .ToDictionary(r => (int?)r.Attribute("r") ?? 0);
        if (!existing.TryGetValue(oldLastRow, out var prototype))
            throw new InvalidDataException("Dòng cuối bảng không có định dạng mẫu.");

        // Every computed column must have a formula. A shared continuation is a
        // legitimate formula; recover its actual expression from the shared master.
        string FormulaAt(string address)
        {
            var cell = existing.Values.SelectMany(r => r.Elements(M + "c"))
                .FirstOrDefault(c => (string?)c.Attribute("r") == address);
            var f = cell?.Element(M + "f")
                ?? throw new InvalidDataException("Thiếu công thức " + address);
            if (!string.IsNullOrWhiteSpace(f.Value)) return f.Value;
            var si = (string?)f.Attribute("si");
            var master = tx.Descendants(M + "f")
                .FirstOrDefault(x => (string?)x.Attribute("si") == si &&
                                     !string.IsNullOrWhiteSpace(x.Value));
            if (si is null || master is null)
                throw new InvalidDataException("Không tìm được công thức shared " + address);
            var masterCell = master.Parent!;
            var masterRow = int.Parse(new string(((string)masterCell.Attribute("r")!)
                .SkipWhile(c => !char.IsDigit(c)).ToArray()), CultureInfo.InvariantCulture);
            return ShiftRelativeRows(master.Value, oldLastRow - masterRow);
        }

        var formulas = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var col in new[] { "E", "F", "H", "I", "J" })
            formulas[col] = FormulaAt(col + oldLastRow);

        for (int rowNo = oldLastRow + 1; rowNo <= targetLastRow; rowNo++)
        {
            var copy = new XElement(prototype);
            copy.SetAttributeValue("r", rowNo);
            foreach (var cell in copy.Elements(M + "c"))
            {
                var addr = (string?)cell.Attribute("r") ?? "";
                var column = new string(addr.TakeWhile(char.IsLetter).ToArray());
                cell.SetAttributeValue("r", column + rowNo);
                cell.Element(M + "v")?.Remove();
                cell.Element(M + "is")?.Remove();
                var f = cell.Element(M + "f");
                if (f is not null)
                {
                    var baseFormula = formulas.TryGetValue(column, out var known) ? known : FormulaAt(addr);
                    f.RemoveAttributes();
                    f.Value = ShiftRelativeRows(baseFormula, rowNo - oldLastRow);
                    cell.Attribute("t")?.Remove();
                }
                else cell.Attribute("t")?.Remove(); // Do not copy old input values.
            }
            // The copy must contain all computed cells with explicit formulas.
            foreach (var col in formulas.Keys)
                if (copy.Elements(M + "c").All(c => (string?)c.Attribute("r") != col + rowNo))
                    throw new InvalidDataException("Dòng mẫu thiếu công thức " + col + rowNo);
            rowsNode.Add(copy);
        }

        var dimension = tx.Root?.Element(M + "dimension");
        if (dimension is not null)
            dimension.SetAttributeValue("ref", ExtendAddress((string?)dimension.Attribute("ref"), oldLastRow, targetLastRow));
        ExtendXmlRanges(tx, oldLastRow, targetLastRow, formulasToo: false);
        // Dependencies: Danh mục stock sums, Báo cáo totals, Phiếu in lookups
        // and tables' calculated-column formulas. Do not touch any other content.
        foreach (var name in originalZip.Entries.Select(e => e.FullName)
            .Where(n => n.StartsWith("xl/worksheets/", StringComparison.Ordinal) ||
                        n.StartsWith("xl/tables/", StringComparison.Ordinal)))
        {
            if (name == transactionSheetEntry) continue;
            XDocument doc;
            using (var stream = originalZip.GetEntry(name)!.Open())
                doc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            if (ExtendXmlRanges(doc, oldLastRow, targetLastRow, formulasToo: true))
                changes[name] = doc;
        }
        // The active table itself must be extended. Never extend table1's catalog
        // row count or table3's static print layout.
        var table = changes.Where(x => x.Key.StartsWith("xl/tables/", StringComparison.Ordinal))
            .Select(x => x.Value.Root).FirstOrDefault(x =>
                (string?)x?.Attribute("name") == "tbl_NhapXuat7");
        // The table may not contain formula limits, so ensure it is staged.
        foreach (var name in originalZip.Entries.Where(e => e.FullName.StartsWith("xl/tables/") &&
                     e.FullName.EndsWith(".xml")).Select(e => e.FullName))
        {
            if (!changes.TryGetValue(name, out var doc))
            {
                using var stream = originalZip.GetEntry(name)!.Open();
                doc = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
            }
            if ((string?)doc.Root?.Attribute("name") != "tbl_NhapXuat7") continue;
            var expected = $"A4:L{oldLastRow}";
            if ((string?)doc.Root.Attribute("ref") != expected)
                throw new InvalidDataException("Phạm vi tbl_NhapXuat7 không khớp với sheet. Dừng mở rộng.");
            doc.Root.SetAttributeValue("ref", $"A4:L{targetLastRow}");
            var filter = doc.Root.Element(M + "autoFilter");
            if (filter is not null) filter.SetAttributeValue("ref", $"A4:L{targetLastRow}");
            changes[name] = doc;
            return changes;
        }
        throw new InvalidDataException("Không tìm thấy bảng tbl_NhapXuat7; không thể mở rộng an toàn.");
    }

    private static bool ExtendXmlRanges(XDocument doc, int oldLast, int newLast, bool formulasToo)
    {
        var changed = false;
        foreach (var element in doc.Descendants())
        {
            if (formulasToo && element.Name.LocalName is "f" or "calculatedColumnFormula" or "definedName" or "formula1" or "formula2")
            {
                if (string.IsNullOrEmpty(element.Value)) continue;
                var updated = ExtendRangeEnds(element.Value, oldLast, newLast);
                if (updated != element.Value) { element.Value = updated; changed = true; }
            }
            foreach (var attribute in element.Attributes().Where(a =>
                a.Name.LocalName is "sqref" or "ref"))
            {
                var before = attribute.Value;
                var after = ExtendAddress(before, oldLast, newLast);
                if (after != before) { attribute.Value = after; changed = true; }
            }
        }
        return changed;
    }

    private static string ExtendRangeEnds(string formula, int oldLast, int newLast) =>
        RangeEnd.Replace(formula, m => m.Groups["last"].Value ==
            oldLast.ToString(CultureInfo.InvariantCulture)
            ? m.Groups["start"].Value + ":" + m.Groups["col"].Value +
                newLast.ToString(CultureInfo.InvariantCulture)
            : m.Value);

    private static string ExtendAddress(string? value, int oldLast, int newLast)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        // Only a range ending on the table boundary is extended, never a
        // single-cell reference, print area or an unrelated named range.
        return ExtendRangeEnds(value, oldLast, newLast);
    }

    private static string ShiftRelativeRows(string formula, int delta) =>
        RefInFormula.Replace(formula, match =>
        {
            if (match.Groups["abs"].Value == "$") return match.Value;
            var row = int.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture);
            return match.Groups["col"].Value + (row + delta).ToString(CultureInfo.InvariantCulture);
        });
}
