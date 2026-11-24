using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowNetworkDiagnostics()
    {
        var window = new Window { Title = "Диагностика сети", Width = 690, Height = 480, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(20) }; var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Укажи имя или IP сервера, который хочешь проверить. При запуске выполняется DNS-запрос и до четырёх ICMP-запросов: DNS-сервер и выбранный узел могут увидеть эти запросы. Параметры сети не меняются.", TextWrapping = TextWrapping.Wrap });
        var input = new TextBox { Margin = new Thickness(0, 10, 0, 8) }; top.Children.Add(input);
        var button = new System.Windows.Controls.Button { Content = "Проверить", HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 0, 10) }; top.Children.Add(button);
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; root.Children.Add(output);
        using var cts = new CancellationTokenSource(); window.Closed += (_, _) => cts.Cancel();
        button.Click += async (_, _) => { button.IsEnabled = false; output.Text = "Выполняется проверка…"; try { output.Text = await NetworkDiagnostics.RunAsync(input.Text, cts.Token); } catch (OperationCanceledException) { output.Text = "Проверка отменена."; } catch (Exception ex) { output.Text = "Проверка не завершена: " + ex.Message; } finally { button.IsEnabled = true; } };
        window.Content = root; window.ShowDialog();
    }
}
