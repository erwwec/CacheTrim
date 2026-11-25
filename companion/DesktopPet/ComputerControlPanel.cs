using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DesktopPet;

// Displays the actual screenshot and proposed click location; closing always cancels.
internal sealed class ComputerControlPanel : Window
{
    private readonly TextBlock _microphoneStatus = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6), FontWeight = FontWeights.Bold };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6), FontSize = 15 };
    private readonly Grid _imageGrid = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(5) };
    private readonly Button _approve = new() { Content = "Разрешить этот шаг", Margin = new Thickness(5), Padding = new Thickness(12, 6, 12, 6), IsEnabled = false };
    private readonly Button _finishVoice = new() { Content = "Закончить запись", Margin = new Thickness(5), Padding = new Thickness(12, 6, 12, 6), Visibility = Visibility.Collapsed };
    private readonly Button _stop = new() { Content = "Остановить задачу", Margin = new Thickness(5), Padding = new Thickness(12, 6, 12, 6) };
    private TaskCompletionSource<bool>? _decision;
    internal bool IsClosed { get; private set; }
    internal ComputerControlPanel(Action stop)
    {
        Title = "Клод V2 — управление ПК"; Width = 720; Height = 590; Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; ShowActivated = false;
        var root = new DockPanel { Margin = new Thickness(8) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(_approve); buttons.Children.Add(_finishVoice); buttons.Children.Add(_stop); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        DockPanel.SetDock(_microphoneStatus, Dock.Top); root.Children.Add(_microphoneStatus);
        DockPanel.SetDock(_status, Dock.Top); root.Children.Add(_status);
        root.Children.Add(new ScrollViewer { Content = _imageGrid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
        _approve.Click += (_, _) => { _approve.IsEnabled = false; _decision?.TrySetResult(true); };
        _stop.Click += (_, _) => { _decision?.TrySetResult(false); stop(); };
        Closed += (_, _) => { IsClosed = true; _decision?.TrySetResult(false); stop(); };
    }
    internal void SetStopListeningStatus(bool active) => _microphoneStatus.Text = active ? "● МИКРОФОН: слушаю «стоп» (локально). Можно остановить и кнопкой." : "Микрофон для голосовой остановки выключен; используйте кнопку.";
    internal void SetRecordingAction(Action? finish)
    {
        _finishVoice.Visibility = finish == null ? Visibility.Collapsed : Visibility.Visible;
        _finishVoice.Click -= FinishVoice_Click;
        _finishRecording = finish;
        if (finish != null) _finishVoice.Click += FinishVoice_Click;
    }
    private Action? _finishRecording;
    private void FinishVoice_Click(object sender, RoutedEventArgs e) { _finishVoice.IsEnabled = false; _finishRecording?.Invoke(); }
    internal void Processing(string text) { _approve.IsEnabled = false; _status.Text = text; _imageGrid.Children.Clear(); }
    internal async Task<bool> WaitForActionAsync(ComputerStep step, byte[] jpeg, bool requireReview, CancellationToken ct)
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = new MemoryStream(jpeg); bitmap.EndInit(); bitmap.Freeze();
        double scale = Math.Min(1, Math.Min(660d / bitmap.PixelWidth, 380d / bitmap.PixelHeight));
        double w = bitmap.PixelWidth * scale, h = bitmap.PixelHeight * scale;
        _imageGrid.Width = w; _imageGrid.Height = h; _imageGrid.Children.Clear();
        _imageGrid.Children.Add(new Image { Source = bitmap, Width = w, Height = h });
        if (step.Action is "click" or "double_click" or "scroll")
        {
            var canvas = new Canvas { Width = w, Height = h, IsHitTestVisible = false };
            var mark = new Ellipse { Width = 20, Height = 20, Stroke = Brushes.Red, StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)) };
            Canvas.SetLeft(mark, step.X * (w - 1) / 999d - 10); Canvas.SetTop(mark, step.Y * (h - 1) / 999d - 10); canvas.Children.Add(mark); _imageGrid.Children.Add(canvas);
        }
        string detail = step.Action switch {
            "click" => "Щелчок в красной точке", "double_click" => "Двойной щелчок в красной точке", "type" => "Ввести текст: «" + step.Text + "»",
            "key" => "Нажать: " + step.Key, "open_youtube" => "Открыть главную страницу YouTube", "open_telegram" => "Открыть Telegram Desktop", "set_volume" => "Установить общую громкость вывода на " + step.Level + "%", "scroll" => "Прокрутить в красной точке " + (step.Direction == "up" ? "вверх" : "вниз"), _ => step.Action };
        _status.Text = (requireReview ? "Требует проверки: " : "Автоматический шаг через 3 секунды: ") + detail + "\nЗачем: " + step.Reason + "\n" + (requireReview ? "Не подтверждай, если экран изменился или действие не нужно." : "Нажми «Остановить задачу», если действие неверное.");
        _decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _approve.Visibility = requireReview ? Visibility.Visible : Visibility.Collapsed;
        _approve.IsEnabled = requireReview;
        using var registration = ct.Register(() => Dispatcher.BeginInvoke(() => _decision?.TrySetResult(false)));
        if (!requireReview)
        {
            await ComputerSafety.WaitBeforeAutomaticAction(ct);
            ct.ThrowIfCancellationRequested();
            return true;
        }
        return await _decision.Task.WaitAsync(ct);
    }
}
