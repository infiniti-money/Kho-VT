using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class MovementWindow : Window, IWorkspaceDirtyPage
{
    private readonly InventoryState _state;
    private readonly ObservableCollection<MovementLine> _lines = [];
    private readonly WorkspaceFormSnapshot _navigationSnapshot;

    public bool HasUnsavedChanges
    {
        get
        {
            // Commit an actively edited quantity before deciding whether navigation discards it.
            if (!LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                !LinesGrid.CommitEdit(DataGridEditingUnit.Row, true)) return true;
            return _navigationSnapshot.HasChanges;
        }
    }

    private string CaptureForm() => WorkspaceFormSnapshot.Serialize(
        SelectedType, DateBox.SelectedDate, WarehouseBox.SelectedItem, ToWarehouseBox.SelectedItem,
        ReferenceBox.Text, PartnerBox.Text, OperatorBox.Text, NoteBox.Text,
        PendingCheck.IsChecked, ScannerBox.Text, ProductBox.Text, QuantityBox.Text,
        _lines.Select(line => new { line.Sku, line.Quantity }).ToArray());
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
        _navigationSnapshot = new WorkspaceFormSnapshot(CaptureForm);
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
            if (!TryResolveNewReceiptMaterial(code, out item)) return;
        }
        AddLine(item, 1);
        ScannerStatus.Text = $"Đã thêm {item.Sku} · {item.Name}";
        ScannerStatus.Foreground = System.Windows.Media.Brushes.SeaGreen;
        ScannerBox.Clear();
        ScannerBox.Focus();
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        InventoryItem? item = ProductBox.SelectedItem as InventoryItem;
        if (item is null && !TryResolveNewReceiptMaterial(ProductBox.Text.Trim(), out item))
            return;
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

    private bool TryResolveNewReceiptMaterial(string code, out InventoryItem? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(code))
        {
            this.NotifyInline("Nhập hoặc quét mã SKU cần tìm.");
            return false;
        }
        if (SelectedType != "Nhập")
        {
            this.NotifyInline($"Mã {code} không có trong kho. Chỉ cho phép tự thêm mã từ file đối chiếu khi nhập hàng.");
            return false;
        }
        try
        {
            var path = ReferenceMaterialLookup.Resolve(_state.ReferencePath, _state.PrimaryExcelPath);
            var reference = ReferenceMaterialLookup.Read(path);
            if (!reference.TryGetValue(code.Trim(), out var found))
            {
                this.NotifyInline($"Mã {code} không có trong file đối chiếu. Không tạo danh mục tự động.");
                return false;
            }
            // This is a staged catalog row: not persisted into HISA or KHO.xlsx
            // until the entire receipt is safely written and acknowledged.
            item = new InventoryItem
            {
                Sku = found.Sku.Trim(),
                Barcode = found.Sku.Trim(),
                Name = found.Name.Trim(),
                Unit = string.IsNullOrWhiteSpace(found.Unit) ? "Cái" : found.Unit.Trim(),
                Category = "Vật tư khác",
                WarehouseStocks = _state.Warehouses.ToDictionary(x => x, _ => 0d)
            };
            ScannerStatus.Text = $"Đã đối chiếu {item.Sku} · {item.Name}. Mã sẽ được thêm vào Danh mục khi lưu phiếu.";
            ScannerStatus.Foreground = System.Windows.Media.Brushes.SeaGreen;
            return true;
        }
        catch (Exception ex)
        {
            this.NotifyInline("Không thể đối chiếu mã mới: " + ex.Message);
            return false;
        }
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
