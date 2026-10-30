using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public sealed class NegativeStockRow
{
    public InventoryItem Item { get; init; } = new();
    public string Warehouse { get; init; } = "";
    public string Sku => Item.Sku;
    public string Name => Item.Name;
    public string Category => Item.Category;
    public string Department => Item.Department;
    public string Unit => Item.Unit;
    public double Stock { get; init; }
    public string StockText => InventoryRow.FormatQuantity(Stock);
    public string MinimumText => Item.MinStockDefined ? InventoryRow.FormatQuantity(Item.MinStock) : "Chưa đặt";
}

public partial class NegativeStockWindow : Window
{
    private readonly List<NegativeStockRow> _rows;
    public InventoryItem? SelectedItem { get; private set; }
    public string? SelectedWarehouse { get; private set; }

    public NegativeStockWindow(InventoryState state)
    {
        InitializeComponent();
        _rows = state.Products.Where(item => item.Active)
            .SelectMany(item => state.Warehouses.Select(warehouse => new NegativeStockRow
            {
                Item = item,
                Warehouse = warehouse,
                Stock = item.WarehouseStocks.GetValueOrDefault(warehouse)
            }))
            .Where(row => row.Stock < 0)
            .OrderBy(row => row.Stock)
            .ThenBy(row => row.Warehouse, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        RefreshRows();
    }

    private void RefreshRows()
    {
        var query = SearchBox.Text.Trim();
        var rows = _rows.Where(row => string.IsNullOrWhiteSpace(query) || new[] { row.Sku, row.Name, row.Warehouse, row.Category, row.Department }
            .Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToList();
        AlertsGrid.ItemsSource = rows;
        CountText.Text = $"{rows.Count:N0} cảnh báo";
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => RefreshRows();
    private void Receipt_Click(object sender, RoutedEventArgs e)
    {
        if (AlertsGrid.SelectedItem is not NegativeStockRow row)
        {
            this.NotifyInline("Chọn một dòng tồn âm.");
            return;
        }
        SelectedItem = row.Item;
        SelectedWarehouse = row.Warehouse;
        this.CompleteEmbedded(true);
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
