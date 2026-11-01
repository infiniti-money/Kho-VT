using NAudio.Wave;

namespace HisaWarehouse.Services;

/// <summary>
/// Windows microphone capture. PCM is handled in memory, never a temp file.
/// The session stops recording before forwarding audio to Gemini or starting TTS.
/// </summary>
public sealed class HandsFreePcmRecorder : IDisposable
{
    private readonly PcmVoiceActivityDetector _vad = new();
    private readonly int _deviceNumber;
    public HandsFreePcmRecorder(int deviceNumber = -1) => _deviceNumber = deviceNumber;
    /// <summary>Micro signal level 0–100 in RAM; does not send or store audio.</summary>
    public event Action<int>? InputLevelChanged;
    private WaveInEvent? _source;
    private int _stopping;
    private bool _aborted;
    private byte[]? _completedAudio;

    public event Action<byte[]>? UtteranceReady;
    public event Action<Exception?>? CaptureStopped;

    public void Start()
    {
        if (_source is not null) throw new InvalidOperationException("Micro đang được sử dụng.");
        _aborted = false;
        _stopping = 0;
        MicrophoneDevices.Validate(_deviceNumber);
        _source = new WaveInEvent
        {
            DeviceNumber = _deviceNumber,
            WaveFormat = new WaveFormat(PcmVoiceActivityDetector.SampleRate, 16, 1),
            BufferMilliseconds = PcmVoiceActivityDetector.FrameMilliseconds,
            NumberOfBuffers = 4
        };
        _source.DataAvailable += CaptureData;
        _source.RecordingStopped += RecordingStopped;
        try { _source.StartRecording(); }
        catch
        {
            _source.Dispose();
            _source = null;
            throw;
        }
    }

    private void CaptureData(object? sender, WaveInEventArgs args)
    {
        if (_aborted || Volatile.Read(ref _stopping) != 0) return;
        var samples = args.BytesRecorded / 2;
        if (samples > 0)
        {
            double squares = 0;
            for (var i = 0; i < args.BytesRecorded - 1; i += 2)
            {
                short amplitude = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(
                    args.Buffer.AsSpan(i, 2));
                squares += (double)amplitude * amplitude;
            }
            var rms = Math.Sqrt(squares / samples);
            InputLevelChanged?.Invoke((int)Math.Clamp(rms * 100.0 / 5500.0, 0, 100));
        }
        if (!_vad.Push(args.Buffer.AsSpan(0, args.BytesRecorded))) return;
        _completedAudio = _vad.TakePcm();
        if (Interlocked.Exchange(ref _stopping, 1) == 0)
            try { _source?.StopRecording(); } catch (Exception ex) { CaptureStopped?.Invoke(ex); }
    }

    private void RecordingStopped(object? sender, StoppedEventArgs args)
    {
        var sample = _completedAudio;
        var cancelled = _aborted;
        CaptureStopped?.Invoke(args.Exception);
        if (!cancelled && args.Exception is null && sample is { Length: > 0 })
            UtteranceReady?.Invoke(sample);
    }

    public void Stop()
    {
        _aborted = true;
        Interlocked.Exchange(ref _stopping, 1);
        try { _source?.StopRecording(); }
        catch { /* Device was already unplugged or stopped. */ }
    }

    public void Dispose()
    {
        Stop();
        if (_source is not null)
        {
            _source.DataAvailable -= CaptureData;
            _source.RecordingStopped -= RecordingStopped;
            _source.Dispose();
            _source = null;
        }
        _completedAudio = null;
        _vad.Reset();
    }
}
