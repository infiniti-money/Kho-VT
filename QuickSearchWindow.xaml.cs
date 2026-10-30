using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class QuickSearchWindow : Window
{
    private readonly InventoryState _state;
    private List<InventoryRow> _rows = [];
    public InventoryItem? SelectedItem { get; private set; }
    public string Action { get; private set; } = "";

    public QuickSearchWindow(InventoryState state)
    {
        InitializeComponent();
        _state = state;
        RefreshResults();
        SearchBox.Focus();
    }

    private void RefreshResults()
    {
        var query = SearchBox.Text.Trim();
        _rows = _state.Products.Where(item => item.Active)
            .Where(item => string.IsNullOrWhiteSpace(query) || new[] { item.Sku, item.Barcode, item.Name, item.Category, item.Department }
                .Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(item => new InventoryRow { Item = item, Stock = item.StockAt(_state.Warehouses) }).ToList();
        ResultsGrid.ItemsSource = _rows;
        CountText.Text = $"{_rows.Count:N0} kết quả";
    }

    private InventoryItem? GetSelected() => (ResultsGrid.SelectedItem as InventoryRow)?.Item;
    private void Search_Changed(object sender, TextChangedEventArgs e) => RefreshResults();
    private void EditSelected_Click(object sender, RoutedEventArgs e)
    {
        SelectedItem = GetSelected();
        if (SelectedItem is null) { this.NotifyInline("Chọn một vật tư trong kết quả tìm kiếm."); return; }
        Action = "edit";
        this.CompleteEmbedded(true);
    }
    private void ReceiptSelected_Click(object sender, RoutedEventArgs e) => CompleteMovement("Nhập");
    private void ShipmentSelected_Click(object sender, RoutedEventArgs e) => CompleteMovement("Xuất");
    private void CompleteMovement(string type)
    {
        SelectedItem = GetSelected();
        if (SelectedItem is null) { this.NotifyInline("Chọn một vật tư trong kết quả tìm kiếm."); return; }
        Action = type;
        this.CompleteEmbedded(true);
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
