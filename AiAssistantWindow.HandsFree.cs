using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Threading;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class AiAssistantWindow
{
    private bool _handsFreeEnabled;
    private bool _sessionClosed;
    private int _handsFreeGeneration;
    private CancellationTokenSource? _handsFreeCts;
    private CancellationTokenSource? _manualRequestCts;
    private HandsFreePcmRecorder? _pcmRecorder;
    private SpeechSynthesizer? _handsFreeSpeaker;

    private bool IsLive(int generation, CancellationToken token) =>
        _handsFreeEnabled && !_sessionClosed && generation == _handsFreeGeneration &&
        !token.IsCancellationRequested;

    private void HandsFree_Click(object sender, RoutedEventArgs e)
    {
        if (_handsFreeEnabled)
        {
            StopHandsFree("Đã tắt rảnh tay. Micro không còn lắng nghe.");
            return;
        }

        if (_sessionClosed || _busy)
        {
            ActivityStatus.Text = "Hãy đợi kết thúc lượt Gemini hiện tại trước khi bật rảnh tay.";
            return;
        }

        if (!GeminiKeyStore.HasKey)
        {
            ActivityStatus.Text = "Cần lưu khóa Gemini trên máy trước khi sử dụng rảnh tay.";
            return;
        }

        var response = MessageBox.Show(Application.Current.MainWindow,
            "BẬT RẢNH TAY HISA?\n\n" +
            "Micro sẽ được mở trong phiên rảnh tay. Các lượt tiếng nói sẽ được tự thu " +
            "sau khoảng lặng 1,2 giây (tối đa 25 giây), gửi âm thanh qua Internet " +
            "cho Google Gemini để nhận dạng tiếng Việt, rồi tự gửi văn bản làm câu hỏi.\n\n" +
            "Gemini trả lời sẽ được đọc bằng loa Windows. Khi loa đọc, micro sẽ tạm dừng; " +
            "sau khi đọc xong, chờ 750ms rồi micro mới nghe tiếp.\n\n" +
            "Không có tệp ghi âm trên ổ đĩa. Âm thanh có thể thu cả tiếng ồn hoặc người khác. " +
            "Bạn có thể tắt rảnh tay hoặc hủy micro bất kỳ lúc nào. " +
            "AI chỉ tư vấn, không tự sửa Excel.\n\nBạn đồng ý bật rảnh tay?",
            "Xác nhận sử dụng micro và gửi âm thanh tới Gemini",
            MessageBoxButton.YesNo, MessageBoxImage.Information,
            MessageBoxResult.No);
        if (response != MessageBoxResult.Yes) return;

        try
        {
            // The old manual recognizer is separate; it is never left running.
            StopManualMicrophone();
            _handsFreeEnabled = true;
            _handsFreeCts = new CancellationTokenSource();
            _handsFreeGeneration++;
            HandsFreeButton.Content = "■ Tắt rảnh tay";
            StartCapture(_handsFreeGeneration, _handsFreeCts.Token);
        }
        catch (Exception ex)
        {
            StopHandsFree("Không bật được micro: " + ex.Message);
        }
    }

    private void StartCapture(int generation, CancellationToken token)
    {
        if (!IsLive(generation, token) || _busy) return;
        StopCapture();
        var recorder = new HandsFreePcmRecorder();
        recorder.UtteranceReady += pcm =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsLive(generation, token))
                    _ = ProcessHandsFreeUtteranceAsync(pcm, generation, token);
            }), DispatcherPriority.Normal);
        recorder.CaptureStopped += error =>
        {
            if (error is null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsLive(generation, token))
                    StopHandsFree("Micro bị ngắt: " + error.Message + ". Có thể bật lại rảnh tay.");
            }));
        };
        _pcmRecorder = recorder;
        try
        {
            recorder.Start();
            HandsFreeStatus.Text = "● Đang nghe giọng nói (PCM)";
            ActivityStatus.Text = "Đang nghe. Nói tiếng Việt, tự dừng sau 1,2 giây im lặng.";
        }
        catch
        {
            _pcmRecorder = null;
            recorder.Dispose();
            throw;
        }
    }

    private async Task ProcessHandsFreeUtteranceAsync(byte[] pcm, int generation, CancellationToken token)
    {
        if (!IsLive(generation, token) || _busy) return;
        // Recording is now stopped before any Internet call or speaker activity.
        StopCapture();
        _busy = true;
        SendButton.IsEnabled = false;
        QuestionInput.IsEnabled = false;
        try
        {
            HandsFreeStatus.Text = "Đang nhận dạng bằng Gemini…";
            var key = GeminiKeyStore.Load();
            var wav = PcmWav.MakeWav(pcm);
            var transcript = await GeminiAssistantClient.TranscribeVietnameseAsync(key, wav, token);
            if (!IsLive(generation, token)) return;
            if (string.IsNullOrWhiteSpace(transcript))
            {
                ActivityStatus.Text = "Gemini chưa nhận dạng được lời nói. Sẽ nghe lượt tiếp theo.";
                return;
            }
            ChatMessages.Add(new AiChatMessage(true, transcript));
            ScrollToBottom();
            ActivityStatus.Text = "Đã nhận dạng: " + transcript;
            HandsFreeStatus.Text = "Gemini đang trả lời…";

            var report = ShareDataCheck.IsChecked == true
                ? AiInventoryReport.Build(_state, DateOnly.FromDateTime(DateTime.Today))
                : "Không chia sẻ số liệu kho theo tùy chọn của người sử dụng.";
            var reply = await GeminiAssistantClient.AskAsync(
                key, transcript, report, _history.ToArray(), token);
            if (!IsLive(generation, token)) return;
            _history.Add(new AiChatTurn("user", transcript));
            _history.Add(new AiChatTurn("model", reply));
            ChatMessages.Add(new AiChatMessage(false, reply));
            ScrollToBottom();

            HandsFreeStatus.Text = "Đang đọc bằng loa; micro tạm dừng.";
            await SpeakReplyForHandsFreeAsync(reply, token);
            if (!IsLive(generation, token)) return;

            HandsFreeStatus.Text = "Loa đã dừng. Nghỉ 750ms…";
            await Task.Delay(750, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Explicit stop / navigation / permission changes must not restart the mic.
            return;
        }
        catch (Exception ex)
        {
            if (IsLive(generation, token))
            {
                ChatMessages.Add(new AiChatMessage(false, "Lượt rảnh tay chưa thành công: " + ex.Message));
                ActivityStatus.Text = "Không nhận dạng được hoặc Gemini/loa gặp lỗi. Kiểm tra mạng, quyền micro và loa.";
            }
        }
        finally
        {
            _busy = false;
            if (!_sessionClosed)
            {
                SendButton.IsEnabled = true;
                QuestionInput.IsEnabled = true;
            }
            if (IsLive(generation, token))
            {
                try { StartCapture(generation, token); }
                catch (Exception ex)
                {
                    StopHandsFree("Không phục hồi micro: " + ex.Message +
                        ". Kiểm tra quyền Windows và thử bật lại.");
                }
            }
        }
    }

    private async Task SpeakReplyForHandsFreeAsync(string reply, CancellationToken token)
    {
        using var speaker = new SpeechSynthesizer();
        _handsFreeSpeaker = speaker;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<SpeakCompletedEventArgs> handler = (_, args) =>
        {
            if (args.Error is not null) tcs.TrySetException(args.Error);
            else tcs.TrySetResult(!args.Cancelled);
        };
        speaker.SpeakCompleted += handler;
        try
        {
            var voices = speaker.GetInstalledVoices().Where(v => v.Enabled).ToList();
            var preferred = voices.FirstOrDefault(v => v.VoiceInfo.Culture.TwoLetterISOLanguageName == "vi" &&
                                                           v.VoiceInfo.Gender == VoiceGender.Female)
                ?? voices.FirstOrDefault(v => v.VoiceInfo.Culture.TwoLetterISOLanguageName == "vi")
                ?? voices.FirstOrDefault(v => v.VoiceInfo.Gender == VoiceGender.Female);
            if (preferred is not null) speaker.SelectVoice(preferred.VoiceInfo.Name);
            speaker.SetOutputToDefaultAudioDevice();
            speaker.SpeakAsync(reply.Length > 4000 ? reply[..4000] : reply);
            await tcs.Task.WaitAsync(token);
        }
        finally
        {
            speaker.SpeakCompleted -= handler;
            if (ReferenceEquals(_handsFreeSpeaker, speaker))
                _handsFreeSpeaker = null;
            try { speaker.SpeakAsyncCancelAll(); } catch { }
        }
    }

    private void StopCapture()
    {
        var recorder = _pcmRecorder;
        _pcmRecorder = null;
        recorder?.Dispose();
    }

    private void StopManualMicrophone()
    {
        var mic = _microphone;
        _microphone = null;
        try { mic?.RecognizeAsyncCancel(); } catch { }
        try { mic?.Dispose(); } catch { }
        MicButton.Content = "🎙 Mic";
    }

    private void StopHandsFree(string status)
    {
        _handsFreeEnabled = false;
        _handsFreeGeneration++;
        var cts = _handsFreeCts;
        _handsFreeCts = null;
        cts?.Cancel();
        StopCapture();
        try { _handsFreeSpeaker?.SpeakAsyncCancelAll(); } catch { }
        HandsFreeButton.Content = "🎙 Bật rảnh tay";
        HandsFreeStatus.Text = "Rảnh tay đang tắt";
        ActivityStatus.Text = status;
        // Pending continuations have the canceled token and cannot restart capture.
    }

    private void CancelMicrophone_Click(object sender, RoutedEventArgs e)
    {
        StopHandsFree("Đã hủy micro và phiên rảnh tay.");
        StopManualMicrophone();
    }

    private void Sharing_Changed(object sender, RoutedEventArgs e)
    {
        if (_handsFreeEnabled)
            StopHandsFree("Quyền chia sẻ thay đổi. Đã tắt rảnh tay; hãy bật lại để đồng ý.");
    }

    private void ClearChat_Click(object sender, RoutedEventArgs e)
    {
        EndHandsFreeAndCancel();
        _history.Clear();
        ChatMessages.Clear();
        ChatMessages.Add(new AiChatMessage(false, "Đã xóa hội thoại ở màn hình này. Rảnh tay đã tắt."));
        ScrollToBottom();
    }

    /// <summary>Called on switch, clear, close and app shutdown, even if AI is busy.</summary>
    public void EndHandsFreeAndCancel()
    {
        StopHandsFree("Rảnh tay đã dừng.");
        StopManualMicrophone();
        _manualRequestCts?.Cancel();
        try { _speaker?.SpeakAsyncCancelAll(); } catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        _sessionClosed = true;
        EndHandsFreeAndCancel();
        _speaker?.Dispose();
        _speaker = null;
        base.OnClosed(e);
    }
}
