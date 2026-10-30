using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class ItemEditorWindow : Window
{
    private readonly InventoryState _state;
    private readonly InventoryItem? _source;
    private readonly Func<InventoryItem, string?>? _validate;
    private readonly Dictionary<string, TextBox> _stockInputs = new(StringComparer.Ordinal);
    private bool _isEditing;
    private bool _isPopulating = true;
    private bool _barcodeOverridden;
    private bool _syncingBarcode;
    private string? _imageDataUrl;

    public InventoryItem? ResultItem { get; private set; }

    public ItemEditorWindow(InventoryState state, InventoryItem? source, Func<InventoryItem, string?>? validate = null)
    {
        InitializeComponent();
        _state = state;
        _source = source;
        _validate = validate;
        _isEditing = source is not null;
        WindowHeading.Text = _isEditing ? "Chỉnh sửa vật tư" : "Thêm vật tư mới";
        CategoryBox.ItemsSource = state.Categories.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        UnitBox.ItemsSource = state.Units.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        DepartmentBox.ItemsSource = state.Departments.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        foreach (var warehouse in state.Warehouses)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            var label = new TextBlock { Text = warehouse, Foreground = System.Windows.Media.Brushes.SlateGray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            var input = new TextBox { Text = "0", HorizontalContentAlignment = HorizontalAlignment.Right, MinHeight = 33, Padding = new Thickness(8, 5, 8, 5) };
            Grid.SetColumn(input, 1);
            row.Children.Add(label);
            row.Children.Add(input);
            WarehouseStockPanel.Children.Add(row);
            _stockInputs[warehouse] = input;
        }
        if (_isEditing && source is not null)
        {
            SkuBox.Text = source.Sku;
            BarcodeBox.Text = source.Barcode;
            NameBox.Text = source.Name;
            CategoryBox.Text = source.Category;
            UnitBox.Text = source.Unit;
            DepartmentBox.Text = source.Department;
            _imageDataUrl = source.ImageDataUrl;
            ProductPhoto.Source = ImageDataService.Decode(_imageDataUrl);
            MinimumBox.Text = source.MinStockDefined ? source.MinStock.ToString("0.##", CultureInfo.InvariantCulture) : "";
            foreach (var (warehouse, input) in _stockInputs)
                input.Text = source.WarehouseStocks.TryGetValue(warehouse, out var qty) ? qty.ToString("0.##", CultureInfo.InvariantCulture) : "0";
        }
        else
        {
            CategoryBox.Text = state.Categories.FirstOrDefault(x => x == "Vật tư khác") ?? "Vật tư khác";
            UnitBox.Text = state.Units.FirstOrDefault() ?? "Cái";
            DepartmentBox.Text = "";
            SkuBox.Text = GenerateSku(state);
            BarcodeBox.Text = SkuBox.Text;
        }
        _isPopulating = false;
        UpdateBarcodePreview();
    }

    private static string GenerateSku(InventoryState state)
    {
        var numbers = state.Products.Select(p => p.Sku).Where(s => s.StartsWith("VT", StringComparison.OrdinalIgnoreCase))
            .Select(s => int.TryParse(s[2..], out var n) ? n : 0).ToList();
        return $"VT{(numbers.DefaultIfEmpty(0).Max() + 1):D5}";
    }

    private void GenerateSku_Click(object sender, RoutedEventArgs e)
    {
        if (!_isEditing || string.IsNullOrWhiteSpace(SkuBox.Text)) SkuBox.Text = GenerateSku(_state);
        if (string.IsNullOrWhiteSpace(BarcodeBox.Text)) BarcodeBox.Text = SkuBox.Text;
    }

    private void SkuTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_barcodeOverridden)
        {
            _syncingBarcode = true;
            BarcodeBox.Text = SkuBox.Text;
            _syncingBarcode = false;
        }
        UpdateBarcodePreview();
    }

    private void BarcodeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isPopulating && !_syncingBarcode) _barcodeOverridden = true;
        UpdateBarcodePreview();
    }

    private void UpdateBarcodePreview() => BarcodePreview.Source = BarcodeService.CreateCode39Image(BarcodeBox.Text);

    private void ChoosePhoto_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn ảnh vật tư",
            Filter = "Ảnh vật tư (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|Tất cả tệp (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _imageDataUrl = ImageDataService.EncodeFile(dialog.FileName);
            ProductPhoto.Source = ImageDataService.Decode(_imageDataUrl);
        }
        catch (Exception ex)
        {
            this.NotifyInline("Không thể dùng ảnh này. " + ex.Message);
        }
    }

    private void RemovePhoto_Click(object sender, RoutedEventArgs e)
    {
        _imageDataUrl = null;
        ProductPhoto.Source = null;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var sku = SkuBox.Text.Trim().ToUpperInvariant();
        var name = NameBox.Text.Trim();
        var category = CategoryBox.Text.Trim();
        var unit = UnitBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(unit))
        {
            this.NotifyInline("Vui lòng nhập SKU, tên vật tư, nhóm hàng và đơn vị tính.");
            return;
        }
        if (!TryReadNumber(MinimumBox.Text, out var minimum, true) || minimum < 0)
        {
            this.NotifyInline("Tồn tối thiểu phải là số không âm.");
            return;
        }

        var stocks = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (warehouse, input) in _stockInputs)
        {
            if (!TryReadNumber(input.Text, out var qty, false))
            {
                this.NotifyInline($"Số dư tại {warehouse} chưa hợp lệ.");
                return;
            }
            stocks[warehouse] = qty;
        }

        var item = new InventoryItem
        {
            Id = _source?.Id ?? Guid.NewGuid().ToString("N"),
            Sku = sku,
            Barcode = BarcodeBox.Text.Trim().ToUpperInvariant() is { Length: > 0 } barcode ? barcode : sku,
            Name = name,
            Category = category,
            Unit = unit,
            Department = DepartmentBox.Text.Trim(),
            ImageDataUrl = _imageDataUrl,
            MinStock = minimum,
            MinStockDefined = MinimumBox.Text.Trim().Length > 0,
            WarehouseStocks = stocks,
            Active = _source?.Active ?? true,
            Legacy = _source?.Legacy ?? false,
            SourceRow = _source?.SourceRow,
            SourceCurrentStock = _source?.SourceCurrentStock,
            SourceOpeningStock = _source?.SourceOpeningStock,
            SourceStatus = _source?.SourceStatus
        };
        var totalImageBytes = ImageDataService.TotalBytes(_state.Products.Where(product => product.Id != item.Id).Append(item));
        if (totalImageBytes > 2_000_000)
        {
            this.NotifyInline("Tổng dung lượng ảnh vật tư vượt 2 MB. Hãy gỡ bớt ảnh hoặc chọn ảnh nhẹ hơn để dữ liệu localStorage lưu ổn định.");
            return;
        }
        var validation = _validate?.Invoke(item);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            this.NotifyInline(validation);
            return;
        }
        ResultItem = item;
        this.CompleteEmbedded(true);
    }

    private static bool TryReadNumber(string text, out double value, bool allowBlank)
    {
        if (allowBlank && string.IsNullOrWhiteSpace(text)) { value = 0; return true; }
        var normalized = NormalizeNumber(text);
        return double.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    private static string NormalizeNumber(string text)
    {
        var normalized = text.Trim().Replace(" ", "");
        if (normalized.Contains(',')) return normalized.Replace(".", "").Replace(',', '.');
        var parts = normalized.Split('.');
        if (parts.Length > 2 || (parts.Length == 2 && parts[1].Length == 3 && parts[0].Length <= 3)) return normalized.Replace(".", "");
        return normalized;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
