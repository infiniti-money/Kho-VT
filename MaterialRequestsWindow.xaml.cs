using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public sealed class MaterialRequestRow
{
    public MaterialRequest Request { get; init; } = new();
    public string DocNo => Request.DocNo;
    public string DateText => Request.Date.ToString("dd/MM/yyyy");
    public string Type => Request.Type;
    public string Department => Request.Department;
    public string Warehouse => string.IsNullOrWhiteSpace(Request.Warehouse) ? "—" : Request.Warehouse;
    public string Requester => Request.Requester;
    public string Recipient => Request.Recipient;
    public int LineCount => Request.Lines.Count;
    public string NeededByText => Request.NeededBy?.ToString("dd/MM/yyyy") ?? "—";
    public string Status => Request.Status;
}

public partial class MaterialRequestsWindow : Window
{
    private readonly InventoryState _state;
    public bool ChangesMade { get; private set; }
    public MaterialRequest? SelectedRequest { get; private set; }
    public string Action { get; private set; } = "";
    public Action<Window, Action<bool>>? OpenChildPage { get; set; }

    public MaterialRequestsWindow(InventoryState state)
    {
        InitializeComponent();
        _state = state;
        RefreshRows();
    }

    private void RefreshRows()
    {
        var rows = _state.MaterialRequests.OrderByDescending(request => request.Date).ThenByDescending(request => request.DocNo)
            .Select(request => new MaterialRequestRow { Request = request }).ToList();
        RequestsGrid.ItemsSource = rows;
        CountText.Text = $"{rows.Count:N0} yêu cầu · {rows.Count(row => row.Request.Status is "Chờ xử lý" or "Chờ duyệt"):N0} đang chờ duyệt";
    }

    private void NewReserveRequest_Click(object sender, RoutedEventArgs e) =>
        CreateRequest("Yêu cầu cấp vật tư dự phòng");
    private void CreateRequest(string type)
    {
        // This page is hosted inside MainWindow, not shown as a WPF Window.
        // The embedding host controls focus and navigation; do not set Owner.
        var editor = new MaterialRequestEditorWindow(_state, type);
        if (OpenChildPage is not null)
        {
            OpenChildPage(editor, accepted => { if (accepted && editor.ResultRequest is MaterialRequest nestedRequest) SaveRequest(nestedRequest, type); });
        }
        else this.NotifyInline("Không thể mở biểu mẫu vì trang chưa được kết nối với cửa sổ chính.");
    }

    private void SaveRequest(MaterialRequest request, string type)
    {
        MaterialRequestService.Add(_state, request);
        ChangesMade = true;
        RefreshRows();
        RequestsGrid.SelectedItem = (RequestsGrid.ItemsSource as IEnumerable<MaterialRequestRow>)?.FirstOrDefault(row => row.Request.Id == request.Id);
    }

    private MaterialRequest? GetSelected() => (RequestsGrid.SelectedItem as MaterialRequestRow)?.Request;

    private void Approve_Click(object sender, RoutedEventArgs e)
    {
        var request = GetSelected();
        if (request is null) { this.NotifyInline("Chọn một yêu cầu để duyệt."); return; }
        if (request.Status == "Hoàn thành") { this.NotifyInline("Yêu cầu đã hoàn thành."); return; }
        if (request.Status == "Đã duyệt") { this.NotifyInline("Yêu cầu này đã được duyệt."); return; }

        var detail = string.Join("\n", request.Lines.Take(12).Select(line =>
            $"• {line.Sku} - {line.ItemName}: {InventoryRow.FormatQuantity(line.Quantity)} {line.Unit}"));
        var approval = MessageBox.Show(
            $"DUYỆT YÊU CẦU {request.DocNo}?\n\n" +
            $"Kho: {request.Warehouse}\nLoại: {request.Type}\n\n" +
            detail + (request.Lines.Count > 12 ? "\n..." : "") +
            "\n\nDuyệt chỉ thay đổi trạng thái yêu cầu, KHÔNG tự nhập hàng hoặc sửa Excel.",
            "An · Xác nhận duyệt phiếu", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No);
        if (approval != MessageBoxResult.Yes) return;
        request.Status = "Đã duyệt";
        ChangesMade = true;
        RefreshRows();
    }

    private void Complete_Click(object sender, RoutedEventArgs e)
    {
        var request = GetSelected();
        if (request is null) { this.NotifyInline("Chọn một yêu cầu."); return; }
        if (request.Status == "Hoàn thành") return;
        request.Status = "Hoàn thành";
        ChangesMade = true;
        RefreshRows();
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        SelectedRequest = GetSelected();
        if (SelectedRequest is null) { this.NotifyInline("Chọn phiếu yêu cầu cần in."); return; }
        Action = "print";
        this.CompleteEmbedded(true);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
