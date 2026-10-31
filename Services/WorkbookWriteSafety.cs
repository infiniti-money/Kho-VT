using System.IO;

namespace HisaWarehouse.Services;

public enum WorkbookWriteStatus
{
    Available,
    NotSelected,
    Missing,
    InvalidFormat,
    ReadOnly,
    ExcelLockPresent,
    BusyOrDenied
}

public sealed record WorkbookWriteInspection(WorkbookWriteStatus Status, string Message)
{
    /// <summary>A snapshot of access, NOT a reserved write lock.</summary>
    public bool CanAttemptWrite => Status == WorkbookWriteStatus.Available;
}

/// <summary>
/// A read-only, non-destructive PRECHECK. The eventual writer must reacquire
/// an exclusive file handle and compare a fresh version/hash BEFORE committing.
/// This check alone cannot authorize or guarantee a safe write.
/// </summary>
public static class WorkbookWriteSafety
{
    public static WorkbookWriteInspection Inspect(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new(WorkbookWriteStatus.NotSelected, "Chưa chọn file KHO.xlsx.");

        string file;
        try { file = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new(WorkbookWriteStatus.InvalidFormat, "Đường dẫn file Excel không hợp lệ.");
        }

        if (!Path.GetExtension(file).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(file).StartsWith("~$", StringComparison.Ordinal))
            return new(WorkbookWriteStatus.InvalidFormat, "Chỉ sử dụng file Excel .xlsx chính, không dùng file khóa tạm ~$.");

        if (!File.Exists(file))
            return new(WorkbookWriteStatus.Missing, "File kho chính không tồn tại hoặc không thể truy cập.");

        // Microsoft Excel generally creates a ~$ owner file while a workbook is open.
        // A stale owner file also blocks writes by design: fail safely.
        var ownerFile = Path.Combine(Path.GetDirectoryName(file)!, "~$" + Path.GetFileName(file));
        if (File.Exists(ownerFile))
            return new(WorkbookWriteStatus.ExcelLockPresent,
                "Phát hiện file khóa của Excel. HISA chỉ được đọc; hãy lưu và đóng Excel trước khi ghi.");

        try
        {
            if ((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0)
                return new(WorkbookWriteStatus.ReadOnly, "File được đặt thuộc tính chỉ đọc. HISA không được ghi.");

            // This deliberately acquires and immediately releases the exclusive handle.
            // The actual writer must acquire its own handle again at commit time.
            using var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return new(WorkbookWriteStatus.Available,
                "Không phát hiện xung đột tại thời điểm kiểm tra. Chế độ ghi hai chiều chưa được bật.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(WorkbookWriteStatus.BusyOrDenied,
                "Excel hoặc ứng dụng khác đang sử dụng file, hoặc tài khoản không có quyền ghi. HISA chỉ đọc.");
        }
    }
}
