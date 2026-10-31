using System.IO;

namespace HisaWarehouse.Services;

/// <summary>
/// Captures a saved Excel workbook into memory using compatible sharing flags.
/// Reading the snapshot never takes a write lock or changes the original file.
/// Unsaved changes in a running Excel instance are intentionally not visible.
/// </summary>
internal static class ExcelReadSnapshot
{
    public static MemoryStream Capture(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path))
            throw new FileNotFoundException("Hãy chọn tệp Excel .xlsx đã được lưu trên ổ đĩa.", path);

        // Excel normally keeps the file open with write access. FileShare.Read
        // can reject an existing writer; ReadWrite|Delete allows a safe snapshot.
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var originalLength = source.Length;
        var originalWriteTime = File.GetLastWriteTimeUtc(path);
        var snapshot = new MemoryStream();
        try
        {
            source.CopyTo(snapshot);
            if (snapshot.Length != originalLength || source.Length != originalLength ||
                File.GetLastWriteTimeUtc(path) != originalWriteTime)
            {
                throw new IOException("Tệp Excel đang được lưu hoặc thay đổi trong khi đọc. Hãy lưu xong rồi thử lại.");
            }

            snapshot.Position = 0;
            return snapshot;
        }
        catch
        {
            snapshot.Dispose();
            throw;
        }
    }
}
