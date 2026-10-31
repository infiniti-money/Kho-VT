using System.Diagnostics;
using System.IO.Compression;
using HisaWarehouse.Updater;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test")
            return RunSelfTest();

        try
        {
            if (args.Length != 6 ||
                args[0] != "--parent-pid" ||
                args[2] != "--install-dir" ||
                args[4] != "--package" ||
                !int.TryParse(args[1], out var parentId) || parentId <= 0)
                throw new ArgumentException("Tham số cập nhật không hợp lệ.");

            var install = Path.GetFullPath(args[3]);
            var package = Path.GetFullPath(args[5]);
            var exe = Path.Combine(install, "HISA Quan Ly Kho.exe");
            if (!File.Exists(exe))
                throw new FileNotFoundException("Không tìm thấy ứng dụng HISA trong thư mục cài.", exe);

            // Avoid overlapping updater processes.
            using var mutex = new Mutex(false, @"Local\HISA-KhoVT-Updater", out var isFirst);
            if (!isFirst)
                throw new InvalidOperationException("Đã có một tiến trình cập nhật HISA đang chạy.");

            try
            {
                using var parent = Process.GetProcessById(parentId);
                if (!parent.WaitForExit(120_000))
                    throw new TimeoutException("HISA chưa thoát sau 120 giây. Không thay file đang sử dụng.");
            }
            catch (ArgumentException) { /* The parent already exited. */ }

            PackageInstaller.Apply(install, package);
            WriteStatus("Cập nhật thành công. Ứng dụng đang được khởi động lại.");
            var start = new ProcessStartInfo(exe)
            {
                WorkingDirectory = install,
                UseShellExecute = true
            };
            Process.Start(start);
            return 0;
        }
        catch (Exception ex)
        {
            // User data and Gemini API keys are never recorded.
            WriteStatus("Cập nhật không hoàn tất: " + ex.Message);
            return 1;
        }
    }

    private static void WriteStatus(string message)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HISA", "QuanLyKho", "Updates");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "last-update.txt"),
                DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") +
                Environment.NewLine + message);
        }
        catch { /* The update must not crash while writing a local status notice. */ }
    }

    private static int RunSelfTest()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "hisa-updater-smoke-" + Guid.NewGuid().ToString("N"));
        var install = Path.Combine(workspace, "install");
        Directory.CreateDirectory(install);
        var pkg = Path.Combine(workspace, "update.zip");
        try
        {
            File.WriteAllText(Path.Combine(install, "HISA Quan Ly Kho.exe"), "old-version");
            File.WriteAllText(Path.Combine(install, "KHO.xlsx"), "original-excel-untouched");
            using (var archive = ZipFile.Open(pkg, ZipArchiveMode.Create))
            {
                void Add(string entryName, string data)
                {
                    using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
                    writer.Write(data);
                }
                Add("HISA Quan Ly Kho.exe", "new-version");
                Add("HISA.Updater.exe", "helper-version");
                Add("data/initial_data.json", "{ }");
            }
            PackageInstaller.Apply(install, pkg);
            if (File.ReadAllText(Path.Combine(install, "HISA Quan Ly Kho.exe")) != "new-version" ||
                File.ReadAllText(Path.Combine(install, "KHO.xlsx")) != "original-excel-untouched" ||
                !File.Exists(Path.Combine(install, "data", "initial_data.json")))
                throw new Exception("File deployment changed a user workbook or lost app files.");

            foreach (var danger in new[] { "../escape.exe", "Secrets/gemini-key.dat",
                         "WebView2/profile.db", "KHO.xlsx", "C:/Windows/bad.exe", "folder\\escape.exe" })
            {
                try
                {
                    PackageInstaller.ValidateRelativePath(danger);
                    throw new Exception("Unsafe archive entry accepted: " + danger);
                }
                catch (InvalidDataException) { }
            }

            Console.WriteLine("PASS: Update, preserve Excel and reject 6 unsafe paths.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL updater smoke test: " + ex);
            return 1;
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch (IOException) { }
        }
    }
}
