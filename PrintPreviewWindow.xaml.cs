using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class PrintPreviewWindow : Window
{
    private readonly string _description;
    private readonly PrintTemplate? _template;

    public PrintPreviewWindow(FlowDocument document, string description, PrintTemplate? template = null)
    {
        InitializeComponent();
        PrintViewer.Document = document;
        _description = description;
        _template = template;
        DescriptionText.Text = description +
            (template is null ? " · Kiểm tra trước khi gửi tới máy in" :
                $" · {template.PaperName} {(template.Landscape ? "ngang" : "dọc")} · Xem trước, không ghi Excel");
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (PrintViewer.Document is not FlowDocument document) return;
        try
        {
            var dialog = new PrintDialog();
            if (_template is not null)
            {
                var dims = PrintPageSetup.Resolve(_template);
                if (!string.IsNullOrWhiteSpace(_template.PrinterName))
                {
                    using var server = new LocalPrintServer();
                    try { dialog.PrintQueue = server.GetPrintQueue(_template.PrinterName); }
                    catch (PrintSystemException error)
                    {
                        this.NotifyInline("Máy in được lưu cho mẫu không còn khả dụng: " +
                            _template.PrinterName + ". Hãy đổi máy in ở Cài đặt mẫu. " + error.Message);
                        return;
                    }
                }
                // Driver ticket uses the unrotated media dimensions, plus orientation.
                var mediaWidth = _template.Landscape ? dims.HeightDip : dims.WidthDip;
                var mediaHeight = _template.Landscape ? dims.WidthDip : dims.HeightDip;
                var ticket = dialog.PrintTicket ?? new PrintTicket();
                ticket.PageMediaSize = new PageMediaSize(mediaWidth, mediaHeight);
                ticket.PageOrientation = _template.Landscape
                    ? PageOrientation.Landscape : PageOrientation.Portrait;
                ticket.CopyCount = _template.Copies;
                dialog.PrintTicket = ticket;
            }
            if (dialog.ShowDialog() != true) return;
            if (_template is not null)
            {
                var page = PrintPageSetup.Resolve(_template);
                var requestedWidth = _template.Landscape ? page.HeightDip : page.WidthDip;
                var requestedHeight = _template.Landscape ? page.WidthDip : page.HeightDip;
                // A driver may reject custom receipt media or silently switch to
                // the default paper. Do not send a mis-sized slip to that printer.
                var actual = dialog.PrintTicket?.PageMediaSize;
                if (actual?.Width is double width && actual.Height is double height &&
                    (Math.Abs(width - requestedWidth) > 10 ||
                     Math.Abs(height - requestedHeight) > 10))
                {
                    this.NotifyInline("Máy in đã đổi khổ giấy so với bản xem trước. " +
                        "Hãy chọn khổ phù hợp trong thuộc tính máy in hoặc dùng máy hỗ trợ khổ này.");
                    return;
                }
                var orientation = dialog.PrintTicket?.PageOrientation;
                if (orientation.HasValue && orientation.Value !=
                    (_template.Landscape ? PageOrientation.Landscape : PageOrientation.Portrait))
                {
                    this.NotifyInline("Máy in đã đổi hướng giấy. Hãy chọn đúng hướng trước khi in.");
                    return;
                }
            }
            dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, _description);
            this.CompleteEmbedded(true);
        }
        catch (Exception ex) { this.NotifyInline("Không in được: " + ex.Message); }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
