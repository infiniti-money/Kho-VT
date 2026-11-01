using System.IO;
using NAudio.Wave;

namespace HisaWarehouse.Services;

public sealed record MicrophoneDeviceOption(int DeviceNumber, string Name)
{
    public string Label => DeviceNumber < 0 ? "Mặc định của Windows" : $"{Name} (#{DeviceNumber + 1})";
}

public static class MicrophoneDevices
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HISA", "QuanLyKho", "Settings", "microphone.txt");

    public static List<MicrophoneDeviceOption> List()
    {
        var result = new List<MicrophoneDeviceOption> { new(-1, "Mặc định của Windows") };
        for (var device = 0; device < WaveIn.DeviceCount; device++)
        {
            try
            {
                var capabilities = WaveIn.GetCapabilities(device);
                result.Add(new MicrophoneDeviceOption(device, capabilities.ProductName));
            }
            catch { /* Device may have been unplugged during enumeration. */ }
        }
        return result;
    }

    public static int ResolveSavedDevice(IReadOnlyList<MicrophoneDeviceOption> devices)
    {
        if (!File.Exists(PreferencePath)) return -1;
        try
        {
            var stored = File.ReadAllText(PreferencePath).Trim();
            if (stored == "default") return -1;
            // Save exact name and index, so identical USB headsets remain distinct
            // while reordered devices can still be recovered by name.
            var parts = stored.Split('|', 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var number)) return -1;
            var match = devices.FirstOrDefault(d => d.DeviceNumber == number && d.Name == parts[1])
                ?? devices.FirstOrDefault(d => d.DeviceNumber >= 0 && d.Name == parts[1]);
            return match?.DeviceNumber ?? -1;
        }
        catch (IOException) { return -1; }
        catch (UnauthorizedAccessException) { return -1; }
    }

    public static void Save(MicrophoneDeviceOption option)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
        var content = option.DeviceNumber < 0 ? "default" :
            $"{option.DeviceNumber}|{option.Name.Replace('|', ' ')}";
        var temp = PreferencePath + ".tmp";
        try
        {
            File.WriteAllText(temp, content);
            File.Move(temp, PreferencePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Validate(int deviceNumber)
    {
        if (deviceNumber == -1)
        {
            if (WaveIn.DeviceCount == 0)
                throw new InvalidOperationException("Windows chưa phát hiện thiết bị micro. Hãy kết nối micro trước.");
            return;
        }
        if (deviceNumber < 0 || deviceNumber >= WaveIn.DeviceCount)
            throw new InvalidOperationException("Micro đã chọn không còn được kết nối. Bấm Làm mới micro và chọn lại.");
    }
}
