using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class ReplenishmentWindow : Window
{
    private readonly InventoryState _state;
    private List<ReplenishmentSuggestion> _suggestions = [];
    public MaterialRequest? ProposedRequest { get; private set; }

    public ReplenishmentWindow(InventoryState state)
    {
        InitializeComponent();
        _state = state;
        WarehouseBox.ItemsSource = state.Warehouses;
        DepartmentBox.ItemsSource = state.Departments;
        RequesterBox.Text = Environment.UserName;
        WarehouseBox.SelectedItem = state.Warehouses.FirstOrDefault(warehouse =>
            warehouse.Equals("Kho tổng", StringComparison.OrdinalIgnoreCase)) ?? state.Warehouses.FirstOrDefault();
        RefreshSuggestions();
    }

    private void Warehouse_Changed(object sender, SelectionChangedEventArgs e) => RefreshSuggestions();

    private void RefreshSuggestions()
    {
        if (SuggestionsGrid is null || SuggestionCountText is null || WarehouseBox?.SelectedItem is not string warehouse) return;
        _suggestions = ReplenishmentService.Generate(_state, warehouse);
        SuggestionsGrid.ItemsSource = _suggestions;
        SuggestionCountText.Text = _suggestions.Count == 0
            ? "Không có mã nào dưới ngưỡng sau khi trừ yêu cầu nhập đang chờ."
            : $"{_suggestions.Count:N0} mã cần bổ sung · Chọn và chỉnh số lượng trước khi lập phiếu.";
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var suggestion in _suggestions) suggestion.Selected = true;
        SuggestionsGrid.Items.Refresh();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var suggestion in _suggestions) suggestion.Selected = false;
        SuggestionsGrid.Items.Refresh();
    }

    private void CreateRequest_Click(object sender, RoutedEventArgs e)
    {
        if (!SuggestionsGrid.CommitEdit(DataGridEditingUnit.Cell, true) ||
            !SuggestionsGrid.CommitEdit(DataGridEditingUnit.Row, true))
        {
            this.NotifyInline("Kiểm tra lại số lượng đề xuất đang nhập.");
            return;
        }

        var selected = _suggestions.Where(suggestion => suggestion.Selected).ToList();
        if (WarehouseBox.SelectedItem is not string warehouse || selected.Count == 0)
        {
            this.NotifyInline("Chọn kho và ít nhất một vật tư cần bổ sung.");
            return;
        }
        if (string.IsNullOrWhiteSpace(RequesterBox.Text))
        {
            this.NotifyInline("Nhập người đề xuất.");
            RequesterBox.Focus();
            return;
        }
        if (selected.Any(suggestion => !double.IsFinite(suggestion.RequestedQuantity) || suggestion.RequestedQuantity <= 0))
        {
            this.NotifyInline("Số lượng nhập đề xuất phải lớn hơn 0 cho tất cả dòng đã chọn.");
            return;
        }

        ProposedRequest = new MaterialRequest
        {
            Type = "Yêu cầu nhập vật tư",
            Date = DateOnly.FromDateTime(DateTime.Today),
            NeededBy = NeededByBox.SelectedDate is DateTime date ? DateOnly.FromDateTime(date) : null,
            Warehouse = warehouse,
            Department = DepartmentBox.Text.Trim(),
            Requester = RequesterBox.Text.Trim(),
            Note = $"Đề xuất bổ sung theo mức tồn tối thiểu tại kho {warehouse}." +
                   (string.IsNullOrWhiteSpace(NoteBox.Text) ? "" : $" {NoteBox.Text.Trim()}"),
            Status = "Chờ duyệt",
            Lines = selected.Select(suggestion => new MovementLine
            {
                Sku = suggestion.Sku,
                ItemName = suggestion.Name,
                Unit = suggestion.Unit,
                Quantity = suggestion.RequestedQuantity
            }).ToList()
        };
        this.CompleteEmbedded(true);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
