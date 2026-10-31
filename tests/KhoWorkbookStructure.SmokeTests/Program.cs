using System.IO.Compression;
using System.Xml.Linq;
using HisaWarehouse.Services;

XNamespace m = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
XNamespace pr = "http://schemas.openxmlformats.org/package/2006/relationships";
var directory = Path.Combine(Path.GetTempPath(), "hisa-structure-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var path = Path.Combine(directory, "KHO.xlsx");
var checks = 0;

XElement Str(string address, string value) =>
    new(m + "c", new XAttribute("r", address), new XAttribute("t", "inlineStr"),
        new XElement(m + "is", new XElement(m + "t", value)));
XElement Num(string address, double value) =>
    new(m + "c", new XAttribute("r", address), new XElement(m + "v", value));
XElement Formula(string address) =>
    new(m + "c", new XAttribute("r", address), new XElement(m + "f", "1+1"), new XElement(m + "v", 2));

void Build(bool missingFormula = false, bool wrongHeader = false)
{
    using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
    void Add(string name, XDocument xml)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        xml.Save(stream);
    }
    Add("xl/workbook.xml", new XDocument(new XElement(m + "workbook",
        new XAttribute(XNamespace.Xmlns + "r", r.NamespaceName),
        new XElement(m + "sheets",
            new XElement(m + "sheet", new XAttribute("name", "Danh mục"), new XAttribute("sheetId", 1), new XAttribute(r + "id", "rId1")),
            new XElement(m + "sheet", new XAttribute("name", "Nhập Xuất"), new XAttribute("sheetId", 2), new XAttribute(r + "id", "rId2"))))));
    Add("xl/_rels/workbook.xml.rels", new XDocument(new XElement(pr + "Relationships",
        new XElement(pr + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Target", "worksheets/sheet1.xml"), new XAttribute("Type", "worksheet")),
        new XElement(pr + "Relationship", new XAttribute("Id", "rId2"), new XAttribute("Target", "worksheets/sheet2.xml"), new XAttribute("Type", "worksheet")))));

    var catHeaders = new[] { "Mã hàng", "Tên hàng", "Đơn vị", "Tồn đầu", "Tồn tối thiểu", "Tồn hiện tại" };
    if (wrongHeader) catHeaders[5] = "Số dư tự nhập";
    var cat = new XElement(m + "sheetData",
        new XElement(m + "row", new XAttribute("r", 3),
            catHeaders.Select((value, i) => Str(((char)('A' + i)).ToString() + "3", value))));
    for (var index = 4; index <= 900; index++)
    {
        var row = new XElement(m + "row", new XAttribute("r", index));
        if (index == 4)
        {
            row.Add(Str("A4", "VT0001"), Str("B4", "Ốc vít"), Str("C4", "cái"), Num("D4", 1),
                Formula("E4"));
        }
        foreach (var col in new[] { "F", "G", "I", "J", "K" })
            if (!missingFormula || index != 4 || col != "F") row.Add(Formula(col + index));
        cat.Add(row);
    }
    Add("xl/worksheets/sheet1.xml", new XDocument(new XElement(m + "worksheet", cat)));

    var txHeaders = new[] { "Ngày", "In", "Loại", "Mã hàng", "Tên hàng", "Đơn vị",
        "Số lượng", "Tồn trước", "Tồn sau", "Cảnh báo", "Giao/nhận", "Ghi chú" };
    var tx = new XElement(m + "sheetData",
        new XElement(m + "row", new XAttribute("r", 4),
            txHeaders.Select((value, i) => Str(((char)('A' + i)).ToString() + "4", value))));
    for (var index = 5; index <= 965; index++)
    {
        var row = new XElement(m + "row", new XAttribute("r", index));
        if (index == 5)
        {
            row.Add(Num("A5", 45678), Str("C5", "Nhập"), Str("D5", "VT0001"), Formula("G5"));
        }
        if (index == 6) row.Add(Str("D6", "VT0002"));
        foreach (var col in new[] { "E", "F", "H", "I", "J" }) row.Add(Formula(col + index));
        tx.Add(row);
    }
    Add("xl/worksheets/sheet2.xml", new XDocument(new XElement(m + "worksheet", tx)));
}

try
{
    Build();
    var report = PrimaryWorkbookInspector.Inspect(path);
    if (report.CatalogItems != 1 || report.TypedTransactions != 1 ||
        report.IncompleteTransactions != 1 || report.MissingTransactionQuantities != 0 || report.FreeTransactionRows != 959 ||
        report.MinimumFormulaCells != 1 || report.QuantityFormulaCells != 1 ||
        report.MissingProtectedFormulas != 0 || report.SafeToPrepareWrite)
        throw new Exception("Workbook sheet/formula/stock inspection was incorrect.");
    checks++;
    Console.WriteLine("OK structured read-only analysis and incomplete-transaction warning");
    File.Delete(path);

    Build(missingFormula: true);
    var damaged = PrimaryWorkbookInspector.Inspect(path);
    if (damaged.MissingProtectedFormulas != 1)
        throw new Exception("The deleted protected formula was not detected.");
    checks++;
    Console.WriteLine("OK detects deleted stock formula");
    File.Delete(path);

    Build(wrongHeader: true);
    try
    {
        PrimaryWorkbookInspector.Inspect(path);
        throw new Exception("Wrong header was accepted.");
    }
    catch (InvalidDataException)
    {
        checks++;
        Console.WriteLine("OK rejects unknown workbook layout");
    }

    Console.WriteLine($"SUCCESS: {checks} KHO.xlsx structure tests.");
}
finally
{
    Directory.Delete(directory, recursive: true);
}
