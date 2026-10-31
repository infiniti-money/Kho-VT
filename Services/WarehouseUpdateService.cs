using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace HisaWarehouse.Services;

/// <summary>Checks the official GitHub Release only. A draft/preview artifact is never auto-installed.</summary>
public sealed record WarehouseRelease(Version Version, string Tag, Uri DownloadUrl, string Sha256, long Size);

public static class WarehouseUpdateService
{
    public const string PackageName = "HISA-KhoVT-Windows-x64.zip";
    private const string Owner = "infiniti-money";
    private const string Repository = "Kho-VT";
    private const long MaximumBytes = 1_073_741_824L;

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(8),
        DefaultRequestHeaders = { { "User-Agent", "HISA-KhoVT-Updater/1" } }
    };

    public static Version CurrentVersion =>
        typeof(WarehouseUpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static async Task<WarehouseRelease?> FindLatestAsync(CancellationToken cancel = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null; // No published release yet; this is not a network error.
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean())
            return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!tag.StartsWith('v') || !Version.TryParse(tag[1..], out var version) ||
            version is null || version.CompareTo(CurrentVersion) <= 0)
            return null;

        if (!root.TryGetProperty("assets", out var assets)) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            if (!string.Equals(asset.GetProperty("name").GetString(), PackageName, StringComparison.Ordinal))
                continue;
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
                !uri.AbsolutePath.StartsWith($"/{Owner}/{Repository}/releases/download/{tag}/", StringComparison.Ordinal) ||
                !uri.AbsolutePath.EndsWith("/" + PackageName, StringComparison.Ordinal))
                throw new InvalidDataException("Đường dẫn tải bản cập nhật không đúng nguồn GitHub đã cấu hình.");

            if (!asset.TryGetProperty("digest", out var digestElement) ||
                digestElement.ValueKind != JsonValueKind.String ||
                digestElement.GetString() is not string digest ||
                !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                digest.Length != 71 ||
                !digest[7..].All(Uri.IsHexDigit))
                throw new InvalidDataException(
                    "Bản phát hành không có mã SHA-256 để kiểm tra. HISA từ chối cập nhật tự động.");

            var size = asset.GetProperty("size").GetInt64();
            if (size <= 0 || size > MaximumBytes)
                throw new InvalidDataException("Bản cập nhật có dung lượng không hợp lệ.");

            return new WarehouseRelease(version, tag, uri, digest[7..].ToUpperInvariant(), size);
        }
        return null;
    }

    public static async Task<string> DownloadVerifiedAsync(WarehouseRelease release, CancellationToken cancel = default)
    {
        var targetFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HISA", "QuanLyKho", "Updates", release.Tag);
        Directory.CreateDirectory(targetFolder);
        var target = Path.Combine(targetFolder, PackageName);
        if (File.Exists(target) && await MatchesDigestAsync(target, release.Sha256, cancel))
            return target;

        var temp = Path.Combine(targetFolder, Guid.NewGuid().ToString("N") + ".part");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, release.DownloadUrl);
            using var response = await Http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancel);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long advertised &&
                advertised > MaximumBytes)
                throw new InvalidDataException("Bản cập nhật vượt giới hạn dung lượng 1 GB.");

            await using var source = await response.Content.ReadAsStreamAsync(cancel);
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                             FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                long copied = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, cancel)) > 0)
                {
                    copied += count;
                    if (copied > MaximumBytes)
                        throw new InvalidDataException("Tệp cập nhật quá lớn.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancel);
                }
                await output.FlushAsync(cancel);
                if (copied != release.Size)
                    throw new InvalidDataException("Dung lượng tải về không khớp GitHub Release.");
            }

            if (!await MatchesDigestAsync(temp, release.Sha256, cancel))
                throw new InvalidDataException("Mã SHA-256 không khớp. HISA từ chối cài tệp tải về.");
            File.Move(temp, target, true);
            return target;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static async Task<bool> MatchesDigestAsync(string path, string expected, CancellationToken cancel)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancel);
        return CryptographicOperations.FixedTimeEquals(
            hash, Convert.FromHexString(expected));
    }

    /// <summary>A dry-run probe only: the separate updater checks permissions again after exit.</summary>
    public static bool IsInstallFolderWritable(string path)
    {
        var test = Path.Combine(path, ".hisa-write-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (new FileStream(test, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally
        {
            try { if (File.Exists(test)) File.Delete(test); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static void StartAutomaticInstall(string verifiedZip)
    {
        var installFolder = Path.GetFullPath(AppContext.BaseDirectory);
        var updaterSource = Path.Combine(installFolder, "HISA.Updater.exe");
        if (!File.Exists(updaterSource))
            throw new FileNotFoundException("Bản HISA đang dùng chưa có chương trình cập nhật ngoài.", updaterSource);
        if (!IsInstallFolderWritable(installFolder))
            throw new UnauthorizedAccessException(
                "Thư mục cài đặt cần quyền quản trị. Hãy sử dụng cài đặt thủ công có xác nhận UAC.");

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HISA", "QuanLyKho", "Updates", "Launcher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var updater = Path.Combine(folder, "HISA.Updater.exe");
        File.Copy(updaterSource, updater, false);
        var info = new ProcessStartInfo(updater)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = folder
        };
        info.ArgumentList.Add("--parent-pid");
        info.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        info.ArgumentList.Add("--install-dir");
        info.ArgumentList.Add(installFolder);
        info.ArgumentList.Add("--package");
        info.ArgumentList.Add(Path.GetFullPath(verifiedZip));

        using var process = Process.Start(info) ??
            throw new IOException("Không khởi chạy được chương trình cập nhật.");
    }
}
