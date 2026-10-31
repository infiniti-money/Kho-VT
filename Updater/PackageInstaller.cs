using System.IO.Compression;

namespace HisaWarehouse.Updater;

public static class PackageInstaller
{
    private const long MaximumExpandedBytes = 2_000_000_000L;

    public static void Apply(string installDir, string packagePath)
    {
        if (!Directory.Exists(installDir))
            throw new DirectoryNotFoundException("Thư mục cài đặt HISA không tồn tại.");
        if (!File.Exists(packagePath))
            throw new FileNotFoundException("Không tìm thấy gói cập nhật.", packagePath);

        // All workspace and rollback files live outside the application directory.
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HISA", "QuanLyKho", "Updates");
        var staging = Path.Combine(local, "Work-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(staging, "Payload");
        var backup = Path.Combine(staging, "Rollback");
        Directory.CreateDirectory(payload);
        Directory.CreateDirectory(backup);
        var replaced = new List<string>();
        var added = new List<string>();

        try
        {
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long expanded = 0;
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                        continue;
                    var path = ValidateRelativePath(entry.FullName);
                    if (!seen.Add(path))
                        throw new InvalidDataException("Gói cập nhật chứa đường dẫn trùng nhau.");
                    if ((entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
                        throw new InvalidDataException("Gói cập nhật không được chứa liên kết tượng trưng.");
                    expanded = checked(expanded + entry.Length);
                    if (expanded > MaximumExpandedBytes || entry.Length < 0)
                        throw new InvalidDataException("Gói cập nhật vượt giới hạn giải nén.");
                    var destination = Path.Combine(payload, path);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination, false);
                }
            }

            foreach (var required in new[] { "HISA Quan Ly Kho.exe", "HISA.Updater.exe", "data/initial_data.json" })
                if (!File.Exists(Path.Combine(payload, required.Replace('/', Path.DirectorySeparatorChar))))
                    throw new InvalidDataException($"Gói cập nhật thiếu {required}.");

            var files = Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories)
                .Select(file => (Path: file, Relative: Path.GetRelativePath(payload, file)))
                .ToList();
            foreach (var (path, relative) in files)
            {
                var destination = Path.Combine(installDir, relative);
                if (File.Exists(destination))
                {
                    var existingBackup = Path.Combine(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(existingBackup)!);
                    File.Copy(destination, existingBackup);
                    replaced.Add(relative);
                }
                else
                {
                    added.Add(relative);
                }
            }

            // Each replacement is atomic at file level. Rollback the entire file set
            // if any file copy/rename fails. Never remove unrelated user files.
            try
            {
                foreach (var (source, relative) in files)
                {
                    var destination = Path.Combine(installDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    var temp = destination + ".hisa-update-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        File.Copy(source, temp);
                        File.Move(temp, destination, true);
                    }
                    finally
                    {
                        if (File.Exists(temp)) File.Delete(temp);
                    }
                }
            }
            catch
            {
                var rollbackErrors = new List<Exception>();
                foreach (var relative in replaced)
                {
                    try { File.Copy(Path.Combine(backup, relative), Path.Combine(installDir, relative), true); }
                    catch (Exception ex) { rollbackErrors.Add(ex); }
                }
                foreach (var relative in added)
                {
                    try { File.Delete(Path.Combine(installDir, relative)); }
                    catch (Exception ex) { rollbackErrors.Add(ex); }
                }
                if (rollbackErrors.Count > 0)
                    throw new AggregateException("Cập nhật lỗi và phục hồi một số file không thành công.", rollbackErrors);
                throw;
            }
        }
        finally
        {
            try { Directory.Delete(staging, true); }
            catch (IOException) { /* An antivirus may temporarily keep a file open. */ }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static string ValidateRelativePath(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) ||
            archivePath.Contains('\\') ||
            archivePath.StartsWith('/') ||
            archivePath.Contains(':') ||
            archivePath.Contains('\0'))
            throw new InvalidDataException("Đường dẫn trong gói cập nhật không an toàn.");

        var segments = archivePath.Split('/');
        if (segments.Any(s => s.Length == 0 || s == "." || s == ".." ||
                              s.EndsWith('.') || s.EndsWith(' ')))
            throw new InvalidDataException("Gói cập nhật có đường dẫn bất hợp lệ.");

        if (segments.Any(s => s.Equals("Secrets", StringComparison.OrdinalIgnoreCase) ||
                              s.Equals("WebView2", StringComparison.OrdinalIgnoreCase) ||
                              s.Equals("UserData", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Gói cập nhật không được chạm dữ liệu người dùng.");

        var last = segments[^1];
        if (last.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
            last.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase) ||
            last.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) ||
            last.Equals("gemini-key.dat", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Gói cập nhật không được chứa file Excel hoặc khóa AI.");

        return Path.Combine(segments);
    }
}
