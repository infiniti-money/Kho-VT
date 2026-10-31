using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace HisaWarehouse.Services;

/// <summary>Shared layout used by both template preview and real inventory slips.</summary>
public static class PrintTemplateLayout
{
    public static void Validate(PrintTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Type) || template.Type.Length > 80)
            throw new ArgumentException("Tên loại mẫu không hợp lệ.");
        if (string.IsNullOrWhiteSpace(template.Title))
            throw new ArgumentException("Mẫu in cần có tiêu đề.");
        if (template.FontSize < 8 || template.FontSize > 20 || !double.IsFinite(template.FontSize))
            throw new ArgumentException("Cỡ chữ phải từ 8 đến 20.");
        if (template.MarginMm < 5 || template.MarginMm > 35 || !double.IsFinite(template.MarginMm))
            throw new ArgumentException("Lề giấy cần từ 5 đến 35 mm.");
        if (template.CellPadding < 2 || template.CellPadding > 16 || !double.IsFinite(template.CellPadding))
            throw new ArgumentException("Khoảng cách trong ô từ 2 đến 16.");
        if (template.HeaderAlignment is not ("Left" or "Center" or "Right"))
            throw new ArgumentException("Căn lề tiêu đề phải là Trái, Giữa hoặc Phải.");
        if (template.FontName is not ("Arial" or "Segoe UI" or "Times New Roman"))
            throw new ArgumentException("Phông chữ chưa được hỗ trợ.");
    }

    public static TextAlignment Alignment(PrintTemplate t) => t.HeaderAlignment switch
    {
        "Left" => TextAlignment.Left,
        "Right" => TextAlignment.Right,
        _ => TextAlignment.Center
    };

    public static void Apply(FlowDocument flow, PrintTemplate t)
    {
        Validate(t);
        flow.PageWidth = 793.7;
        flow.PageHeight = 1122.5;
        var margin = t.MarginMm * 96 / 25.4;
        flow.PagePadding = new Thickness(margin);
        flow.ColumnWidth = Math.Max(200, flow.PageWidth - 2 * margin);
        flow.FontFamily = new FontFamily(t.FontName);
        flow.FontSize = t.FontSize;
    }

    public static void ApplyTableCellPadding(Table table, PrintTemplate template)
    {
        foreach (var group in table.RowGroups)
            foreach (var row in group.Rows)
                foreach (var cell in row.Cells)
                    cell.Padding = new Thickness(template.CellPadding);
    }

    public static FlowDocument Preview(PrintTemplate t)
    {
        var flow = new FlowDocument { Foreground = Brushes.Black };
        Apply(flow, t);
        foreach (var line in new[] { t.Organization, t.HeaderNote, t.Title })
            if (!string.IsNullOrWhiteSpace(line))
                flow.Blocks.Add(new Paragraph(new Run(line)) {
                    TextAlignment = Alignment(t),
                    FontWeight = line == t.HeaderNote ? FontWeights.Normal : FontWeights.Bold,
                    FontSize = line == t.Title ? t.FontSize + 8 : t.FontSize + 1,
                    Margin = new Thickness(0, 0, 0, line == t.Title ? 18 : 5) });
        flow.Blocks.Add(new Paragraph(new Run("Số phiếu: PN-0001                         Ngày: 31/10/2026"))
            { Margin = new Thickness(0, 5, 0, 10) });
        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5) };
        foreach (var weight in new[] { 0.5, 1.2, 3.2, 0.9, 1.1 })
            table.Columns.Add(new TableColumn { Width = new GridLength(weight, GridUnitType.Star) });
        var rows = new TableRowGroup();
        string[][] items = [
            ["STT", "Mã vật tư", "Tên vật tư", "ĐVT", "Số lượng"],
            ["1", "VT001", "Vật tư minh họa", "Cái", "12"],
            ["2", "VT002", "Thiết bị minh họa", "Bộ", "3"]
        ];
        for (var rowIndex = 0; rowIndex < items.Length; rowIndex++)
        {
            var row = new TableRow();
            if (rowIndex == 0) { row.FontWeight = FontWeights.SemiBold; row.Background = Brushes.AliceBlue; }
            foreach (var value in items[rowIndex])
                row.Cells.Add(new TableCell(new Paragraph(new Run(value)) { Margin = new Thickness(1) })
                {
                    BorderBrush = Brushes.Gray, BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(t.CellPadding),
                    TextAlignment = rowIndex == 0 ? TextAlignment.Center : TextAlignment.Left
                });
            rows.Rows.Add(row);
        }
        table.RowGroups.Add(rows);
        flow.Blocks.Add(table);
        if (!string.IsNullOrWhiteSpace(t.FooterNote))
            flow.Blocks.Add(new Paragraph(new Run(t.FooterNote)) { Margin = new Thickness(0, 16, 0, 0) });
        var signs = new Table { Margin = new Thickness(0, 30, 0, 0) };
        for (int i = 0; i < 3; i++) signs.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var signRow = new TableRow();
        foreach (var signer in new[] { t.Signer1, t.Signer2, t.Signer3 })
            signRow.Cells.Add(new TableCell(new Paragraph(new Run(signer + "\n\n\n(Ký, ghi rõ họ tên)"))
            { TextAlignment = TextAlignment.Center, FontWeight = FontWeights.SemiBold }));
        signs.RowGroups.Add(new TableRowGroup { Rows = { signRow } });
        flow.Blocks.Add(signs);
        return flow;
    }
}
