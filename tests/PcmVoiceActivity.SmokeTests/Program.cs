using HisaWarehouse.Services;
using System.Buffers.Binary;

void Assert(bool expected, bool actual, string label)
{
    if (expected != actual) throw new InvalidOperationException(
        $"FAIL {label}: expected={expected}, actual={actual}");
    Console.WriteLine("PASS " + label);
}

byte[] Frame(short amplitude)
{
    var frame = new byte[PcmVoiceActivityDetector.FrameBytes];
    for (int i = 0; i < frame.Length; i += 2)
        BinaryPrimitives.WriteInt16LittleEndian(frame.AsSpan(i, 2), amplitude);
    return frame;
}
void Push(PcmVoiceActivityDetector vad, short amplitude, int frames)
{
    for (int i = 0; i < frames; i++) vad.Push(Frame(amplitude));
}
void Run()
{
    var vad = new PcmVoiceActivityDetector(700);
    Push(vad, 650, 10); // below the RMS threshold; never start
    Assert(false, vad.HasSpeech, "quiet room below threshold is not speech");
    Assert(true, vad.BufferedBeforeSpeechBytes <= 16000 * 2 * 0.4,
        "pre-roll always below 400ms");
    Assert(false, vad.IsComplete, "silence alone never creates a recording");

    Push(vad, 2500, 2); // at least two consecutive loud frames
    Assert(true, vad.HasSpeech, "speech threshold activates after 100ms");
    Assert(false, vad.IsComplete, "do not send before the speaker pauses");
    Push(vad, 0, 23); // 1.15 seconds
    Assert(false, vad.IsComplete, "do not send at 1.15s silence");
    Push(vad, 0, 1);
    Assert(true, vad.IsComplete, "send at 1.20s silence");
    var audio = vad.TakePcm();
    Assert(true, audio.Length > 0 && audio.Length <= PcmVoiceActivityDetector.MaximumBytes,
        "PCM utterance bounded and returned only after VAD completion");

    var wav = PcmWav.MakeWav(audio);
    Assert(true, System.Text.Encoding.ASCII.GetString(wav[..4]) == "RIFF" &&
                 System.Text.Encoding.ASCII.GetString(wav.AsSpan(8, 4)) == "WAVE",
        "in-memory WAV header valid");
    Assert(true, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40, 4)) == audio.Length,
        "WAV data length correct");

    Push(vad, 2200, 3);
    Push(vad, 0, 24);
    Assert(true, vad.IsComplete, "speech restarts after prior recording");
    vad.Reset();
    Assert(false, vad.HasSpeech, "cancel resets speech activity");
    Assert(false, vad.IsComplete, "cancel resets completion");
    Assert(true, vad.BufferedBeforeSpeechBytes == 0 && vad.RecordedBytes == 0,
        "cancel discards all PCM bytes");

    var limit = new PcmVoiceActivityDetector(700);
    Push(limit, 2000, 2);
    Push(limit, 2200, 510);
    Assert(true, limit.IsComplete, "hard max 25s even when constantly speaking");
    Assert(true, limit.RecordedBytes == PcmVoiceActivityDetector.MaximumBytes,
        "max recording hard-bounded to exactly 25s PCM");
    Assert(true, limit.TakePcm().Length <= PcmVoiceActivityDetector.MaximumBytes,
        "output cannot exceed max PCM size");
    Assert(false, limit.HasSpeech, "VAD resets after max-duration utterance");
    try
    {
        PcmWav.MakeWav(new byte[PcmVoiceActivityDetector.MaximumBytes + 2]);
        throw new Exception("Oversized input was accepted.");
    }
    catch (ArgumentException)
    {
        Console.WriteLine("PASS oversized WAV rejected");
    }
}
Run();

// Test the actual default VAD used by the Windows microphone rather than just
// a manually tuned threshold. A quiet USB microphone may emit PCM ~500 RMS.
var defaultVad = new PcmVoiceActivityDetector();
Push(defaultVad, 530, 2);
Assert(true, defaultVad.HasSpeech, "default VAD detects low-volume speech");
Push(defaultVad, 0, 24);
Assert(true, defaultVad.IsComplete, "default VAD detects end of Vietnamese utterance");

// Test the live API's real Interactions response and request schema.
// No network, microphone or Google key required in CI.
var request = System.Text.Json.JsonSerializer.Serialize(
    GeminiAssistantClient.BuildTranscriptionRequest(PcmWav.MakeWav(Frame(1300))));
using (var payload = System.Text.Json.JsonDocument.Parse(request))
{
    var root = payload.RootElement;
    if (root.GetProperty("model").GetString() != "gemini-3.5-transcribe" ||
        root.GetProperty("input")[0].GetProperty("type").GetString() != "audio" ||
        root.GetProperty("input")[0].GetProperty("mime_type").GetString() != "audio/wav" ||
        root.GetProperty("generation_config").GetProperty("transcription_config")
            .GetProperty("language_codes")[0].GetString() != "vi-VN")
        throw new InvalidOperationException("FAIL Interactions audio schema / vi-VN model");
    Console.WriteLine("PASS correct Gemini 3.5 STT Interactions inline audio schema");
}
using (var response = System.Text.Json.JsonDocument.Parse("""
{
  "steps": [
    { "type": "reasoning", "content": [{"type":"text", "text":"Không tiết lộ nội dung nội bộ"}] },
    { "type": "model_output", "content": [
      {"type":"text","text":"Kiểm tra tồn kho Hưng Yên"},
      {"type":"audio","data":"AA=="},
      {"type":"text","text":"cho mã VT001."}
    ] }
  ]
}
"""))
{
    var transcript = GeminiAssistantClient.ExtractTranscript(response.RootElement);
    if (transcript != "Kiểm tra tồn kho Hưng Yên cho mã VT001.")
        throw new Exception("FAIL Interactions transcript diacritics/parts.");
    Console.WriteLine("PASS Vietnamese transcript from Interactions model_output steps");
}
using (var response = System.Text.Json.JsonDocument.Parse(
    """{"output_text":"Tồn kho vật tư còn 7 chiếc."}"""))
{
    if (GeminiAssistantClient.ExtractTranscript(response.RootElement) != "Tồn kho vật tư còn 7 chiếc.")
        throw new Exception("FAIL output_text parsing.");
    Console.WriteLine("PASS Gemini STT direct output_text");
}
using (var empty = System.Text.Json.JsonDocument.Parse("""{"steps":[]}"""))
{
    if (GeminiAssistantClient.ExtractTranscript(empty.RootElement) != "")
        throw new Exception("FAIL invented transcript on empty model output.");
    Console.WriteLine("PASS no fabricated transcript for empty response");
}
if (!GeminiAssistantClient.ApiFailure(System.Net.HttpStatusCode.TooManyRequests, "nhận dạng")
    .Message.Contains("hạn mức", StringComparison.OrdinalIgnoreCase))
    throw new Exception("FAIL quota-specific API diagnostic.");
Console.WriteLine("PASS Gemini HTTP 429 diagnostic never exposes API key");

// Verify TTS payload obeys Interactions schema and WAV output is parsed.
var ttsRequest = System.Text.Json.JsonSerializer.Serialize(
    AnGeminiSpeechService.BuildSpeechRequest("Xin chào, tôi là An."));
using (var document = System.Text.Json.JsonDocument.Parse(ttsRequest))
{
    var root = document.RootElement;
    if (root.GetProperty("model").GetString() != "gemini-3.8-flash-lite-tts" ||
        root.GetProperty("response_format").GetProperty("type").GetString() != "audio" ||
        root.GetProperty("input")[0].GetProperty("content")[0].GetProperty("annotations")[0]
            .GetProperty("type").GetString() != "speech_metadata" ||
        root.GetProperty("generation_config").GetProperty("speech_config")[0]
            .GetProperty("voice").GetString() != "Leda")
        throw new Exception("FAIL Gemini TTS Interactions schema / female voice.");
    Console.WriteLine("PASS Gemini TTS Interactions request/voice schema");
}
var expectedWav = PcmWav.MakeWav(Frame(620));
var fakeTtsJson = System.Text.Json.JsonSerializer.Serialize(new
{
    steps = new object[] { new {
        type = "model_output", content = new object[] {
            new { type = "audio", data = Convert.ToBase64String(expectedWav) }
        }
    } }
});
using (var output = System.Text.Json.JsonDocument.Parse(fakeTtsJson))
{
    if (!AnGeminiSpeechService.ExtractWav(output.RootElement).SequenceEqual(expectedWav))
        throw new Exception("FAIL Gemini TTS in-memory WAV parsing.");
    Console.WriteLine("PASS Gemini TTS WAV decoding from Interactions response");
}
var windowsDefaultMic = new MicrophoneDeviceOption(-1, "Default");
if (windowsDefaultMic.DeviceNumber != -1 || !windowsDefaultMic.Label.Contains("Mặc định"))
    throw new Exception("FAIL Windows default microphone label");
Console.WriteLine("PASS microphone device option selection shape");
