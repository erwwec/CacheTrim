using System;
using System.ComponentModel;
using System.Windows;

namespace DesktopPet;

public partial class MainWindow
{
    private Window? _assistantWindow;

    private void OpenAssistant_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        SettingsPopup.IsOpen = false;
        if (_assistantWindow == null)
        {
            _assistantWindow = new Window
            {
                Title = "Клод V2 — помощник (тест)",
                Width = 540, Height = 690, MinWidth = 400, MinHeight = 420,
                Owner = this, WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = AssistantContent, ShowInTaskbar = true
            };
            _assistantWindow.Closing += AssistantWindow_Closing;
            _assistantWindow.Closed += (_, _) => _assistantWindow = null;
        }
        if (!_assistantWindow.IsVisible) _assistantWindow.Show();
        _assistantWindow.Activate();
    }

    private void AssistantWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing) return;
        e.Cancel = true;
        _assistantWindow?.Hide();
    }

    private void StopAssistant_Click(object sender, RoutedEventArgs e)
    {
        StopVoice();
        StopCloud();
        AssistantCloudStatus.Text = "Текущий запрос остановлен. Уже выполненные действия не отменяются.";
    }

    private bool HideAssistantForScreenCapture()
    {
        if (_assistantWindow?.IsVisible != true) return false;
        _assistantWindow.Hide();
        return true;
    }

    private void RestoreAssistantAfterScreenCapture(bool wasVisible)
    {
        if (wasVisible && !_isClosing && _experimentalModel && _assistantWindow != null && !_assistantWindow.IsVisible)
            _assistantWindow.Show();
    }
}
