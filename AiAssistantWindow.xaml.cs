using System.Collections.ObjectModel;
using System.Globalization;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Threading;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public sealed record AiChatMessage(bool IsUser, string Text)
{
    public string RoleLabel => IsUser ? "Bạn" : "An";
}

public partial class AiAssistantWindow : Window
{
    private readonly InventoryState _state;
    private readonly List<AiChatTurn> _history = [];
    private SpeechSynthesizer? _speaker;
    private CancellationTokenSource? _manualTtsCts;
    private string _lastAssistantText = "";
    private bool _busy;

    public ObservableCollection<AiChatMessage> ChatMessages { get; } = [];
    // Open the existing human-reviewed replenishment form, never apply changes from AI.
    public Action? OpenReplenishment { get; set; }
    public Action? BringToForeground { get; set; }
    public Action<AnCatalogEditService.Plan>? ReviewCatalogEdit { get; set; }
    public bool IsHandsFreeActive => _handsFreeEnabled;
    public bool KeepListeningWhenMinimized => _handsFreeEnabled &&
        BackgroundListeningCheck.IsChecked == true;

    public AiAssistantWindow(InventoryState state)
    {
        InitializeComponent();
        _state = state;
        DataContext = this;
        ChatMessages.Add(new AiChatMessage(false,
            "Chào bạn, mình là An, trợ lý kho vật tư HISA. " +
            "An có thể đọc báo cáo, kiểm tra tình trạng hàng, lập đề xuất nhập kho để bạn duyệt " +
            "và trò chuyện bằng giọng nói tiếng Việt qua Gemini. " +
            "Để dùng micro, hãy lưu khóa API rồi nhấn Mic hoặc Bật rảnh tay."));
        var metrics = AiInventoryReport.Metrics(state);
        LowStockCount.Text = metrics.LowStock.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        OutOfStockCount.Text = metrics.OutOfStock.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));
        MovementCountText.Text = $"Giao dịch nhập/xuất 30 ngày: {metrics.MovementsMonth:N0} dòng có ngày và số lượng hợp lệ.";
        RefreshKeyStatus();
        LoadMicrophones(true);
    }

    private void RefreshKeyStatus()
    {
        KeyStatus.Text = GeminiKeyStore.HasKey
            ? "● Khóa Gemini đã lưu trong Windows" : "○ Chưa có khóa Gemini trên máy này";
        KeyStatus.Foreground = GeminiKeyStore.HasKey
            ? System.Windows.Media.Brushes.SeaGreen : System.Windows.Media.Brushes.DarkGoldenrod;
    }

    private void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        EndHandsFreeAndCancel();
        try
        {
            GeminiKeyStore.Save(KeyInput.Password);
            KeyInput.Clear();
            RefreshKeyStatus();
            ActivityStatus.Text = "Đã lưu khóa API được mã hóa trong hồ sơ Windows; nâng cấp ứng dụng sẽ không xóa.";
        }
        catch (Exception ex) { ActivityStatus.Text = ex.Message; }
    }

    private void RemoveKey_Click(object sender, RoutedEventArgs e)
    {
        EndHandsFreeAndCancel();
        try
        {
            GeminiKeyStore.Remove();
            KeyInput.Clear();
            RefreshKeyStatus();
            ActivityStatus.Text = "Đã xóa khóa Gemini được lưu trong Windows.";
        }
        catch (Exception ex) { ActivityStatus.Text = "Không xóa được khóa: " + ex.Message; }
    }

    private void ReviewProposals_Click(object sender, RoutedEventArgs e)
    {
        // Explicit navigation: close and release AI/microphone resources BEFORE
        // opening another form, rather than stacking it on top of the assistant.
        if (!TryCloseAssistant()) return;
        OpenReplenishment?.Invoke();
    }

    private void LocalReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _lastAssistantText = "BÁO CÁO TÍNH TẠI MÁY (không gửi lên mạng):\n" +
                AiInventoryReport.Build(_state, DateOnly.FromDateTime(DateTime.Today));
            ChatMessages.Add(new AiChatMessage(false, _lastAssistantText));
            ActivityStatus.Text = "Đã lập báo cáo từ dữ liệu HISA đang lưu trên máy; chưa xác minh đồng bộ Excel.";
            ScrollToBottom();
        }
        catch (Exception ex) { ActivityStatus.Text = "Không lập được báo cáo: " + ex.Message; }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (_handsFreeEnabled)
        {
            ActivityStatus.Text = "Đang bật rảnh tay. Tắt rảnh tay trước khi gửi câu hỏi thủ công.";
            return;
        }
        if (_busy || _manualMicCts is not null)
        {
            if (_manualMicCts is not null)
                ActivityStatus.Text = "Micro đang nghe hoặc nhận dạng. Hủy micro trước khi gửi câu hỏi thủ công.";
            return;
        }
        var question = QuestionInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            ActivityStatus.Text = "Bạn hãy nhập câu hỏi trước khi gửi nhé.";
            QuestionInput.Focus();
            return;
        }

        if (TryHandleAnCommand(question)) return;

        string key;
        try { key = GeminiKeyStore.Load(); }
        catch (Exception ex) { ActivityStatus.Text = ex.Message; return; }

        var context = ShareDataCheck.IsChecked == true
            ? AiInventoryReport.Build(_state, DateOnly.FromDateTime(DateTime.Today))
            : "Người dùng chưa đồng ý gửi số liệu kho. Không có dữ liệu kho; không được suy đoán tồn.";
        var prior = _history.ToArray();
        _busy = true;
        _manualRequestCts?.Dispose();
        _manualRequestCts = new CancellationTokenSource();
        var requestToken = _manualRequestCts.Token;
        SendButton.IsEnabled = false;
        QuestionInput.IsEnabled = false;
        ChatMessages.Add(new AiChatMessage(true, question));
        QuestionInput.Clear();
        ScrollToBottom();
        ActivityStatus.Text = "Đang hỏi Gemini qua kết nối Internet…";
        try
        {
            var answer = await GeminiAssistantClient.AskAsync(key, question, context, prior, requestToken);
            if (requestToken.IsCancellationRequested || _sessionClosed) return;
            _history.Add(new AiChatTurn("user", question));
            _history.Add(new AiChatTurn("model", answer));
            ChatMessages.Add(new AiChatMessage(false, answer));
            _lastAssistantText = answer;
            ActivityStatus.Text = ShareDataCheck.IsChecked == true
                ? "Đã nhận trả lời; số liệu gửi tới Gemini là thống kê rút gọn."
                : "Đã nhận trả lời; không gửi thống kê kho.";
            if (SpeakAnswerCheck.IsChecked == true) Speak(answer);
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
            // Cancellation requested by user or navigation; do not add a failure message.
        }
        catch (Exception ex)
        {
            if (_sessionClosed || requestToken.IsCancellationRequested) return;
            ChatMessages.Add(new AiChatMessage(false, "Chưa thể kết nối Gemini: " + ex.Message));
            ActivityStatus.Text = "Lần hỏi chưa thành công; bạn có thể thử lại.";
        }
        finally
        {
            _busy = false;
            if (!_sessionClosed)
            {
                SendButton.IsEnabled = true;
                QuestionInput.IsEnabled = true;
                QuestionInput.Focus();
                ScrollToBottom();
            }
        }
    }

    private void Speak(string text)
    {
        try
        {
            _speaker ??= new SpeechSynthesizer();
            _speaker.SpeakAsyncCancelAll();
            AnVoiceSelector.Configure(_speaker);
            _speaker.SetOutputToDefaultAudioDevice();
            _speaker.SpeakAsync(text.Length > 4000 ? text[..4000] : text);
        }
        catch (Exception ex)
        {
            ActivityStatus.Text = "An chưa đọc được tiếng Việt: " + ex.Message;
        }
    }

    private async void ReadAloud_Click(object sender, RoutedEventArgs e)
    {
        var value = !string.IsNullOrWhiteSpace(_lastAssistantText) ? _lastAssistantText :
            ChatMessages.LastOrDefault(message => !message.IsUser)?.Text;
        if (string.IsNullOrWhiteSpace(value))
        {
            ActivityStatus.Text = "An chưa có nội dung để đọc.";
            return;
        }
        try
        {
            using var probe = new SpeechSynthesizer();
            AnVoiceSelector.Configure(probe);
            Speak(value);
            ActivityStatus.Text = "An đang đọc bằng giọng nữ tiếng Việt cài trên Windows.";
            return;
        }
        catch (InvalidOperationException) { /* No suitable local voice. */ }
        catch (Exception ex) { ActivityStatus.Text = "Lỗi giọng đọc Windows: " + ex.Message; }

        if (!GeminiKeyStore.HasKey)
        {
            ActivityStatus.Text = "Windows thiếu giọng nữ tiếng Việt. Hãy lưu khóa Gemini để dùng giọng An qua Internet.";
            return;
        }
        if (MessageBox.Show(this,
            "Windows chưa có giọng nữ tiếng Việt phù hợp.\n\n" +
            "Bạn đồng ý gửi nội dung đang hiển thị tới Google Gemini để An đọc " +
            "bằng giọng nữ nhẹ nhàng theo phong cách Hà Nội?\n\n" +
            "Nội dung có thể chứa số liệu kho, vì vậy chỉ tiếp tục khi bạn cho phép.",
            "Dùng giọng đọc Gemini của An", MessageBoxButton.YesNo,
            MessageBoxImage.Information, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        _manualTtsCts?.Cancel();
        _manualTtsCts?.Dispose();
        var cts = new CancellationTokenSource();
        _manualTtsCts = cts;
        ActivityStatus.Text = "An đang tạo giọng nữ tiếng Việt bằng Gemini…";
        try
        {
            await AnGeminiSpeechService.SpeakAsync(GeminiKeyStore.Load(), value, cts.Token);
            if (!_sessionClosed && !cts.IsCancellationRequested)
                ActivityStatus.Text = "An đã đọc xong nội dung.";
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!_sessionClosed && !cts.IsCancellationRequested)
                ActivityStatus.Text = "Không đọc được bằng Gemini: " + ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_manualTtsCts, cts)) _manualTtsCts = null;
            cts.Dispose();
        }
    }

    private void CreateReplenishment_Click(object sender, RoutedEventArgs e)
    {
        if (_handsFreeEnabled) StopHandsFree("An sẽ dừng nghe trước khi mở phiếu duyệt.");
        if (_busy)
        {
            ActivityStatus.Text = "Đang có yêu cầu Gemini. Hãy đợi An hoàn tất trước khi mở phiếu.";
            return;
        }
        if (!TryCloseAssistant()) return;
        OpenReplenishment?.Invoke();
    }

    private bool TryHandleAnCommand(string question)
    {
        var stockAnswer = AiInventoryReport.TryAnswerStockBySku(_state, question);
        if (stockAnswer is not null &&
            !AnCatalogEditService.TryParseCommand(question, out _))
        {
            ChatMessages.Add(new AiChatMessage(true, question));
            ChatMessages.Add(new AiChatMessage(false, stockAnswer));
            _lastAssistantText = stockAnswer;
            QuestionInput.Clear();
            ScrollToBottom();
            ActivityStatus.Text = "An đã đối chiếu mã hàng với dữ liệu đang lưu trong HISA.";
            return true;
        }
        if (AnCatalogEditService.TryParseCommand(question, out var edit) && edit is not null)
        {
            ChatMessages.Add(new AiChatMessage(true, question));
            QuestionInput.Clear();
            try
            {
                var plan = AnCatalogEditService.Prepare(
                    _state.PrimaryExcelPath, _state.PrimarySyncFingerprint, edit);
                ChatMessages.Add(new AiChatMessage(false,
                    "An đã kiểm tra file Excel và chuẩn bị bản xem trước. " +
                    "Bạn cần xác nhận trước khi An sửa ô dữ liệu."));
                ReviewCatalogEdit?.Invoke(plan);
            }
            catch (Exception ex)
            {
                ChatMessages.Add(new AiChatMessage(false,
                    "An không sửa file vì điều kiện bảo vệ chưa đạt: " + ex.Message));
            }
            ScrollToBottom();
            return true;
        }
        var lowered = question.ToLowerInvariant();
        if (lowered.Contains("phiếu yêu cầu nhập") ||
            lowered.Contains("đề xuất nhập kho") || lowered.Contains("lập phiếu nhập đề xuất"))
        {
            ChatMessages.Add(new AiChatMessage(true, question));
            ChatMessages.Add(new AiChatMessage(false,
                "An sẽ mở đề xuất từ tồn tối thiểu. Bạn cần chọn số lượng, lưu phiếu " +
                "ở trạng thái Chờ duyệt, rồi tự phê duyệt ở mục Yêu cầu vật tư. " +
                "An không tự cộng tồn kho."));
            if (TryCloseAssistant()) OpenReplenishment?.Invoke();
            return true;
        }
        if (lowered.Contains("báo cáo tồn kho") ||
            lowered.Contains("báo cáo tình trạng hàng") || lowered.Contains("kiểm tra tình trạng kho"))
        {
            ChatMessages.Add(new AiChatMessage(true, question));
            QuestionInput.Clear();
            LocalReport_Click(this, new RoutedEventArgs());
            return true;
        }
        return false;
    }

    private void Microphone_Click(object sender, RoutedEventArgs e)
    {
        if (_handsFreeEnabled)
        {
            ActivityStatus.Text = "Đang bật rảnh tay. Hãy tắt rảnh tay trước khi dùng micro một lượt.";
            return;
        }
        if (_manualMicCts is not null)
        {
            StopManualMicrophone();
            ActivityStatus.Text = "Đã hủy lượt micro. Không gửi thêm âm thanh tới Gemini.";
            return;
        }
        StartManualMicrophone();
    }

    private void StopSpeaking_Click(object sender, RoutedEventArgs e)
    {
        _speaker?.SpeakAsyncCancelAll();
        try { _manualTtsCts?.Cancel(); } catch { }
        try { _speechPlaybackCts?.Cancel(); } catch { }
        try { _handsFreeSpeaker?.SpeakAsyncCancelAll(); } catch { }
        ActivityStatus.Text = "Đã dừng đọc câu trả lời.";
    }

    private void ScrollToBottom() =>
        Dispatcher.BeginInvoke(() => ChatScroller.ScrollToEnd(), DispatcherPriority.Background);

    private void Close_Click(object sender, RoutedEventArgs e) => TryCloseAssistant();

    private bool TryCloseAssistant()
    {
        _sessionClosed = true;
        EndHandsFreeAndCancel();
        _speaker?.Dispose();
        _speaker = null;
        this.CompleteEmbedded(false);
        return true;
    }
}
