using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using HisaWarehouse.Services;

XNamespace m = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
XNamespace p = "http://schemas.openxmlformats.org/package/2006/relationships";
var scratch = Path.Combine(Path.GetTempPath(), "hisa-append-safety-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
string file = Path.Combine(scratch, "KHO.xlsx");
int checks = 0;
void Check(bool yes, string text)
{
    if (!yes) throw new InvalidOperationException("FAIL: " + text);
    Console.WriteLine("PASS " + text);
    checks++;
}
XElement Str(string address, string text) =>
    new(m + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"),
        new XElement(m + "is", new XElement(m + "t", text)));
XElement Num(string address, double n) =>
    new(m + "c", new XAttribute("r", address), new XElement(m + "v", n.ToString(CultureInfo.InvariantCulture)));
XElement Blank(string address) => new(m + "c", new XAttribute("r", address));
XElement Formula(string address, int row) =>
    new(m + "c", new XAttribute("r", address), new XElement(m + "f", "1+1"),
        new XElement(m + "v", row == 4 ? "12" : "2"));

void BuildWorkbook(bool missingFormula = false)
{
    using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
    void Add(string name, XDocument data)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        data.Save(stream);
    }
    Add("xl/workbook.xml", new XDocument(new XElement(m + "workbook",
        new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName),
        new XElement(m + "sheets",
            new XElement(m + "sheet", new XAttribute("name", "Danh mục"), new XAttribute("sheetId", 1), new XAttribute(r + "id", "rId1")),
            new XElement(m + "sheet", new XAttribute("name", "Nhập Xuất"), new XAttribute("sheetId", 2), new XAttribute(r + "id", "rId2")),
            new XElement(m + "sheet", new XAttribute("name", "Dashboard"), new XAttribute("sheetId", 3), new XAttribute(r + "id", "rId3"))))));
    Add("xl/_rels/workbook.xml.rels", new XDocument(new XElement(p + "Relationships",
        new XElement(p + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Target", "worksheets/sheet1.xml")),
        new XElement(p + "Relationship", new XAttribute("Id", "rId2"), new XAttribute("Target", "worksheets/sheet2.xml")),
        new XElement(p + "Relationship", new XAttribute("Id", "rId3"), new XAttribute("Target", "worksheets/sheet3.xml")))));

    var catHeaders = new[] { "Mã hàng", "Tên hàng", "Đơn vị", "Tồn đầu", "Tồn tối thiểu", "Tồn hiện tại" };
    var catalog = new XElement(m + "sheetData",
        new XElement(m + "row", new XAttribute("r", 3),
            catHeaders.Select((title, i) => Str((char)('A' + i) + "3", title))));
    for (int row = 4; row <= 900; row++)
    {
        var cells = new XElement(m + "row", new XAttribute("r", row));
        if (row == 4 || row == 5)
            cells.Add(Str("A" + row, row == 4 ? "VT0001" : "VT0002"),
                Str("B" + row, "Vật tư thử"), Str("C" + row, "Cái"), Num("D" + row, 10),
                Num("E" + row, 2));
        foreach (string col in new[] { "F", "G", "I", "J", "K" })
            cells.Add(col == "F" && row == 4
                ? new XElement(m + "c", new XAttribute("r", "F4"),
                    new XElement(m + "f", "SUMIFS('Nhập Xuất'!$G$5:$G$965,'Nhập Xuất'!$D$5:$D$965,A4)"),
                    new XElement(m + "v", "12"))
                : Formula(col + row, row));
        catalog.Add(cells);
    }
    Add("xl/worksheets/sheet1.xml", new XDocument(new XElement(m + "worksheet", catalog)));

    var txHeaders = new[] { "Ngày", "In", "Loại", "Mã hàng", "Tên hàng", "Đơn vị", "Số lượng",
        "Tồn trước", "Tồn sau", "Cảnh báo", "Giao/nhận", "Ghi chú" };
    var data = new XElement(m + "sheetData",
        new XElement(m + "row", new XAttribute("r", 4),
            txHeaders.Select((title, i) => Str((char)('A' + i) + "4", title))));
    for (int row = 5; row <= 965; row++)
    {
        var cells = new XElement(m + "row", new XAttribute("r", row));
        foreach (string col in new[] { "A", "B", "C", "D", "G", "K", "L" })
        {
            if (row == 5 && col == "A") cells.Add(Num(col + row, 46000));
            else if (row == 5 && col == "C") cells.Add(Str(col + row, "Nhập"));
            else if (row is 5 or 6 && col == "D") cells.Add(Str(col + row, "VT0001"));
            else if (row == 5 && col == "G") cells.Add(Num(col + row, 2));
            else cells.Add(Blank(col + row));
        }
        foreach (string col in new[] { "E", "F", "H", "I", "J" })
        {
            if (missingFormula && row == 7 && col == "I")
                cells.Add(Blank(col + row));
            else if (col == "I")
                cells.Add(new XElement(m + "c", new XAttribute("r", "I" + row),
                    row == 5
                        ? new XElement(m + "f", new XAttribute("t", "shared"),
                            new XAttribute("si", "0"), new XAttribute("ref", "I5:I965"),
                            "IF(D5=\"\",\"\",H5+IF(C5=\"Nhập\",G5,-G5))")
                        : new XElement(m + "f", new XAttribute("t", "shared"), new XAttribute("si", "0")),
                    new XElement(m + "v", "2")));
            else cells.Add(Formula(col + row, row));
        }
        if (row == 7) cells.Add(Str("M7", "Ô trợ giúp, phải giữ"));
        data.Add(cells);
    }
    Add("xl/worksheets/sheet2.xml", new XDocument(new XElement(m + "worksheet",
        new XElement(m + "dimension", new XAttribute("ref", "A1:Q965")),
        data,
        new XElement(m + "dataValidations", new XAttribute("count", "1"),
            new XElement(m + "dataValidation", new XAttribute("sqref", "C5:C965"))))));
    Add("xl/tables/table2.xml", new XDocument(new XElement(m + "table",
        new XAttribute("name", "tbl_NhapXuat7"), new XAttribute("ref", "A4:L965"),
        new XElement(m + "autoFilter", new XAttribute("ref", "A4:L965")))));
    Add("xl/worksheets/sheet5.xml", new XDocument(new XElement(m + "worksheet",
        new XElement(m + "sheetData", new XElement(m + "row", new XAttribute("r", 3),
            new XElement(m + "c", new XAttribute("r", "A3"),
                new XElement(m + "f", "SUM('Nhập Xuất'!$G$5:$G$965)+SUM($A$5:$A$965)"),
                new XElement(m + "v", "0")))))));
    Add("xl/worksheets/sheet3.xml",
        new XDocument(new XElement(m + "worksheet",
            new XElement(m + "dimension", new XAttribute("ref", "A1:Q965")),
            new XElement(m + "sheetData",
                new XElement(m + "row", new XAttribute("r", 1), Str("A1", "Báo cáo cần bảo toàn"))),
            new XElement(m + "dataValidations",
                new XElement(m + "dataValidation", new XAttribute("sqref", "C5:C965"))))));
    Add("xl/charts/chart1.xml", new XDocument(new XElement("ChartPart", "unchanged")));
    Add("xl/externalLinks/externalLink1.xml", new XDocument(new XElement("ForeignBook", "unchanged")));
}

XDocument Sheet(ZipArchive zip, string name)
{
    using var stream = zip.GetEntry(name)!.Open();
    return XDocument.Load(stream);
}
XElement Cell(XDocument xml, string address) =>
    xml.Descendants(m + "c").Single(x => (string?)x.Attribute("r") == address);
string Text(XElement cell) =>
    (string?)cell.Element(m + "v") ??
    string.Concat(cell.Descendants(m + "t").Select(t => t.Value));

try
{
    BuildWorkbook();
    var verifiedBackup = Path.Combine(scratch, "KHO-verified.bak");
    File.Copy(file, verifiedBackup);

    // Two successive inbound reconciliations must not replay historical rows
    // or add their quantity twice to the Excel-computed stock total.
    var localState = new HisaWarehouse.InventoryState
    {
        Warehouses = ["Kho tổng"],
        Products = new System.Collections.ObjectModel.ObservableCollection<HisaWarehouse.InventoryItem>
        {
            new() { Sku = "VT0001", Name = "Vật tư cũ", Unit = "Cái",
                WarehouseStocks = new Dictionary<string, double> { ["Kho tổng"] = 10 } },
            new() { Sku = "VT0002", Name = "Vật tư thử", Unit = "Cái",
                WarehouseStocks = new Dictionary<string, double> { ["Kho tổng"] = 2 } }
        }
    };
    var proposed = PrimaryWorkbookReconciler.Prepare(file, localState);
    PrimaryWorkbookReconciler.Apply(proposed, localState);
    Check(localState.Products.Single(i => i.Sku == "VT0001").TotalStock == 12,
        "adopt Excel-computed stock, never replay historical transaction quantities");
    var synced = PrimaryWorkbookReconciler.Prepare(file, localState);
    PrimaryWorkbookReconciler.Apply(synced, localState);
    Check(localState.Products.Single(i => i.Sku == "VT0001").TotalStock == 12 &&
          localState.Transactions.Count(t => t.SourceRow == 5) == 1,
        "resynchronizing the same saved Excel does not double count or duplicate history");

    byte[] original;
    using (var z = ZipFile.OpenRead(file))
    {
        using var input = z.GetEntry("xl/worksheets/sheet3.xml")!.Open();
        original = SHA256.HashData(input);
    }
    var moved = new[] {
        new PrimaryWorkbookTransactionWriter.Input(new DateOnly(2026, 11, 1), "Nhập", "VT0001", 4, "Nhân viên A", "Kiểm tra"),
        new PrimaryWorkbookTransactionWriter.Input(new DateOnly(2026, 11, 1), "Nhập", "VT0002", 2.5, "Nhân viên A", "")
    };
    var result = PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-001", moved);
    Check(result.FirstRow == 7 && result.RowCount == 2, "append after all existing business rows incl incomplete legacy row");
    Check(File.Exists(result.BackupPath), "original is backed up before atomic replacement");
    using (var workbook = ZipFile.OpenRead(file))
    {
        var tx = Sheet(workbook, "xl/worksheets/sheet2.xml");
        var recalc = Sheet(workbook, "xl/workbook.xml").Descendants(m + "calcPr").Single();
        Check((string?)recalc.Attribute("fullCalcOnLoad") == "1" &&
              (string?)recalc.Attribute("forceFullCalc") == "1",
              "force Excel to recalculate preserved formula results when workbook opens");
        Check(Text(Cell(tx, "C7")) == "Nhập" && Text(Cell(tx, "D7")) == "VT0001" &&
              Text(Cell(tx, "G7")) == "4" && Text(Cell(tx, "K7")) == "Nhân viên A",
              "write only typed input columns");
        Check(Text(Cell(tx, "D8")) == "VT0002" && Text(Cell(tx, "G8")) == "2.5",
              "all transaction lines are committed");
        Check(Text(Cell(tx, "L7")).Contains("[HISA:PN-20261101-001]"),
              "document marker keeps Excel appends idempotent");
        Check(Text(Cell(tx, "B7")) == "", "do not modify Excel print selector B");
        Check(Text(Cell(tx, "M7")) == "Ô trợ giúp, phải giữ", "preserve M:Q helpers on same sheet");
        Check(new[] { "E", "F", "H", "I", "J" }.All(col =>
              Cell(tx, col + "7").Element(m + "f") is not null &&
              Cell(tx, col + "8").Element(m + "f") is not null), "preserve all formula cells");
        using var unchanged = workbook.GetEntry("xl/worksheets/sheet3.xml")!.Open();
        Check(SHA256.HashData(unchanged).SequenceEqual(original), "other worksheets byte-exact");
        Check(workbook.GetEntry("xl/charts/chart1.xml") is not null &&
              workbook.GetEntry("xl/externalLinks/externalLink1.xml") is not null,
              "keep chart and external workbook links");
    }
    try
    {
        PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-001", moved);
        throw new Exception("Duplicate was accepted.");
    }
    catch (InvalidDataException)
    {
        Check(true, "reject re-writing a document after Excel commit");
    }

    var lockPath = Path.Combine(scratch, "~$KHO.xlsx");
    File.WriteAllText(lockPath, "Excel owner lock");
    var originalBytes = SHA256.HashData(File.ReadAllBytes(file));
    try
    {
        PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-002", moved);
        throw new Exception("Excel owner lock accepted.");
    }
    catch (IOException)
    {
        Check(SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(originalBytes),
            "Excel owner lock blocks write without changing workbook");
    }
    File.Delete(lockPath);

    var many = Enumerable.Repeat(moved[0], 920).ToArray();
    var expanded = PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-003", many);
    Check(expanded.FirstRow == 9, "automatically append 920 lines after prior transactions");
    using (var workbook = ZipFile.OpenRead(file))
    {
        var tx = Sheet(workbook, "xl/worksheets/sheet2.xml");
        var finalRow = PrimaryWorkbookTableExpander.LastRow(tx);
        Check(finalRow >= 978 && finalRow - 928 >= 50,
            "grow table with at least 50 free input rows after all new transactions");
        Check(Cell(tx, "I" + finalRow).Element(m + "f") is not null &&
              Cell(tx, "H" + finalRow).Element(m + "f") is not null,
            "extend calculated columns to final spare row");
        Check((string?)Cell(tx, "I5").Element(m + "f")?.Attribute("ref") == "I5:I965" &&
              (string?)Cell(tx, "I" + finalRow).Element(m + "f")?.Attribute("t") is null &&
              Cell(tx, "I" + finalRow).Element(m + "f")!.Value.Contains("H" + finalRow),
            "keep original shared formula group intact and use independent formulas for new rows");
        var table = Sheet(workbook, "xl/tables/table2.xml");
        Check((string?)table.Root?.Attribute("ref") == "A4:L" + finalRow,
            "extend tbl_NhapXuat7 including its table metadata");
        var cat = Sheet(workbook, "xl/worksheets/sheet1.xml");
        var printed = Sheet(workbook, "xl/worksheets/sheet5.xml");
        Check(Cell(cat, "F4").Element(m + "f")!.Value.Contains("$G$" + finalRow) &&
              Cell(printed, "A3").Element(m + "f")!.Value.Contains("$G$" + finalRow),
            "update inventory SUMIFS and original Phiếu in template formula references");
        Check(Cell(printed, "A3").Element(m + "f")!.Value.Contains("$A$5:$A$965"),
            "do not extend unrelated ranges within a formula referring to Nhập Xuất");
        using (var unchanged = workbook.GetEntry("xl/worksheets/sheet3.xml")!.Open())
            Check(SHA256.HashData(unchanged).SequenceEqual(original),
                "do not change unrelated sheet dimensions or validation ranges on expansion");
        Check(SHA256.HashData(File.ReadAllBytes(expanded.BackupPath)).SequenceEqual(originalBytes),
            "keep exact pre-expansion workbook as backup");
        Check(Text(Cell(tx, "M7")) == "Ô trợ giúp, phải giữ",
            "unrelated cells on transaction sheet retained after expansion");
        Check((string?)tx.Descendants(m + "dataValidation").Single().Attribute("sqref") ==
            "C5:C" + finalRow, "expand import/export type validation");
    }

    File.Delete(file);
    BuildWorkbook(missingFormula: true);
    var missingBefore = SHA256.HashData(File.ReadAllBytes(file));
    try
    {
        PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-004", moved);
        throw new Exception("Missing calculation formula was accepted.");
    }
    catch (InvalidDataException)
    {
        Check(SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(missingBefore),
            "missing Excel formula causes fail-closed without touching source");
    }

    // Restoration requires a verified donor, preserves every other sheet,
    // and forces Excel recalculation without replacing the original workbook.
    var plan = PrimaryFormulaRecovery.Inspect(file, verifiedBackup);
    Check(plan.Missing.Count == 1 && plan.Missing[0].Sheet == "Nhập Xuất" &&
          plan.Missing[0].Address == "I7",
        "identify only the deleted protected formula before showing confirmation");
    var recoveredFrom = PrimaryFormulaRecovery.Restore(plan);
    using (var restored = ZipFile.OpenRead(file))
    {
        Check(Cell(Sheet(restored, "xl/worksheets/sheet2.xml"), "I7").Element(m + "f") is not null,
            "restore only I7 formula from verified backup");
        using var untouched = restored.GetEntry("xl/worksheets/sheet3.xml")!.Open();
        Check(SHA256.HashData(untouched).SequenceEqual(original),
            "unrelated worksheet is byte-exact after formula restore");
    }
    Check(File.Exists(recoveredFrom) &&
          PrimaryWorkbookInspector.Inspect(file).MissingProtectedFormulas == 0,
        "restore writes rollback backup and removes missing formula condition");

    Console.WriteLine($"SUCCESS {checks} Excel-only-input-column regression checks.");
}
finally
{
    Directory.Delete(scratch, recursive: true);
}
