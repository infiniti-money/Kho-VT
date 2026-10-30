using System.Globalization;
using System.IO;
using ClosedXML.Excel;

namespace HisaWarehouse.Services;

public sealed class ExcelInventoryImportRow
{
    public int RowNumber { get; init; }
    public string Sku { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Category { get; init; }
    public string? Unit { get; init; }
    public string? Department { get; init; }
    public double? Minimum { get; init; }
    public Dictionary<string, double> Stocks { get; init; } = new(StringComparer.Ordinal);
}

public sealed class ExcelInventoryImportResult
{
    public List<ExcelInventoryImportRow> Rows { get; } = [];
    public List<string> Errors { get; } = [];
}

public sealed class ReferenceStockValue
{
    public string Sku { get; init; } = "";
    public string Name { get; init; } = "";
    public string Unit { get; init; } = "";
    public double Quantity { get; init; }
}

public static class ExcelInventoryService
{
    private static readonly string[] Headers = ["Mã SKU", "Tên vật tư", "Nhóm hàng", "Đơn vị tính", "Tồn tối thiểu", "Bộ phận"];

    public static void CreateTemplate(string path, IReadOnlyList<string> warehouses)
    {
        using var workbook = new XLWorkbook();
        var guide = workbook.Worksheets.Add("Hướng dẫn");
        guide.Cell("A1").Value = "MẪU NHẬP HÀNG HÓA · QUẢN LÝ KHO";
        guide.Cell("A1").Style.Font.Bold = true;
        guide.Cell("A1").Style.Font.FontSize = 16;
        guide.Cell("A3").Value = "Điền dữ liệu tại trang HangHoa. Không đổi tên các cột.";
        guide.Cell("A4").Value = "Mã SKU và Tên vật tư là bắt buộc. SKU đã tồn tại sẽ được cập nhật thông tin có dữ liệu.";
        guide.Cell("A5").Value = "Để trống ô Tồn tối thiểu hoặc số dư kho nếu muốn giữ nguyên giá trị đang có.";
        guide.Cell("A6").Value = "Số dư nhập vào sẽ tạo dòng Điều chỉnh trong nhật ký để có thể truy vết.";
        guide.Cell("A7").Value = "Không nhập đơn giá hoặc giá trị tiền; ứng dụng theo dõi số lượng theo từng đơn vị.";
        guide.Column(1).Width = 110;
        foreach (var row in guide.RowsUsed()) row.Height = 23;

        var sheet = workbook.Worksheets.Add("HangHoa");
        var headers = Headers.Concat(warehouses.Select(name => $"Tồn - {name}")).ToArray();
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        StyleHeader(sheet.Range(1, 1, 1, headers.Length));
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, 301, headers.Length).SetAutoFilter();
        sheet.Row(1).Height = 30;
        for (var column = 1; column <= headers.Length; column++) sheet.Column(column).Width = column <= 2 ? 28 : column == 6 ? 22 : 18;
        sheet.Columns(5, headers.Length).Style.NumberFormat.Format = "#,##0.##";
        workbook.SaveAs(path);
    }

    public static void ExportInventory(string path, InventoryState state)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("HangHoa");
        var headers = Headers.Concat(state.Warehouses.Select(name => $"Tồn - {name}")).ToArray();
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        var rowNumber = 2;
        foreach (var item in state.Products.Where(item => item.Active).OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase))
        {
            sheet.Cell(rowNumber, 1).Value = item.Sku;
            sheet.Cell(rowNumber, 2).Value = item.Name;
            sheet.Cell(rowNumber, 3).Value = item.Category;
            sheet.Cell(rowNumber, 4).Value = item.Unit;
            if (item.MinStockDefined) sheet.Cell(rowNumber, 5).Value = item.MinStock;
            sheet.Cell(rowNumber, 6).Value = item.Department;
            for (var index = 0; index < state.Warehouses.Count; index++)
                sheet.Cell(rowNumber, 7 + index).Value = item.WarehouseStocks.GetValueOrDefault(state.Warehouses[index]);
            rowNumber++;
        }
        StyleHeader(sheet.Range(1, 1, 1, headers.Length));
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(2, rowNumber - 1), headers.Length).SetAutoFilter();
        sheet.Row(1).Height = 30;
        for (var column = 1; column <= headers.Length; column++) sheet.Column(column).Width = column <= 2 ? 30 : column == 6 ? 22 : 18;
        sheet.Columns(5, headers.Length).Style.NumberFormat.Format = "#,##0.##";
        workbook.SaveAs(path);
    }

    public static ExcelInventoryImportResult ReadImport(string path, IReadOnlyList<string> warehouses)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheets.FirstOrDefault(candidate => candidate.Name.Equals("HangHoa", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault() ?? throw new InvalidDataException("Tệp Excel không có trang dữ liệu.");
        var firstUsedRow = sheet.FirstRowUsed()?.RowNumber() ?? 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? firstUsedRow;
        if (firstUsedRow == 0) throw new InvalidDataException("Trang Excel không có tiêu đề cột.");
        var headerRow = 0;
        for (var candidate = firstUsedRow; candidate <= Math.Min(lastRow, firstUsedRow + 20); candidate++)
        {
            var candidateHeaders = Enumerable.Range(1, sheet.Row(candidate).LastCellUsed()?.Address.ColumnNumber ?? 0)
                .Select(column => sheet.Cell(candidate, column).GetString().Trim()).ToList();
            if (candidateHeaders.Any(value => value.Equals("Mã SKU", StringComparison.OrdinalIgnoreCase) || value.Equals("Mã hàng", StringComparison.OrdinalIgnoreCase) || value.Equals("SKU", StringComparison.OrdinalIgnoreCase))
                && candidateHeaders.Any(value => value.Equals("Tên vật tư", StringComparison.OrdinalIgnoreCase) || value.Equals("Tên hàng", StringComparison.OrdinalIgnoreCase) || value.Equals("Tên sản phẩm", StringComparison.OrdinalIgnoreCase)))
            { headerRow = candidate; break; }
        }
        if (headerRow == 0) throw new InvalidDataException("Không tìm thấy hàng tiêu đề có Mã SKU và Tên vật tư.");
        var lastColumn = sheet.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 0;
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 1; column <= lastColumn; column++)
        {
            var name = sheet.Cell(headerRow, column).GetString().Trim();
            if (name.Length > 0) columns[name] = column;
        }
        int Column(string name) => columns.GetValueOrDefault(name);
        int ColumnAny(params string[] names) => names.Select(Column).FirstOrDefault(index => index > 0);
        var skuColumn = ColumnAny("Mã SKU", "Mã hàng", "SKU");
        var nameColumn = ColumnAny("Tên vật tư", "Tên hàng", "Tên sản phẩm");
        if (skuColumn == 0 || nameColumn == 0)
            throw new InvalidDataException("Không tìm thấy hai cột bắt buộc “Mã SKU” và “Tên vật tư”. Hãy dùng mẫu Excel của ứng dụng.");

        var result = new ExcelInventoryImportResult();
        lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
        {
            var sku = CellText(sheet, rowNumber, skuColumn).Trim();
            var name = CellText(sheet, rowNumber, nameColumn).Trim();
            if (sku.Length == 0 && name.Length == 0) continue;
            if (sku.Length == 0 || name.Length == 0)
            {
                result.Errors.Add($"Dòng {rowNumber}: thiếu Mã SKU hoặc Tên vật tư.");
                continue;
            }
            if (!seen.Add(sku))
            {
                result.Errors.Add($"Dòng {rowNumber}: SKU {sku} bị lặp trong tệp, dòng sau được bỏ qua.");
                continue;
            }

            var minimum = ReadOptionalNumber(sheet, rowNumber, Column("Tồn tối thiểu"), rowNumber, "Tồn tối thiểu", result.Errors);
            if (minimum < 0) { result.Errors.Add($"Dòng {rowNumber}: Tồn tối thiểu phải không âm."); minimum = null; }
            var stocks = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var warehouse in warehouses)
            {
                var index = Column($"Tồn - {warehouse}");
                if (index == 0) continue;
                var value = ReadOptionalNumber(sheet, rowNumber, index, rowNumber, $"Tồn - {warehouse}", result.Errors);
                if (value.HasValue) stocks[warehouse] = value.Value;
            }
            if (stocks.Count == 0 && Column("Tồn hiện tại") > 0)
            {
                var current = ReadOptionalNumber(sheet, rowNumber, Column("Tồn hiện tại"), rowNumber, "Tồn hiện tại", result.Errors);
                if (current.HasValue && warehouses.Contains("Kho tổng", StringComparer.OrdinalIgnoreCase)) stocks[warehouses.First(value => value.Equals("Kho tổng", StringComparison.OrdinalIgnoreCase))] = current.Value;
            }
            var categoryColumn = ColumnAny("Nhóm hàng", "Danh mục", "Loại hàng");
            var unitColumn = ColumnAny("Đơn vị tính", "Đơn vị", "ĐVT");
            var departmentColumn = ColumnAny("Bộ phận", "Phòng ban");
            result.Rows.Add(new ExcelInventoryImportRow
            {
                RowNumber = rowNumber,
                Sku = sku.ToUpperInvariant(),
                Name = name,
                Category = NonBlank(sheet, rowNumber, categoryColumn),
                Unit = NonBlank(sheet, rowNumber, unitColumn),
                Department = NonBlank(sheet, rowNumber, departmentColumn),
                Minimum = minimum,
                Stocks = stocks
            });
        }
        return result;
    }

    public static Dictionary<string, ReferenceStockValue> ReadReferenceStock(string path)
    {
        using var workbook = new XLWorkbook(path);
        foreach (var sheet in workbook.Worksheets)
        {
            var first = sheet.FirstRowUsed()?.RowNumber() ?? 0;
            var last = sheet.LastRowUsed()?.RowNumber() ?? first;
            if (first == 0) continue;
            for (var headerRow = first; headerRow <= Math.Min(last, first + 25); headerRow++)
            {
                var lastColumn = sheet.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 0;
                var headings = Enumerable.Range(1, lastColumn).Select(column => sheet.Cell(headerRow, column).GetString().Trim()).ToList();
                int FindColumn(params string[] options) => headings.Select((name, index) => (name, index))
                    .Where(pair => options.Any(option => pair.name.Equals(option, StringComparison.OrdinalIgnoreCase)))
                    .Select(pair => pair.index + 1).FirstOrDefault();
                var skuColumn = FindColumn("Mã SKU", "Mã hàng", "SKU", "Mã sản phẩm");
                var quantityColumn = FindColumn("Tồn thực tế", "Số lượng thực tế", "Tồn hiện tại", "Tồn cuối", "Số lượng tồn", "Tồn");
                if (skuColumn == 0 || quantityColumn == 0) continue;
                var nameColumn = FindColumn("Tên vật tư", "Tên hàng", "Tên sản phẩm");
                var unitColumn = FindColumn("Đơn vị tính", "Đơn vị", "ĐVT");
                var values = new Dictionary<string, ReferenceStockValue>(StringComparer.OrdinalIgnoreCase);
                for (var row = headerRow + 1; row <= last; row++)
                {
                    var sku = sheet.Cell(row, skuColumn).GetString().Trim();
                    if (sku.Length == 0) continue;
                    var quantity = ReadOptionalNumber(sheet, row, quantityColumn, row, "Tồn thực tế", []);
                    if (!quantity.HasValue) continue;
                    var name = nameColumn > 0 ? sheet.Cell(row, nameColumn).GetString().Trim() : "";
                    var unit = unitColumn > 0 ? sheet.Cell(row, unitColumn).GetString().Trim() : "";
                    if (values.TryGetValue(sku, out var prior)) quantity += prior.Quantity;
                    values[sku] = new ReferenceStockValue { Sku = sku, Name = name, Unit = unit, Quantity = quantity.Value };
                }
                if (values.Count > 0) return values;
            }
        }
        throw new InvalidDataException("Không tìm thấy bảng tồn thực tế. Cần có cột mã SKU/Mã hàng và cột Tồn thực tế/Tồn hiện tại trong tệp tham chiếu.");
    }

    public static void ExportTransactionReport(string path, IEnumerable<InventoryTransaction> transactions, string title, IReadOnlyDictionary<string, string>? departments = null)
    {
        var records = transactions.ToList();
        using var workbook = new XLWorkbook();
        var summary = workbook.Worksheets.Add("Tổng hợp");
        summary.Cell("A1").Value = title.ToUpperInvariant();
        summary.Cell("A1").Style.Font.Bold = true;
        summary.Cell("A1").Style.Font.FontSize = 16;
        summary.Cell("A3").Value = "Số dòng giao dịch"; summary.Cell("B3").Value = records.Count;
        summary.Cell("A4").Value = "Phiếu nhập"; summary.Cell("B4").Value = records.Where(item => item.Type == "Nhập").Select(item => item.DocNo).Distinct().Count();
        summary.Cell("A5").Value = "Phiếu xuất"; summary.Cell("B5").Value = records.Where(item => item.Type == "Xuất").Select(item => item.DocNo).Distinct().Count();
        summary.Cell("A6").Value = "Phiếu điều chuyển"; summary.Cell("B6").Value = records.Where(item => item.Type == "Điều chuyển").Select(item => item.DocNo).Distinct().Count();
        summary.Column(1).Width = 30; summary.Column(2).Width = 20;

        var sheet = workbook.Worksheets.Add("ChiTiet");
        var headers = new[] { "Ngày", "Số phiếu", "Chứng từ tham chiếu", "Loại", "Mã SKU", "Tên vật tư", "Nhóm hàng", "Bộ phận", "Số lượng", "Đơn vị", "Kho", "Kho nhận", "Nơi giao nhận", "Người thực hiện", "Ghi chú" };
        for (var column = 0; column < headers.Length; column++) sheet.Cell(1, column + 1).Value = headers[column];
        var rowNumber = 2;
        foreach (var transaction in records.OrderBy(item => item.Date ?? DateOnly.MinValue).ThenBy(item => item.DocNo, StringComparer.OrdinalIgnoreCase))
        {
            if (transaction.Date.HasValue) sheet.Cell(rowNumber, 1).Value = transaction.Date.Value.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(rowNumber, 2).Value = transaction.DocNo;
            sheet.Cell(rowNumber, 3).Value = transaction.ReferenceNo;
            sheet.Cell(rowNumber, 4).Value = transaction.Type;
            sheet.Cell(rowNumber, 5).Value = transaction.Sku;
            sheet.Cell(rowNumber, 6).Value = transaction.ItemName;
            sheet.Cell(rowNumber, 7).Value = transaction.Category;
            sheet.Cell(rowNumber, 8).Value = departments?.GetValueOrDefault(transaction.Sku) ?? "";
            if (transaction.Quantity.HasValue) sheet.Cell(rowNumber, 9).Value = transaction.Quantity.Value;
            sheet.Cell(rowNumber, 10).Value = transaction.Unit;
            sheet.Cell(rowNumber, 11).Value = transaction.Type == "Điều chuyển" ? transaction.FromWarehouse : transaction.Warehouse;
            sheet.Cell(rowNumber, 12).Value = transaction.ToWarehouse;
            sheet.Cell(rowNumber, 13).Value = transaction.Partner;
            sheet.Cell(rowNumber, 14).Value = transaction.Operator;
            sheet.Cell(rowNumber, 15).Value = transaction.Note;
            rowNumber++;
        }
        StyleHeader(sheet.Range(1, 1, 1, headers.Length));
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(2, rowNumber - 1), headers.Length).SetAutoFilter();
        sheet.Column(1).Style.DateFormat.Format = "dd/mm/yyyy";
        sheet.Column(9).Style.NumberFormat.Format = "#,##0.##";
        sheet.Columns().AdjustToContents(1, Math.Max(1, rowNumber - 1));
        sheet.Column(6).Width = Math.Min(38, Math.Max(18, sheet.Column(6).Width));
        sheet.Column(15).Width = Math.Min(40, Math.Max(18, sheet.Column(15).Width));
        workbook.SaveAs(path);
    }

    private static string CellText(IXLWorksheet sheet, int row, int column) => column == 0 ? "" : sheet.Cell(row, column).GetString();
    private static string? NonBlank(IXLWorksheet sheet, int row, int column)
    {
        var value = CellText(sheet, row, column).Trim();
        return value.Length == 0 ? null : value;
    }

    private static double? ReadOptionalNumber(IXLWorksheet sheet, int row, int column, int reportRow, string field, List<string> errors)
    {
        if (column == 0 || sheet.Cell(row, column).IsEmpty()) return null;
        var cell = sheet.Cell(row, column);
        if (cell.TryGetValue<double>(out var numeric) && double.IsFinite(numeric)) return numeric;
        var text = cell.GetString().Trim().Replace(" ", "");
        var normalized = text.Contains(',') ? text.Replace(".", "").Replace(',', '.') : text;
        if (double.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out numeric) && double.IsFinite(numeric)) return numeric;
        errors.Add($"Dòng {reportRow}: {field} không phải số hợp lệ.");
        return null;
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#183754");
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Font.Bold = true;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
    }
}
