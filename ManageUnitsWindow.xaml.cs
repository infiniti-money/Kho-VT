using System.Windows;

namespace HisaWarehouse;

public partial class ManageUnitsWindow : Window
{
    private readonly IReadOnlyCollection<InventoryItem> _products;
    public List<string> Units { get; private set; }

    public ManageUnitsWindow(IEnumerable<string> units, IReadOnlyCollection<InventoryItem> products)
    {
        InitializeComponent();
        _products = products;
        Units = units.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        RefreshList();
    }

    private void RefreshList() => UnitList.ItemsSource = Units.Select(unit => _products.Any(p => p.Active && p.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase)) ? $"{unit}  ·  đang sử dụng" : unit).ToList();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var unit = UnitInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(unit)) return;
        if (Units.Contains(unit, StringComparer.OrdinalIgnoreCase))
        {
            this.NotifyInline("Đơn vị này đã có trong danh sách.");
            return;
        }
        Units.Add(unit);
        Units = Units.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();
        UnitInput.Clear();
        RefreshList();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (UnitList.SelectedItem is not string displayed) return;
        var unit = displayed.Split("  ·  ", StringSplitOptions.None)[0];
        if (_products.Any(p => p.Active && p.Unit.Equals(unit, StringComparison.OrdinalIgnoreCase)))
        {
            this.NotifyInline("Đơn vị đang được một hoặc nhiều mặt hàng sử dụng.");
            return;
        }
        Units.RemoveAll(x => x.Equals(unit, StringComparison.OrdinalIgnoreCase));
        RefreshList();
    }

    private void Done_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(true);
}
