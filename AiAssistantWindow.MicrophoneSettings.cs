using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class AiAssistantWindow
{
    private bool _updatingMicrophoneList;
    private int _selectedMicrophone = -1;

    private void LoadMicrophones(bool restorePreference)
    {
        _updatingMicrophoneList = true;
        try
        {
            var options = MicrophoneDevices.List();
            var requested = restorePreference
                ? MicrophoneDevices.ResolveSavedDevice(options) : _selectedMicrophone;
            var option = options.FirstOrDefault(x => x.DeviceNumber == requested) ?? options[0];
            _selectedMicrophone = option.DeviceNumber;
            InputMicrophoneBox.ItemsSource = options;
            InputMicrophoneBox.SelectedItem = option;
            MicrophoneLevelBar.Value = 0;
            if (options.Count == 1)
                ActivityStatus.Text = "Windows không tìm thấy micro. Hãy cắm thiết bị và bấm Làm mới mic.";
        }
        catch (Exception ex) { ActivityStatus.Text = "Không liệt kê được micro: " + ex.Message; }
        finally { _updatingMicrophoneList = false; }
    }

    private void RefreshMicrophones_Click(object sender, RoutedEventArgs e)
    {
        if (_handsFreeEnabled) StopHandsFree("Đã ngừng nghe khi làm mới danh sách micro.");
        StopManualMicrophone();
        LoadMicrophones(false);
        ActivityStatus.Text = InputMicrophoneBox.SelectedItem is MicrophoneDeviceOption choice
            ? "Đã làm mới micro. Đang chọn: " + choice.Label
            : "Không tìm thấy micro đang sử dụng.";
    }

    private void MicrophoneSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingMicrophoneList || InputMicrophoneBox?.SelectedItem is not MicrophoneDeviceOption selected)
            return;
        if (_handsFreeEnabled) StopHandsFree("Đã dừng rảnh tay vì bạn đổi micro đầu vào. Hãy bật lại để đồng ý.");
        StopManualMicrophone();
        _selectedMicrophone = selected.DeviceNumber;
        MicrophoneLevelBar.Value = 0;
        try
        {
            MicrophoneDevices.Save(selected);
            ActivityStatus.Text = "An sẽ dùng: " + selected.Label + ". Nhấn Mic hoặc Bật rảnh tay để thử.";
        }
        catch (Exception ex)
        {
            ActivityStatus.Text = "Đã chọn micro cho phiên này nhưng chưa lưu được tùy chọn: " + ex.Message;
        }
    }

    private void DisplayMicrophoneLevel(int level, bool validSession)
    {
        if (!validSession) return;
        MicrophoneLevelBar.Value = level;
    }

    private async void TestGemini_Click(object sender, RoutedEventArgs e)
    {
        if (_handsFreeEnabled || _manualMicCts is not null || _busy)
        {
            ActivityStatus.Text = "Hãy dừng micro hoặc kết thúc yêu cầu hiện tại rồi kiểm tra API.";
            return;
        }
        TestGeminiButton.IsEnabled = false;
        ActivityStatus.Text = "Đang kiểm tra kết nối và quyền truy cập 3 mô hình Gemini…";
        try
        {
            var key = GeminiKeyStore.Load();
            var result = await GeminiAssistantClient.TestConnectionAsync(key);
            if (!_sessionClosed) ActivityStatus.Text = "✓ " + result;
        }
        catch (Exception ex)
        {
            if (!_sessionClosed) ActivityStatus.Text = "Không kết nối được: " + ex.Message;
        }
        finally { if (!_sessionClosed) TestGeminiButton.IsEnabled = true; }
    }
}
