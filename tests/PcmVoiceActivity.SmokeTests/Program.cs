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
