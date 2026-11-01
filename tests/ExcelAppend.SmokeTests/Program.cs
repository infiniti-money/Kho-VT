using System.IO;
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

void BuildWorkbook(bool missingFormula = false, bool overwriteFormulaWithText = false,
    string? sharedSkuValue = null, bool plainNameUnitColumns = false)
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

    if (sharedSkuValue is not null)
        Add("xl/sharedStrings.xml", new XDocument(new XElement(m + "sst",
            new XElement(m + "si", new XElement(m + "t", sharedSkuValue)))));

    var catHeaders = new[] { "Mã hàng", "Tên hàng", "Đơn vị", "Tồn đầu", "Tồn tối thiểu", "Tồn hiện tại" };
    var catalog = new XElement(m + "sheetData",
        new XElement(m + "row", new XAttribute("r", 3),
            catHeaders.Select((title, i) => Str((char)('A' + i) + "3", title))));
    for (int row = 4; row <= 900; row++)
    {
        var cells = new XElement(m + "row", new XAttribute("r", row));
        if (row == 4 || row == 5)
            cells.Add(row == 4 && sharedSkuValue is not null
                    ? new XElement(m + "c", new XAttribute("r", "A4"),
                        new XAttribute("t", "s"), new XElement(m + "v", "0"))
                    : Str("A" + row, row == 4 ? "VT0001" : "VT0002"),
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
            if (plainNameUnitColumns && col is "E" or "F")
                cells.Add(Blank(col + row));
            else if (missingFormula && row == 7 && col == "I")
                cells.Add(Blank(col + row));
            else if (overwriteFormulaWithText && row == 7 && col == "I")
                cells.Add(Str("I7", "Dữ liệu nhập nhầm"));
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
    // A reserve-material request is only a pending document, not an inventory
    // receipt/shipment. Manual lines may have no SKU yet, and do not create
    // catalog products or Excel transactions.
    var requestState = new HisaWarehouse.InventoryState();
    var reserve = new HisaWarehouse.MaterialRequest
    {
        Type = "Yêu cầu cấp vật tư dự phòng",
        Requester = "Nhân viên bảo trì",
        Recipient = "Bộ phận kỹ thuật",
        Status = "Chờ xử lý",
        Lines =
        [
            new() { Sku = "", ItemName = "Vòng bi dự phòng", Unit = "Bộ", Quantity = 2 },
            new() { Sku = "", ItemName = "Dây nguồn dự phòng", Unit = "Mét", Quantity = 4 },
            new() { Sku = "VT-NEW", ItemName = "Cáp điều khiển", Unit = "Cuộn", Quantity = 1 }
        ]
    };
    MaterialRequestService.Add(requestState, reserve);
    Check(reserve.DocNo.StartsWith("YC-DP-", StringComparison.Ordinal) &&
          reserve.Lines.Count == 3 &&
          reserve.Lines.Count(line => line.Sku.Length == 0) == 2,
          "reserve request persists independent manual lines with optional SKU");
    Check(requestState.MaterialRequests.Count == 1 && requestState.Products.Count == 0 &&
          requestState.Transactions.Count == 0,
          "saving a reserve request does not create products, transactions or change stock");
    MaterialRequestService.Add(requestState, new HisaWarehouse.MaterialRequest
    {
        Type = "Yêu cầu cấp vật tư", Lines = [new() { Sku = "VT001", ItemName = "Mẫu", Unit = "Cái", Quantity = 1 }]
    });
    MaterialRequestService.Add(requestState, new HisaWarehouse.MaterialRequest
    {
        Type = "Yêu cầu nhập vật tư", Lines = [new() { Sku = "VT002", ItemName = "Mẫu", Unit = "Cái", Quantity = 1 }]
    });
    Check(requestState.MaterialRequests.Count == 3 &&
          requestState.MaterialRequests.Any(q => q.DocNo.StartsWith("YC-CAP-", StringComparison.Ordinal)) &&
          requestState.MaterialRequests.Any(q => q.DocNo.StartsWith("YC-NHAP-", StringComparison.Ordinal)),
          "existing request types and historical document-number prefixes remain compatible");
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

    // Reconciliation must not silently overwrite journal edits made after preview.
    var stalePreview = PrimaryWorkbookReconciler.Prepare(file, localState);
    var editedHistory = localState.Transactions.Single(t => t.SourceRow == 5);
    var previousPartner = editedHistory.Partner;
    editedHistory.Partner = "Đã chỉnh khi đang xem trước";
    try
    {
        PrimaryWorkbookReconciler.Apply(stalePreview, localState);
        throw new Exception("Stale reconciliation preview was accepted.");
    }
    catch (InvalidOperationException)
    {
        Check(true, "reject preview after journal partner metadata changed");
    }
    editedHistory.Partner = previousPartner;

    // Only transactions written by the newer HISA writer carry a SourceRow anchor.
    // Old unanchored local movements remain valid during upgrade. Once anchored,
    // a missing Excel marker is a conflict, not an external import.
    var signedSlip = new HisaWarehouse.InventoryTransaction
    {
        DocNo = "PN-20261101-SIGNED", Type = "Nhập", Sku = "VT0001",
        Quantity = 2, Legacy = false
    };
    localState.Transactions.Add(signedSlip);
    PrimaryWorkbookReconciler.Prepare(file, localState);
    Check(true, "legacy HISA local movement without anchor does not cause false conflict");
    signedSlip.SourceRow = 5;
    try
    {
        PrimaryWorkbookReconciler.Prepare(file, localState);
        throw new Exception("Missing signed Excel document was accepted.");
    }
    catch (InvalidDataException)
    {
        Check(true, "reject deleted or altered HISA signed document marker");
    }
    localState.Transactions.Remove(signedSlip);

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
    var trustedBeforeWrite = PrimaryWorkbookReconciler.Fingerprint(file);
    var result = PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-001", moved, trustedBeforeWrite);
    Check(result.WorkbookHash == PrimaryWorkbookReconciler.Fingerprint(file) &&
          result.WorkbookHash != trustedBeforeWrite,
        "return exact staged workbook fingerprint for subsequent HISA writes");
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
        Check(Text(Cell(tx, "B7")) == "PN-20261101-001" &&
              Text(Cell(tx, "B8")) == "PN-20261101-001",
              "write print-slip number in column B for both transaction lines");
        Check(Cell(tx, "A7").Element(m + "v") is not null &&
              Text(Cell(tx, "K7")) == "Nhân viên A" &&
              Text(Cell(tx, "L7")).Contains("Kiểm tra"),
              "write date A, partner K and note L alongside type C and quantity G");
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
    // E/F may be real Excel lookup formulas (above) or plain input cells.
    // The writer must put the validated catalog name/unit into plain E/F
    // without touching mandatory H/I/J formulas.
    var plainWorkbook = Path.Combine(scratch, "KHO-plain.xlsx");
    File.Move(file, plainWorkbook);
    BuildWorkbook(plainNameUnitColumns: true);
    var plainHash = PrimaryWorkbookReconciler.Fingerprint(file);
    PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-PLAIN",
        moved, plainHash);
    using (var plainZip = ZipFile.OpenRead(file))
    {
        var plainTx = Sheet(plainZip, "xl/worksheets/sheet2.xml");
        Check(Text(Cell(plainTx, "E7")) == "Vật tư thử" &&
              Text(Cell(plainTx, "F7")) == "Cái",
              "fill E/F with name and unit when cells contain no lookup formulas");
        Check(new[] { "H", "I", "J" }.All(col =>
              Cell(plainTx, col + "7").Element(m + "f") is not null),
              "preserve required H/I/J formulas with direct name/unit input");
    }
    File.Delete(file);
    File.Move(plainWorkbook, file);

    var changedBytes = SHA256.HashData(File.ReadAllBytes(file));
    try
    {
        PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-NEW", moved, trustedBeforeWrite);
        throw new Exception("Externally changed workbook was accepted.");
    }
    catch (IOException)
    {
        Check(SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(changedBytes),
            "reject stale cached fingerprint before writing even with a fresh document ID");
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
    var expanded = PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-003", many, result.WorkbookHash);
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

    // A pasted text value can leave t="inlineStr" and <is> in the calculated
    // cell. Restore must remove the stale type and text, not append <f> beside it.
    File.Delete(file);
    BuildWorkbook(overwriteFormulaWithText: true);
    var overwritten = PrimaryFormulaRecovery.Inspect(file, verifiedBackup);
    Check(overwritten.Missing.Count == 1 && overwritten.Missing[0].Address == "I7",
        "detect text pasted over the protected formula I7");
    PrimaryFormulaRecovery.Restore(overwritten);
    using (var recoveredText = ZipFile.OpenRead(file))
    {
        var fixedCell = Cell(Sheet(recoveredText, "xl/worksheets/sheet2.xml"), "I7");
        Check(fixedCell.Element(m + "f") is not null &&
              fixedCell.Attribute("t") is null &&
              fixedCell.Element(m + "is") is null &&
              fixedCell.Element(m + "v") is null,
            "formula recovery removes stale inline-string metadata and cached value");
    }

    // The on-save recovery is automatic ONLY when a compatible trusted HISA
    // backup is present. It must refuse missing/overwritten formulas if no
    // valid donor exists, without touching the damaged workbook.
    var trustedAuto = Path.Combine(scratch, "KHO.xlsx.HISA-backup-verified.bak");
    File.Copy(verifiedBackup, trustedAuto, overwrite: true);
    File.Delete(file);
    BuildWorkbook(overwriteFormulaWithText: true);
    var damagedHash = PrimaryWorkbookReconciler.Fingerprint(file);
    var automatic = AutomaticFormulaRecovery.EnsureProtectedFormulas(file, damagedHash);
    Check(automatic.Recovered && automatic.RestoredCells == 1 &&
          automatic.WorkbookHash != damagedHash,
          "automatically restore overwritten I7 from verified matching HISA backup");
    using (var recoveredAutomatically = ZipFile.OpenRead(file))
        Check(Cell(Sheet(recoveredAutomatically, "xl/worksheets/sheet2.xml"),
                  "I7").Element(m + "f") is not null,
              "restored I7 is a valid formula instead of pasted text");
    // Previous save/restore operations also created verified HISA backup
    // files in this scratch directory. Clear *all* of them to exercise the
    // genuine no-donor case, rather than accidentally selecting an old one.
    foreach (var backupFile in Directory.GetFiles(scratch, "KHO.xlsx.HISA-*.bak"))
        File.Delete(backupFile);
    File.Delete(file);
    BuildWorkbook(missingFormula: true);
    var noDonorHash = PrimaryWorkbookReconciler.Fingerprint(file);
    try
    {
        AutomaticFormulaRecovery.EnsureProtectedFormulas(file, noDonorHash);
        throw new Exception("Missing formula automatically recovered without verified donor.");
    }
    catch (InvalidDataException)
    {
        Check(PrimaryWorkbookReconciler.Fingerprint(file) == noDonorHash,
            "refuse automatic repair without verified HISA backup; no file changes");
    }
    // Existing source changed outside HISA: even a valid donor cannot be used
    // until explicitly resynchronized, preventing accidental overwrite.
    File.Copy(verifiedBackup, trustedAuto, overwrite: true);
    try
    {
        AutomaticFormulaRecovery.EnsureProtectedFormulas(file, new string('A', 64));
        throw new Exception("Stale fingerprint accepted for automatic repair.");
    }
    catch (IOException)
    {
        Check(PrimaryWorkbookReconciler.Fingerprint(file) == noDonorHash,
            "refuse to auto-restore if workbook fingerprint is stale");
    }
    File.Delete(trustedAuto);

    // Each workbook owns a different shared-string table: the numeric index
    // alone is NOT evidence that donor and live catalog SKUs match.
    File.Delete(file);
    BuildWorkbook(sharedSkuValue: "VT0001");
    var donorWithSharedStrings = Path.Combine(scratch, "shared-sku-donor.bak");
    File.Copy(file, donorWithSharedStrings);
    File.Delete(file);
    BuildWorkbook(missingFormula: true, sharedSkuValue: "VT9999");
    try
    {
        PrimaryFormulaRecovery.Inspect(file, donorWithSharedStrings);
        throw new Exception("Different shared-string SKU was accepted as matching.");
    }
    catch (InvalidDataException)
    {
        Check(true, "reject backup when shared-string indexes match but decoded SKUs differ");
    }

    // A new receipt code may be resolved from a separate reference catalog.
    // Add it to the same Excel Danh mục and Nhập Xuất atomically, never
    // changing formula cells or nonbusiness worksheets.
    File.Delete(file);
    BuildWorkbook();
    var newReceipt = new[] {
        new PrimaryWorkbookTransactionWriter.Input(new DateOnly(2026, 11, 1),
            "Nhập", "VT0003", 7, "Người gửi", "Mã mới đối chiếu")
    };
    var catalogInput = new[] {
        new PrimaryWorkbookTransactionWriter.CatalogInput("VT0003", "Bu lông mới", "Bộ")
    };
    var newBefore = PrimaryWorkbookReconciler.Fingerprint(file);
    var appended = PrimaryWorkbookTransactionWriter.Append(
        file, "PN-20261101-REF", newReceipt, newBefore, catalogInput);
    Check(File.Exists(appended.BackupPath) &&
          appended.WorkbookHash != newBefore,
        "new SKU receipt backs up original workbook and changes the fingerprint");
    using (var resultZip = ZipFile.OpenRead(file))
    {
        var cat = Sheet(resultZip, "xl/worksheets/sheet1.xml");
        var tx = Sheet(resultZip, "xl/worksheets/sheet2.xml");
        Check(Text(Cell(cat, "A6")) == "VT0003" &&
              string.Concat(Cell(cat, "B6").Descendants(m + "t").Select(t => t.Value)) == "Bu lông mới" &&
              string.Concat(Cell(cat, "C6").Descendants(m + "t").Select(t => t.Value)) == "Bộ" &&
              Text(Cell(cat, "D6")) == "0",
            "new product code, name and unit written only to blank catalog input cells with opening stock zero");
        Check(new[] { "F", "G", "I", "J", "K" }.All(col =>
            Cell(cat, col + "6").Element(m + "f") is not null),
            "new catalog row preserves every protected stock formula");
        Check(Text(Cell(tx, "D7")) == "VT0003" && Text(Cell(tx, "G7")) == "7" &&
              Text(Cell(tx, "L7")).Contains("[HISA:PN-20261101-REF]"),
            "new product receipt and registration committed in one workbook");
        using var other = resultZip.GetEntry("xl/worksheets/sheet3.xml")!.Open();
        Check(SHA256.HashData(other).SequenceEqual(original), "new SKU does not change other worksheets");
    }
    try
    {
        PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-REF2",
            newReceipt, appended.WorkbookHash, catalogInput);
        throw new Exception("Duplicate new SKU registered again.");
    }
    catch (InvalidDataException)
    {
        Check(true, "a known catalog SKU cannot be registered twice");
    }
    File.Delete(file);
    BuildWorkbook();
    var newRejected = SHA256.HashData(File.ReadAllBytes(file));
    try
    {
        PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-UNKNOWN",
            newReceipt, PrimaryWorkbookReconciler.Fingerprint(file));
        throw new Exception("New SKU without reference confirmation was accepted.");
    }
    catch (InvalidDataException)
    {
        Check(SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(newRejected),
            "unknown SKU without reference catalog is rejected without changing Excel");
    }

    // An can only edit two non-formula catalog input columns after an exact
    // user command and an explicit preview/approval. Check isolated workbook
    // modification, backup, stale hash rejection and untouched other sheet.
    File.Delete(file);
    BuildWorkbook();
    var catalogBeforeEdit = SHA256.HashData(File.ReadAllBytes(file));
    Check(AnCatalogEditService.TryParseCommand(
        "An đổi tên vật tư mã VT0001 thành Vật tư nhập từ An", out var rename) &&
        rename is not null && rename.Target == AnCatalogEditService.Field.Name &&
        rename.Sku == "VT0001", "An parses exact Vietnamese name edit with SKU");
    var namePlan = AnCatalogEditService.Prepare(file,
        PrimaryWorkbookReconciler.Fingerprint(file), rename!);
    Check(namePlan.Row == 4 && namePlan.OldValue == "Vật tư thử" &&
          namePlan.NewValue == "Vật tư nhập từ An",
        "An previews exact catalog cell, before and after values");
    var renamed = AnCatalogEditService.Apply(namePlan);
    Check(File.Exists(renamed.BackupPath) &&
          SHA256.HashData(File.ReadAllBytes(renamed.BackupPath)).SequenceEqual(catalogBeforeEdit),
        "An preserves the exact workbook before an approved edit");
    using (var inspected = ZipFile.OpenRead(file))
    {
        var sheet = Sheet(inspected, "xl/worksheets/sheet1.xml");
        Check(Cell(sheet, "B4").Element(m + "f") is null &&
              string.Concat(Cell(sheet, "B4").Descendants(m + "t").Select(t => t.Value)) ==
              "Vật tư nhập từ An" &&
              Cell(sheet, "F4").Element(m + "f") is not null,
            "An modifies only catalog name and preserves the stock formula");
        var foreign = Sheet(inspected, "xl/worksheets/sheet3.xml");
        Check(foreign.Root?.Element(m + "dimension")?.Attribute("ref")?.Value == "A1:Q965",
            "An does not change unrelated worksheet ranges");
    }
    try
    {
        AnCatalogEditService.Apply(namePlan);
        throw new Exception("An accepted a previously applied preview.");
    }
    catch (IOException)
    {
        Check(true, "An rejects a stale edit preview after a workbook change");
    }
    Check(AnCatalogEditService.TryParseCommand(
        "Sửa tồn tối thiểu mã VT0001 thành 7", out var minimum) &&
        minimum is not null && minimum.Target == AnCatalogEditService.Field.Minimum,
        "An recognizes a minimum-stock adjustment request");
    var minimumPlan = AnCatalogEditService.Prepare(file, renamed.WorkbookHash, minimum!);
    AnCatalogEditService.Apply(minimumPlan);
    using (var inspected = ZipFile.OpenRead(file))
    {
        var sheet = Sheet(inspected, "xl/worksheets/sheet1.xml");
        Check(Text(Cell(sheet, "E4")) == "7" &&
              Cell(sheet, "F4").Element(m + "f") is not null,
            "An edits minimum only and preserves calculated inventory");
    }
    Check(!AnCatalogEditService.TryParseCommand("Sửa công thức F4 thành 0", out _),
        "An never accepts a command to overwrite a formula");

    Console.WriteLine($"SUCCESS {checks} Excel-only-input-column regression checks.");
}
finally
{
    Directory.Delete(scratch, recursive: true);
}
