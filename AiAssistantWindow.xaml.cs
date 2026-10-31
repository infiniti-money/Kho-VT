using System.Collections.ObjectModel;
using System.Globalization;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Threading;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public sealed record AiChatMessage(bool IsUser, string Text)
{
    public string RoleLabel => IsUser ? "Bạn" : "HISA AI";
}

public partial class AiAssistantWindow : Window
{
    private readonly InventoryState _state;
    private readonly List<AiChatTurn> _history = [];
    private SpeechRecognitionEngine? _microphone;
    private SpeechSynthesizer? _speaker;
    private bool _busy;

    public ObservableCollection<AiChatMessage> ChatMessages { get; } = [];
    // Open the existing human-reviewed replenishment form, never apply changes from AI.
    public Action? OpenReplenishment { get; set; }

    public AiAssistantWindow(InventoryState state)
    {
        InitializeComponent();
        _state = state;
        DataContext = this;
        ChatMessages.Add(new AiChatMessage(false,
            "Chào bạn! Mình có thể lập báo cáo nhập, xuất và tồn kho theo 7 hoặc 30 ngày, " +
            "cảnh báo vật tư sắp hết và giải thích số liệu. Báo cáo tại máy không cần Internet; " +
            "để trò chuyện với Gemini hãy lưu khóa API và nhấn Gửi."));
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
            ChatMessages.Add(new AiChatMessage(false,
                "BÁO CÁO TÍNH TẠI MÁY (không gửi lên mạng):\n" +
                AiInventoryReport.Build(_state, DateOnly.FromDateTime(DateTime.Today))));
            ActivityStatus.Text = "Đã lập báo cáo từ dữ liệu HISA đang lưu trên máy; chưa xác minh đồng bộ Excel.";
            ScrollToBottom();
        }
        catch (Exception ex) { ActivityStatus.Text = "Không lập được báo cáo: " + ex.Message; }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
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
        SendButton.IsEnabled = false;
        QuestionInput.IsEnabled = false;
        ChatMessages.Add(new AiChatMessage(true, question));
        QuestionInput.Clear();
        ScrollToBottom();
        ActivityStatus.Text = "Đang hỏi Gemini qua kết nối Internet…";
        try
        {
            var answer = await GeminiAssistantClient.AskAsync(key, question, context, prior);
            _history.Add(new AiChatTurn("user", question));
            _history.Add(new AiChatTurn("model", answer));
            ChatMessages.Add(new AiChatMessage(false, answer));
            ActivityStatus.Text = ShareDataCheck.IsChecked == true
                ? "Đã nhận trả lời; số liệu gửi tới Gemini là thống kê rút gọn."
                : "Đã nhận trả lời; không gửi thống kê kho.";
            if (SpeakAnswerCheck.IsChecked == true) Speak(answer);
        }
        catch (Exception ex)
        {
            ChatMessages.Add(new AiChatMessage(false, "Chưa thể kết nối Gemini: " + ex.Message));
            ActivityStatus.Text = "Lần hỏi chưa thành công; bạn có thể thử lại.";
        }
        finally
        {
            _busy = false;
            SendButton.IsEnabled = true;
            QuestionInput.IsEnabled = true;
            QuestionInput.Focus();
            ScrollToBottom();
        }
    }

    private void Speak(string text)
    {
        try
        {
            _speaker ??= new SpeechSynthesizer();
            _speaker.SpeakAsyncCancelAll();
            // Prefer an installed Vietnamese voice; otherwise a female voice.
            // The exact voice depends entirely on Windows speech packs.
            var voices = _speaker.GetInstalledVoices().Where(v => v.Enabled).ToList();
            var choice = voices.FirstOrDefault(v => v.VoiceInfo.Culture.TwoLetterISOLanguageName == "vi" &&
                v.VoiceInfo.Gender == VoiceGender.Female)
                ?? voices.FirstOrDefault(v => v.VoiceInfo.Culture.TwoLetterISOLanguageName == "vi")
                ?? voices.FirstOrDefault(v => v.VoiceInfo.Gender == VoiceGender.Female);
            if (choice is not null) _speaker.SelectVoice(choice.VoiceInfo.Name);
            _speaker.SetOutputToDefaultAudioDevice();
            _speaker.SpeakAsync(text.Length > 4000 ? text[..4000] : text);
        }
        catch (Exception ex)
        {
            ActivityStatus.Text = "Không thể đọc bằng âm thanh: " + ex.Message +
                " (kiểm tra giọng nói được cài đặt trên Windows).";
        }
    }

    private void Microphone_Click(object sender, RoutedEventArgs e)
    {
        if (_microphone is not null)
        {
            _microphone.RecognizeAsyncCancel();
            ActivityStatus.Text = "Đang dừng ghi âm…";
            return;
        }
        try
        {
            var info = SpeechRecognitionEngine.InstalledRecognizers()
                .FirstOrDefault(item => item.Culture.TwoLetterISOLanguageName == "vi");
            if (info is null)
            {
                ActivityStatus.Text = "Windows chưa có bộ nhận dạng giọng nói tiếng Việt. " +
                    "Bạn có thể nhập chữ hoặc cài bộ giọng nói tiếng Việt trong Windows.";
                return;
            }
            _microphone = new SpeechRecognitionEngine(info);
            _microphone.LoadGrammar(new DictationGrammar());
            _microphone.SetInputToDefaultAudioDevice();
            _microphone.SpeechRecognized += (_, args) =>
                Dispatcher.BeginInvoke(() =>
                {
                    if (args.Result.Confidence < 0.4 || string.IsNullOrWhiteSpace(args.Result.Text)) return;
                    QuestionInput.Text = args.Result.Text;
                    QuestionInput.Focus();
                    QuestionInput.CaretIndex = QuestionInput.Text.Length;
                    ActivityStatus.Text = "Đã nhận giọng nói; kiểm tra lại câu hỏi rồi nhấn Gửi Gemini.";
                });
            _microphone.RecognizeCompleted += (_, args) =>
                Dispatcher.BeginInvoke(() =>
                {
                    _microphone?.Dispose();
                    _microphone = null;
                    MicButton.Content = "🎙 Mic";
                    if (args.Error is not null) ActivityStatus.Text = "Lỗi nhận dạng giọng nói: " + args.Error.Message;
                });
            _microphone.RecognizeAsync(RecognizeMode.Single);
            MicButton.Content = "■ Dừng mic";
            ActivityStatus.Text = "Đang nghe tiếng Việt qua micro của Windows…";
        }
        catch (Exception ex)
        {
            _microphone?.Dispose();
            _microphone = null;
            MicButton.Content = "🎙 Mic";
            ActivityStatus.Text = "Không mở được micro: " + ex.Message;
        }
    }

    private void StopSpeaking_Click(object sender, RoutedEventArgs e)
    {
        _speaker?.SpeakAsyncCancelAll();
        ActivityStatus.Text = "Đã dừng đọc câu trả lời.";
    }

    private void ScrollToBottom() =>
        Dispatcher.BeginInvoke(() => ChatScroller.ScrollToEnd(), DispatcherPriority.Background);

    private void Close_Click(object sender, RoutedEventArgs e) => TryCloseAssistant();

    private bool TryCloseAssistant()
    {
        if (_busy)
        {
            ActivityStatus.Text = "Đang đợi Gemini trả lời; hãy đợi trước khi đóng trang.";
            return false;
        }
        try { _microphone?.RecognizeAsyncCancel(); }
        catch { /* Already stopped. */ }
        _microphone?.Dispose();
        _microphone = null;
        _speaker?.SpeakAsyncCancelAll();
        _speaker?.Dispose();
        _speaker = null;
        this.CompleteEmbedded(false);
        return true;
    }
}
