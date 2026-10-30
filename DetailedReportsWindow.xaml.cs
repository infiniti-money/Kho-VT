using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public sealed class ReportViewRow
{
    public InventoryTransaction Transaction { get; init; } = new();
    public string DateText => Transaction.Date?.ToString("dd/MM/yyyy") ?? "—";
    public string DocNo => Transaction.DocNo;
    public string ReferenceNo => Transaction.ReferenceNo;
    public string Type => Transaction.Type;
    public string Sku => Transaction.Sku;
    public string ItemName => Transaction.ItemName;
    public string Department { get; init; } = "";
    public string QuantityText => Transaction.Quantity is double quantity ? $"{(Transaction.Type == "Điều chỉnh" && quantity > 0 ? "+" : "")}{InventoryRow.FormatQuantity(quantity)} {Transaction.Unit}" : "—";
    public string Warehouse => Transaction.Type == "Điều chuyển" ? Transaction.FromWarehouse ?? "" : Transaction.Warehouse;
    public string ToWarehouse => Transaction.ToWarehouse ?? "";
    public string Partner => Transaction.Partner;
    public string Operator => Transaction.Operator;
    public string Note => Transaction.Note;
}

public partial class DetailedReportsWindow : Window
{
    private readonly InventoryState _state;
    private List<InventoryTransaction> _filtered = [];

    public DetailedReportsWindow(InventoryState state)
    {
        InitializeComponent();
        _state = state;
        WarehouseFilter.ItemsSource = new[] { "Tất cả kho" }.Concat(state.Warehouses).ToList();
        WarehouseFilter.SelectedIndex = 0;
        RefreshReport();
    }

    private List<InventoryTransaction> GetFiltered()
    {
        var search = SearchBox.Text.Trim();
        var type = (TypeFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả loại";
        var warehouse = WarehouseFilter.SelectedItem?.ToString() ?? "Tất cả kho";
        DateOnly? from = FromDate.SelectedDate is DateTime fromDate ? DateOnly.FromDateTime(fromDate) : null;
        DateOnly? to = ToDate.SelectedDate is DateTime toDate ? DateOnly.FromDateTime(toDate) : null;
        return _state.Transactions
            .Where(transaction => type == "Tất cả loại" || transaction.Type == type)
            .Where(transaction => warehouse == "Tất cả kho" || (transaction.Type == "Điều chuyển"
                ? transaction.FromWarehouse == warehouse || transaction.ToWarehouse == warehouse
                : transaction.Warehouse == warehouse))
            .Where(transaction => from is null || transaction.Date >= from)
            .Where(transaction => to is null || transaction.Date <= to)
            .Where(transaction => string.IsNullOrWhiteSpace(search) || new[] { transaction.DocNo, transaction.ReferenceNo, transaction.Type, transaction.Sku, transaction.ItemName, transaction.Partner, transaction.Operator, transaction.Note, transaction.Warehouse, transaction.ToWarehouse ?? "" }
                .Any(value => value.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(transaction => transaction.Date ?? DateOnly.MinValue).ThenByDescending(transaction => transaction.DocNo, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RefreshReport()
    {
        if (!IsInitialized || SearchBox is null) return;
        _filtered = GetFiltered();
        var departmentBySku = _state.Products.ToDictionary(item => item.Sku, item => item.Department, StringComparer.OrdinalIgnoreCase);
        ReportGrid.ItemsSource = _filtered.Select(transaction => new ReportViewRow
        {
            Transaction = transaction,
            Department = departmentBySku.GetValueOrDefault(transaction.Sku) ?? ""
        }).ToList();
        var receiptCount = _filtered.Where(transaction => transaction.Type == "Nhập").Select(transaction => transaction.DocNo).Distinct().Count();
        var shipmentCount = _filtered.Where(transaction => transaction.Type == "Xuất").Select(transaction => transaction.DocNo).Distinct().Count();
        var transferCount = _filtered.Where(transaction => transaction.Type == "Điều chuyển").Select(transaction => transaction.DocNo).Distinct().Count();
        SummaryText.Text = $"{_filtered.Count:N0} dòng · {receiptCount:N0} phiếu nhập · {shipmentCount:N0} phiếu xuất · {transferCount:N0} phiếu điều chuyển";
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => RefreshReport();
    private void ClearFilters_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        TypeFilter.SelectedIndex = 0;
        WarehouseFilter.SelectedIndex = 0;
        FromDate.SelectedDate = null;
        ToDate.SelectedDate = null;
        RefreshReport();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Xuất báo cáo chi tiết", Filter = "Sổ Excel (*.xlsx)|*.xlsx", FileName = $"Bao-cao-kho-{DateTime.Today:yyyyMMdd}.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var departmentBySku = _state.Products.ToDictionary(item => item.Sku, item => item.Department, StringComparer.OrdinalIgnoreCase);
            var title = $"Báo cáo kho · {_filtered.Count:N0} dòng";
            ExcelInventoryService.ExportTransactionReport(dialog.FileName, _filtered, title, departmentBySku);
            this.NotifyInline("Đã xuất báo cáo chi tiết ra Excel.");
        }
        catch (Exception ex) { this.NotifyInline(ex.Message); }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
