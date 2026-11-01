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
    private string _lastAssistantText = "";
    private bool _busy;

    public ObservableCollection<AiChatMessage> ChatMessages { get; } = [];
    // Open the existing human-reviewed replenishment form, never apply changes from AI.
    public Action? OpenReplenishment { get; set; }
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

    private void ReadAloud_Click(object sender, RoutedEventArgs e)
    {
        var value = !string.IsNullOrWhiteSpace(_lastAssistantText) ? _lastAssistantText :
            ChatMessages.LastOrDefault(message => !message.IsUser)?.Text;
        if (string.IsNullOrWhiteSpace(value))
        {
            ActivityStatus.Text = "An chưa có nội dung để đọc.";
            return;
        }
        Speak(value);
        ActivityStatus.Text = "An đang đọc nội dung bằng giọng nữ tiếng Việt trên Windows.";
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
