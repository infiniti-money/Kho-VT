using System.Globalization;
using System.Printing;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class PrintTemplateWindow : Window, IWorkspaceDirtyPage
{
    private readonly List<PrintTemplate> _templates;
    private PrintTemplate? _current;
    private bool _loading;
    private readonly string _originalTemplates;

    public bool HasUnsavedChanges =>
        !string.Equals(_originalTemplates, JsonSerializer.Serialize(_templates), StringComparison.Ordinal) ||
        !string.IsNullOrWhiteSpace(NewTypeBox.Text) || !CurrentEditorMatchesTemplate();

    private bool CurrentEditorMatchesTemplate()
    {
        if (_current is null) return true;
        try
        {
            return OrganizationBox.Text.Trim() == _current.Organization &&
                TitleBox.Text.Trim() == _current.Title &&
                HeaderNoteBox.Text.Trim() == _current.HeaderNote &&
                FooterNoteBox.Text.Trim() == _current.FooterNote &&
                Signer1Box.Text.Trim() == _current.Signer1 &&
                Signer2Box.Text.Trim() == _current.Signer2 &&
                Signer3Box.Text.Trim() == _current.Signer3 &&
                ((FontBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Arial") == _current.FontName &&
                ReadNumber(FontSizeBox, "cỡ chữ") == _current.FontSize &&
                ReadNumber(MarginBox, "lề trang") == _current.MarginMm &&
                ReadNumber(CellPaddingBox, "đệm ô") == _current.CellPadding &&
                ((AlignBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Center") == _current.HeaderAlignment &&
                CurrentPaperName() == _current.PaperName &&
                LandscapeCheck.IsChecked == _current.Landscape &&
                ReadNumber(CustomWidthBox, "chiều rộng") == _current.CustomWidthMm &&
                ReadNumber(CustomHeightBox, "chiều cao") == _current.CustomHeightMm &&
                ReadNumber(TopMarginBox, "lề trên") == (_current.MarginTopMm ?? _current.MarginMm) &&
                ReadNumber(BottomMarginBox, "lề dưới") == (_current.MarginBottomMm ?? _current.MarginMm) &&
                ReadNumber(LeftMarginBox, "lề trái") == (_current.MarginLeftMm ?? _current.MarginMm) &&
                ReadNumber(RightMarginBox, "lề phải") == (_current.MarginRightMm ?? _current.MarginMm) &&
                (PrinterBox.Text?.Trim() ?? "") == _current.PrinterName &&
                ReadCopies() == _current.Copies;
        }
        catch (ArgumentException) { return false; } // Invalid uncommitted numeric edit.
    }
    public List<PrintTemplate> Templates { get; private set; } = [];

    public PrintTemplateWindow(IEnumerable<PrintTemplate> templates)
    {
        InitializeComponent();
        _templates = templates.Select(Clone).ToList();
        try
        {
            using var server = new LocalPrintServer();
            PrinterBox.ItemsSource = server.GetPrintQueues()
                .Select(queue => queue.FullName).OrderBy(x => x).ToArray();
        }
        catch (Exception) { EditorStatus.Text = "Không tìm thấy máy in Windows. Có thể tiếp tục thiết kế mẫu và xem trước."; }
        RefreshTypes(_templates.FirstOrDefault()?.Type);
        _originalTemplates = JsonSerializer.Serialize(_templates);
    }

    private static PrintTemplate Clone(PrintTemplate value) => new()
    {
        Type = value.Type,
        Organization = value.Organization,
        Title = value.Title,
        HeaderNote = value.HeaderNote,
        FooterNote = value.FooterNote,
        Signer1 = value.Signer1,
        Signer2 = value.Signer2,
        Signer3 = value.Signer3,
        FontName = value.FontName,
        FontSize = value.FontSize,
        MarginMm = value.MarginMm,
        CellPadding = value.CellPadding,
        HeaderAlignment = value.HeaderAlignment,
        PaperName = value.PaperName,
        Landscape = value.Landscape,
        CustomWidthMm = value.CustomWidthMm,
        CustomHeightMm = value.CustomHeightMm,
        MarginTopMm = value.MarginTopMm,
        MarginBottomMm = value.MarginBottomMm,
        MarginLeftMm = value.MarginLeftMm,
        MarginRightMm = value.MarginRightMm,
        PrinterName = value.PrinterName,
        Copies = value.Copies,
        FitToPage = value.FitToPage
    };

    private void RefreshTypes(string? selected)
    {
        _loading = true;
        _current = null;
        TypeBox.ItemsSource = _templates.Select(t => t.Type).ToList();
        _loading = false;
        TypeBox.SelectedItem = selected;
        if (TypeBox.SelectedIndex < 0 && _templates.Count > 0) TypeBox.SelectedIndex = 0;
        if (_templates.Count == 0)
        {
            ClearFields();
            PreviewViewer.Document = null;
            EditorStatus.Text = "Chưa có mẫu. Nhập tên loại phiếu và chọn Thêm.";
        }
    }

    private void ClearFields()
    {
        foreach (var input in new[] { OrganizationBox, TitleBox, HeaderNoteBox, FooterNoteBox,
                 Signer1Box, Signer2Box, Signer3Box, FontSizeBox, MarginBox, CellPaddingBox })
            input.Clear();
        FontBox.SelectedIndex = 0;
        AlignBox.SelectedIndex = 1;
        PaperBox.SelectedIndex = 0;
        LandscapeCheck.IsChecked = false;
        CustomWidthBox.Text = "210";
        CustomHeightBox.Text = "297";
        foreach (var box in new[] { TopMarginBox, BottomMarginBox, LeftMarginBox, RightMarginBox }) box.Text = "15";
        PrinterBox.Text = "";
        CopiesBox.Text = "1";
    }

    private void Type_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (TypeBox.SelectedItem is not string name) return;
        // A user can correct invalid values before switching. Warn instead of
        // silently overwriting or losing their unsaved editor changes.
        if (_current is not null)
        {
            try { SaveEditor(); }
            catch (Exception ex)
            {
                _loading = true;
                TypeBox.SelectedItem = _current.Type;
                _loading = false;
                EditorStatus.Text = ex.Message + " (hãy sửa lại trước khi chuyển mẫu).";
                return;
            }
        }
        LoadTemplate(name);
    }

    private void LoadTemplate(string name)
    {
        _current = _templates.FirstOrDefault(t => t.Type == name);
        if (_current is null) return;
        _loading = true;
        OrganizationBox.Text = _current.Organization;
        TitleBox.Text = _current.Title;
        HeaderNoteBox.Text = _current.HeaderNote;
        FooterNoteBox.Text = _current.FooterNote;
        Signer1Box.Text = _current.Signer1;
        Signer2Box.Text = _current.Signer2;
        Signer3Box.Text = _current.Signer3;
        FontSizeBox.Text = _current.FontSize.ToString(CultureInfo.InvariantCulture);
        MarginBox.Text = _current.MarginMm.ToString(CultureInfo.InvariantCulture);
        CellPaddingBox.Text = _current.CellPadding.ToString(CultureInfo.InvariantCulture);
        PaperBox.SelectedIndex = Math.Max(0, PaperBox.Items.Cast<ComboBoxItem>().ToList()
            .FindIndex(x => (string?)x.Tag == _current.PaperName));
        LandscapeCheck.IsChecked = _current.Landscape;
        CustomWidthBox.Text = _current.CustomWidthMm.ToString(CultureInfo.InvariantCulture);
        CustomHeightBox.Text = _current.CustomHeightMm.ToString(CultureInfo.InvariantCulture);
        TopMarginBox.Text = (_current.MarginTopMm ?? _current.MarginMm).ToString(CultureInfo.InvariantCulture);
        BottomMarginBox.Text = (_current.MarginBottomMm ?? _current.MarginMm).ToString(CultureInfo.InvariantCulture);
        LeftMarginBox.Text = (_current.MarginLeftMm ?? _current.MarginMm).ToString(CultureInfo.InvariantCulture);
        RightMarginBox.Text = (_current.MarginRightMm ?? _current.MarginMm).ToString(CultureInfo.InvariantCulture);
        PrinterBox.Text = _current.PrinterName;
        CopiesBox.Text = _current.Copies.ToString(CultureInfo.InvariantCulture);
        CustomPaperPanel.Visibility = _current.PaperName == "Tùy chỉnh" ? Visibility.Visible : Visibility.Collapsed;
        FontBox.SelectedIndex = _current.FontName switch
        {
            "Segoe UI" => 1,
            "Times New Roman" => 2,
            _ => 0
        };
        AlignBox.SelectedIndex = _current.HeaderAlignment switch
        {
            "Left" => 0,
            "Right" => 2,
            _ => 1
        };
        _loading = false;
        EditorStatus.Text = "";
        RefreshPreview();
    }

    private static double ReadNumber(TextBox text, string field)
    {
        if (double.TryParse(text.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            double.TryParse(text.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return value;
        throw new ArgumentException($"Giá trị {field} phải là số.");
    }

    private string CurrentPaperName() => (PaperBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "A4";

    private int ReadCopies()
    {
        if (!int.TryParse(CopiesBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var copies))
            throw new ArgumentException("Số bản in phải là số nguyên.");
        return copies;
    }

    private void Paper_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CustomPaperPanel is null || PaperBox.SelectedItem is null) return;
        var paper = CurrentPaperName();
        CustomPaperPanel.Visibility = paper == "Tùy chỉnh" ? Visibility.Visible : Visibility.Collapsed;
        if (paper is "58mm" or "80mm" &&
            new[] { TopMarginBox, BottomMarginBox, LeftMarginBox, RightMarginBox }
                .All(input => input.Text.Trim() == "15"))
            foreach (var box in new[] { TopMarginBox, BottomMarginBox, LeftMarginBox, RightMarginBox })
                box.Text = "3";
    }

    private void ResetPage_Click(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            PaperBox.SelectedIndex = 0;
            LandscapeCheck.IsChecked = false;
            CustomWidthBox.Text = "210";
            CustomHeightBox.Text = "297";
            foreach (var box in new[] { TopMarginBox, BottomMarginBox, LeftMarginBox, RightMarginBox })
                box.Text = "15";
            PrinterBox.Text = "";
            CopiesBox.Text = "1";
            CustomPaperPanel.Visibility = Visibility.Collapsed;
        }
        finally { _loading = false; }
        Preview_Click(sender, e);
    }

    private void SaveEditor()
    {
        if (_loading || _current is null) return;
        var next = Clone(_current);
        next.Organization = OrganizationBox.Text.Trim();
        next.Title = TitleBox.Text.Trim();
        next.HeaderNote = HeaderNoteBox.Text.Trim();
        next.FooterNote = FooterNoteBox.Text.Trim();
        next.Signer1 = Signer1Box.Text.Trim();
        next.Signer2 = Signer2Box.Text.Trim();
        next.Signer3 = Signer3Box.Text.Trim();
        next.FontName = (FontBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Arial";
        next.FontSize = ReadNumber(FontSizeBox, "cỡ chữ");
        next.MarginMm = ReadNumber(MarginBox, "lề trang");
        next.CellPadding = ReadNumber(CellPaddingBox, "đệm ô");
        next.HeaderAlignment = (AlignBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Center";
        next.PaperName = CurrentPaperName();
        next.Landscape = LandscapeCheck.IsChecked == true;
        next.CustomWidthMm = ReadNumber(CustomWidthBox, "chiều rộng");
        next.CustomHeightMm = ReadNumber(CustomHeightBox, "chiều cao");
        next.MarginTopMm = ReadNumber(TopMarginBox, "lề trên");
        next.MarginBottomMm = ReadNumber(BottomMarginBox, "lề dưới");
        next.MarginLeftMm = ReadNumber(LeftMarginBox, "lề trái");
        next.MarginRightMm = ReadNumber(RightMarginBox, "lề phải");
        next.PrinterName = PrinterBox.Text?.Trim() ?? "";
        next.Copies = ReadCopies();
        PrintTemplateLayout.Validate(next);
        var index = _templates.IndexOf(_current);
        _templates[index] = next;
        _current = next;
    }

    private void RefreshPreview()
    {
        if (_current is null) return;
        try
        {
            PreviewViewer.Document = PrintTemplateLayout.Preview(_current);
            var dimension = PrintPageSetup.Resolve(_current);
            PreviewHeading.Text = $"XEM TRƯỚC · {_current.PaperName} · {(_current.Landscape ? "NGANG" : "DỌC")} · {dimension.WidthMm:0.#} × {dimension.HeightMm:0.#} mm";
            EditorStatus.Text = "Bản xem trước phân trang bằng đúng kích thước đã chọn; chưa in và chưa ghi vào Excel.";
        }
        catch (Exception ex)
        {
            EditorStatus.Text = ex.Message;
        }
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        try { SaveEditor(); RefreshPreview(); }
        catch (Exception ex) { EditorStatus.Text = ex.Message; }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var type = NewTypeBox.Text.Trim();
        if (type.Length == 0 || type.Length > 80)
        {
            EditorStatus.Text = "Nhập tên mẫu từ 1 đến 80 ký tự.";
            return;
        }
        if (_templates.Any(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase)))
        {
            EditorStatus.Text = "Tên mẫu đã tồn tại.";
            return;
        }
        try { SaveEditor(); }
        catch (Exception ex) { EditorStatus.Text = ex.Message; return; }
        _templates.Add(new PrintTemplate { Type = type, Title = type.ToUpperInvariant() });
        NewTypeBox.Clear();
        RefreshTypes(type);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        var type = _current.Type;
        _templates.Remove(_current);
        RefreshTypes(_templates.FirstOrDefault()?.Type);
        EditorStatus.Text = $"Đã bỏ mẫu {type} khỏi danh sách. Nếu loại phiếu nghiệp vụ cần in, hệ thống sử dụng mẫu mặc định khi in.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveEditor();
            foreach (var template in _templates) PrintTemplateLayout.Validate(template);
            Templates = _templates.Select(Clone).ToList();
            this.CompleteEmbedded(true);
        }
        catch (Exception ex) { EditorStatus.Text = ex.Message; }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
