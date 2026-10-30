using System.Windows;
using System.Windows.Controls;

namespace HisaWarehouse;

public sealed class PendingMovementRow
{
    public PendingMovement Movement { get; init; } = new();
    public string DateText => Movement.Date.ToString("dd/MM/yyyy");
    public string DocNo => Movement.DocNo;
    public string Type => Movement.Type;
    public int LineCount => Movement.Lines.Count;
    public string Warehouse => Movement.Warehouse;
    public string ToWarehouse => Movement.ToWarehouse;
    public string Partner => Movement.Partner;
    public string Operator => Movement.Operator;
    public string Note => Movement.Note;
}

public partial class PendingMovementsWindow : Window
{
    public PendingMovement? SelectedMovement { get; private set; }
    public string Action { get; private set; } = "";

    public PendingMovementsWindow(IEnumerable<PendingMovement> pending)
    {
        InitializeComponent();
        var rows = pending.Where(movement => movement.Status == "Chờ xác nhận").OrderByDescending(movement => movement.CreatedAt).Select(movement => new PendingMovementRow { Movement = movement }).ToList();
        PendingGrid.ItemsSource = rows;
        CountText.Text = $"{rows.Count:N0} phiếu chờ xác nhận";
    }

    private PendingMovement? SelectMovement() => (PendingGrid.SelectedItem as PendingMovementRow)?.Movement;
    private void Confirm_Click(object sender, RoutedEventArgs e) => Complete("confirm");
    private void CancelPending_Click(object sender, RoutedEventArgs e) => Complete("cancel");
    private void Complete(string action)
    {
        SelectedMovement = SelectMovement();
        if (SelectedMovement is null)
        {
            this.NotifyInline("Chọn một phiếu trong danh sách.");
            return;
        }
        Action = action;
        this.CompleteEmbedded(true);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => this.CompleteEmbedded(false);
}
