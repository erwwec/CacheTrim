using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowGameCapture()
    {
        var window = new Window { Title = "Запись игровой сессии", Width = 880, Height = 650, MinWidth = 600, MinHeight = 450, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) }; var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Выбери запущенную игру и официальный PresentMon 2.6.0 x64. Проверяется закреплённый SHA-256, затем файл запускается с правами CacheTrim. Сбор может не поддерживаться игрой, драйвером или античитом. CSV сохраняется локально.", TextWrapping = TextWrapping.Wrap });
        var bar = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) }; top.Children.Add(bar);
        System.Windows.Controls.Button Button(string title) { var b = new System.Windows.Controls.Button { Content = title, Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 0, 8, 8) }; bar.Children.Add(b); return b; }
        var refresh = Button("Обновить процессы");
        bar.Children.Add(new TextBlock { Text = "Секунд:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 8) });
        var duration = new System.Windows.Controls.ComboBox { Width = 65, ItemsSource = new[] { 10, 30, 60, 120 }, SelectedIndex = 1, Margin = new Thickness(0, 0, 8, 8) }; bar.Children.Add(duration);
        bar.Children.Add(new TextBlock { Text = "Задержка:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 8) });
        var delay = new System.Windows.Controls.ComboBox { Width = 60, ItemsSource = new[] { 0, 5, 10, 30 }, SelectedIndex = 0, Margin = new Thickness(0, 0, 8, 8) }; bar.Children.Add(delay);
        var run = Button("Выбрать PresentMon и записать");
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(output, Dock.Bottom); root.Children.Add(output);
        var list = new System.Windows.Controls.ListBox { HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch }; root.Children.Add(list);
        async Task Refresh() { refresh.IsEnabled = false; try { list.ItemsSource = await Task.Run(ProcessManager.List); } catch (Exception ex) { output.Text = ex.Message; } finally { refresh.IsEnabled = true; } }
        refresh.Click += async (_, _) => await Refresh();
        run.Click += async (_, _) =>
        {
            if (list.SelectedItem is not ProcessManager.Entry selected) { output.Text = "Выбери запущенную игру в списке."; return; }
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Выбери официальный PresentMon 2.6.0 x64", Filter = "Программы (*.exe)|*.exe" }; if (dlg.ShowDialog(window) != true) return;
            if (MessageBox.Show(window, "Записать " + selected.Name + " (PID " + selected.Id + ")?\n\nБудет запущен файл:\n" + dlg.FileName + "\nс правами администратора после проверки SHA-256. Убедись, что правила игры разрешают эту утилиту.", "Запись игровой сессии", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            run.IsEnabled = false; refresh.IsEnabled = false; output.Text = "Идёт запись… Дождись завершения.";
            try { output.Text = await GameCapture.RunAsync(dlg.FileName, selected, (int)duration.SelectedItem, (int)delay.SelectedItem); }
            catch (Exception ex) { output.Text = "Запись не выполнена: " + ex.Message; }
            finally { run.IsEnabled = true; refresh.IsEnabled = true; }
        };
        window.Loaded += async (_, _) => await Refresh(); window.Content = root; window.ShowDialog();
    }
}
