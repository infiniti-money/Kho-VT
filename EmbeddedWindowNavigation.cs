using System.Windows;

namespace HisaWarehouse;

public static class EmbeddedWindowNavigation
{
    public static event Action<string>? NotificationRequested;

    public static void NotifyInline(this Window window, string message)
    {
        NotificationRequested?.Invoke(message);
    }

    public static void CompleteEmbedded(this Window window, bool accepted)
    {
        // An embedded WPF Window is not actually Closed by main-page navigation.
        // Stop mic and cancel Internet requests before detaching the AI content.
        if (window is AiAssistantWindow ai)
            ai.EndHandsFreeAndCancel();
        if (window.Tag is Action<bool> completion)
        {
            window.Tag = null;
            completion(accepted);
            return;
        }
        if (window.IsVisible) window.DialogResult = accepted;
        else window.Close();
    }
}
