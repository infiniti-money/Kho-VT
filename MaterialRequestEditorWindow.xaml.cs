using HisaWarehouse.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HisaWarehouse;

public partial class MaterialRequestEditorWindow : Window, IWorkspaceDirtyPage
{
    private readonly InventoryState _state;
    private readonly string _type;
    private readonly ObservableCollection<MovementLine> _lines = [];
    private readonly WorkspaceFormSnapshot _navigationSnapshot;

    public bool HasUnsavedChanges
    {
        get
        {
            if (!LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
                !LinesGrid.CommitEdit(DataGridEditingUnit.Row, true)) return true;
            return _navigationSnapshot.HasChanges;
        }
    }

    private string CaptureForm() => WorkspaceFormSnapshot.Serialize(
        DateBox.SelectedDate, NeededByBox.SelectedDate, DepartmentBox.Text,
        RequesterBox.Text, RecipientBox.Text, NoteBox.Text, ProductBox.Text, QuantityBox.Text,
        ManualEntryCheck.IsChecked, ManualSkuBox.Text, ManualNameBox.Text, ManualUnitBox.Text,
        _lines.Select(line => new { line.Sku, line.ItemName, line.Unit, line.Quantity }).ToArray());
    public MaterialRequest? ResultRequest { get; private set; }

    public MaterialRequestEditorWindow(InventoryState state, string type)
    {
        InitializeComponent();
        _state = state;
        _type = type;
        Heading.Text = type == "Yêu cầu cấp vật tư dự phòng" ? "Phiếu yêu cầu cấp vật tư dự phòng" : type;
        RecipientLabel.Text = type == "Yêu cầu nhập vật tư" ? "NHÀ CUNG CẤP / NGUỒN" : "NGƯỜI NHẬN";
        DateBox.SelectedDate = DateTime.Today;
        DepartmentBox.ItemsSource = state.Departments;
        RecipientBox.ItemsSource = state.Recipients;
        ProductBox.ItemsSource = state.Products.Where(item => item.Active).OrderBy(item => item.Name).ToList();
        ManualUnitBox.ItemsSource = state.Units.OrderBy(unit => unit, StringComparer.CurrentCultureIgnoreCase).ToList();
        RequesterBox.Text = Environment.UserName;
        LinesGrid.ItemsSource = _lines;
        UpdateLineCount();
        _navigationSnapshot = new WorkspaceFormSnapshot(CaptureForm);
    }

    private void ManualEntry_Changed(object sender, RoutedEventArgs e)
    {
        if (ManualEntryPanel is null || ProductBox is null) return;
        var manual = ManualEntryCheck.IsChecked == true;
        ManualEntryPanel.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        ProductBox.IsEnabled = !manual;
        if (manual) ManualNameBox.Focus();
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadQuantity(QuantityBox.Text, out var quantity) || quantity <= 0)
        {
            this.NotifyInline("Số lượng yêu cầu phải lớn hơn 0.");
            return;
        }

        if (ManualEntryCheck.IsChecked == true)
        {
            var name = ManualNameBox.Text.Trim();
            var unit = ManualUnitBox.Text.Trim();
            var sku = ManualSkuBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(unit))
            {
                this.NotifyInline("Nhập tên vật tư và đơn vị tính của dòng yêu cầu dự phòng.");
                return;
            }
            if (sku.Length > 130 || name.Length > 300 || unit.Length > 80)
            {
                this.NotifyInline("Mã, tên hoặc đơn vị vật tư quá dài.");
                return;
            }
            if (sku.Length > 0 && _state.Products.Any(p =>
                p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)))
            {
                this.NotifyInline("Mã này đã có trong danh mục. Hãy bỏ chọn Nhập tay rồi chọn vật tư có sẵn.");
                return;
            }
            // A manual line is intentionally independent: no stock write, no
            // catalog creation, and blank SKU is permitted until coding.
            _lines.Add(new MovementLine { Sku = sku, ItemName = name, Unit = unit, Quantity = quantity });
            ManualSkuBox.Clear();
            ManualNameBox.Clear();
            ManualUnitBox.Text = "";
            ManualNameBox.Focus();
        }
        else
        {
            if (ProductBox.SelectedItem is not InventoryItem item)
            {
                this.NotifyInline("Chọn vật tư trong danh mục hoặc bật Nhập tay để khai báo vật tư chưa có.");
                return;
            }
            var line = _lines.FirstOrDefault(existing =>
                existing.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase) &&
                existing.ItemName.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            if (line is null)
                _lines.Add(new MovementLine { Sku = item.Sku, ItemName = item.Name, Unit = item.Unit, Quantity = quantity });
            else
                line.Quantity += quantity;
            ProductBox.SelectedItem = null;
            ProductBox.Text = "";
        }
        QuantityBox.Text = "1";
        LinesGrid.Items.Refresh();
        UpdateLineCount();
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is MovementLine line)
        {
            _lines.Remove(line);
            UpdateLineCount();
        }
    }

    private void ClearLines_Click(object sender, RoutedEventArgs e) { _lines.Clear(); UpdateLineCount(); }
    private void UpdateLineCount() => LineCountText.Text = $"{_lines.Count:N0} mặt hàng · {InventoryRow.FormatQuantity(_lines.Sum(line => line.Quantity))} đơn vị theo dòng";

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        LinesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        LinesGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (_lines.Count == 0)
        {
            this.NotifyInline("Thêm ít nhất một vật tư vào yêu cầu.");
            return;
        }
        if (DateBox.SelectedDate is not DateTime date || string.IsNullOrWhiteSpace(RequesterBox.Text))
        {
            this.NotifyInline("Nhập ngày và người yêu cầu.");
            return;
        }
        if (_lines.Any(line => !double.IsFinite(line.Quantity) || line.Quantity <= 0))
        {
            this.NotifyInline("Kiểm tra số lượng trên các dòng yêu cầu.");
            return;
        }
        ResultRequest = new MaterialRequest
        {
            Type = _type,
            Date = DateOnly.FromDateTime(date),
            NeededBy = NeededByBox.SelectedDate is DateTime neededBy ? DateOnly.FromDateTime(neededBy) : null,
            Department = DepartmentBox.Text.Trim(),
            Requester = RequesterBox.Text.Trim(),
            Recipient = RecipientBox.Text.Trim(),
            Note = NoteBox.Text.Trim(),
            Status = "Chờ xử lý",
            Lines = _lines.Select(line => new MovementLine { Sku = line.Sku, ItemName = line.ItemName, Unit = line.Unit, Quantity = line.Quantity }).ToList()
        };
        this.CompleteEmbedded(true);
    }

    private static bool TryReadQuantity(string text, out double quantity)
    {
        var value = text.Trim().Replace(" ", "");
        if (value.Contains(',')) value = value.Replace(".", "").Replace(',', '.');
        else
        {
            var parts = value.Split('.');
            if (parts.Length > 2 || (parts.Length == 2 && parts[1].Length == 3 && parts[0].Length <= 3)) value = value.Replace(".", "");
        }
        return double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out quantity) && double.IsFinite(quantity);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
