using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;

namespace HisaWarehouse.Services;

public static class ImageDataService
{
    private const int MaxImageBytes = 160 * 1024;
    private static readonly ConcurrentDictionary<string, BitmapImage> Cache = new(StringComparer.Ordinal);

    public static string EncodeFile(string path)
    {
        BitmapImage Load(int pixelWidth)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = pixelWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }

        foreach (var (width, quality) in new[] { (640, 76), (480, 66), (360, 56) })
        {
            using var stream = new MemoryStream();
            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(Load(width)));
            encoder.Save(stream);
            if (stream.Length <= MaxImageBytes)
                return "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray());
        }
        throw new InvalidDataException("Ảnh vẫn lớn hơn 220 KB sau khi nén. Chọn ảnh nhỏ hơn.");
    }

    public static BitmapImage? Decode(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        return Cache.GetOrAdd(dataUrl, value =>
        {
            var comma = value.IndexOf(',');
            var bytes = Convert.FromBase64String(comma >= 0 ? value[(comma + 1)..] : value);
            using var stream = new MemoryStream(bytes, writable: false);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 320;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        });
    }

    public static int TotalBytes(IEnumerable<InventoryItem> products) => products
        .Where(item => !string.IsNullOrWhiteSpace(item.ImageDataUrl))
        .Sum(item => (item.ImageDataUrl!.Length * 3) / 4);
}
