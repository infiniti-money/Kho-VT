using System.IO;
using System.Buffers.Binary;

namespace HisaWarehouse.Services;

/// <summary>
/// Local amplitude-based VAD. Receives PCM16LE 16 kHz mono, never writes audio
/// to disk. No input is persisted until speech begins; only 350ms pre-roll.
/// Not speech identification: loud noise and other speakers may trigger it.
/// </summary>
public sealed class PcmVoiceActivityDetector
{
    public const int SampleRate = 16000;
    public const int BytesPerSample = 2;
    public const int FrameMilliseconds = 50;
    public const int FrameBytes = SampleRate * BytesPerSample * FrameMilliseconds / 1000;
    public const int PreRollMilliseconds = 350;
    public const int SilenceMilliseconds = 1200;
    public const int MaximumMilliseconds = 25000;
    public const int MaximumBytes = SampleRate * BytesPerSample * MaximumMilliseconds / 1000;

    private readonly int _rmsThreshold;
    private readonly Queue<byte[]> _preRoll = new();
    private readonly MemoryStream _utterance = new();
    private readonly byte[] _frame = new byte[FrameBytes];
    private int _frameCount;
    private int _silenceMs;
    private int _consecutiveLoudFrames;
    private bool _speechStarted;
    private bool _completed;

    public bool HasSpeech => _speechStarted;
    public bool IsComplete => _completed;
    public int BufferedBeforeSpeechBytes => _preRoll.Sum(x => x.Length);
    public int RecordedBytes => (int)_utterance.Length;

    public PcmVoiceActivityDetector(int rmsThreshold = 450)
    {
        if (rmsThreshold < 50 || rmsThreshold > 12000)
            throw new ArgumentOutOfRangeException(nameof(rmsThreshold));
        _rmsThreshold = rmsThreshold;
    }

    public bool Push(ReadOnlySpan<byte> pcm)
    {
        if (_completed) return true;
        while (!pcm.IsEmpty && !_completed)
        {
            int take = Math.Min(FrameBytes - _frameCount, pcm.Length);
            pcm[..take].CopyTo(_frame.AsSpan(_frameCount));
            _frameCount += take;
            pcm = pcm[take..];
            if (_frameCount != FrameBytes) continue;

            ProcessFrame(_frame);
            _frameCount = 0;
        }
        return _completed;
    }

    private void ProcessFrame(byte[] frame)
    {
        double sum = 0;
        for (int i = 0; i < frame.Length; i += BytesPerSample)
        {
            int amplitude = BinaryPrimitives.ReadInt16LittleEndian(frame.AsSpan(i, 2));
            sum += (double)amplitude * amplitude;
        }
        double rms = Math.Sqrt(sum / (frame.Length / BytesPerSample));
        bool loud = rms >= _rmsThreshold;
        if (!_speechStarted)
        {
            _preRoll.Enqueue((byte[])frame.Clone());
            while (_preRoll.Count * FrameMilliseconds > PreRollMilliseconds)
                _preRoll.Dequeue();

            _consecutiveLoudFrames = loud ? _consecutiveLoudFrames + 1 : 0;
            if (_consecutiveLoudFrames < 2) return;

            _speechStarted = true;
            while (_preRoll.TryDequeue(out var buffered))
                WriteLimited(buffered);
            return;
        }

        WriteLimited(frame);
        _silenceMs = loud ? 0 : _silenceMs + FrameMilliseconds;
        if (_silenceMs >= SilenceMilliseconds)
            _completed = true;
    }

    private void WriteLimited(byte[] bytes)
    {
        int remaining = MaximumBytes - (int)_utterance.Length;
        if (remaining > 0)
            _utterance.Write(bytes, 0, Math.Min(bytes.Length, remaining));
        if (_utterance.Length >= MaximumBytes) _completed = true;
    }

    public byte[] TakePcm()
    {
        if (!_completed || !_speechStarted) return [];
        var audio = _utterance.ToArray();
        Reset();
        return audio;
    }

    public void Reset()
    {
        _preRoll.Clear();
        _utterance.SetLength(0);
        _frameCount = 0;
        _silenceMs = 0;
        _consecutiveLoudFrames = 0;
        _speechStarted = false;
        _completed = false;
        Array.Clear(_frame);
    }
}

/// <summary>WAV header assembled in memory; audio is not written to files.</summary>
public static class PcmWav
{
    public static byte[] MakeWav(ReadOnlySpan<byte> pcm)
    {
        if (pcm.IsEmpty || pcm.Length > PcmVoiceActivityDetector.MaximumBytes || pcm.Length % 2 != 0)
            throw new ArgumentException("Đoạn PCM phải hợp lệ và không quá 25 giây.");
        using var stream = new MemoryStream(44 + pcm.Length);
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcm.Length);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1); // PCM
        writer.Write((short)1); // mono
        writer.Write(PcmVoiceActivityDetector.SampleRate);
        writer.Write(PcmVoiceActivityDetector.SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(pcm.Length);
        writer.Write(pcm);
        writer.Flush();
        return stream.ToArray();
    }
}
