using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HisaWarehouse;

public partial class MovementWindow : Window
{
    private readonly InventoryState _state;
    private readonly ObservableCollection<MovementLine> _lines = [];
    public MovementDraft? Draft { get; private set; }

    public MovementWindow(InventoryState state, string? initialSku = null, string? initialType = null, string? initialWarehouse = null)
    {
        InitializeComponent();
        _state = state;
        DateBox.SelectedDate = DateTime.Today;
        WarehouseBox.ItemsSource = state.Warehouses;
        ToWarehouseBox.ItemsSource = state.Warehouses;
        WarehouseBox.SelectedItem = state.Warehouses.FirstOrDefault();
        ToWarehouseBox.SelectedItem = state.Warehouses.Skip(1).FirstOrDefault() ?? state.Warehouses.FirstOrDefault();
        ProductBox.ItemsSource = state.Products.Where(p => p.Active).OrderBy(p => p.Name).ToList();
        PartnerBox.ItemsSource = state.Recipients.OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase).ToList();
        OperatorBox.Text = Environment.UserName;
        LinesGrid.ItemsSource = _lines;
        if (!string.IsNullOrWhiteSpace(initialType))
        {
            var index = MovementTypeBox.Items.Cast<ComboBoxItem>().ToList().FindIndex(item => item.Content?.ToString() == initialType);
            if (index >= 0) MovementTypeBox.SelectedIndex = index;
        }
        if (!string.IsNullOrWhiteSpace(initialWarehouse) && state.Warehouses.Contains(initialWarehouse)) WarehouseBox.SelectedItem = initialWarehouse;
        if (!string.IsNullOrWhiteSpace(initialSku))
        {
            var initialItem = state.Products.FirstOrDefault(item => item.Active && item.Sku.Equals(initialSku, StringComparison.OrdinalIgnoreCase));
            if (initialItem is not null) AddLine(initialItem, 1);
        }
        UpdateLineCount();
    }

    private string SelectedType => (MovementTypeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Nhập";

    private void MovementType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ToWarehousePanel is null || MovementTypeBox.SelectedItem is not ComboBoxItem selected) return;
        var type = selected.Content?.ToString();
        ToWarehousePanel.Visibility = type == "Điều chuyển" ? Visibility.Visible : Visibility.Collapsed;
        PartnerLabel.Text = "NGƯỜI NHẬN / GỬI";
        ScannerStatus.Text = type == "Nhập" ? "Nhập hàng sẽ cộng vào kho đã chọn." : type == "Xuất" ? "Xuất hàng sẽ trừ tồn của kho đã chọn." : "Điều chuyển sẽ trừ kho xuất và cộng kho nhận.";
    }

    private void Scanner_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var code = ScannerBox.Text.Trim();
        var item = _state.Products.FirstOrDefault(p => p.Active &&
            (string.Equals(p.Sku, code, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Barcode, code, StringComparison.OrdinalIgnoreCase)));
        if (item is null)
        {
            ScannerStatus.Text = $"Không tìm thấy SKU hoặc barcode: {code}";
            ScannerStatus.Foreground = System.Windows.Media.Brushes.IndianRed;
            ScannerBox.SelectAll();
            return;
        }
        AddLine(item, 1);
        ScannerStatus.Text = $"Đã thêm {item.Sku} · {item.Name}";
        ScannerStatus.Foreground = System.Windows.Media.Brushes.SeaGreen;
        ScannerBox.Clear();
        ScannerBox.Focus();
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        if (ProductBox.SelectedItem is not InventoryItem item)
        {
            this.NotifyInline("Chọn vật tư cần thêm vào phiếu.");
            return;
        }
        if (!TryReadQuantity(QuantityBox.Text, out var qty) || qty <= 0)
        {
            this.NotifyInline("Số lượng phải lớn hơn 0.");
            return;
        }
        AddLine(item, qty);
        ProductBox.SelectedItem = null;
        ProductBox.Text = "";
        QuantityBox.Text = "1";
    }

    private void AddLine(InventoryItem item, double quantity)
    {
        var existing = _lines.FirstOrDefault(line => line.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase));
        if (existing is null) _lines.Add(new MovementLine { Sku = item.Sku, ItemName = item.Name, Unit = item.Unit, Quantity = quantity });
        else existing.Quantity += quantity;
        LinesGrid.Items.Refresh();
        UpdateLineCount();
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string sku)
        {
            var line = _lines.FirstOrDefault(x => x.Sku == sku);
            if (line is not null) _lines.Remove(line);
            UpdateLineCount();
        }
    }

    private void ClearLines_Click(object sender, RoutedEventArgs e)
    {
        _lines.Clear();
        UpdateLineCount();
    }

    private void UpdateLineCount() => LineCountText.Text = $"{_lines.Count} mặt hàng · {InventoryRow.FormatQuantity(_lines.Sum(x => x.Quantity))} đơn vị theo dòng";

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (_lines.Count == 0)
        {
            this.NotifyInline("Thêm ít nhất một mặt hàng vào phiếu.");
            return;
        }
        if (!DateBox.SelectedDate.HasValue || WarehouseBox.SelectedItem is not string warehouse)
        {
            this.NotifyInline("Chọn ngày và kho giao dịch.");
            return;
        }
        var type = SelectedType;
        var destination = ToWarehouseBox.SelectedItem as string ?? "";
        if (type == "Điều chuyển" && (string.IsNullOrWhiteSpace(destination) || destination == warehouse))
        {
            this.NotifyInline("Kho nhận phải khác kho xuất.");
            return;
        }
        foreach (var line in _lines)
        {
            if (!double.IsFinite(line.Quantity) || line.Quantity <= 0)
            {
                this.NotifyInline($"Số lượng của {line.Sku} phải lớn hơn 0.");
                return;
            }
            if ((type is "Xuất" or "Điều chuyển") && PendingCheck.IsChecked != true)
            {
                var item = _state.Products.FirstOrDefault(p => p.Active && p.Sku == line.Sku);
                var available = item?.WarehouseStocks.GetValueOrDefault(warehouse) ?? 0;
                if (item is null || line.Quantity > available)
                {
                    this.NotifyInline($"Không đủ tồn {line.Unit} tại {warehouse} cho {line.Sku}. Hiện có {InventoryRow.FormatQuantity(available)}.");
                    return;
                }
            }
        }
        if (string.IsNullOrWhiteSpace(OperatorBox.Text))
        {
            this.NotifyInline("Nhập người thực hiện để lưu vào nhật ký.");
            OperatorBox.Focus();
            return;
        }
        Draft = new MovementDraft
        {
            Type = type,
            Date = DateOnly.FromDateTime(DateBox.SelectedDate.Value),
            Warehouse = warehouse,
            ToWarehouse = destination,
            ReferenceNo = ReferenceBox.Text.Trim(),
            Partner = PartnerBox.Text.Trim(),
            Operator = OperatorBox.Text.Trim(),
            Note = NoteBox.Text.Trim(),
            Pending = PendingCheck.IsChecked == true,
            Lines = _lines.Select(x => new MovementLine { Sku = x.Sku, ItemName = x.ItemName, Unit = x.Unit, Quantity = x.Quantity }).ToList()
        };
        this.CompleteEmbedded(true);
    }

    private static bool TryReadQuantity(string text, out double quantity)
    {
        var normalized = text.Trim().Replace(" ", "");
        if (normalized.Contains(',')) normalized = normalized.Replace(".", "").Replace(',', '.');
        else
        {
            var parts = normalized.Split('.');
            if (parts.Length > 2 || (parts.Length == 2 && parts[1].Length == 3 && parts[0].Length <= 3)) normalized = normalized.Replace(".", "");
        }
        return double.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out quantity) && double.IsFinite(quantity);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
