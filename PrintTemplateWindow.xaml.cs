using System.Globalization;
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
                ((AlignBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Center") == _current.HeaderAlignment;
        }
        catch (ArgumentException) { return false; } // Invalid uncommitted numeric edit.
    }
    public List<PrintTemplate> Templates { get; private set; } = [];

    public PrintTemplateWindow(IEnumerable<PrintTemplate> templates)
    {
        InitializeComponent();
        _templates = templates.Select(Clone).ToList();
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
        HeaderAlignment = value.HeaderAlignment
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
            EditorStatus.Text = "Bản xem trước sử dụng dữ liệu minh họa, chưa in và chưa ghi vào Excel.";
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
