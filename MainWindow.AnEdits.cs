using System.IO;
using System.Windows;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class MainWindow
{
    private void ReviewAnCatalogEdit(AnCatalogEditService.Plan plan)
    {
        if (_state is null || Volatile.Read(ref _activeExcelCommits) > 0)
        {
            ShowToast("An chưa thể sửa Excel trong khi thao tác kho khác đang chạy.");
            return;
        }
        // A hidden assistant cannot display an approval dialog reliably.
        // Bring HISA back in front before displaying any proposed file edit.
        if (!IsVisible || WindowState == WindowState.Minimized)
            RestoreFromAnTray();
        ConfirmInApp(plan.Summary + "\n\nXác nhận CHỈ sửa ô này?", accepted =>
        {
            if (accepted) _ = CommitAnCatalogEditAsync(plan);
            else ShowToast("Bạn không duyệt. An không thay đổi file Excel.");
        }, "Duyệt sửa Excel", "Hủy");
    }

    private async Task CommitAnCatalogEditAsync(AnCatalogEditService.Plan plan)
    {
        Interlocked.Increment(ref _activeExcelCommits);
        await _excelMovementWriteGate.WaitAsync();
        try
        {
            var result = await Task.Run(() => AnCatalogEditService.Apply(plan));
            // Only update HISA's cache after successful atomic Excel replacement.
            var item = State.Products.FirstOrDefault(x =>
                x.Sku.Equals(plan.Sku, StringComparison.OrdinalIgnoreCase));
            if (item is null)
                throw new InvalidOperationException("Excel đã ghi nhưng HISA thiếu mã vật tư. Hãy đối chiếu lại.");
            if (plan.Target == AnCatalogEditService.Field.Name)
                item.Name = plan.NewValue;
            else
            {
                item.MinStock = plan.NumericValue!.Value;
                item.MinStockDefined = true;
            }
            State.PrimarySyncFingerprint = result.WorkbookHash;
            State.PrimaryExcelSyncedWriteUtc = File.GetLastWriteTimeUtc(plan.Path);
            if (!await PersistAsync(false))
                throw new IOException("Excel đã lưu, nhưng HISA chưa ghi được cache. " +
                    "Không lập phiếu mới, hãy đồng bộ Excel lại.");
            RenderAll();
            MessageBox.Show(this,
                $"An đã sửa Danh mục!{(plan.Target == AnCatalogEditService.Field.Name ? "B" : "E")}{plan.Row}.\n" +
                $"Mã: {plan.Sku}\nGiá trị mới: {plan.NewValue}\n" +
                $"Bản sao lưu trước sửa: {result.BackupPath}\n\n" +
                "Hãy mở Excel, tính lại công thức và lưu trước lần đồng bộ tiếp theo.",
                "An · Đã lưu thay đổi có duyệt",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "An chưa thể hoàn tất thao tác: " + ex.Message +
                "\nNếu file Excel đã ghi, hãy kiểm tra bản sao lưu và đối chiếu lại trước khi lập phiếu.",
                "An · Bảo vệ Excel", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _excelMovementWriteGate.Release();
            Interlocked.Decrement(ref _activeExcelCommits);
        }
    }
}
