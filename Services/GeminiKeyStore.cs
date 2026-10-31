using System.Security.Cryptography;
using System.IO;
using System.Text;

namespace HisaWarehouse.Services;

/// <summary>
/// The Gemini API key belongs to the current Windows user, never to the
/// installed application's directory or the warehouse JSON backup.
/// </summary>
public static class GeminiKeyStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HISA", "QuanLyKho", "Secrets", "gemini-key.dat");

    public static bool HasKey => File.Exists(FilePath);

    public static void Save(string apiKey)
    {
        var value = apiKey.Trim();
        if (value.Length < 16 || value.Any(char.IsWhiteSpace))
            throw new ArgumentException("Khóa API Gemini không hợp lệ.");
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null,
            DataProtectionScope.CurrentUser);
        var temp = FilePath + ".tmp";
        try
        {
            File.WriteAllBytes(temp, encrypted);
            File.Move(temp, FilePath, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    public static string Load()
    {
        if (!File.Exists(FilePath))
            throw new InvalidOperationException("Chưa lưu khóa API Gemini trên máy tính này.");
        var encrypted = File.ReadAllBytes(FilePath);
        try
        {
            var clear = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(clear); }
            finally { CryptographicOperations.ZeroMemory(clear); }
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(
                "Không mở được khóa Gemini của người dùng Windows hiện tại. Hãy nhập lại khóa.");
        }
    }

    public static void Remove()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
