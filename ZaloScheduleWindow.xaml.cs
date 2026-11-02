using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public sealed class ZaloPlanRow
{
    public bool IsSelected { get; set; }
    public ZaloPlannedWork Plan { get; init; } = new();
    public string Status { get; init; } = "Dự kiến";
}

public partial class ZaloScheduleWindow : Window, IWorkspaceDirtyPage
{
    private readonly InventoryState _state;
    private readonly List<ZaloPlanRow> _recognized = [];
    private readonly List<ZaloPlanRow> _plans = [];
    private bool _recognizing;

    public Func<Task<bool>>? PersistPlansAsync { get; set; }
    public Action<ZaloScheduleService.PendingPlan>? OpenDraftEditor { get; set; }
    public bool HasUnsavedChanges => _recognized.Any(x => x.IsSelected);

    public ZaloScheduleWindow(InventoryState state)
    {
        InitializeComponent();
        _state = state;
        RefreshPlans();
        RefreshRecognized();
    }

    private static string FilterType(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả công việc";

    private static bool Matches(ZaloPlanRow row, string query, string type)
    {
        var plan = row.Plan;
        return (type == "Tất cả công việc" || type == plan.WorkType) &&
            (query.Length == 0 ||
             plan.ContractNo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             plan.PackageNo.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             plan.Sku.Contains(query, StringComparison.OrdinalIgnoreCase) ||
             plan.ItemName.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private List<ZaloPlanRow> FilterRecognized() =>
        _recognized.Where(x => Matches(x, RecognizedSearch?.Text.Trim() ?? "",
            RecognizedTypeFilter is null ? "Tất cả công việc" : FilterType(RecognizedTypeFilter))).ToList();

    private List<ZaloPlanRow> FilterPlans() =>
        _plans.Where(x => Matches(x, PlansSearch?.Text.Trim() ?? "",
            PlansTypeFilter is null ? "Tất cả công việc" : FilterType(PlansTypeFilter))).ToList();

    private void RefreshRecognized()
    {
        if (RecognizedGrid is null) return;
        var rows = FilterRecognized();
        RecognizedGrid.ItemsSource = null;
        RecognizedGrid.ItemsSource = rows;
        RecognizedCount.Text = $"{rows.Count} dòng đang hiển thị / {_recognized.Count} dòng nhận diện";
    }

    public void RefreshPlans()
    {
        if (PlansGrid is null) return;
        var selected = _plans.Where(r => r.IsSelected).Select(r => r.Plan.Id)
            .ToHashSet(StringComparer.Ordinal);
        _plans.Clear();
        _plans.AddRange(_state.ZaloPlans.Select(p =>
        {
            var pending = _state.PendingMovements.FirstOrDefault(m => m.DocNo == p.PendingDocNo);
            return new ZaloPlanRow
            {
                Plan = p, IsSelected = selected.Contains(p.Id),
                Status = pending?.Status == "Đã xác nhận" ? "Đã xác nhận thực tế" :
                         pending?.Status == "Chờ xác nhận" ? "Đã lên phiếu chờ" : "Dự kiến"
            };
        }));
        var filtered = FilterPlans();
        PlansGrid.ItemsSource = null;
        PlansGrid.ItemsSource = filtered;
        PlansCount.Text = $"{filtered.Count} dòng đang lọc / {_plans.Count} trong Tổng hợp";
    }

    private void RecognizedFilter_Changed(object sender, EventArgs e) => RefreshRecognized();
    private void PlansFilter_Changed(object sender, EventArgs e) => RefreshPlansVisible();

    private void RefreshPlansVisible()
    {
        if (PlansGrid is null) return;
        var filtered = FilterPlans();
        PlansGrid.ItemsSource = null;
        PlansGrid.ItemsSource = filtered;
        PlansCount.Text = $"{filtered.Count} dòng đang lọc / {_plans.Count} trong Tổng hợp";
    }

    private void SelectRecognized_Click(object sender, RoutedEventArgs e)
    {
        foreach (var x in FilterRecognized()) x.IsSelected = true;
        RefreshRecognized();
    }
    private void ClearRecognized_Click(object sender, RoutedEventArgs e)
    {
        foreach (var x in FilterRecognized()) x.IsSelected = false;
        RefreshRecognized();
    }
    private void SelectPlans_Click(object sender, RoutedEventArgs e)
    {
        foreach (var x in FilterPlans()) x.IsSelected = true;
        RefreshPlansVisible();
    }
    private void ClearPlans_Click(object sender, RoutedEventArgs e)
    {
        foreach (var x in FilterPlans()) x.IsSelected = false;
        RefreshPlansVisible();
    }

    private void AddManual_Click(object sender, RoutedEventArgs e)
    {
        _recognized.Add(new ZaloPlanRow
        {
            IsSelected = true,
            Plan = new ZaloPlannedWork { WorkType = ZaloScheduleService.Shipment }
        });
        RefreshRecognized();
        StatusText.Text = "Đã thêm dòng để nhập tay; điền số hợp đồng trước khi lưu vào Tổng hợp.";
    }

    private async void Recognize_Click(object sender, RoutedEventArgs e)
    {
        if (_recognizing) return;
        var dialog = new OpenFileDialog
        {
            Title = "Chọn ảnh lịch Zalo",
            Filter = "Ảnh Zalo (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp"
        };
        if (dialog.ShowDialog() != true) return;
        if (!GeminiKeyStore.HasKey)
        {
            this.NotifyInline("Hãy cấu hình khóa Gemini trong Trợ lý An trước khi nhận diện ảnh.");
            return;
        }
        if (MessageBox.Show(Application.Current.MainWindow, "Bạn đồng ý gửi ảnh Zalo vừa chọn tới Google Gemini " +
            "để nhận diện lịch dự kiến? Ảnh có thể chứa số hợp đồng và thông tin công việc. " +
            "Không có dữ liệu nào tự ghi vào Excel hoặc đánh dấu đã xuất hàng.",
            "Xác nhận gửi ảnh", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _recognizing = true;
        try
        {
            StatusText.Text = "Đang nhận diện; kết quả cần được bạn kiểm tra trước khi nhập.";
            var bytes = await File.ReadAllBytesAsync(dialog.FileName);
            var mime = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => throw new InvalidDataException("Định dạng ảnh không được hỗ trợ.")
            };
            var rows = await ZaloScheduleImageReader.RecognizeAsync(bytes, mime, GeminiKeyStore.Load());
            _recognized.Clear();
            _recognized.AddRange(rows.Select(p => new ZaloPlanRow { IsSelected = true, Plan = p }));
            RefreshRecognized();
            WorkTabs.SelectedIndex = 0;
            StatusText.Text = $"Nhận diện {rows.Count} dòng. Kiểm tra hợp đồng, kiện, SKU, số lượng và ngày dự kiến trước khi lưu.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Chưa thể nhận diện ảnh: " + ex.Message;
        }
        finally { _recognizing = false; }
    }

    private async void ImportSelected_Click(object sender, RoutedEventArgs e)
    {
        if (WorkTabs.SelectedIndex != 0)
        {
            StatusText.Text = "Chuyển sang tab Quét ảnh và rà soát để nhập hợp đồng đã chọn.";
            return;
        }
        RecognizedGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        RecognizedGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var rows = _recognized.Where(x => x.IsSelected).Select(x => x.Plan).ToArray();
        if (PersistPlansAsync is null) { StatusText.Text = "Chưa có chức năng lưu Tổng hợp."; return; }
        var backup = JsonSerializer.Serialize(_state.ZaloPlans);
        try
        {
            var result = ZaloScheduleService.Upsert(_state.ZaloPlans, rows);
            if (!await PersistPlansAsync())
                throw new IOException("Không lưu được kế hoạch vào HISA.");
            foreach (var row in _recognized.Where(x => x.IsSelected)) row.IsSelected = false;
            RefreshRecognized();
            RefreshPlans();
            WorkTabs.SelectedIndex = 1;
            StatusText.Text = $"Đã ghi Tổng hợp: {result.Added} mới, {result.Updated} cập nhật lịch, " +
                $"{result.Unchanged} trùng bỏ qua. Chưa thay đổi tồn kho.";
        }
        catch (Exception ex)
        {
            _state.ZaloPlans = JsonSerializer.Deserialize<ObservableCollection<ZaloPlannedWork>>(backup) ?? [];
            RefreshPlans();
            StatusText.Text = "Không nhập Tổng hợp: " + ex.Message;
        }
    }

    private void CreatePending_Click(object sender, RoutedEventArgs e)
    {
        if (WorkTabs.SelectedIndex != 1)
        {
            StatusText.Text = "Chuyển sang tab Tổng hợp để chọn hợp đồng lên phiếu.";
            return;
        }
        try
        {
            var selection = _plans.Where(x => x.IsSelected).Select(x => x.Plan).ToArray();
            if (selection.Length == 0) throw new ArgumentException("Hãy chọn các hợp đồng muốn lập phiếu chờ.");
            var warehouse = _state.Warehouses.FirstOrDefault() ?? "";
            var pending = ZaloScheduleService.PreparePending(
                selection, _state.Products.ToList(), warehouse, Environment.UserName);
            if (pending.Draft.Type == "Xuất")
            {
                var shortages = pending.Draft.Lines.Where(line =>
                {
                    var product = _state.Products.First(p => p.Sku == line.Sku);
                    return product.WarehouseStocks.GetValueOrDefault(warehouse) < line.Quantity;
                }).Select(line => line.Sku).ToArray();
                if (shortages.Length > 0 && MessageBox.Show(Application.Current.MainWindow,
                    "Có vật tư chưa đủ tồn tại " + warehouse + ": " +
                    string.Join(", ", shortages) +
                    ". Bạn vẫn có thể lưu PHIẾU CHỜ, nhưng không thể xác nhận xuất khi thiếu tồn. Tiếp tục?",
                    "Cảnh báo thiếu tồn kho", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            }
            if (OpenDraftEditor is null)
                throw new InvalidOperationException("Không có màn hình duyệt phiếu.");
            OpenDraftEditor(pending);
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
