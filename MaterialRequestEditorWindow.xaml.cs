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
        _lines.Select(line => new { line.Sku, line.Quantity }).ToArray());
    public MaterialRequest? ResultRequest { get; private set; }

    public MaterialRequestEditorWindow(InventoryState state, string type)
    {
        InitializeComponent();
        _state = state;
        _type = type;
        Heading.Text = type;
        RecipientLabel.Text = type == "Yêu cầu cấp vật tư" ? "NGƯỜI NHẬN" : "NHÀ CUNG CẤP / NGUỒN";
        DateBox.SelectedDate = DateTime.Today;
        DepartmentBox.ItemsSource = state.Departments;
        RecipientBox.ItemsSource = state.Recipients;
        ProductBox.ItemsSource = state.Products.Where(item => item.Active).OrderBy(item => item.Name).ToList();
        RequesterBox.Text = Environment.UserName;
        LinesGrid.ItemsSource = _lines;
        UpdateLineCount();
        _navigationSnapshot = new WorkspaceFormSnapshot(CaptureForm);
    }

    private void AddLine_Click(object sender, RoutedEventArgs e)
    {
        if (ProductBox.SelectedItem is not InventoryItem item)
        {
            this.NotifyInline("Chọn vật tư cần đưa vào yêu cầu.");
            return;
        }
        if (!TryReadQuantity(QuantityBox.Text, out var quantity) || quantity <= 0)
        {
            this.NotifyInline("Số lượng yêu cầu phải lớn hơn 0.");
            return;
        }
        var line = _lines.FirstOrDefault(existing => existing.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase));
        if (line is null) _lines.Add(new MovementLine { Sku = item.Sku, ItemName = item.Name, Unit = item.Unit, Quantity = quantity });
        else line.Quantity += quantity;
        LinesGrid.Items.Refresh();
        ProductBox.SelectedItem = null;
        ProductBox.Text = "";
        QuantityBox.Text = "1";
        UpdateLineCount();
    }

    private void RemoveLine_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string sku)
        {
            var line = _lines.FirstOrDefault(item => item.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
            if (line is not null) _lines.Remove(line);
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
