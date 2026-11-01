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
            "https://generativelanguage.googleapis.com/v1beta/interactions");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(BuildSpeechRequest(text));
        using var response = await Client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cancel);
        if (!response.IsSuccessStatusCode)
            throw GeminiAssistantClient.ApiFailure(response.StatusCode, "đọc giọng An");
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);
        return ExtractWav(json.RootElement);
    }

    public static object BuildSpeechRequest(string text) => new
    {
        model = Model,
        input = new[] { new
        {
            type = "user_input",
            content = new[] { new
            {
                type = "text",
                text = text.Length > 2800 ? text[..2800] : text,
                annotations = new[] { new
                {
                    type = "speech_metadata",
                    style = "Nữ tiếng Việt, giọng Hà Nội miền Bắc, dịu dàng và ấm áp, " +
                            "tốc độ vừa phải, phát âm rõ số lượng vật tư."
                } }
            } }
        } },
        response_format = new { type = "audio", mime_type = "audio/wav" },
        generation_config = new { speech_config = new[] { new { voice = "Leda" } } }
    };

    public static byte[] ExtractWav(JsonElement root)
    {
        if (!root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Gemini không trả bước âm thanh.");
        foreach (var step in steps.EnumerateArray())
        {
            if (!step.TryGetProperty("type", out var type) || type.GetString() != "model_output" ||
                !step.TryGetProperty("content", out var parts) || parts.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var part in parts.EnumerateArray())
            {
                if (!part.TryGetProperty("type", out var kind) || kind.GetString() != "audio" ||
                    !part.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.String)
                    continue;
                var wav = Convert.FromBase64String(data.GetString() ?? "");
                if (wav.Length < 44 || wav.Length > 20_000_000 ||
                    System.Text.Encoding.ASCII.GetString(wav, 0, 4) != "RIFF" ||
                    System.Text.Encoding.ASCII.GetString(wav, 8, 4) != "WAVE")
                    throw new InvalidDataException("Gemini trả âm thanh WAV không hợp lệ.");
                return wav;
            }
        }
        throw new InvalidDataException("Gemini chưa gửi giọng đọc trong phản hồi.");
    }
}
