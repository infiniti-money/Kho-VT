using System.Windows;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class ExcelPdfPreviewWindow : Window
{
    private ExcelSlipTemplatePrinter.Preview? _preview;
    public ExcelPdfPreviewWindow(ExcelSlipTemplatePrinter.Preview preview)
    {
        InitializeComponent();
        _preview = preview;
    }

    private async void Viewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (_preview is null) return;
        try
        {
            await Viewer.EnsureCoreWebView2Async();
            Viewer.CoreWebView2.Navigate(new Uri(_preview.PdfPath).AbsoluteUri);
        }
        catch (Exception ex) { this.NotifyInline("Không mở được bản xem trước PDF: " + ex.Message); }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (Viewer.CoreWebView2 is null)
        {
            this.NotifyInline("Bản xem trước chưa tải xong.");
            return;
        }
        Viewer.CoreWebView2.ShowPrintUI();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);

    public void ReleaseTemporaryFiles()
    {
        try { Viewer.Dispose(); } catch { }
        _preview?.Dispose();
        _preview = null;
    }
}
