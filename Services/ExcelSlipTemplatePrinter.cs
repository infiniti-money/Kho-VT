using System.IO;
using System.Runtime.InteropServices;
using HisaWarehouse.Services;

namespace HisaWarehouse.Services;

/// <summary>
/// Renders the user's actual "Phiếu in" Excel sheet to PDF with Excel itself.
/// The source KHO.xlsx is NEVER changed: selectors B/C4 are assigned only in
/// an isolated temporary workbook copy. Requires Microsoft Excel for Windows.
/// </summary>
public static class ExcelSlipTemplatePrinter
{
    public sealed record Preview(string PdfPath, string WorkDirectory) : IDisposable
    {
        public void Dispose()
        {
            try { Directory.Delete(WorkDirectory, recursive: true); }
            catch (IOException) { /* PDF viewer may still hold it; retry on app exit. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static Task<Preview> CreatePreviewAsync(
        string source, string docNo, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<Preview>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(CreatePreview(source, docNo, cancellationToken)); }
            catch (Exception ex) { completion.TrySetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA); // Required by Excel COM automation
        thread.IsBackground = true;
        thread.Start();
        return completion.Task;
    }

    private static Preview CreatePreview(string path, string docNo, CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(docNo)) throw new ArgumentException("Chưa có số phiếu để in.");
        cancel.ThrowIfCancellationRequested();
        // Snapshot is safe even when Excel has the workbook open for reading.
        using var snapshot = ExcelReadSnapshot.Capture(path);
        var folder = Path.Combine(Path.GetTempPath(), "HISA-ExcelPrint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var copy = Path.Combine(folder, "PrintTemplate.xlsx");
        var pdf = Path.Combine(folder, "Phieu-in.pdf");
        using (var file = new FileStream(copy, FileMode.CreateNew, FileAccess.Write))
            snapshot.CopyTo(file);
        object? appObject = null, bookObject = null;
        try
        {
            var excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType is null)
                throw new NotSupportedException("Để in theo sheet Phiếu in gốc, máy cần cài Microsoft Excel.");
            appObject = Activator.CreateInstance(excelType)
                ?? throw new NotSupportedException("Không khởi động được Microsoft Excel.");
            dynamic excel = appObject;
            excel.Visible = false;
            excel.DisplayAlerts = false;
            excel.AskToUpdateLinks = false;
            excel.AutomationSecurity = 3; // msoAutomationSecurityForceDisable
            cancel.ThrowIfCancellationRequested();

            // Work only on a temporary xlsx. Never save changes to KHO.xlsx.
            bookObject = excel.Workbooks.Open(copy, UpdateLinks: 0, ReadOnly: false);
            dynamic book = bookObject;
            dynamic transactions = book.Worksheets["Nhập Xuất"];
            dynamic print = book.Worksheets["Phiếu in"];
            var last = (int)transactions.UsedRange.Rows.Count;
            string token = "[HISA:" + docNo + "]";
            string selector = "HISA-" + docNo;
            int selected = 0;
            for (int row = 5; row <= last; row++)
            {
                cancel.ThrowIfCancellationRequested();
                string note = Convert.ToString(transactions.Range["L" + row].Value2) ?? "";
                if (!note.Contains(token, StringComparison.Ordinal)) continue;
                transactions.Range["B" + row].Value2 = selector;
                selected++;
            }
            if (selected == 0)
                throw new InvalidDataException("Chưa tìm thấy phiếu trong Excel. Hãy lưu/duyệt phiếu trước khi in.");
            if (selected > 20)
                throw new InvalidDataException(
                    "Mẫu Phiếu in gốc chỉ có 20 dòng chi tiết (dòng 8–27). " +
                    "Hãy mở rộng mẫu trong Excel trước khi in phiếu có hơn 20 mặt hàng.");
            print.Range["C4"].Value2 = selector;
            excel.CalculateFullRebuild();
            cancel.ThrowIfCancellationRequested();
            // Use the exact template's existing print area, merged cells, layout,
            // fonts, borders and built-in formulas, not a recreated WPF receipt.
            print.ExportAsFixedFormat(0, pdf);
            if (!File.Exists(pdf) || new FileInfo(pdf).Length == 0)
                throw new IOException("Excel chưa xuất được PDF từ sheet Phiếu in.");
            return new Preview(pdf, folder);
        }
        catch
        {
            try { Directory.Delete(folder, recursive: true); } catch { }
            throw;
        }
        finally
        {
            if (bookObject is not null)
            {
                try { ((dynamic)bookObject).Close(false); } catch { }
                try { Marshal.FinalReleaseComObject(bookObject); } catch { }
            }
            if (appObject is not null)
            {
                try { ((dynamic)appObject).Quit(); } catch { }
                try { Marshal.FinalReleaseComObject(appObject); } catch { }
            }
            // The copied workbook is no longer needed once PDF has been rendered.
            try { if (File.Exists(copy)) File.Delete(copy); } catch { }
        }
    }
}
