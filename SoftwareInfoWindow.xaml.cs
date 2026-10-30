using System.Windows;

namespace HisaWarehouse;

public partial class SoftwareInfoWindow : Window
{
    public SoftwareInfoWindow() => InitializeComponent();
    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
