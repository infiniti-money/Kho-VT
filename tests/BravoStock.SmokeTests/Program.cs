using System.Security.Cryptography;
using ClosedXML.Excel;
using HisaWarehouse.Services;

var dir = Path.Combine(Path.GetTempPath(), "hisa-bravo-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
var path = Path.Combine(dir, "Bravo.xlsx");
var assertions = 0;
void Verify(bool valid, string description)
{
    if (!valid) throw new InvalidOperationException("FAIL " + description);
    assertions++;
    Console.WriteLine("PASS " + description);
}

try
{
    using (var book = new XLWorkbook())
    {
        book.Worksheets.Add("Hướng dẫn").Cell("A1").Value = "Báo cáo tổng hợp Bravo";
        var sheet = book.Worksheets.Add("Tổng hợp tồn");
        sheet.Cell("A1").Value = "Mã sản phẩm";
        sheet.Cell("B1").Value = "Tên vật tư";
        sheet.Cell("C1").Value = "Đơn vị tính";
        sheet.Cell("D1").Value = "Giá trị sai cần bỏ qua";
        sheet.Cell("L1").Value = "Số lượng tồn cuối";
        sheet.Cell("A2").Value = "VT001";
        sheet.Cell("B2").Value = "Bu lông";
        sheet.Cell("C2").Value = "Bộ";
        sheet.Cell("D2").Value = 999999;
        sheet.Cell("L2").Value = -3.5;
        sheet.Cell("A3").Value = "VT002";
        sheet.Cell("B3").Value = "Tấm thép";
        sheet.Cell("C3").Value = "Tấm";
        sheet.Cell("L3").Value = 0;
        sheet.Cell("A4").Value = "vt001";
        sheet.Cell("B4").Value = "Bu lông";
        sheet.Cell("C4").Value = "Bộ";
        sheet.Cell("L4").Value = -1.5;
        sheet.Cell("A5").Value = "VT003";
        sheet.Cell("B5").Value = "Ống inox";
        sheet.Cell("C5").Value = "Mét";
        sheet.Cell("L5").Value = "-2,5"; // text, decimal comma and negative
        sheet.Cell("A6").Value = "VT004";
        sheet.Cell("B6").Value = "Dây điện";
        sheet.Cell("C6").Value = "Mét";
        sheet.Cell("L6").Value = 10;
        book.SaveAs(path);
    }
    var before = SHA256.HashData(File.ReadAllBytes(path));
    var rows = BravoStockReader.Read(path);
    Verify(rows.Count == 4, "reads material codes from column A, including zero");
    Verify(rows["VT001"].Name == "Bu lông" && rows["vt001"].Unit == "Bộ",
        "takes name from B, unit from C, case insensitive SKU");
    Verify(Math.Abs(rows["VT001"].Quantity - (-5)) < 0.000001,
        "aggregates signed negative quantities for repeated SKU");
    Verify(rows["VT002"].Quantity == 0, "zero on-hand stock remains valid");
    Verify(rows["VT003"].Quantity == -2.5, "parses negative decimal-comma text in L");
    Verify(rows["VT004"].Quantity == 10, "never confuses another quantity column with L");
    Verify(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))),
        "comparing Bravo does not change source workbook");

    using (var book = new XLWorkbook(path))
    {
        book.Worksheet("Tổng hợp tồn").Cell("L4").Value = "không rõ";
        book.Save();
    }
    try
    {
        BravoStockReader.Read(path);
        throw new InvalidOperationException("Invalid Bravo L value silently ignored.");
    }
    catch (InvalidDataException)
    {
        Verify(true, "fail closed on invalid signed stock instead of silently dropping SKU");
    }
    Console.WriteLine($"SUCCESS: {assertions} Bravo fixed-column signed-balance tests.");
}
finally
{
    if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
}
