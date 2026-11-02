using System.Windows;
using System.Windows.Controls;
using HisaWarehouse.Services;

namespace HisaWarehouse;

public partial class MainWindow
{
    private bool _loadingMicrophonePreferences;

    private void LoadMicrophonePreferences()
    {
        _loadingMicrophonePreferences = true;
        try
        {
            SettingsRememberMicrophone.IsChecked = MicrophoneConsentPreference.RememberAllowed;
            var options = MicrophoneDevices.List();
            SettingsMicrophoneBox.ItemsSource = options;
            var selected = MicrophoneDevices.ResolveSavedDevice(options);
            SettingsMicrophoneBox.SelectedItem =
                options.FirstOrDefault(option => option.DeviceNumber == selected) ?? options[0];
        }
        catch (Exception error) { ShowToast("Không nạp được cài đặt micro: " + error.Message); }
        finally { _loadingMicrophonePreferences = false; }
    }

    private void SettingsRememberMicrophone_Click(object sender, RoutedEventArgs e)
    {
        if (_loadingMicrophonePreferences) return;
        try
        {
            MicrophoneConsentPreference.SetRememberAllowed(SettingsRememberMicrophone.IsChecked == true);
            ShowToast("Đã lưu quyền nhắc lại micro cho tài khoản Windows hiện tại.");
        }
        catch (Exception error)
        {
            SettingsRememberMicrophone.IsChecked = MicrophoneConsentPreference.RememberAllowed;
            ShowToast("Không lưu được cài đặt micro: " + error.Message);
        }
    }

    private void SettingsMicrophone_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingMicrophonePreferences ||
            SettingsMicrophoneBox.SelectedItem is not MicrophoneDeviceOption chosen) return;
        try
        {
            MicrophoneDevices.Save(chosen);
            ShowToast("Đã lưu thiết bị micro: " + chosen.Label);
        }
        catch (Exception error) { ShowToast("Không lưu được thiết bị micro: " + error.Message); }
    }

    private void SettingsMicrophoneRefresh_Click(object sender, RoutedEventArgs e) =>
        LoadMicrophonePreferences();
}
