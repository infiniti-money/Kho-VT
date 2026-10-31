using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HisaWarehouse.Services;
using Microsoft.Web.WebView2.Core;

namespace HisaWarehouse;

public partial class MainWindow : Window
{
    private sealed record EmbeddedPageEntry(Window Source, FrameworkElement Content, string Title, string Subtitle);

    private InventoryState? _state;
    private DetailedReportsWindow? _detailedReportPage;
    private CoreWebView2Environment? _webViewEnvironment;
    private string _currentPage = "dashboard";
    private string _trendUnit = "*";
    private int _trendPeriodDays = 180;
    private readonly HashSet<string> _labelSelection = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _isReady;
    private readonly Stack<EmbeddedPageEntry> _embeddedPageStack = new();
    private EmbeddedPageEntry? _currentEmbeddedPage;
    private string _embeddedReturnPage = "dashboard";
    private Action<bool>? _confirmationCompletion;

    public ObservableCollection<InventoryRow> InventoryRows { get; } = [];
    public ObservableCollection<InventoryRow> LabelRows { get; } = [];
    public ObservableCollection<AuditRow> AuditRows { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        UpdateStatusText.Text = $"Phiên bản {WarehouseUpdateService.CurrentVersion.ToString(3)} · Chưa kiểm tra bản phát hành mới.";
        SystemInfoText.Text = $"Phiên bản {WarehouseUpdateService.CurrentVersion.ToString(3)} · Windows x64 · HISA Kho-VT";
        _toastTimer.Tick += (_, _) => { ToastBorder.Visibility = Visibility.Collapsed; _toastTimer.Stop(); };
        EmbeddedWindowNavigation.NotificationRequested += ShowToast;
        Closed += (_, _) => EmbeddedWindowNavigation.NotificationRequested -= ShowToast;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var webData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HISA", "QuanLyKho", "WebView2");
            Directory.CreateDirectory(webData);
            _webViewEnvironment = await CoreWebView2Environment.CreateAsync(null, webData);
            await StorageWebView.EnsureCoreWebView2Async(_webViewEnvironment);
            await ChartsWebView.EnsureCoreWebView2Async(_webViewEnvironment);
            ConfigureWebView(StorageWebView.CoreWebView2);
            ConfigureWebView(ChartsWebView.CoreWebView2);
            await NavigateAndWaitAsync(StorageWebView, "https://hisa.local/Web/storage.html");
            await NavigateAndWaitAsync(ChartsWebView, "https://hisa.local/Web/charts.html");
            ChartsWebView.CoreWebView2.WebMessageReceived += ChartsWebMessageReceived;

            _state = await InventoryStore.LoadOrSeedAsync(StorageWebView.CoreWebView2);
            InitializeFilters();
            RenderAll();
            _isReady = true;
            ShowToast("Đã nạp dữ liệu kho từ KHO.xlsx");
        }
        catch (Exception ex)
        {
            BreadcrumbTitle.Text = "Không thể khởi tạo ứng dụng";
            BreadcrumbSubtitle.Text = "Kiểm tra Microsoft Edge WebView2 Runtime và thư mục dữ liệu ứng dụng.";
            EmbeddedPageHost.Content = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(227, 233, 241)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(24),
                Margin = new Thickness(28, 22, 28, 22),
                Child = new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.IndianRed, FontSize = 13 }
            };
            EmbeddedPageHost.Visibility = Visibility.Visible;
        }
    }

    private void ConfigureWebView(CoreWebView2 core)
    {
        core.SetVirtualHostNameToFolderMapping("hisa.local", AppContext.BaseDirectory, CoreWebView2HostResourceAccessKind.Allow);
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
    }

    private static async Task NavigateAndWaitAsync(Microsoft.Web.WebView2.Wpf.WebView2 webView, string address)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<CoreWebView2NavigationCompletedEventArgs>? handler = null;
        handler = (_, args) =>
        {
            webView.NavigationCompleted -= handler;
            if (args.IsSuccess) completion.TrySetResult();
            else completion.TrySetException(new InvalidOperationException($"Không tải được nội dung biểu đồ: {args.WebErrorStatus}."));
        };
        webView.NavigationCompleted += handler;
        webView.CoreWebView2.Navigate(address);
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    private InventoryState State => _state ?? throw new InvalidOperationException("Dữ liệu kho chưa được nạp.");
    private List<string> SelectedWarehouses => State.SelectedWarehouses.Where(State.Warehouses.Contains).Distinct().ToList();

    private void InitializeFilters()
    {
        CategoryFilter.ItemsSource = new[] { "Tất cả nhóm" }.Concat(State.Categories.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)).ToList();
        CategoryFilter.SelectedIndex = 0;
        LabelCategoryFilter.ItemsSource = new[] { "Tất cả nhóm" }.Concat(State.Categories.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)).ToList();
        LabelCategoryFilter.SelectedIndex = 0;
        RenderWarehouseChecks();
        SettingsWarehouses.ItemsSource = State.Warehouses;
        ReferencePathBox.Text = State.ReferencePath;
        var pendingCount = State.PendingMovements.Count(movement => movement.Status == "Chờ xác nhận");
        NavPendingText.Text = pendingCount == 0 ? "Phiếu chờ xác nhận" : $"Phiếu chờ ({pendingCount:N0})";
        UpdateWarehouseButton();
    }

    private void RenderWarehouseChecks()
    {
        var panel = new StackPanel();
        foreach (var warehouse in State.Warehouses)
        {
            var check = new CheckBox
            {
                Content = warehouse,
                Tag = warehouse,
                IsChecked = State.SelectedWarehouses.Contains(warehouse),
                Margin = new Thickness(6, 5, 3, 5),
                Foreground = new SolidColorBrush(Color.FromRgb(39, 59, 83)),
                FontSize = 12
            };
            check.Checked += WarehouseCheck_Changed;
            check.Unchecked += WarehouseCheck_Changed;
            panel.Children.Add(check);
        }
        WarehouseChecks.ItemsSource = new[] { panel };
    }

    private async void WarehouseCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isReady || sender is not CheckBox check || check.Tag is not string warehouse) return;
        if (check.IsChecked == true)
        {
            if (!State.SelectedWarehouses.Contains(warehouse)) State.SelectedWarehouses.Add(warehouse);
        }
        else State.SelectedWarehouses.Remove(warehouse);
        UpdateWarehouseButton();
        RenderAll();
        await PersistAsync(false);
    }

    private void UpdateWarehouseButton()
    {
        var count = State.SelectedWarehouses.Count;
        WarehouseFilterLabel.Text = count == State.Warehouses.Count ? $"{count} kho đang chọn" : $"{count} kho đang chọn";
    }

    private void WarehouseFilter_Click(object sender, RoutedEventArgs e) => WarehousePopup.IsOpen = !WarehousePopup.IsOpen;

    private async void SelectAllWarehouses_Click(object sender, RoutedEventArgs e)
    {
        State.SelectedWarehouses = [.. State.Warehouses];
        RenderWarehouseChecks(); UpdateWarehouseButton(); RenderAll(); await PersistAsync(false);
    }

    private async void ClearWarehouses_Click(object sender, RoutedEventArgs e)
    {
        State.SelectedWarehouses.Clear();
        RenderWarehouseChecks(); UpdateWarehouseButton(); RenderAll(); await PersistAsync(false);
    }

    private void RenderAll()
    {
        if (_state is null) return;
        RefreshDashboard();
        RefreshInventory();
        RefreshAudit();
        RefreshLabels();
        RefreshSettings();
    }

    private void RefreshDashboard()
    {
        var products = State.Products.Where(p => p.Active).ToList();
        var selected = SelectedWarehouses;
        var rows = selected.Count == 0 ? new List<InventoryRow>() : products.Select(p => new InventoryRow { Item = p, Stock = p.StockAt(selected) }).ToList();
        var low = rows.Where(r => r.Stock > 0 && r.Item.MinStockDefined && r.MinStock > 0 && r.Stock < r.MinStock).OrderBy(r => r.Stock / r.MinStock).ToList();
        var outRows = rows.Where(r => r.Stock <= 0).ToList();
        var negative = outRows.Count(r => r.Stock < 0);
        KpiProductCount.Text = rows.Count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        KpiLowStock.Text = low.Count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        KpiOutOfStock.Text = outRows.Count.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        KpiOutOfStockCaption.Text = $"{outRows.Count - negative} hết hàng · {negative} âm kho";

        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayTransactions = GetWarehouseTransactions(State.Transactions).Where(t => t.Date == today).ToList();
        var receipts = todayTransactions.Where(t => t.Type == "Nhập").ToList();
        var shipments = todayTransactions.Where(t => t.Type == "Xuất").ToList();
        KpiToday.Text = $"{receipts.Select(t => t.DocNo).Distinct().Count()} / {shipments.Select(t => t.DocNo).Distinct().Count()}";
        KpiTodayCaption.Text = $"Phiếu nhập / phiếu xuất · {receipts.Count + shipments.Count} dòng hàng";

        var sourceDate = DateOnly.TryParse(State.SourceLastModified, out var parsed) ? parsed.ToString("dd/MM/yyyy") : State.SourceLastModified;
        DashboardDateText.Text = $"Nguồn KHO.xlsx cập nhật {sourceDate} · {products.Count} mã hàng đang theo dõi";
        LowStockGrid.ItemsSource = low.Concat(outRows).Take(5).ToList();
        DataQualitySummary.Text = $"{State.SourceAudit.CatalogCount} mã hàng · {State.SourceAudit.TypedTransactionCount} dòng nhập/xuất · {State.SourceAudit.HistoryRowsWithMissingQuantity + State.SourceAudit.HistoryRowsWithMissingType} dòng lịch sử cần rà soát";
        var mismatch = State.SourceAudit.SnapshotLedgerMismatches.FirstOrDefault();
        var mismatchText = mismatch is null ? "" : $" Số dư {mismatch.Sku} trong danh mục là {InventoryRow.FormatQuantity(mismatch.Snapshot)}, trong khi sổ giao dịch cộng ra {InventoryRow.FormatQuantity(mismatch.LedgerCalculated)} ({InventoryRow.FormatQuantity(mismatch.Difference)} {products.FirstOrDefault(p => p.Sku == mismatch.Sku)?.Unit}).";
        DataQualityDetail.Text = $"Workbook không tách tồn theo kho con; số dư ban đầu được gán vào Kho tổng, 7 kho con bắt đầu ở 0. App theo dõi số lượng, không tính giá trị tiền.{mismatchText}";
        _ = UpdateChartsAsync();
    }

    private async Task UpdateChartsAsync()
    {
        if (_state is null || ChartsWebView.CoreWebView2 is null) return;
        var selected = SelectedWarehouses;
        var products = State.Products.Where(p => p.Active).ToList();
        var categoryGroups = products.Where(p => p.StockAt(selected) > 0).GroupBy(p => p.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ThenBy(g => g.Category, StringComparer.CurrentCultureIgnoreCase).ToList();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var earliest = _trendPeriodDays == 0 ? DateOnly.MinValue : today.AddDays(-_trendPeriodDays);
        var trendTransactions = GetWarehouseTransactions(State.Transactions)
            .Where(t => t.Date.HasValue && t.Date.Value <= today && t.Date.Value >= earliest && (t.Type is "Nhập" or "Xuất" or "Điều chuyển" or "Điều chỉnh"))
            .Where(t => _trendUnit == "*" || t.Unit.Equals(_trendUnit, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var groups = trendTransactions.GroupBy(t => new DateOnly(t.Date!.Value.Year, t.Date.Value.Month, 1)).OrderBy(g => g.Key).ToList();
        double Measure(IEnumerable<InventoryTransaction> items) => _trendUnit == "*" ? items.Count() : items.Sum(t => t.Quantity ?? 0);
        var payload = new ChartPayload
        {
            CategoryLabels = categoryGroups.Select(g => g.Category).ToList(),
            CategoryCounts = categoryGroups.Select(g => g.Count).ToList(),
            TrendLabels = groups.Select(g => g.Key.ToString("MMM yy", CultureInfo.GetCultureInfo("vi-VN"))).ToList(),
            Receipts = groups.Select(g => Measure(g.Where(t => t.Type == "Nhập"))).ToList(),
            Shipments = groups.Select(g => Measure(g.Where(t => t.Type == "Xuất"))).ToList(),
            Transfers = groups.Select(g => Measure(g.Where(t => t.Type == "Điều chuyển"))).ToList(),
            Adjustments = groups.Select(g => Measure(g.Where(t => t.Type == "Điều chỉnh"))).ToList(),
            TrendUnit = _trendUnit == "*" ? "Số dòng giao dịch theo tháng" : $"Số lượng { _trendUnit } theo tháng",
            AvailableUnits = new List<TrendUnitOption> { new() { Value = "*", Label = "Số dòng phiếu" } }
                .Concat(State.Units.OrderBy(u => u, StringComparer.CurrentCultureIgnoreCase).Select(u => new TrendUnitOption { Value = u, Label = u }))
                .ToList(),
            SelectedTrendUnit = _trendUnit,
            PeriodDays = _trendPeriodDays
        };
        try
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await ChartsWebView.CoreWebView2.ExecuteScriptAsync($"window.updateCharts({json});");
        }
        catch { /* The chart is refreshed again when the dashboard page becomes active. */ }
    }

    private void ChartsWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type == "category-filter")
            {
                var category = root.GetProperty("category").GetString();
                NavigateTo("inventory");
                CategoryFilter.SelectedItem = category;
            }
            else if (type == "trend-unit")
            {
                _trendUnit = root.GetProperty("value").GetString() ?? "*";
                _ = UpdateChartsAsync();
            }
            else if (type == "trend-period")
            {
                _trendPeriodDays = root.GetProperty("value").GetInt32();
                _ = UpdateChartsAsync();
            }
        }
        catch { ShowToast("Không đọc được lựa chọn biểu đồ."); }
    }

    private IEnumerable<InventoryTransaction> GetWarehouseTransactions(IEnumerable<InventoryTransaction> transactions)
    {
        var selected = SelectedWarehouses;
        return transactions.Where(t => t.Type == "Điều chuyển"
            ? (t.FromWarehouse is not null && selected.Contains(t.FromWarehouse)) || (t.ToWarehouse is not null && selected.Contains(t.ToWarehouse))
            : selected.Contains(t.Warehouse));
    }

    private void RefreshInventory()
    {
        if (_state is null) return;
        var query = InventorySearch.Text.Trim();
        var category = CategoryFilter.SelectedItem?.ToString() ?? "Tất cả nhóm";
        var status = (StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả trạng thái";
        var selectedWarehouses = SelectedWarehouses;
        var rows = State.Products.Where(p => p.Active)
            .Where(_ => selectedWarehouses.Count > 0)
            .Select(p => new InventoryRow { Item = p, Stock = p.StockAt(selectedWarehouses) })
            .Where(r => string.IsNullOrWhiteSpace(query) || r.Sku.Contains(query, StringComparison.CurrentCultureIgnoreCase) || r.Item.Barcode.Contains(query, StringComparison.CurrentCultureIgnoreCase) || r.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || r.Department.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Where(r => category == "Tất cả nhóm" || r.Category == category)
            .Where(r => status == "Tất cả trạng thái" || r.Status == status)
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        InventoryRows.Clear();
        foreach (var row in rows) InventoryRows.Add(row);
        InventoryCountText.Text = $"{rows.Count:N0} mã hàng hiển thị · {State.Products.Count(p => p.Active):N0} mã đang hoạt động";
    }

    private void RefreshAudit()
    {
        if (_state is null) return;
        var search = AuditSearch.Text.Trim();
        var type = (AuditTypeFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tất cả giao dịch";
        DateOnly? date = AuditDateFilter.SelectedDate is DateTime d ? DateOnly.FromDateTime(d) : null;
        var rows = GetWarehouseTransactions(State.Transactions)
            .Where(t => type == "Tất cả giao dịch" || t.Type == type)
            .Where(t => date is null || t.Date == date)
            .Where(t => string.IsNullOrWhiteSpace(search) || new[] { t.DocNo, t.ReferenceNo, t.Sku, t.ItemName, t.Partner, t.Operator, t.Note }.Any(v => v.Contains(search, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(t => t.Date ?? DateOnly.MinValue).ThenByDescending(t => t.SourceRow ?? 0)
            .Select(t => new AuditRow { Transaction = t }).ToList();
        AuditRows.Clear();
        foreach (var row in rows) AuditRows.Add(row);
        AuditCountText.Text = $"{rows.Count:N0} dòng hiển thị · {State.Transactions.Count(t => t.Legacy):N0} dòng lịch sử từ Excel";
    }

    private void RefreshLabels()
    {
        if (_state is null) return;
        var search = LabelSearch.Text.Trim();
        var category = LabelCategoryFilter.SelectedItem?.ToString() ?? "Tất cả nhóm";
        var selectedWarehouses = SelectedWarehouses;
        var rows = State.Products.Where(p => p.Active)
            .Where(_ => selectedWarehouses.Count > 0)
            .Select(p => new InventoryRow { Item = p, Stock = p.StockAt(selectedWarehouses), SelectedForLabel = _labelSelection.Contains(p.Sku) })
            .Where(r => string.IsNullOrWhiteSpace(search) || r.Sku.Contains(search, StringComparison.CurrentCultureIgnoreCase) || r.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .Where(r => category == "Tất cả nhóm" || r.Category == category)
            .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        LabelRows.Clear();
        foreach (var row in rows) LabelRows.Add(row);
        UpdateLabelPreview();
    }

    private void UpdateLabelPreview()
    {
        if (!IsInitialized || _state is null) return;
        LabelPreviewPanel.Children.Clear();
        var selected = _state.Products.Where(p => _labelSelection.Contains(p.Sku)).ToList();
        LabelSelectionText.Text = selected.Count == 0 ? "Chưa chọn mã hàng" : $"{selected.Count} tem sẽ được in";
        foreach (var item in selected.Take(24))
        {
            var card = new Border { Width = 196, Height = 114, Margin = new Thickness(0, 0, 9, 9), Padding = new Thickness(8), Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(220, 228, 238)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7) };
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = item.Name, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(77, 95, 118)), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(1, 0, 1, 3) });
            panel.Children.Add(new Image { Source = BarcodeService.CreateCode39Image(item.Barcode), Height = 61, Stretch = Stretch.Uniform });
            panel.Children.Add(new TextBlock { Text = $"{item.Sku}  ·  {item.Unit}", FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(23, 43, 69)) });
            card.Child = panel;
            LabelPreviewPanel.Children.Add(card);
        }
        if (selected.Count > 24) LabelSelectionText.Text += $" · xem trước 24/{selected.Count}";
    }

    private void RefreshSettings()
    {
        if (_state is null) return;
        var pendingCount = State.PendingMovements.Count(movement => movement.Status == "Chờ xác nhận");
        NavPendingText.Text = pendingCount == 0 ? "Phiếu chờ xác nhận" : $"Phiếu chờ ({pendingCount:N0})";
        if (PrimaryPathBox is not null && !PrimaryPathBox.IsKeyboardFocusWithin) PrimaryPathBox.Text = State.PrimaryExcelPath;
        if (ReferencePathBox is not null && !ReferencePathBox.IsKeyboardFocusWithin) ReferencePathBox.Text = State.ReferencePath;
        SettingsSourceText.Text = $"Tệp nguồn: {State.SourceFile} · ngày cập nhật: {State.SourceLastModified}\n{State.SourceAudit.CatalogCount} mã hàng, {State.SourceAudit.TypedTransactionCount} dòng nhập/xuất có loại giao dịch. Số dư đầu kỳ nhập vào Kho tổng.";
        var mismatch = State.SourceAudit.SnapshotLedgerMismatches.FirstOrDefault();
        var mismatchText = mismatch is null ? "Không ghi nhận chênh lệch giữa snapshot và sổ." : $"Chênh lệch cần rà soát: {mismatch.Sku} · danh mục {InventoryRow.FormatQuantity(mismatch.Snapshot)} · tính theo sổ {InventoryRow.FormatQuantity(mismatch.LedgerCalculated)} · lệch {InventoryRow.FormatQuantity(mismatch.Difference)} đơn vị.";
        SettingsAuditText.Text = $"{mismatchText}\nLịch sử có {State.SourceAudit.HistoryRowsWithMissingQuantity} dòng thiếu số lượng và {State.SourceAudit.HistoryRowsWithMissingType} dòng thiếu loại giao dịch. Dashboard Excel có {State.SourceAudit.DashboardFormulaErrors} ô #REF! và workbook chứa {State.SourceAudit.ExternalWorkbookLinks} liên kết ngoài. Các giao dịch lịch sử không tự cộng lại vào snapshot hiện tại.";
    }

    private async Task<bool> PersistAsync(bool notify = true)
    {
        if (_state is null || StorageWebView.CoreWebView2 is null) return false;
        try
        {
            await InventoryStore.SaveAsync(StorageWebView.CoreWebView2, State);
            RefreshDashboard(); RefreshInventory(); RefreshAudit(); RefreshLabels(); RefreshSettings();
            if (notify) ShowToast("Đã lưu thay đổi vào thiết bị này.");
            return true;
        }
        catch (Exception ex) { ShowToast("Không thể lưu: " + ex.Message); return false; }
    }

    private bool CanOpenAnotherPage()
    {
        // Background operations must not silently replace an active editor.
        return _currentEmbeddedPage is null;
    }

    private void RequestRootNavigation(Action open)
    {
        if (_currentEmbeddedPage is null)
        {
            open();
            return;
        }

        // Read-only pages are replaced immediately. Editors require a single
        // confirmation so users cannot accidentally lose uncommitted input.
        var editor = _currentEmbeddedPage.Source;
        var mayHaveChanges = editor is MovementWindow or ItemEditorWindow
            or MaterialRequestEditorWindow or PrintTemplateWindow or CatalogsWindow
            or ManageUnitsWindow or ReplenishmentWindow;
        void Switch()
        {
            // Unwind the explicit request-editor child as well as its parent.
            // Completions receive false, so no unapplied edits get saved.
            while (_currentEmbeddedPage is not null)
                CompleteEmbeddedPage(_currentEmbeddedPage.Source, false, null);
            open();
        }
        if (mayHaveChanges)
            ConfirmInApp("Trang hiện tại có thể có dữ liệu chưa lưu. Chuyển trang và bỏ các thay đổi chưa lưu?",
                accepted => { if (accepted) Switch(); }, "Chuyển trang", "Ở lại");
        else Switch();
    }

    private void HideStandardPages()
    {
        DashboardPage.Visibility = Visibility.Collapsed;
        InventoryWorkspaceTabs.Visibility = Visibility.Collapsed;
        AuditReportTabs.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
    }

    private void NavigateTo(string page)
    {
        if (!CanOpenAnotherPage()) return;
        _currentPage = page;
        DashboardPage.Visibility = page == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        InventoryWorkspaceTabs.Visibility = page is "inventory" or "labels" ? Visibility.Visible : Visibility.Collapsed;
        if (page == "inventory") InventoryWorkspaceTabs.SelectedIndex = 0;
        if (page == "labels") InventoryWorkspaceTabs.SelectedIndex = 1;
        AuditReportTabs.Visibility = page is "reports" or "audit" ? Visibility.Visible : Visibility.Collapsed;
        if (page is "reports" or "audit")
        {
            EnsureDetailedReportTab();
            if (page == "audit") AuditReportTabs.SelectedIndex = 0;
        }
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        var pageDetails = page switch
        {
            "inventory" => ("Danh mục tồn kho", "Tìm kiếm, phân loại, cập nhật số dư và quản lý SKU"),
            "audit" => ("Nhật ký giao dịch", "Tra cứu chứng từ, luồng hàng và người thực hiện"),
            "reports" => ("Báo cáo & nhật ký", "Tra cứu lịch sử, lọc nâng cao và xuất báo cáo Excel"),
            "labels" => ("Tem & mã vạch", "Tạo nhãn SKU và in theo lô"),
            "settings" => ("Sao lưu & dữ liệu", "Thông tin nguồn và phục hồi dữ liệu ứng dụng"),
            "movement" => ("Nhập & Xuất kho", "Ghi nhận giao nhận và điều chuyển vật tư"),
            _ => ("Tổng quan kho", "Theo dõi tồn kho và luân chuyển vật tư")
        };
        BreadcrumbTitle.Text = pageDetails.Item1;
        BreadcrumbSubtitle.Text = pageDetails.Item2;
        SetNavState();
        if (page == "dashboard") _ = UpdateChartsAsync();
    }

    private void EnsureDetailedReportTab()
    {
        if (_state is null || _detailedReportPage is not null) return;
        _detailedReportPage = new DetailedReportsWindow(State) { Owner = this };
        var view = _detailedReportPage.Content;
        _detailedReportPage.Content = null;
        _detailedReportPage.Tag = new Action<bool>(_ => AuditReportTabs.SelectedIndex = 0);
        ReportDetailHost.Content = view;
    }

    private void ShowEmbeddedPage(Window window, string title, string subtitle,
        Action<bool>? completion = null, bool isChildOfCurrentPage = false)
    {
        // Main navigation must never push a second unrelated page over the current one.
        // Only explicit child actions inside the currently visible form may temporarily
        // replace its content. Closing that child restores the original form.
        if (isChildOfCurrentPage)
        {
            if (!WorkspaceNavigationPolicy.CanOpenRequestEditor(
                    _currentEmbeddedPage?.Source is MaterialRequestsWindow, _embeddedPageStack.Count))
                throw new InvalidOperationException("Chỉ có thể mở phiếu con từ trang Yêu cầu vật tư đang hiển thị.");
        }
        else if (!CanOpenAnotherPage()) return;

        if (window.Content is not FrameworkElement content)
            throw new InvalidOperationException($"Trang {window.Title} chưa có nội dung để hiển thị.");
        if (_currentEmbeddedPage is null) _embeddedReturnPage = _currentPage;
        else
        {
            EmbeddedPageHost.Content = null;
            _embeddedPageStack.Push(_currentEmbeddedPage);
        }
        window.Owner = this;
        window.Content = null;
        window.Tag = new Action<bool>(accepted => CompleteEmbeddedPage(window, accepted, completion));
        _currentEmbeddedPage = new EmbeddedPageEntry(window, content, title, subtitle);
        HideStandardPages(); // No background page remains visible under the editor.
        EmbeddedPageHost.Content = content;
        EmbeddedPageHost.Visibility = Visibility.Visible;
        UpdateEmbeddedBreadcrumb(title, subtitle);
    }

    private void ShowRequestEditorChild(Window child, Action<bool> completion)
    {
        ShowEmbeddedPage(child, "Lập phiếu yêu cầu",
            "Nhập thông tin vật tư cần cấp hoặc cần nhập", completion,
            isChildOfCurrentPage: true);
    }

    private void CompleteEmbeddedPage(Window window, bool accepted, Action<bool>? completion)
    {
        if (_currentEmbeddedPage?.Source != window) return;
        EmbeddedPageHost.Content = null;
        if (_embeddedPageStack.Count > 0)
        {
            _currentEmbeddedPage = _embeddedPageStack.Pop();
            EmbeddedPageHost.Content = _currentEmbeddedPage.Content;
            UpdateEmbeddedBreadcrumb(_currentEmbeddedPage.Title, _currentEmbeddedPage.Subtitle);
        }
        else
        {
            _currentEmbeddedPage = null;
            EmbeddedPageHost.Visibility = Visibility.Collapsed;
            NavigateTo(_embeddedReturnPage);
        }
        completion?.Invoke(accepted);
    }

    private void UpdateEmbeddedBreadcrumb(string title, string subtitle)
    {
        BreadcrumbTitle.Text = title;
        BreadcrumbSubtitle.Text = subtitle;
        SetNavState();
    }

    private void NavGroup_Expanded(object sender, RoutedEventArgs e)
    {
        // Behave like an accordion. Expanding a different group only changes
        // the menu, not the active work page or any unsaved form.
        if (!IsLoaded || sender is not Expander opened) return;
        foreach (var group in new[] { NavGroupStock, NavGroupDocuments, NavGroupProposals, NavGroupReports, NavGroupSystem })
            if (!ReferenceEquals(group, opened)) group.IsExpanded = false;
    }

    private void SetNavState()
    {
        var activeName = _currentEmbeddedPage?.Source switch
        {
            QuickSearchWindow => "NavQuickSearch",
            NegativeStockWindow => "NavNegativeStock",
            MovementWindow => "NavMovement",
            PendingMovementsWindow => "NavPending",
            MaterialRequestsWindow or MaterialRequestEditorWindow => "NavRequests",
            ReplenishmentWindow => "NavReplenishment",
            PrintTemplateWindow => "NavPrintTemplates",
            AiAssistantWindow => "NavAi",
            DetailedReportsWindow => "NavReports",
            CatalogsWindow or ManageUnitsWindow => "NavCatalogs",
            SoftwareInfoWindow => "NavSoftwareInfo",
            ItemEditorWindow => "NavInventory",
            ReferenceInventoryWindow => "NavSettings",
            _ => _currentPage switch
            {
                "dashboard" => "NavDashboard",
                "inventory" => "NavInventory",
                "audit" or "reports" => "NavReports",
                "labels" => "NavInventory",
                "settings" => "NavSettings",
                _ => "NavDashboard"
            }
        };

        foreach (var button in new[] { NavDashboard, NavInventory, NavMovement, NavAudit,
                     NavLabels, NavSettings, NavQuickSearch, NavNegativeStock, NavReports,
                     NavCatalogs, NavSoftwareInfo, NavPending, NavRequests, NavReplenishment,
                     NavAi, NavPrintTemplates })
        {
            var active = button.Name == activeName;
            button.Background = active ? new SolidColorBrush(Color.FromRgb(21, 127, 134)) : Brushes.Transparent;
            button.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(193, 204, 218));
        }

        // Keep the active feature discoverable inside its module.
        var targetGroup = SidebarModuleGroups.ModuleFor(activeName);
        if (targetGroup is not null)
        {
            var section = targetGroup switch
            {
                SidebarModuleGroups.Stock => NavGroupStock,
                SidebarModuleGroups.Documents => NavGroupDocuments,
                SidebarModuleGroups.Proposals => NavGroupProposals,
                SidebarModuleGroups.Reports => NavGroupReports,
                SidebarModuleGroups.System => NavGroupSystem,
                _ => null
            };
            if (section is not null && !section.IsExpanded) section.IsExpanded = true;
        }
    }

    private void NavDashboard_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => NavigateTo("dashboard"));
    private void NavInventory_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => NavigateTo("inventory"));
    private void NavAudit_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => NavigateTo("reports"));
    private void NavLabels_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => NavigateTo("labels"));
    private void NavSettings_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => NavigateTo("settings"));
    private void NavMovement_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => _ = OpenMovementAsync());
    private void QuickMovement_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => _ = OpenMovementAsync());
    private void NavPending_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => _ = OpenPendingMovementsAsync());
    private void NavRequests_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => _ = OpenMaterialRequestsAsync());
    private void NavReplenishment_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(OpenReplenishment);
    private void NavAi_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(OpenAiAssistant);

    private void OpenAiAssistant()
    {
        if (_state is null || !CanOpenAnotherPage()) return;
        var assistant = new AiAssistantWindow(State)
        {
            OpenReplenishment = () => OpenReplenishment()
        };
        ShowEmbeddedPage(assistant, "Trợ lý AI Gemini",
            "Trò chuyện, báo cáo tồn kho theo tuần và tháng, mic và phản hồi bằng âm thanh");
    }
    private void NavPrintTemplates_Click(object sender, RoutedEventArgs e) => RequestRootNavigation(() => _ = OpenPrintTemplatesAsync());

    private void NavQuickSearch_Click(object sender, RoutedEventArgs e) =>
        RequestRootNavigation(OpenQuickSearch);

    private void OpenQuickSearch()
    {
        if (!CanOpenAnotherPage()) return;
        var page = new QuickSearchWindow(State);
        ShowEmbeddedPage(page, "Tìm kiếm nhanh", "Tìm mã, barcode, tên vật tư và lập phiếu từ kết quả", accepted =>
        {
            if (!accepted || page.SelectedItem is null) return;
            if (page.Action == "edit") EditItem(page.SelectedItem);
            else _ = OpenMovementAsync(page.SelectedItem.Sku, page.Action);
        });
    }

    private void NavNegativeStock_Click(object sender, RoutedEventArgs e) =>
        RequestRootNavigation(OpenNegativeStock);

    private void OpenNegativeStock()
    {
        if (!CanOpenAnotherPage()) return;
        var page = new NegativeStockWindow(State);
        ShowEmbeddedPage(page, "Cảnh báo tồn âm", "Kiểm tra số âm theo từng mã và từng kho", accepted =>
        {
            if (accepted && page.SelectedItem is not null) _ = OpenMovementAsync(page.SelectedItem.Sku, "Nhập", page.SelectedWarehouse);
        });
    }

    private void NavCatalogs_Click(object sender, RoutedEventArgs e) =>
        RequestRootNavigation(() => _ = OpenCatalogsAsync());
    private void NavReports_Click(object sender, RoutedEventArgs e) =>
        RequestRootNavigation(() => NavigateTo("reports"));
    private void NavSoftwareInfo_Click(object sender, RoutedEventArgs e) =>
        RequestRootNavigation(() => NavigateTo("settings"));

    private void OpenReplenishment()
    {
        if (!CanOpenAnotherPage()) return;
        if (_state is null) return;
        var page = new ReplenishmentWindow(State);
        ShowEmbeddedPage(page, "Đề xuất nhập kho", "Rà soát mức tồn tối thiểu và tạo yêu cầu bổ sung có kiểm soát", accepted =>
        {
            if (!accepted || page.ProposedRequest is not MaterialRequest proposal) return;
            MaterialRequestService.Add(State, proposal);
            _ = PersistAsync();
        });
    }

    private async Task OpenMaterialRequestsAsync()
    {
        if (!CanOpenAnotherPage()) return;
        var page = new MaterialRequestsWindow(State)
        {
            OpenChildPage = ShowRequestEditorChild
        };
        ShowEmbeddedPage(page, "Yêu cầu vật tư", "Tạo và quản lý phiếu yêu cầu cấp hoặc nhập vật tư", accepted =>
        {
            if (page.ChangesMade) _ = PersistAsync();
            if (accepted && page.Action == "print" && page.SelectedRequest is MaterialRequest request) PrintRequest(request);
        });
        await Task.CompletedTask;
    }

    private async Task OpenPrintTemplatesAsync()
    {
        if (!CanOpenAnotherPage()) return;
        PrintTemplateService.EnsureDefaults(State);
        var page = new PrintTemplateWindow(State.PrintTemplates);
        ShowEmbeddedPage(page, "Mẫu in phiếu", "Chỉnh sửa tiêu đề, nội dung và chữ ký trên phiếu", accepted =>
        {
            if (!accepted) return;
            State.PrintTemplates = page.Templates;
            _ = PersistAsync();
        });
        await Task.CompletedTask;
    }

    private async void BrowsePrimaryFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Chọn KHO.xlsx làm file chính", Filter = "Tệp Excel (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        PrimaryPathBox.Text = dialog.FileName;
        State.PrimaryExcelPath = dialog.FileName;
        await PersistAsync(false);
        CheckPrimaryFile_Click(sender, e);
    }

    private async void CheckPrimaryFile_Click(object sender, RoutedEventArgs e)
    {
        var path = PrimaryPathBox.Text.Trim();
        var writeStatus = WorkbookWriteSafety.Inspect(path);
        PrimaryWriteStatusText.Text = writeStatus.Message;
        try
        {
            var audit = PrimaryWorkbookInspector.Inspect(path);
            var stocks = PrimaryWorkbookReader.ReadStocks(path);
            // Individual history rows can be incomplete in the original file.
            // Show the audit instead of aborting on the first incomplete quantity.
            PrimaryWriteStatusText.Text = writeStatus.Message + "\n" + audit.Summary;
            if (audit.MissingProtectedFormulas > 0)
            {
                ShowToast($"Phát hiện {audit.MissingProtectedFormulas:N0} ô thiếu công thức bảo vệ. Không cho phép chuẩn bị ghi vào file.");
                return;
            }
            State.PrimaryExcelPath = path;
            if (!await PersistAsync(false)) return;
            ShowToast($"Đã kiểm tra {stocks.Count:N0} mã, {audit.TypedTransactions:N0} dòng Nhập/Xuất. {audit.IncompleteTransactions:N0} dòng thiếu loại, {audit.MissingTransactionQuantities:N0} dòng thiếu số lượng. KHÔNG ghi vào Excel.");
        }
        catch (Exception ex)
        {
            ShowToast("Không thể đọc file kho chính: " + ex.Message);
        }
    }

    private async void BrowseReferenceFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Chọn thư mục chứa tệp Excel tham chiếu", Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        ReferencePathBox.Text = dialog.FolderName;
        await SaveReferencePathAsync();
    }

    private async void BrowseReferenceFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Chọn tệp Excel tham chiếu tồn", Filter = "Tệp Excel (*.xlsx)|*.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        ReferencePathBox.Text = dialog.FileName;
        await SaveReferencePathAsync();
    }

    private async Task SaveReferencePathAsync()
    {
        State.ReferencePath = ReferencePathBox.Text.Trim();
        await PersistAsync();
    }

    private async void CheckReference_Click(object sender, RoutedEventArgs e)
    {
        if (!CanOpenAnotherPage()) return;
        State.ReferencePath = ReferencePathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(State.ReferencePath))
        {
            ShowToast("Chọn thư mục chứa Excel hoặc chọn trực tiếp một tệp tham chiếu .xlsx.");
            return;
        }
        if (string.IsNullOrWhiteSpace(PrimaryPathBox.Text))
        {
            ShowToast("Chọn file KHO.xlsx chính trước khi đối chiếu.");
            return;
        }
        State.PrimaryExcelPath = PrimaryPathBox.Text.Trim();
        if (Path.GetFullPath(State.PrimaryExcelPath).Equals(Path.GetFullPath(State.ReferencePath), StringComparison.OrdinalIgnoreCase))
        {
            ShowToast("File đối chiếu phải khác file KHO.xlsx chính.");
            return;
        }
        await PersistAsync(false);
        try
        {
            var page = new ReferenceInventoryWindow(State, State.ReferencePath, State.PrimaryExcelPath);
            ShowEmbeddedPage(page, "Đối chiếu hai file Excel", "Lấy tồn từ KHO.xlsx, so mã và số lượng với file đối chiếu; không tự ghi vào file nào");
        }
        catch (Exception ex) { ShowToast("Không đối chiếu được tồn: " + ex.Message); }
    }

    private async Task ApplyReferenceChangesAsync(IReadOnlyList<ReferenceComparisonRow> rows, string warehouse)
    {
        if (!State.Warehouses.Contains(warehouse, StringComparer.Ordinal))
        {
            ShowToast("Kho điều chỉnh không còn trong danh mục.");
            return;
        }

        var updates = rows.Where(row => row.Item is not null && row.ProposedStock.HasValue && double.IsFinite(row.ProposedStock.Value)).ToList();
        if (updates.Count == 0)
        {
            ShowToast("Chọn ít nhất một mã hàng thuộc danh mục và nhập tồn đề xuất.");
            return;
        }

        var changed = 0;
        foreach (var row in updates)
        {
            var item = row.Item!;
            var before = new Dictionary<string, double>(item.WarehouseStocks, StringComparer.Ordinal);
            var delta = row.ProposedStock!.Value - item.StockAt(State.Warehouses);
            if (Math.Abs(delta) < 0.0000001) continue;
            item.WarehouseStocks[warehouse] = item.WarehouseStocks.GetValueOrDefault(warehouse) + delta;
            RecordStockAdjustments(item, before, $"Điều chỉnh theo đối chiếu nguồn {Path.GetFileName(State.ReferencePath)}");
            changed++;
        }

        await PersistAsync(false);
        if (_currentEmbeddedPage?.Source is ReferenceInventoryWindow page) page.RefreshComparisons();
        ShowToast(changed == 0 ? "Không có chênh lệch cần điều chỉnh." : $"Đã điều chỉnh tồn {changed:N0} mã và ghi vào nhật ký.");
    }

    private async Task OpenCatalogsAsync()
    {
        if (!CanOpenAnotherPage()) return;
        var page = new CatalogsWindow(State);
        ShowEmbeddedPage(page, "Quản lý danh mục", "Kho hàng, đơn vị tính, bộ phận và nơi giao nhận", accepted =>
        {
            if (accepted) _ = SaveCatalogsAsync(page);
        });
        await Task.CompletedTask;
    }

    private async Task SaveCatalogsAsync(CatalogsWindow dialog)
    {
        var wasAllWarehousesSelected = State.SelectedWarehouses.Count == State.Warehouses.Count;
        var warehouses = dialog.Warehouses;
        foreach (var item in State.Products)
        {
            foreach (var warehouse in warehouses) item.WarehouseStocks.TryAdd(warehouse, 0);
            foreach (var removed in item.WarehouseStocks.Keys.Where(warehouse => !warehouses.Contains(warehouse, StringComparer.Ordinal)).ToList()) item.WarehouseStocks.Remove(removed);
        }
        State.Warehouses = warehouses;
        State.Units = dialog.Units;
        State.Departments = dialog.Departments;
        State.Recipients = dialog.Recipients;
        State.SelectedWarehouses = wasAllWarehousesSelected ? [.. warehouses] : State.SelectedWarehouses.Where(warehouses.Contains).Distinct().ToList();
        if (State.SelectedWarehouses.Count == 0) State.SelectedWarehouses = [.. warehouses];
        InitializeFilters();
        await PersistAsync();
    }

    private async Task OpenMovementAsync(string? initialSku = null, string? initialType = null, string? initialWarehouse = null)
    {
        if (!CanOpenAnotherPage()) return;
        if (_state is null) return;
        var page = new MovementWindow(State, initialSku, initialType, initialWarehouse);
        ShowEmbeddedPage(page, "Nhập & Xuất kho", "Lập phiếu nhập, xuất hoặc điều chuyển; có thể lưu chờ xác nhận", accepted =>
        {
            if (accepted && page.Draft is not null) _ = SaveMovementDraftAsync(page.Draft);
        });
        await Task.CompletedTask;
    }

    private async Task SaveMovementDraftAsync(MovementDraft draft)
    {
        var docNo = NextMovementDocNo(draft.Type, draft.Date);
        if (draft.Pending)
        {
            State.PendingMovements.Add(ToPendingMovement(draft, docNo));
            AddRecipient(draft.Partner);
            await PersistAsync(false);
            ShowToast($"Đã lưu {docNo} ở trạng thái chờ xác nhận; tồn kho chưa thay đổi.");
            return;
        }
        ApplyMovement(draft, docNo);
        await PersistAsync(false);
        ShowToast($"Đã lưu {docNo}. Mở Nhật ký giao dịch để in phiếu.");
    }

    private string NextMovementDocNo(string type, DateOnly date)
    {
        var prefix = type == "Nhập" ? "PN" : type == "Xuất" ? "PX" : "DC";
        var datePart = date.ToString("yyyyMMdd");
        var prefixWithDate = $"{prefix}-{datePart}-";
        var numbers = State.Transactions.Select(transaction => transaction.DocNo)
            .Concat(State.PendingMovements.Select(movement => movement.DocNo))
            .Where(number => number.StartsWith(prefixWithDate, StringComparison.OrdinalIgnoreCase))
            .Select(number => int.TryParse(number[(number.LastIndexOf('-') + 1)..], out var value) ? value : 0);
        return $"{prefix}-{datePart}-{numbers.DefaultIfEmpty(0).Max() + 1:000}";
    }

    private static PendingMovement ToPendingMovement(MovementDraft draft, string docNo) => new()
    {
        DocNo = docNo,
        ReferenceNo = string.IsNullOrWhiteSpace(draft.ReferenceNo) ? docNo : draft.ReferenceNo,
        Date = draft.Date,
        Type = draft.Type,
        Warehouse = draft.Warehouse,
        ToWarehouse = draft.ToWarehouse,
        Partner = draft.Partner,
        Operator = draft.Operator,
        Note = draft.Note,
        Lines = draft.Lines.Select(line => new MovementLine { Sku = line.Sku, ItemName = line.ItemName, Unit = line.Unit, Quantity = line.Quantity }).ToList()
    };

    private List<InventoryTransaction> ApplyMovement(MovementDraft draft, string docNo)
    {
        var records = new List<InventoryTransaction>();
        foreach (var line in draft.Lines)
        {
            var item = State.Products.First(product => product.Active && product.Sku.Equals(line.Sku, StringComparison.OrdinalIgnoreCase));
            item.WarehouseStocks.TryAdd(draft.Warehouse, 0);
            if (draft.Type == "Nhập") item.WarehouseStocks[draft.Warehouse] += line.Quantity;
            else if (draft.Type == "Xuất") item.WarehouseStocks[draft.Warehouse] -= line.Quantity;
            else
            {
                item.WarehouseStocks[draft.Warehouse] -= line.Quantity;
                item.WarehouseStocks.TryAdd(draft.ToWarehouse, 0);
                item.WarehouseStocks[draft.ToWarehouse] += line.Quantity;
            }
            records.Add(new InventoryTransaction
            {
                DocNo = docNo,
                ReferenceNo = string.IsNullOrWhiteSpace(draft.ReferenceNo) ? docNo : draft.ReferenceNo,
                Date = draft.Date,
                Type = draft.Type,
                Sku = item.Sku,
                ItemName = item.Name,
                Category = item.Category,
                Unit = item.Unit,
                Quantity = line.Quantity,
                Warehouse = draft.Warehouse,
                FromWarehouse = draft.Type is "Xuất" or "Điều chuyển" ? draft.Warehouse : null,
                ToWarehouse = draft.Type is "Nhập" or "Điều chuyển" ? (draft.Type == "Nhập" ? draft.Warehouse : draft.ToWarehouse) : null,
                Partner = draft.Partner,
                Operator = draft.Operator,
                Note = draft.Note,
                Legacy = false,
                AffectsStock = true
            });
        }
        foreach (var record in records) State.Transactions.Add(record);
        AddRecipient(draft.Partner);
        return records;
    }

    private void AddRecipient(string value)
    {
        var recipient = value.Trim();
        if (recipient.Length > 0 && !State.Recipients.Contains(recipient, StringComparer.OrdinalIgnoreCase))
        {
            State.Recipients.Add(recipient);
            State.Recipients.Sort(StringComparer.CurrentCultureIgnoreCase);
        }
    }

    private async Task OpenPendingMovementsAsync()
    {
        if (!CanOpenAnotherPage()) return;
        var page = new PendingMovementsWindow(State.PendingMovements);
        ShowEmbeddedPage(page, "Phiếu chờ xác nhận", "Rà soát phiếu nhập, xuất và điều chuyển trước khi cập nhật tồn kho", accepted =>
        {
            if (accepted && page.SelectedMovement is PendingMovement pending) _ = HandlePendingMovementAsync(page, pending);
        });
        await Task.CompletedTask;
    }

    private async Task HandlePendingMovementAsync(PendingMovementsWindow page, PendingMovement pending)
    {
        if (page.Action == "cancel")
        {
            pending.Status = "Đã hủy";
            await PersistAsync();
            ShowToast($"Đã hủy phiếu chờ {pending.DocNo}; tồn kho không thay đổi.");
            return;
        }
        var movementDraft = new MovementDraft
        {
            Type = pending.Type,
            Date = pending.Date,
            Warehouse = pending.Warehouse,
            ToWarehouse = pending.ToWarehouse,
            ReferenceNo = pending.ReferenceNo,
            Partner = pending.Partner,
            Operator = pending.Operator,
            Note = pending.Note,
            Lines = pending.Lines.Select(line => new MovementLine { Sku = line.Sku, ItemName = line.ItemName, Unit = line.Unit, Quantity = line.Quantity }).ToList()
        };
        if (!ValidateCurrentStock(movementDraft)) return;
        ApplyMovement(movementDraft, pending.DocNo);
        pending.Status = "Đã xác nhận";
        await PersistAsync(false);
        ShowToast($"Đã xác nhận {pending.DocNo} và cập nhật tồn kho. Mở Nhật ký giao dịch để in phiếu.");
    }

    private bool ValidateCurrentStock(MovementDraft draft)
    {
        if (draft.Type is not ("Xuất" or "Điều chuyển")) return true;
        foreach (var group in draft.Lines.GroupBy(line => line.Sku, StringComparer.OrdinalIgnoreCase))
        {
            var item = State.Products.FirstOrDefault(product => product.Active && product.Sku.Equals(group.Key, StringComparison.OrdinalIgnoreCase));
            var available = item?.WarehouseStocks.GetValueOrDefault(draft.Warehouse) ?? 0;
            var needed = group.Sum(line => line.Quantity);
            if (item is not null && needed <= available) continue;
            ShowToast($"Không đủ tồn tại thời điểm xác nhận. {group.Key}: cần {InventoryRow.FormatQuantity(needed)}, hiện có {InventoryRow.FormatQuantity(available)} {item?.Unit} ở {draft.Warehouse}. Phiếu vẫn ở trạng thái chờ.");
            return false;
        }
        return true;
    }

    private void InventoryFilter_Changed(object sender, RoutedEventArgs e) => RefreshInventory();
    private void AuditFilter_Changed(object sender, RoutedEventArgs e) => RefreshAudit();
    private void LabelFilter_Changed(object sender, RoutedEventArgs e) => RefreshLabels();
    private void ClearAuditDate_Click(object sender, RoutedEventArgs e) { AuditDateFilter.SelectedDate = null; RefreshAudit(); }
    private void LabelGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => Dispatcher.BeginInvoke(UpdateLabelSelectionFromRows, DispatcherPriority.Background);
    private void LabelGrid_CurrentCellChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(UpdateLabelSelectionFromRows, DispatcherPriority.Background);
    private void UpdateLabelSelectionFromRows()
    {
        foreach (var row in LabelRows)
        {
            if (row.SelectedForLabel) _labelSelection.Add(row.Sku);
            else _labelSelection.Remove(row.Sku);
        }
        UpdateLabelPreview();
    }

    private void InventoryGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (InventoryGrid.SelectedItem is InventoryRow row) EditItem(row.Item);
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        if (!CanOpenAnotherPage()) return;
        var page = new ItemEditorWindow(State, null, ValidateItemSku);
        ShowEmbeddedPage(page, "Thêm vật tư", "Khai báo SKU, hình ảnh, đơn vị và tồn ban đầu theo từng kho", accepted =>
        {
            if (accepted && page.ResultItem is not null) _ = SaveNewItemAsync(page.ResultItem);
        });
    }

    private async Task SaveNewItemAsync(InventoryItem item)
    {
        State.Products.Add(item);
        AddCategoryAndUnit(item);
        RecordStockAdjustments(item, null, "Số dư ban đầu khi tạo mã hàng");
        await PersistAsync();
    }

    private void ExportExcelTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Lưu mẫu nhập hàng hóa", Filter = "Mẫu Excel (*.xlsx)|*.xlsx", FileName = "Mau-nhap-hang-hoa.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        try { ExcelInventoryService.CreateTemplate(dialog.FileName, State.Warehouses); ShowToast("Đã tạo mẫu Excel nhập hàng hóa."); }
        catch (Exception ex) { ShowToast("Không tạo được mẫu: " + ex.Message); }
    }

    private void ExportInventoryExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Xuất danh mục hàng hóa", Filter = "Sổ Excel (*.xlsx)|*.xlsx", FileName = $"Danh-muc-kho-{DateTime.Today:yyyyMMdd}.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        try { ExcelInventoryService.ExportInventory(dialog.FileName, State); ShowToast("Đã xuất danh mục kho ra Excel."); }
        catch (Exception ex) { ShowToast("Không xuất được danh mục: " + ex.Message); }
    }

    private async void ImportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Chọn tệp Excel hàng hóa", Filter = "Tệp Excel (*.xlsx)|*.xlsx|Tất cả tệp (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var imported = ExcelInventoryService.ReadImport(dialog.FileName, State.Warehouses);
            if (imported.Rows.Count == 0)
            {
                var details = imported.Errors.Count == 0 ? "Không có dòng hàng hóa để nhập." : string.Join("\n", imported.Errors.Take(10));
                ShowToast(details);
                return;
            }
            var preview = $"Tìm thấy {imported.Rows.Count:N0} dòng hợp lệ.\nSKU mới: {imported.Rows.Count(row => State.Products.All(item => !item.Sku.Equals(row.Sku, StringComparison.OrdinalIgnoreCase))):N0}.\nSKU đã có sẽ được cập nhật. Số dư có dữ liệu sẽ tạo dòng Điều chỉnh trong nhật ký.";
            if (imported.Errors.Count > 0) preview += $"\n\nCó {imported.Errors.Count:N0} dòng/cột cần bỏ qua hoặc rà soát:\n" + string.Join("\n", imported.Errors.Take(8));
            preview += "\n\nBạn muốn nhập các dòng hợp lệ?";
            ConfirmInApp(preview, accepted => { if (accepted) _ = ImportExcelRowsAsync(imported); });
        }
        catch (Exception ex) { ShowToast("Không nhập được Excel: " + ex.Message); }
    }

    private async Task ImportExcelRowsAsync(ExcelInventoryImportResult imported)
    {
        try
        {
            var created = 0;
            foreach (var row in imported.Rows)
            {
                var item = State.Products.FirstOrDefault(candidate => candidate.Sku.Equals(row.Sku, StringComparison.OrdinalIgnoreCase));
                Dictionary<string, double>? before = null;
                if (item is null)
                {
                    item = new InventoryItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Sku = row.Sku,
                        Barcode = row.Sku,
                        Name = row.Name,
                        Category = row.Category ?? "Vật tư khác",
                        Unit = row.Unit ?? State.Units.FirstOrDefault() ?? "Cái",
                        Department = row.Department ?? "",
                        WarehouseStocks = State.Warehouses.ToDictionary(warehouse => warehouse, _ => 0d, StringComparer.Ordinal)
                    };
                    State.Products.Add(item);
                    created++;
                }
                else
                {
                    before = new Dictionary<string, double>(item.WarehouseStocks, StringComparer.Ordinal);
                    item.Name = row.Name;
                    if (row.Category is not null) item.Category = row.Category;
                    if (row.Unit is not null) item.Unit = row.Unit;
                    if (row.Department is not null) item.Department = row.Department;
                }
                if (row.Minimum.HasValue) { item.MinStock = row.Minimum.Value; item.MinStockDefined = true; }
                foreach (var (warehouse, quantity) in row.Stocks) item.WarehouseStocks[warehouse] = quantity;
                if (row.Stocks.Count > 0) RecordStockAdjustments(item, before, $"Nhập hàng hóa từ Excel · dòng {row.RowNumber}");
                if (!State.Categories.Contains(item.Category, StringComparer.OrdinalIgnoreCase)) State.Categories.Add(item.Category);
                if (!State.Units.Contains(item.Unit, StringComparer.OrdinalIgnoreCase)) State.Units.Add(item.Unit);
                if (!string.IsNullOrWhiteSpace(item.Department) && !State.Departments.Contains(item.Department, StringComparer.OrdinalIgnoreCase)) State.Departments.Add(item.Department);
            }
            State.Categories.Sort(StringComparer.CurrentCultureIgnoreCase);
            State.Units.Sort(StringComparer.CurrentCultureIgnoreCase);
            State.Departments.Sort(StringComparer.CurrentCultureIgnoreCase);
            InitializeFilters();
            await PersistAsync();
            ShowToast($"Đã nhập {imported.Rows.Count:N0} mã · {created:N0} mã mới.");
        }
        catch (Exception ex) { ShowToast("Không nhập được Excel: " + ex.Message); }
    }

    private void EditItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is InventoryItem item) EditItem(item);
    }

    private void EditItem(InventoryItem item)
    {
        if (!CanOpenAnotherPage()) return;
        var page = new ItemEditorWindow(State, item, ValidateItemSku);
        ShowEmbeddedPage(page, "Chỉnh sửa vật tư", "Cập nhật thông tin, ảnh minh họa và tồn theo từng kho", accepted =>
        {
            if (accepted && page.ResultItem is not null) _ = SaveEditedItemAsync(item, page.ResultItem);
        });
    }

    private async Task SaveEditedItemAsync(InventoryItem original, InventoryItem edited)
    {
        RecordStockAdjustments(edited, original.WarehouseStocks, "Điều chỉnh số dư từ biểu mẫu vật tư");
        var index = State.Products.IndexOf(original);
        if (index >= 0) State.Products[index] = edited;
        AddCategoryAndUnit(edited);
        await PersistAsync();
    }

    private void RecordStockAdjustments(InventoryItem item, IReadOnlyDictionary<string, double>? previousStocks, string note)
    {
        var changes = State.Warehouses.Select(warehouse =>
        {
            var before = previousStocks?.GetValueOrDefault(warehouse) ?? 0;
            var after = item.WarehouseStocks.GetValueOrDefault(warehouse);
            return (Warehouse: warehouse, Delta: after - before);
        }).Where(change => Math.Abs(change.Delta) > 0.0000001).ToList();
        if (changes.Count == 0) return;
        var date = DateOnly.FromDateTime(DateTime.Today);
        var datePart = date.ToString("yyyyMMdd");
        var prior = State.Transactions.Select(t => t.DocNo).Where(n => n.StartsWith($"DCN-{datePart}-", StringComparison.OrdinalIgnoreCase))
            .Select(n => int.TryParse(n[(n.LastIndexOf('-') + 1)..], out var number) ? number : 0).DefaultIfEmpty(0).Max();
        var docNo = $"DCN-{datePart}-{prior + 1:000}";
        foreach (var change in changes)
        {
            State.Transactions.Add(new InventoryTransaction
            {
                DocNo = docNo,
                ReferenceNo = docNo,
                Date = date,
                Type = "Điều chỉnh",
                Sku = item.Sku,
                ItemName = item.Name,
                Category = item.Category,
                Unit = item.Unit,
                Quantity = change.Delta,
                Warehouse = change.Warehouse,
                Partner = "Kiểm kê",
                Operator = Environment.UserName,
                Note = $"{note} · {(change.Delta > 0 ? "tăng" : "giảm")} {InventoryRow.FormatQuantity(Math.Abs(change.Delta))} {item.Unit}",
                Legacy = false,
                AffectsStock = true
            });
        }
    }

    private string? ValidateItemSku(InventoryItem candidate)
    {
        var duplicate = State.Products.FirstOrDefault(p => p.Id != candidate.Id && p.Sku.Equals(candidate.Sku, StringComparison.OrdinalIgnoreCase));
        return duplicate is null ? null : $"SKU {candidate.Sku} đã được dùng cho {duplicate.Name}.";
    }

    private void AddCategoryAndUnit(InventoryItem item)
    {
        if (!State.Categories.Contains(item.Category, StringComparer.OrdinalIgnoreCase)) State.Categories.Add(item.Category);
        if (!State.Units.Contains(item.Unit, StringComparer.OrdinalIgnoreCase)) State.Units.Add(item.Unit);
        if (!string.IsNullOrWhiteSpace(item.Department) && !State.Departments.Contains(item.Department, StringComparer.OrdinalIgnoreCase)) State.Departments.Add(item.Department);
        State.Categories.Sort(StringComparer.CurrentCultureIgnoreCase);
        State.Units.Sort(StringComparer.CurrentCultureIgnoreCase);
        State.Departments.Sort(StringComparer.CurrentCultureIgnoreCase);
        InitializeFilters();
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not InventoryItem item) return;
        var hasHistory = State.Transactions.Any(t => t.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase));
        var prompt = hasHistory
            ? $"{item.Sku} đã có giao dịch. Ngừng theo dõi vật tư này? Nhật ký cũ sẽ được giữ lại."
            : $"Xóa {item.Sku} · {item.Name} khỏi danh mục?";
        ConfirmInApp(prompt, accepted => { if (accepted) _ = DeleteItemAsync(item, hasHistory); });
    }

    private async Task DeleteItemAsync(InventoryItem item, bool hasHistory)
    {
        if (hasHistory) item.Active = false;
        else State.Products.Remove(item);
        await PersistAsync();
    }

    private async void ManageUnits_Click(object sender, RoutedEventArgs e)
    {
        if (!CanOpenAnotherPage()) return;
        var page = new ManageUnitsWindow(State.Units, State.Products);
        ShowEmbeddedPage(page, "Quản lý đơn vị tính", "Thêm, sửa hoặc gộp đơn vị đang dùng", accepted =>
        {
            if (accepted) _ = SaveUnitsAsync(page);
        });
        await Task.CompletedTask;
    }

    private async Task SaveUnitsAsync(ManageUnitsWindow page)
    {
        State.Units = page.Units;
        InitializeFilters();
        await PersistAsync();
    }

    private async void SelectAllLabels_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in LabelRows) { row.SelectedForLabel = true; _labelSelection.Add(row.Sku); }
        LabelGrid.Items.Refresh();
        UpdateLabelPreview();
        await Task.CompletedTask;
    }

    private void PrintSelectedLabels_Click(object sender, RoutedEventArgs e)
    {
        UpdateLabelSelectionFromRows();
        var selected = State.Products.Where(p => _labelSelection.Contains(p.Sku)).ToList();
        if (selected.Count == 0)
        {
            ShowToast("Chọn ít nhất một mã hàng để in tem.");
            return;
        }
        PrintLabels(selected);
    }

    private void PrintSlip(IReadOnlyCollection<InventoryTransaction> records)
    {
        if (records.Count == 0) return;
        var first = records.First();
        var template = PrintTemplateService.Get(State, first.Type);
        var flow = NewPrintDocument();
        AddTemplateHeading(flow, template);
        AddMetadata(flow, "Số phiếu", first.DocNo, "Ngày", first.Date?.ToString("dd/MM/yyyy") ?? "—");
        AddMetadata(flow, "Kho", first.Type == "Nhập" ? first.ToWarehouse ?? first.Warehouse : first.Warehouse, first.Type == "Điều chuyển" ? "Kho nhận" : first.Type == "Nhập" ? "Nhà cung cấp" : "Nơi nhận", first.Type == "Điều chuyển" ? first.ToWarehouse ?? "" : first.Partner);
        AddMetadata(flow, "Người thực hiện", first.Operator, "Chứng từ tham chiếu", first.ReferenceNo);
        flow.Blocks.Add(new Paragraph(new Run(" ")) { Margin = new Thickness(0, 2, 0, 2) });
        var table = CreateSlipTable(records);
        flow.Blocks.Add(table);
        flow.Blocks.Add(new Paragraph(new Run("Ghi chú: " + first.Note + (string.IsNullOrWhiteSpace(template.FooterNote) ? "" : $"\n{template.FooterNote}"))) { Margin = new Thickness(0, 12, 0, 0), FontSize = 10 });
        flow.Blocks.Add(CreateSignatureBlock(template));
        ShowPrintDialog(flow, first.DocNo);
    }

    private void PrintRequest(MaterialRequest request)
    {
        var template = PrintTemplateService.Get(State, request.Type);
        var flow = NewPrintDocument();
        AddTemplateHeading(flow, template);
        AddMetadata(flow, "Số yêu cầu", request.DocNo, "Ngày yêu cầu", request.Date.ToString("dd/MM/yyyy"));
        AddMetadata(flow, "Bộ phận", request.Department, "Cần trước ngày", request.NeededBy?.ToString("dd/MM/yyyy") ?? "—");
        if (!string.IsNullOrWhiteSpace(request.Warehouse)) AddMetadata(flow, "Kho đề xuất", request.Warehouse, "Trạng thái", request.Status);
        AddMetadata(flow, request.Type == "Yêu cầu cấp vật tư" ? "Người nhận" : "Nhà cung cấp", request.Recipient, "Người yêu cầu", request.Requester);
        var records = request.Lines.Select(line => new InventoryTransaction { Sku = line.Sku, ItemName = line.ItemName, Unit = line.Unit, Quantity = line.Quantity }).ToList();
        flow.Blocks.Add(CreateSlipTable(records));
        flow.Blocks.Add(new Paragraph(new Run($"Lý do / ghi chú: {request.Note}" + (string.IsNullOrWhiteSpace(template.FooterNote) ? "" : $"\n{template.FooterNote}"))) { Margin = new Thickness(0, 12, 0, 0), FontSize = 10 });
        flow.Blocks.Add(CreateSignatureBlock(template));
        ShowPrintDialog(flow, request.DocNo);
    }

    private static void AddTemplateHeading(FlowDocument flow, PrintTemplate template)
    {
        if (!string.IsNullOrWhiteSpace(template.Organization))
            flow.Blocks.Add(new Paragraph(new Run(template.Organization)) { TextAlignment = TextAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 3) });
        if (!string.IsNullOrWhiteSpace(template.HeaderNote))
            flow.Blocks.Add(new Paragraph(new Run(template.HeaderNote)) { TextAlignment = TextAlignment.Center, FontSize = 10, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 15) });
        flow.Blocks.Add(new Paragraph(new Run(template.Title)) { TextAlignment = TextAlignment.Center, FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 14) });
    }

    private static FlowDocument NewPrintDocument() => new()
    {
        PageWidth = 793,
        PageHeight = 1122,
        PagePadding = new Thickness(48),
        ColumnWidth = 700,
        FontFamily = new FontFamily("Arial"),
        FontSize = 10,
        Foreground = Brushes.Black
    };

    private static void AddMetadata(FlowDocument flow, string leftLabel, string leftValue, string rightLabel, string rightValue)
    {
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(105) });
        table.Columns.Add(new TableColumn { Width = new GridLength(240) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100) });
        table.Columns.Add(new TableColumn { Width = new GridLength(250) });
        var group = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run(leftLabel + ":"))) { BorderThickness = new Thickness(0), Padding = new Thickness(2) });
        row.Cells.Add(new TableCell(new Paragraph(new Run(leftValue))) { BorderThickness = new Thickness(0), Padding = new Thickness(2) });
        row.Cells.Add(new TableCell(new Paragraph(new Run(rightLabel + ":"))) { BorderThickness = new Thickness(0), Padding = new Thickness(2) });
        row.Cells.Add(new TableCell(new Paragraph(new Run(rightValue))) { BorderThickness = new Thickness(0), Padding = new Thickness(2) });
        group.Rows.Add(row); table.RowGroups.Add(group); flow.Blocks.Add(table);
    }

    private static Table CreateSlipTable(IEnumerable<InventoryTransaction> records)
    {
        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(.5) };
        foreach (var width in new[] { 34d, 88d, 285d, 58d, 92d, 102d }) table.Columns.Add(new TableColumn { Width = new GridLength(width) });
        var group = new TableRowGroup();
        var header = new TableRow { Background = new SolidColorBrush(Color.FromRgb(236, 241, 247)), FontWeight = FontWeights.Bold };
        foreach (var text in new[] { "STT", "Mã hàng", "Tên vật tư", "ĐVT", "Số lượng", "Ghi chú" }) header.Cells.Add(MakeCell(text, true));
        group.Rows.Add(header);
        var i = 0;
        foreach (var item in records)
        {
            var row = new TableRow();
            row.Cells.Add(MakeCell((++i).ToString(CultureInfo.InvariantCulture)));
            row.Cells.Add(MakeCell(item.Sku));
            row.Cells.Add(MakeCell(item.ItemName));
            row.Cells.Add(MakeCell(item.Unit));
            row.Cells.Add(MakeCell(item.Quantity is double q ? InventoryRow.FormatQuantity(q) : "—"));
            row.Cells.Add(MakeCell(""));
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group);
        return table;
    }

    private static TableCell MakeCell(string text, bool header = false) => new(new Paragraph(new Run(text)) { Margin = new Thickness(2), FontSize = 9 })
    {
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(.5),
        Padding = new Thickness(4, 6, 4, 6),
        TextAlignment = header ? TextAlignment.Center : TextAlignment.Left
    };

    private static Block CreateSignatureBlock(PrintTemplate template)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 38, 0, 0) };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var row = new TableRow();
        foreach (var title in new[] { template.Signer1, template.Signer2, template.Signer3 })
        {
            var paragraph = new Paragraph(new Run(title + "\n\n\n\n(Ký, ghi rõ họ tên)")) { TextAlignment = TextAlignment.Center, FontSize = 9, FontWeight = FontWeights.SemiBold };
            row.Cells.Add(new TableCell(paragraph) { BorderThickness = new Thickness(0), Padding = new Thickness(3) });
        }
        table.RowGroups.Add(new TableRowGroup { Rows = { row } });
        return table;
    }

    private void PrintLabels(IReadOnlyList<InventoryItem> products)
    {
        var flow = NewPrintDocument();
        flow.PagePadding = new Thickness(28);
        flow.Blocks.Add(new Paragraph(new Run("TEM MÃ VẠCH VẬT TƯ")) { TextAlignment = TextAlignment.Center, FontSize = 15, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12) });
        var table = new Table { CellSpacing = 6 };
        for (var i = 0; i < 3; i++) table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var group = new TableRowGroup();
        for (var start = 0; start < products.Count; start += 3)
        {
            var row = new TableRow();
            for (var col = 0; col < 3; col++)
            {
                if (start + col >= products.Count) { row.Cells.Add(new TableCell(new Paragraph(new Run("")))); continue; }
                var product = products[start + col];
                var cell = new TableCell
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(185, 195, 207)),
                    BorderThickness = new Thickness(.5),
                    Padding = new Thickness(5)
                };
                cell.Blocks.Add(new Paragraph(new Run(product.Name)) { FontSize = 8, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 2) });
                cell.Blocks.Add(new BlockUIContainer(new Image { Source = BarcodeService.CreateCode39Image(product.Barcode), Height = 54, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center }));
                cell.Blocks.Add(new Paragraph(new Run($"{product.Sku} · {product.Unit}")) { FontSize = 8, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
                row.Cells.Add(cell);
            }
            group.Rows.Add(row);
        }
        table.RowGroups.Add(group); flow.Blocks.Add(table);
        ShowPrintDialog(flow, "Tem mã vạch");
    }

    private void ShowPrintDialog(FlowDocument flow, string description)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() == true) dialog.PrintDocument(((IDocumentPaginatorSource)flow).DocumentPaginator, description);
    }

    private void ExportBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Xuất bản sao lưu dữ liệu kho", Filter = "Bản sao lưu HISA (*.json)|*.json", FileName = $"HISA-Kho-{DateTime.Today:yyyyMMdd}.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, InventoryStore.SerializeBackup(State)); ShowToast("Đã xuất bản sao lưu."); }
        catch (Exception ex) { ShowToast("Không xuất được bản sao lưu: " + ex.Message); }
    }

    private async void ImportBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Chọn bản sao lưu HISA", Filter = "Bản sao lưu HISA (*.json)|*.json|Tất cả tệp (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        ConfirmInApp("Nhập bản sao lưu sẽ thay dữ liệu hiện tại. Bạn muốn tiếp tục?", accepted =>
        {
            if (accepted) _ = RestoreBackupAsync(dialog.FileName);
        });
    }

    private async Task RestoreBackupAsync(string fileName)
    {
        try
        {
            var restored = InventoryStore.ReadBackup(fileName) ?? throw new InvalidDataException("Tệp JSON không có dữ liệu HISA hợp lệ.");
            var previousState = _state;
            _state = restored;
            InitializeFilters();
            RenderAll();
            if (!await PersistAsync())
            {
                // Keep the previous in-memory inventory if the restored data cannot be saved.
                _state = previousState;
                InitializeFilters();
                RenderAll();
                return;
            }
            ShowToast("Đã phục hồi bản sao lưu.");
        }
        catch (Exception ex) { ShowToast("Không phục hồi được dữ liệu: " + ex.Message); }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckUpdateButton.IsEnabled) return;
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Đang kiểm tra GitHub Releases…";
        try
        {
            var release = await WarehouseUpdateService.FindLatestAsync();
            if (release is null)
            {
                UpdateStatusText.Text = "Chưa có bản phát hành chính thức mới hơn. " +
                    $"Máy đang dùng phiên bản {WarehouseUpdateService.CurrentVersion.ToString(3)}.";
                return;
            }

            UpdateStatusText.Text = $"Đã tìm thấy {release.Tag}. Đang tải xuống và kiểm tra SHA-256…";
            var package = await WarehouseUpdateService.DownloadVerifiedAsync(release);
            var hasUpdater = File.Exists(Path.Combine(AppContext.BaseDirectory, "HISA.Updater.exe"));
            var canInstall = hasUpdater && WarehouseUpdateService.IsInstallFolderWritable(AppContext.BaseDirectory);
            if (canInstall)
            {
                UpdateStatusText.Text = $"Đã tải và xác minh thành công {release.Tag}. Chờ xác nhận cài đặt.";
                ConfirmInApp(
                    $"Đã tải xong bản {release.Tag} và xác minh SHA-256. " +
                    "Bạn muốn HISA tự đóng, cập nhật im lặng rồi mở lại không? " +
                    "Nếu chọn Không, tệp đã tải được giữ lại để cài thủ công.",
                    accepted =>
                    {
                        if (accepted) _ = InstallDownloadedUpdateAsync(package);
                        else ShowManualUpdatePackage(package);
                    },
                    "Có, cập nhật", "Không, thủ công");
            }
            else
            {
                UpdateStatusText.Text = $"Bản {release.Tag} đã tải và xác minh. " +
                    "Thư mục cài đặt cần quyền quản trị hoặc thiếu HISA.Updater.exe; chỉ cài thủ công.";
                ConfirmInApp(
                    "Tệp tải về đã được xác minh. Máy này không đủ điều kiện cập nhật im lặng " +
                    "(có thể đang cài trong Program Files). Mở vị trí tệp để cài bằng quyền quản trị?",
                    accepted => { if (accepted) ShowManualUpdatePackage(package); },
                    "Mở tệp tải về", "Để sau");
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Kiểm tra / tải cập nhật chưa thành công: " + ex.Message;
            ShowToast("Không cập nhật được: " + ex.Message);
        }
        finally { CheckUpdateButton.IsEnabled = true; }
    }

    private async Task InstallDownloadedUpdateAsync(string package)
    {
        try
        {
            // Persist warehouse changes BEFORE closing the app or launching the updater.
            if (_state is not null && !await PersistAsync(false))
            {
                UpdateStatusText.Text = "Không thể lưu dữ liệu kho; HISA chưa thoát và chưa cập nhật.";
                return;
            }

            WarehouseUpdateService.StartAutomaticInstall(package);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Chưa thể tự động cập nhật: " + ex.Message;
            ShowToast("Bản cập nhật vẫn được giữ để cài thủ công.");
            ShowManualUpdatePackage(package);
        }
    }

    private void ShowManualUpdatePackage(string path)
    {
        try
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            start.ArgumentList.Add("/select," + path);
            Process.Start(start);
            UpdateStatusText.Text = "Đã mở tệp ZIP cập nhật. Hãy đóng HISA và " +
                "giải nén bộ mới, hoặc dùng bộ cài được cấp quyền quản trị. " +
                "Không thay thế file kho chính hoặc thư mục dữ liệu người dùng.";
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Đã tải tệp vào " + path + ". Không mở được Explorer: " + ex.Message;
        }
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBorder.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void ConfirmInApp(string message, Action<bool> completion,
        string yesLabel = "Xác nhận", string noLabel = "Quay lại")
    {
        ConfirmationYesButton.Content = yesLabel;
        ConfirmationNoButton.Content = noLabel;
        _toastTimer.Stop();
        ToastBorder.Visibility = Visibility.Collapsed;
        ConfirmationText.Text = message;
        _confirmationCompletion = completion;
        ConfirmationOverlay.Visibility = Visibility.Visible;
    }

    private void ConfirmationYes_Click(object sender, RoutedEventArgs e) => CompleteConfirmation(true);
    private void ConfirmationNo_Click(object sender, RoutedEventArgs e) => CompleteConfirmation(false);

    private void CompleteConfirmation(bool accepted)
    {
        ConfirmationOverlay.Visibility = Visibility.Collapsed;
        var completion = _confirmationCompletion;
        _confirmationCompletion = null;
        completion?.Invoke(accepted);
    }
}
