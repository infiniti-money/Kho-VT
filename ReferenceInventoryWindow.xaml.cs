using System.ComponentModel;
using ClosedXML.Excel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public sealed class ReferenceComparisonRow : INotifyPropertyChanged
{
    private double? _proposedStock;

    public event PropertyChangedEventHandler? PropertyChanged;
    public InventoryItem? Item { get; init; }
    public string Sku { get; init; } = "";
    public string Name { get; init; } = "";
    public string Unit { get; init; } = "";
    public double? AppStock { get; init; }
    public double? ActualStock { get; init; }
    public double? Difference => AppStock.HasValue && ActualStock.HasValue ? ActualStock.Value - AppStock.Value : null;
    public string AppStockText => AppStock.HasValue ? InventoryRow.FormatQuantity(AppStock.Value) : "—";
    public string ActualStockText => ActualStock.HasValue ? InventoryRow.FormatQuantity(ActualStock.Value) : "—";
    public string DifferenceText => Difference.HasValue ? $"{(Difference > 0 ? "+" : "")}{InventoryRow.FormatQuantity(Difference.Value)}" : "—";
    public string Status => !AppStock.HasValue ? "Mã ngoài file chính" : !ActualStock.HasValue ? "Không có trong tệp đối chiếu" : Math.Abs(Difference!.Value) < 0.000001 ? "Khớp" : "Chênh lệch";

    public double? ProposedStock
    {
        get => _proposedStock;
        set
        {
            if (_proposedStock == value) return;
            _proposedStock = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public partial class ReferenceInventoryWindow : Window
{
    private readonly InventoryState _state;
    private readonly string _primaryPath;
    public string ResolvedPath { get; }
    public Func<IReadOnlyList<ReferenceComparisonRow>, string, Task>? ApplyChanges { get; set; }

    public ReferenceInventoryWindow(InventoryState state, string path, string primaryPath)
    {
        InitializeComponent();
        _state = state;
        _primaryPath = primaryPath;
        ResolvedPath = ResolveWorkbook(path);
        if (Path.GetFullPath(ResolvedPath).Equals(Path.GetFullPath(_primaryPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Tệp đối chiếu không được trùng với KHO.xlsx chính. Hãy chọn một tệp Excel khác.");
        WarehouseBox.ItemsSource = state.Warehouses;
        WarehouseBox.SelectedItem = state.Warehouses.FirstOrDefault(warehouse => warehouse.Equals("Kho tổng", StringComparison.OrdinalIgnoreCase))
            ?? state.Warehouses.FirstOrDefault();
        RefreshComparisons();
    }

    public void RefreshComparisons()
    {
        var info = new FileInfo(ResolvedPath);
        SourceText.Text = $"{ResolvedPath} · cập nhật {info.LastWriteTime:dd/MM/yyyy HH:mm}";
        var reference = ExcelInventoryService.ReadReferenceStock(ResolvedPath);
        var primary = PrimaryWorkbookReader.ReadStocks(_primaryPath);
        var products = _state.Products.Where(item => item.Active).ToDictionary(item => item.Sku, StringComparer.OrdinalIgnoreCase);
        var rows = new List<ReferenceComparisonRow>();

        foreach (var item in primary.Values.OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase))
        {
            reference.TryGetValue(item.Sku, out var actual);
            products.TryGetValue(item.Sku, out var appItem);
            rows.Add(new ReferenceComparisonRow
            {
                Item = appItem,
                Sku = item.Sku,
                Name = item.Name,
                Unit = item.Unit,
                AppStock = item.Current,
                ActualStock = actual?.Quantity,
                ProposedStock = item.Current
            });
        }

        foreach (var external in reference.Values.Where(value => !primary.ContainsKey(value.Sku))
                     .OrderBy(value => value.Sku, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new ReferenceComparisonRow
            {
                Sku = external.Sku,
                Name = external.Name,
                Unit = external.Unit,
                ActualStock = external.Quantity,
                ProposedStock = external.Quantity
            });
        }

        ComparisonGrid.ItemsSource = rows;
        ComparisonGrid.IsReadOnly = true;
        ApplyButton.Visibility = Visibility.Collapsed;
        WarehouseBox.IsEnabled = false;
        var matches = rows.Count(row => row.Status == "Khớp");
        var differences = rows.Count(row => row.Status == "Chênh lệch");
        var missing = rows.Count(row => row.Status == "Không có trong tệp đối chiếu");
        var outside = rows.Count(row => row.Status == "Mã ngoài file chính");
        SummaryText.Text = $"Tổng {rows.Count:N0} mã · khớp {matches:N0} · lệch {differences:N0} · thiếu Bravo {missing:N0} · chỉ có Bravo {outside:N0} · tồn âm Bravo {rows.Count(row => row.ActualStock < 0):N0}";
    }

    private void ExportBravoComparison_Click(object sender, RoutedEventArgs e)
    {
        var rows = (ComparisonGrid.ItemsSource as IEnumerable<ReferenceComparisonRow>)?.ToList() ?? [];
        if (rows.Count == 0)
        {
            this.NotifyInline("Chưa có dòng đối chiếu để xuất.");
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Xuất kết quả đối chiếu tồn Bravo với KHO.xlsx",
            Filter = "Sổ Excel (*.xlsx)|*.xlsx",
            FileName = "Doi-chieu-Bravo-" + DateTime.Today.ToString("yyyyMMdd") + ".xlsx"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var book = new XLWorkbook();
            var sheet = book.Worksheets.Add("DoiChieuBravo");
            var headers = new[] { "Mã vật tư", "Tên vật tư", "Đơn vị tính",
                "Tồn KHO.xlsx", "Tồn Bravo (cột L)", "Chênh lệch Bravo - KHO", "Trạng thái" };
            for (var c = 0; c < headers.Length; c++)
                sheet.Cell(1, c + 1).Value = headers[c];
            var number = 2;
            foreach (var row in rows)
            {
                sheet.Cell(number, 1).Value = row.Sku;
                sheet.Cell(number, 2).Value = row.Name;
                sheet.Cell(number, 3).Value = row.Unit;
                if (row.AppStock.HasValue) sheet.Cell(number, 4).Value = row.AppStock.Value;
                if (row.ActualStock.HasValue) sheet.Cell(number, 5).Value = row.ActualStock.Value;
                if (row.Difference.HasValue) sheet.Cell(number, 6).Value = row.Difference.Value;
                sheet.Cell(number, 7).Value = row.Status;
                if (row.ActualStock < 0)
                    sheet.Cell(number, 5).Style.Font.FontColor = XLColor.DarkRed;
                number++;
            }
            sheet.Range(1, 1, 1, 7).Style.Font.Bold = true;
            sheet.Columns(4, 6).Style.NumberFormat.Format = "#,##0.##;-#,##0.##;0";
            sheet.SheetView.FreezeRows(1);
            sheet.Range(1, 1, Math.Max(2, number - 1), 7).SetAutoFilter();
            sheet.Columns(1, 7).AdjustToContents();
            book.SaveAs(dialog.FileName);
            this.NotifyInline("Đã xuất báo cáo đối chiếu. Không thay đổi file KHO.xlsx hoặc Bravo.");
        }
        catch (Exception ex)
        {
            this.NotifyInline("Không xuất được báo cáo đối chiếu: " + ex.Message);
        }
    }

    private void SelectDifferences_Click(object sender, RoutedEventArgs e)
    {
        ComparisonGrid.SelectedItems.Clear();
        foreach (var row in (ComparisonGrid.ItemsSource as IEnumerable<ReferenceComparisonRow>) ?? [])
            if (row.Item is not null && row.ActualStock.HasValue && Math.Abs(row.Difference ?? 0) > 0.000001)
                ComparisonGrid.SelectedItems.Add(row);
    }

    private async void ApplySelected_Click(object sender, RoutedEventArgs e)
    {
        ComparisonGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        ComparisonGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        if (WarehouseBox.SelectedItem is not string warehouse)
        {
            this.NotifyInline("Chọn kho sẽ nhận phần điều chỉnh tồn.");
            return;
        }

        var selected = ComparisonGrid.SelectedItems.OfType<ReferenceComparisonRow>().ToList();
        if (selected.Count == 0)
        {
            this.NotifyInline("Chọn các dòng cần áp dụng. Có thể dùng nút Chọn SKU lệch.");
            return;
        }
        if (selected.Any(row => row.Item is null))
        {
            this.NotifyInline("Các SKU ngoài danh mục chỉ để tham khảo; hãy thêm SKU vào danh mục trước khi điều chỉnh tồn.");
            return;
        }
        if (selected.Any(row => !row.ProposedStock.HasValue || !double.IsFinite(row.ProposedStock.Value)))
        {
            this.NotifyInline("Kiểm tra lại cột tồn đề xuất. Số lượng phải là một số hợp lệ.");
            return;
        }

        if (ApplyChanges is not null) await ApplyChanges(selected, warehouse);
    }

    private static string ResolveWorkbook(string path)
    {
        if (File.Exists(path) && Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) return path;
        if (!Directory.Exists(path)) throw new FileNotFoundException("Đường dẫn tham chiếu không tồn tại.", path);
        var workbook = Directory.EnumerateFiles(path, "*.xlsx", SearchOption.TopDirectoryOnly)
            .Where(file => !Path.GetFileName(file).StartsWith("~$", StringComparison.Ordinal))
            .OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        return workbook ?? throw new FileNotFoundException("Thư mục không có tệp Excel .xlsx để đối chiếu.", path);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
