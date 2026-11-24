using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace CacheTrim;
public partial class MainWindow
{
    private void ShowFileReview()
    {
        var window = new Window { Title = "Поиск файлов", Width = 920, Height = 650, MinWidth = 660, MinHeight = 480,
            Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) };
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.Children.Add(new TextBlock { Text = "Выбери папку и тип поиска. Ничего не удаляется автоматически; проверь файл перед удалением в Проводнике.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var controls = new WrapPanel(); header.Children.Add(controls);
        var modes = new System.Windows.Controls.ComboBox { Width = 220, Margin = new Thickness(0, 0, 8, 0), SelectedIndex = 0 };
        modes.Items.Add("Крупные файлы"); modes.Items.Add("Дубликаты по SHA-256"); modes.Items.Add("Старые установщики"); modes.Items.Add("Записи и изображения"); controls.Children.Add(modes);
        var browse = new System.Windows.Controls.Button { Content = "Выбрать папку и искать", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0) }; controls.Children.Add(browse);
        var cancel = new System.Windows.Controls.Button { Content = "Остановить", Padding = new Thickness(12, 5, 12, 5), IsEnabled = false }; controls.Children.Add(cancel);
        var status = new TextBlock { Text = "Выбери папку для поиска.", Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap }; header.Children.Add(status);
        var footer = new System.Windows.Controls.Button { Content = "Открыть расположение выбранного файла", Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var list = new System.Windows.Controls.ListBox { HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch }; root.Children.Add(list);
        CancellationTokenSource? running = null;
        cancel.Click += (_, _) => running?.Cancel();
        footer.Click += (_, _) => {
            if (list.SelectedItem is not ReviewItem item) return;
            try { var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = false }; info.ArgumentList.Add(item.IsFolder ? item.Path : "/select," + item.Path); Process.Start(info); }
            catch (Exception ex) { MessageBox.Show(window, ex.Message, "Не удалось открыть папку", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        browse.Click += async (_, _) => {
            var dialog = new OpenFolderDialog { Title = "Выбери папку для поиска файлов", Multiselect = false };
            if (dialog.ShowDialog(window) != true) return;
            var mode = (ReviewMode)modes.SelectedIndex;
            using var cts = new CancellationTokenSource(); running = cts; browse.IsEnabled = false; modes.IsEnabled = false; cancel.IsEnabled = true; list.Items.Clear(); status.Text = "Поиск может занять время…";
            try
            {
                var result = await Task.Run(() => FileReview.Scan(dialog.FolderName, mode, cts.Token), cts.Token);
                foreach (var item in result.Items) list.Items.Add(item);
                status.Text = "Проверено файлов: " + result.Examined + "; результатов: " + result.Items.Count + "; пропущено: " + result.Skipped + (result.Truncated ? ". Список неполный из-за ограничения." : ".");
            }
            catch (OperationCanceledException) { status.Text = "Поиск остановлен."; }
            catch (Exception ex) { status.Text = "Поиск не выполнен: " + ex.Message; }
            finally { running = null; browse.IsEnabled = true; modes.IsEnabled = true; cancel.IsEnabled = false; }
        };
        window.Closed += (_, _) => running?.Cancel();
        window.Content = root; window.ShowDialog();
    }
}
