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
    private HandsFreePcmRecorder? _manualRecorder;
    private CancellationTokenSource? _manualMicCts;
    private int _manualMicGeneration;
    private SpeechSynthesizer? _handsFreeSpeaker;
    private CancellationTokenSource? _speechPlaybackCts;

    /// <summary>
    /// Dashboard one-click entry opens An and immediately asks the SAME explicit
    /// microphone/cloud processing consent as the ordinary rảnh tay button.
    /// It never records before permission is granted.
    /// </summary>
    public void BeginHandsFreeFromHome() =>
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_sessionClosed && !_handsFreeEnabled)
                HandsFree_Click(this, new RoutedEventArgs());
        }), DispatcherPriority.ContextIdle);

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

        if (_microphoneProbeCts is not null) StopMicrophoneProbe();
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
        var recorder = new HandsFreePcmRecorder(_selectedMicrophone);
        recorder.UtteranceReady += pcm =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsLive(generation, token))
                    _ = ProcessHandsFreeUtteranceAsync(pcm, generation, token);
            }), DispatcherPriority.Normal);
        recorder.InputLevelChanged += level =>
            Dispatcher.BeginInvoke(new Action(() =>
                DisplayMicrophoneLevel(level, IsLive(generation, token))));
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
            HandsFreeStatus.Text = "● Đang nghe: " + (InputMicrophoneBox.SelectedItem as MicrophoneDeviceOption)?.Label;
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
            // Local An actions are deterministic and require their own approval.
            // Do not send an Excel-edit command to the free-form chat model.
            var commandText = transcript.ToLowerInvariant();
            if (AnCatalogEditService.TryParseCommand(transcript, out _) ||
                commandText.Contains("phiếu yêu cầu nhập") ||
                commandText.Contains("đề xuất nhập kho"))
            {
                StopHandsFree("An dừng rảnh tay để bạn xem và duyệt thao tác.");
                BringToForeground?.Invoke();
                TryHandleAnCommand(transcript);
                return;
            }
            if (BravoStockComparison.IsComparisonQuestion(transcript) ||
                (ReadWorkbooksCheck.IsChecked == true &&
                 AiInventoryReport.TryAnswerStockBySku(_state, transcript) is not null))
            {
                var answer = AnswerFromWorkbooks(transcript);
                ChatMessages.Add(new AiChatMessage(true, transcript));
                ChatMessages.Add(new AiChatMessage(false, answer));
                _lastAssistantText = answer;
                ScrollToBottom();
                HandsFreeStatus.Text = "Đã đối chiếu tồn tại máy. An đang đọc kết quả.";
                await SpeakReplyForHandsFreeAsync(answer, token,
                    allowCloud: ShareDataCheck.IsChecked == true);
                if (IsLive(generation, token)) await Task.Delay(750, token);
                return;
            }
            // Specific SKU lookup is resolved locally so An never invents
            // the stock of a product omitted from Gemini's shortened context.
            var stockAnswer = AiInventoryReport.TryAnswerStockBySku(_state, transcript);
            if (stockAnswer is not null)
            {
                ChatMessages.Add(new AiChatMessage(true, transcript));
                _lastAssistantText = stockAnswer;
                ChatMessages.Add(new AiChatMessage(false, stockAnswer));
                ScrollToBottom();
                HandsFreeStatus.Text = "An đang đọc tình trạng mã vật tư.";
                await SpeakReplyForHandsFreeAsync(stockAnswer, token,
                    allowCloud: ShareDataCheck.IsChecked == true);
                if (IsLive(generation, token)) await Task.Delay(750, token);
                return;
            }
            if (commandText.Contains("báo cáo tồn kho") ||
                commandText.Contains("báo cáo tình trạng hàng") ||
                commandText.Contains("kiểm tra tình trạng kho"))
            {
                ChatMessages.Add(new AiChatMessage(true, transcript));
                var metrics = AiInventoryReport.Metrics(_state);
                var briefing = $"An báo cáo tình trạng kho hiện tại theo dữ liệu đang lưu trong HISA. " +
                    $"Có {metrics.OutOfStock} mã hết hoặc âm tồn, " +
                    $"{metrics.LowStock} mã dưới tồn tối thiểu và " +
                    $"{metrics.MovementsMonth} lượt nhập xuất trong 30 ngày. " +
                    "Số liệu này có thể chưa được đồng bộ với Excel.";
                _lastAssistantText = briefing;
                ChatMessages.Add(new AiChatMessage(false, briefing));
                ScrollToBottom();
                HandsFreeStatus.Text = "An đang đọc báo cáo; micro tạm dừng.";
                await SpeakReplyForHandsFreeAsync(briefing, token,
                    allowCloud: ShareDataCheck.IsChecked == true);
                if (IsLive(generation, token)) await Task.Delay(750, token);
                return;
            }
            ChatMessages.Add(new AiChatMessage(true, transcript));
            ScrollToBottom();
            ActivityStatus.Text = "Đã nhận dạng: " + transcript;
            HandsFreeStatus.Text = "Gemini đang trả lời…";

            var report = BuildGeminiStockContext();
            var reply = await GeminiAssistantClient.AskAsync(
                key, transcript, report, _history.ToArray(), token);
            if (!IsLive(generation, token)) return;
            _history.Add(new AiChatTurn("user", transcript));
            _history.Add(new AiChatTurn("model", reply));
            ChatMessages.Add(new AiChatMessage(false, reply));
            _lastAssistantText = reply;
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
                ActivityStatus.Text = "An chưa hoàn thành lượt nói: " + ex.Message;
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

    private async Task SpeakReplyForHandsFreeAsync(string reply, CancellationToken token,
        bool allowCloud = true)
    {
        // Gemini can synthesize a soft Northern Vietnamese female voice without
        // requiring Windows 11 to have a vi-VN SAPI voice installed.
        using var speechToken = CancellationTokenSource.CreateLinkedTokenSource(token);
        _speechPlaybackCts = speechToken;
        try
        {
            // A locally computed report is PRIVATE when ShareDataCheck is off.
            // Never send stock quantities in TTS requests without consent.
            if (allowCloud)
            {
                await AnGeminiSpeechService.SpeakAsync(GeminiKeyStore.Load(), reply, speechToken.Token);
                return;
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            // User pressed Stop speaking. Resume the mic after the quiet gap.
            return;
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            ActivityStatus.Text = "Gemini chưa phát được giọng nữ. An thử giọng nữ tiếng Việt trên Windows: " +
                error.Message;
        }
        finally
        {
            if (ReferenceEquals(_speechPlaybackCts, speechToken)) _speechPlaybackCts = null;
        }
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
            AnVoiceSelector.Configure(speaker);
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

    /// <summary>
    /// One-shot manual mic uses the same in-memory PCM and Gemini recognition as
    /// hands-free. Windows 11 does not need an installed vi-VN SAPI recognizer.
    /// This mode only fills the text box and NEVER submits the answer by itself.
    /// </summary>
    private void StartManualMicrophone()
    {
        if (_sessionClosed || _handsFreeEnabled || _busy || _manualMicCts is not null) return;
        if (_microphoneProbeCts is not null) StopMicrophoneProbe();
        if (!GeminiKeyStore.HasKey)
        {
            ActivityStatus.Text = "Hãy lưu khóa API Gemini trước khi bật micro nhận dạng tiếng Việt.";
            return;
        }

        var answer = MessageBox.Show(this,
            "Cho phép HISA dùng micro cho một lượt nói?\n\n" +
            "Khi bạn nói xong và ngừng khoảng 1,2 giây, âm thanh WAV (tối đa 25 giây) " +
            "sẽ được gửi tới Google Gemini qua Internet để nhận dạng tiếng Việt. " +
            "HISA không lưu tệp âm thanh trên ổ đĩa.\n\n" +
            "Văn bản nhận dạng chỉ được đưa vào ô câu hỏi; bạn tự kiểm tra và nhấn Gửi. " +
            "Bấm lại Mic hoặc Hủy micro để dừng.",
            "Cho phép gửi giọng nói đến Gemini",
            MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        var cts = new CancellationTokenSource();
        _manualMicCts = cts;
        var generation = ++_manualMicGeneration;
        var recorder = new HandsFreePcmRecorder(_selectedMicrophone);
        _manualRecorder = recorder;
        recorder.UtteranceReady += pcm =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ManualMicLive(generation, cts.Token))
                    _ = ProcessManualMicrophoneAsync(pcm, generation, cts.Token);
            }), DispatcherPriority.Normal);
        recorder.InputLevelChanged += level =>
            Dispatcher.BeginInvoke(new Action(() =>
                DisplayMicrophoneLevel(level, ManualMicLive(generation, cts.Token))));
        recorder.CaptureStopped += error =>
        {
            if (error is null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!ManualMicLive(generation, cts.Token)) return;
                StopManualMicrophone();
                ActivityStatus.Text = "Micro Windows bị ngắt: " + error.Message +
                    ". Hãy kiểm tra quyền micro và thiết bị thu mặc định.";
            }));
        };
        try
        {
            recorder.Start();
            MicButton.Content = "■ Hủy mic";
            ActivityStatus.Text = "Đang nghe tiếng Việt bằng: " +
                (InputMicrophoneBox.SelectedItem as MicrophoneDeviceOption)?.Label +
                ". Nói rõ, tạm ngừng 1,2 giây để gửi tới Gemini.";
        }
        catch (Exception ex)
        {
            StopManualMicrophone();
            ActivityStatus.Text = "Không mở được micro: " + ex.Message +
                ". Kiểm tra Settings → Privacy & security → Microphone và micro mặc định.";
        }
    }

    private bool ManualMicLive(int generation, CancellationToken token) =>
        !_sessionClosed && !_handsFreeEnabled && generation == _manualMicGeneration &&
        _manualMicCts is not null && !token.IsCancellationRequested;

    private async Task ProcessManualMicrophoneAsync(byte[] pcm, int generation, CancellationToken token)
    {
        if (!ManualMicLive(generation, token)) return;

        // Stop listening before the network request, but keep the cancellation
        // token alive so the user can still cancel an in-flight transcription.
        var recorder = _manualRecorder;
        _manualRecorder = null;
        recorder?.Dispose();
        MicButton.Content = "■ Hủy nhận dạng";
        ActivityStatus.Text = "Đang nhận dạng tiếng Việt bằng Gemini…";
        try
        {
            var key = GeminiKeyStore.Load();
            var transcript = await GeminiAssistantClient.TranscribeVietnameseAsync(
                key, PcmWav.MakeWav(pcm), token);
            if (!ManualMicLive(generation, token)) return;
            if (string.IsNullOrWhiteSpace(transcript))
            {
                ActivityStatus.Text = "Không nghe rõ lời nói trong đoạn thu. Kiểm tra micro và thử lại.";
                return;
            }
            QuestionInput.Text = transcript;
            QuestionInput.CaretIndex = transcript.Length;
            QuestionInput.Focus();
            ActivityStatus.Text = "Gemini đã nhận dạng tiếng Việt. Kiểm tra nội dung rồi nhấn Gửi Gemini.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (ManualMicLive(generation, token))
                ActivityStatus.Text = "Nhận dạng chưa thành công: " + ex.Message;
        }
        finally
        {
            if (ManualMicLive(generation, token))
                StopManualMicrophone();
        }
    }

    private void StopManualMicrophone()
    {
        ++_manualMicGeneration;
        var cts = _manualMicCts;
        _manualMicCts = null;
        cts?.Cancel();
        var recorder = _manualRecorder;
        _manualRecorder = null;
        recorder?.Dispose();
        cts?.Dispose();
        MicButton.Content = "🎙 Mic";
        MicrophoneLevelBar.Value = 0;
    }

    private void StopHandsFree(string status)
    {
        _handsFreeEnabled = false;
        _handsFreeGeneration++;
        var cts = _handsFreeCts;
        _handsFreeCts = null;
        cts?.Cancel();
        StopCapture();
        try { _speechPlaybackCts?.Cancel(); } catch { }
        try { _handsFreeSpeaker?.SpeakAsyncCancelAll(); } catch { }
        HandsFreeButton.Content = "🎙 Bật rảnh tay";
        HandsFreeStatus.Text = "Rảnh tay đang tắt";
        MicrophoneLevelBar.Value = 0;
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
        ChatMessages.Add(new AiChatMessage(false, "Đã xóa hội thoại với An. Rảnh tay đã tắt."));
        _lastAssistantText = "";
        ScrollToBottom();
    }

    /// <summary>Called on switch, clear, close and app shutdown, even if AI is busy.</summary>
    public void EndHandsFreeAndCancel()
    {
        StopHandsFree("Rảnh tay đã dừng.");
        StopManualMicrophone();
        StopMicrophoneProbe();
        _manualRequestCts?.Cancel();
        try { _manualTtsCts?.Cancel(); } catch { }
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
