using System.Windows;

namespace HisaWarehouse;

public partial class PrintTemplateWindow : Window
{
    private readonly List<PrintTemplate> _templates;
    private PrintTemplate? _current;
    private bool _loading;
    public List<PrintTemplate> Templates { get; private set; } = [];

    public PrintTemplateWindow(IEnumerable<PrintTemplate> templates)
    {
        InitializeComponent();
        _templates = templates.Select(template => new PrintTemplate
        {
            Type = template.Type,
            Organization = template.Organization,
            Title = template.Title,
            HeaderNote = template.HeaderNote,
            FooterNote = template.FooterNote,
            Signer1 = template.Signer1,
            Signer2 = template.Signer2,
            Signer3 = template.Signer3
        }).ToList();
        TypeBox.ItemsSource = _templates.Select(template => template.Type).ToList();
        TypeBox.SelectedIndex = 0;
        if (TypeBox.SelectedItem is string type) LoadTemplate(type);
    }

    private void Type_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (TypeBox.SelectedItem is not string type) return;
        if (_current is not null) SaveEditor();
        LoadTemplate(type);
    }

    private void LoadTemplate(string type)
    {
        _current = _templates.FirstOrDefault(template => template.Type == type);
        if (_current is null) return;
        _loading = true;
        OrganizationBox.Text = _current.Organization;
        TitleBox.Text = _current.Title;
        HeaderNoteBox.Text = _current.HeaderNote;
        FooterNoteBox.Text = _current.FooterNote;
        Signer1Box.Text = _current.Signer1;
        Signer2Box.Text = _current.Signer2;
        Signer3Box.Text = _current.Signer3;
        _loading = false;
    }

    private void SaveEditor()
    {
        if (_loading || _current is null) return;
        _current.Organization = OrganizationBox.Text.Trim();
        _current.Title = TitleBox.Text.Trim();
        _current.HeaderNote = HeaderNoteBox.Text.Trim();
        _current.FooterNote = FooterNoteBox.Text.Trim();
        _current.Signer1 = Signer1Box.Text.Trim();
        _current.Signer2 = Signer2Box.Text.Trim();
        _current.Signer3 = Signer3Box.Text.Trim();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveEditor();
        if (_templates.Any(template => string.IsNullOrWhiteSpace(template.Title)))
        {
            this.NotifyInline("Mỗi mẫu in cần có tiêu đề.");
            return;
        }
        Templates = _templates;
        this.CompleteEmbedded(true);
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
