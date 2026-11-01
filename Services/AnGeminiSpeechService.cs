using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using NAudio.Wave;

namespace HisaWarehouse.Services;

/// <summary>
/// One-shot, in-memory TTS using the Gemini API, with a female Northern
/// Vietnamese delivery request. Actual accent depends on the model/voice.
/// No audio files are saved or written to the user's workbook.
/// </summary>
public static class AnGeminiSpeechService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(90) };
    private const string Model = "gemini-3.8-flash-lite-tts";

    public static async Task SpeakAsync(string apiKey, string text,
        CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var audio = await SynthesizeAsync(apiKey, text, cancel);
        cancel.ThrowIfCancellationRequested();
        using var memory = new MemoryStream(audio, writable: false);
        using var reader = new WaveFileReader(memory);
        if (reader.WaveFormat.Encoding != WaveFormatEncoding.Pcm ||
            reader.WaveFormat.Channels != 1 || reader.WaveFormat.BitsPerSample != 16)
            throw new InvalidDataException("Gemini trả định dạng giọng nói không được hỗ trợ.");
        using var playback = new WaveOutEvent { DeviceNumber = -1 };
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        playback.PlaybackStopped += (_, e) =>
        {
            if (e.Exception is not null) completion.TrySetException(e.Exception);
            else completion.TrySetResult();
        };
        playback.Init(reader);
        using var registration = cancel.Register(() =>
        {
            try { playback.Stop(); } catch { }
        });
        playback.Play();
        await completion.Task.WaitAsync(cancel);
    }

    private static async Task<byte[]> SynthesizeAsync(string apiKey, string text, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            contents = new[] { new { role = "user", parts = new[] { new
            {
                text = text.Length > 2800 ? text[..2800] : text,
                speechMetadata = new
                {
                    style = "Nữ Việt Nam, giọng Hà Nội chuẩn miền Bắc, dịu dàng, ấm áp, " +
                        "tốc độ vừa phải, rõ từng chữ, đọc đúng số lượng vật tư."
                }
            } } } },
            generationConfig = new
            {
                responseModalities = new[] { "AUDIO" },
                speechConfig = new
                {
                    languageCode = "vi-VN",
                    voiceConfig = new { voice = "Leda" }
                }
            }
        });
        using var response = await Client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancel);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Gemini TTS HTTP {(int)response.StatusCode}. " +
                "Hãy kiểm tra quyền sử dụng giọng đọc, kết nối và hạn mức API.");

        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);
        if (!json.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.GetArrayLength() == 0 ||
            !candidates[0].TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts))
            throw new InvalidDataException("Gemini chưa tạo được âm thanh.");

        foreach (var part in parts.EnumerateArray())
        {
            if (!part.TryGetProperty("inlineData", out var inline) ||
                !inline.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.String) continue;
            var wav = Convert.FromBase64String(data.GetString() ?? "");
            if (wav.Length < 44 || wav.Length > 20_000_000 ||
                System.Text.Encoding.ASCII.GetString(wav, 0, 4) != "RIFF")
                throw new InvalidDataException("Gemini trả âm thanh không phải WAV hợp lệ.");
            return wav;
        }
        throw new InvalidDataException("Gemini không trả tệp giọng nói WAV.");
    }
}
