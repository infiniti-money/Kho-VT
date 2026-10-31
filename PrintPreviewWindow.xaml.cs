using System.Windows;
using System.Windows.Documents;

namespace HisaWarehouse;

public partial class PrintPreviewWindow : Window
{
    private readonly string _description;
    public PrintPreviewWindow(FlowDocument document, string description)
    {
        InitializeComponent();
        PrintViewer.Document = document;
        _description = description;
        DescriptionText.Text = description + " · Kiểm tra trước khi gửi tới máy in";
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (PrintViewer.Document is not FlowDocument document) return;
        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() == true)
            {
                dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, _description);
                this.CompleteEmbedded(true);
            }
        }
        catch (Exception ex) { this.NotifyInline("Không in được: " + ex.Message); }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
