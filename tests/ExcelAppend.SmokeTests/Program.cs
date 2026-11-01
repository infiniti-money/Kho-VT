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

void BuildWorkbook()
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
            cells.Add(Formula(col + row, row));
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
            cells.Add(Formula(col + row, row));
        if (row == 7) cells.Add(Str("M7", "Ô trợ giúp, phải giữ"));
        data.Add(cells);
    }
    Add("xl/worksheets/sheet2.xml", new XDocument(new XElement(m + "worksheet", data)));
    Add("xl/worksheets/sheet3.xml",
        new XDocument(new XElement(m + "worksheet", new XElement(m + "sheetData",
            new XElement(m + "row", new XAttribute("r", 1), Str("A1", "Báo cáo cần bảo toàn"))))));
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

    try
    {
        var many = Enumerable.Repeat(moved[0], 960).ToArray();
        PrimaryWorkbookTransactionWriter.Append(file, "PN-20261101-003", many);
        throw new Exception("Beyond formula coverage was accepted.");
    }
    catch (InvalidDataException)
    {
        Check(SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(originalBytes),
            "capacity >965 rejected without changing workbook");
    }

    Console.WriteLine($"SUCCESS {checks} Excel-only-input-column regression checks.");
}
finally
{
    Directory.Delete(scratch, recursive: true);
}
