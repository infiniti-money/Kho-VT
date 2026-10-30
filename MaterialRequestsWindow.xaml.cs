using System.Windows;
using System.Windows.Controls;

namespace HisaWarehouse;

public sealed class MaterialRequestRow
{
    public MaterialRequest Request { get; init; } = new();
    public string DocNo => Request.DocNo;
    public string DateText => Request.Date.ToString("dd/MM/yyyy");
    public string Type => Request.Type;
    public string Department => Request.Department;
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
        CountText.Text = $"{rows.Count:N0} yêu cầu · {rows.Count(row => row.Request.Status == "Chờ xử lý"):N0} đang chờ";
    }

    private void NewIssueRequest_Click(object sender, RoutedEventArgs e) => CreateRequest("Yêu cầu cấp vật tư");
    private void NewReceiptRequest_Click(object sender, RoutedEventArgs e) => CreateRequest("Yêu cầu nhập vật tư");
    private void CreateRequest(string type)
    {
        var editor = new MaterialRequestEditorWindow(_state, type) { Owner = this };
        if (OpenChildPage is not null)
        {
            OpenChildPage(editor, accepted => { if (accepted && editor.ResultRequest is MaterialRequest nestedRequest) SaveRequest(nestedRequest, type); });
        }
        else this.NotifyInline("Không thể mở biểu mẫu vì trang chưa được kết nối với cửa sổ chính.");
    }

    private void SaveRequest(MaterialRequest request, string type)
    {
        var datePart = request.Date.ToString("yyyyMMdd");
        var prefix = type == "Yêu cầu cấp vật tư" ? "YC-CAP" : "YC-NHAP";
        var prior = _state.MaterialRequests.Select(item => item.DocNo).Where(number => number.StartsWith($"{prefix}-{datePart}-", StringComparison.OrdinalIgnoreCase))
            .Select(number => int.TryParse(number[(number.LastIndexOf('-') + 1)..], out var value) ? value : 0).DefaultIfEmpty(0).Max();
        request.DocNo = $"{prefix}-{datePart}-{prior + 1:000}";
        _state.MaterialRequests.Add(request);
        if (!string.IsNullOrWhiteSpace(request.Department) && !_state.Departments.Contains(request.Department, StringComparer.OrdinalIgnoreCase)) _state.Departments.Add(request.Department);
        if (!string.IsNullOrWhiteSpace(request.Recipient) && !_state.Recipients.Contains(request.Recipient, StringComparer.OrdinalIgnoreCase)) _state.Recipients.Add(request.Recipient);
        ChangesMade = true;
        RefreshRows();
        RequestsGrid.SelectedItem = (RequestsGrid.ItemsSource as IEnumerable<MaterialRequestRow>)?.FirstOrDefault(row => row.Request.Id == request.Id);
    }

    private MaterialRequest? GetSelected() => (RequestsGrid.SelectedItem as MaterialRequestRow)?.Request;
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
