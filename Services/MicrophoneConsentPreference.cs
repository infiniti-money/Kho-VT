using System.IO;

namespace HisaWarehouse.Services;

/// <summary>
/// The user may explicitly remember cloud microphone consent in Settings.
/// This never grants Windows microphone permission and never starts recording
/// without pressing Mic / Hands-free in the visible assistant.
/// </summary>
public static class MicrophoneConsentPreference
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HISA", "QuanLyKho", "Settings", "microphone-cloud-consent.txt");

    public static bool RememberAllowed
    {
        get
        {
            try { return File.Exists(FilePath) && File.ReadAllText(FilePath).Trim() == "enabled"; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

    public static void SetRememberAllowed(bool allowed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, allowed ? "enabled" : "disabled");
            File.Move(temp, FilePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
