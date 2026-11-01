using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace HisaWarehouse;

public partial class CatalogsWindow : Window
{
    private readonly InventoryState _state;
    private readonly Dictionary<string, ObservableCollection<string>> _values;
    private readonly Dictionary<string, ListBox> _lists;
    private readonly Dictionary<string, TextBox> _inputs;

    public List<string> Warehouses => _values["warehouses"].ToList();
    public List<string> Categories => _values["categories"].ToList();
    public List<string> Units => _values["units"].ToList();
    public List<string> Departments => _values["departments"].ToList();
    public List<string> Recipients => _values["recipients"].ToList();

    public CatalogsWindow(InventoryState state)
    {
        InitializeComponent();
        _state = state;
        _values = new Dictionary<string, ObservableCollection<string>>
        {
            ["warehouses"] = new(state.Warehouses),
            ["categories"] = new(state.Categories.Concat(state.Products.Select(product => product.Category)).Where(category => !string.IsNullOrWhiteSpace(category)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase)),
            ["units"] = new(state.Units),
            ["departments"] = new(state.Departments),
            ["recipients"] = new(state.Recipients)
        };
        _lists = new Dictionary<string, ListBox> { ["warehouses"] = WarehouseList, ["categories"] = CategoryList, ["units"] = UnitList, ["departments"] = DepartmentList, ["recipients"] = RecipientList };
        _inputs = new Dictionary<string, TextBox> { ["warehouses"] = WarehouseInput, ["categories"] = CategoryInput, ["units"] = UnitInput, ["departments"] = DepartmentInput, ["recipients"] = RecipientInput };
        foreach (var (key, list) in _lists) list.ItemsSource = _values[key];
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var key = (sender as Button)?.Tag as string;
        if (key is null || !_values.TryGetValue(key, out var list)) return;
        var input = _inputs[key];
        var value = input.Text.Trim();
        if (value.Length == 0) { input.Focus(); return; }
        if (list.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            this.NotifyInline("Giá trị này đã có trong danh mục.");
            return;
        }
        list.Add(value);
        input.Clear();
        _lists[key].SelectedItem = value;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        var key = (sender as Button)?.Tag as string;
        if (key is null || !_values.TryGetValue(key, out var list) || _lists[key].SelectedItem is not string selected) return;
        if (list.Count <= 1 && key == "warehouses")
        {
            this.NotifyInline("Ứng dụng cần giữ ít nhất một kho hàng.");
            return;
        }
        if (key == "warehouses" && _state.Products.Any(product => Math.Abs(product.WarehouseStocks.GetValueOrDefault(selected)) > 0.0000001))
        {
            this.NotifyInline($"Kho {selected} vẫn còn số dư vật tư. Hãy điều chuyển hoặc điều chỉnh hết tồn trước khi bớt kho.");
            return;
        }
        if (key == "warehouses" && _state.PendingMovements.Any(movement => movement.Status == "Chờ xác nhận" && (movement.Warehouse == selected || movement.ToWarehouse == selected)))
        {
            this.NotifyInline($"Kho {selected} đang được dùng trong phiếu chờ. Hãy xác nhận hoặc hủy các phiếu đó trước.");
            return;
        }
        if (key == "categories" && _state.Products.Any(product => product.Category.Equals(selected, StringComparison.OrdinalIgnoreCase)))
        {
            this.NotifyInline($"Phân loại {selected} vẫn đang được sử dụng. Chuyển vật tư sang phân loại khác trước khi xóa.");
            return;
        }
        if (key == "units" && _state.Products.Any(product => product.Unit.Equals(selected, StringComparison.OrdinalIgnoreCase)))
        {
            this.NotifyInline($"Đơn vị {selected} đang được dùng trong danh mục vật tư. Hãy đổi đơn vị của các vật tư trước.");
            return;
        }
        if (key == "departments" && _state.Products.Any(product => product.Department.Equals(selected, StringComparison.OrdinalIgnoreCase)))
        {
            this.NotifyInline($"Bộ phận {selected} đang được gán cho vật tư. Hãy đổi bộ phận của các vật tư trước.");
            return;
        }
        list.Remove(selected);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(true);
    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
