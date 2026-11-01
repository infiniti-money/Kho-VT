using System.IO;
using Forms = System.Windows.Forms;

namespace HisaWarehouse;

public partial class MainWindow
{
    private Forms.NotifyIcon? _anTray;
    private System.Drawing.Icon? _anTrayIcon;

    private void InitializeAnTray()
    {
        StateChanged += OnAnWindowStateChanged;
        Closed += (_, _) => DisposeAnTray();
    }

    private void OnAnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != System.Windows.WindowState.Minimized ||
            _currentEmbeddedPage?.Source is not AiAssistantWindow an ||
            !an.KeepListeningWhenMinimized) return;

        try
        {
            if (_anTray is null)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "hisa.ico");
                if (File.Exists(path)) _anTrayIcon = new System.Drawing.Icon(path);
                var tray = new Forms.NotifyIcon
                {
                    Icon = _anTrayIcon ?? System.Drawing.SystemIcons.Application,
                    Text = "An · Trợ lý kho HISA — rảnh tay đang bật",
                    Visible = true
                };
                var menu = new Forms.ContextMenuStrip();
                menu.Items.Add("Mở HISA", null, (_, _) => Dispatcher.BeginInvoke(RestoreFromAnTray));
                menu.Items.Add("Dừng An và mở HISA", null, (_, _) => Dispatcher.BeginInvoke(() =>
                {
                    if (_currentEmbeddedPage?.Source is AiAssistantWindow current)
                        current.EndHandsFreeAndCancel();
                    RestoreFromAnTray();
                }));
                tray.ContextMenuStrip = menu;
                tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(RestoreFromAnTray);
                _anTray = tray;
            }
            _anTray.Visible = true;
            Hide();
            _anTray.ShowBalloonTip(2500, "An đang nghe",
                "HISA đã thu nhỏ. Âm thanh vẫn được gửi tới Gemini trong phiên đã đồng ý. " +
                "Nhấp đúp biểu tượng để mở hoặc chọn Dừng An.", Forms.ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            // A tray failure must not trap the application in an invisible state
            // or leave the microphone listening without a visible stop control.
            if (_currentEmbeddedPage?.Source is AiAssistantWindow current)
                current.EndHandsFreeAndCancel();
            Show();
            WindowState = System.Windows.WindowState.Normal;
            ShowToast("Không thể chạy An ở khay hệ thống: " + ex.Message);
        }
    }

    private void RestoreFromAnTray()
    {
        Show();
        WindowState = System.Windows.WindowState.Normal;
        Activate();
        if (_anTray is not null) _anTray.Visible = false;
    }

    private void DisposeAnTray()
    {
        if (_anTray is not null)
        {
            _anTray.Visible = false;
            _anTray.ContextMenuStrip?.Dispose();
            _anTray.Dispose();
            _anTray = null;
        }
        _anTrayIcon?.Dispose();
        _anTrayIcon = null;
    }
}
