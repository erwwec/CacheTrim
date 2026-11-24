using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using TextBox = System.Windows.Controls.TextBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowIdleSchedule()
    {
        var window = new Window { Title = "Расписание очистки", Width = 650, Height = 380, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Ежедневная задача проверяет выбранные категории только при простое Windows и питании от сети. Защита игр действует и для неё. Компьютер не будет специально разбужен. На батарее запуск откладывается; пропущенная задача может запуститься позже. Используются текущие настройки очистки.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "Время (часы:минуты):" });
        var time = new TextBox { Text = "", Width = 120, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 12) }; panel.Children.Add(time);
        var status = new TextBlock { Text = "Проверка текущего состояния…", Margin = new Thickness(0, 0, 0, 8) }; panel.Children.Add(status);
        window.Loaded += async (_, _) => { try { status.Text = await Task.Run(TaskSetup.IsIdleInstalled) ? "Задача существует. Укажи время, чтобы заменить её настройки." : "Задача не найдена."; } catch { status.Text = "Не удалось проверить состояние задачи."; } };
        var controls = new WrapPanel(); panel.Children.Add(controls);
        var save = new System.Windows.Controls.Button { Content = "Включить расписание", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0) };
        var remove = new System.Windows.Controls.Button { Content = "Отключить расписание", Padding = new Thickness(12, 6, 12, 6) }; controls.Children.Add(save); controls.Children.Add(remove);
        save.Click += async (_, _) => {
            if (!TimeSpan.TryParseExact(time.Text.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var parsed) || parsed >= TimeSpan.FromDays(1)) { MessageBox.Show(window, "Укажи время в формате 00:00–23:59."); return; }
            if (MessageBox.Show(window, "Включить ежедневную очистку выбранных категорий в " + time.Text + " при простое?", "Расписание", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            save.IsEnabled = false; remove.IsEnabled = false;
            string exe = Environment.ProcessPath ?? string.Empty;
            string error = await Task.Run(() => TaskSetup.InstallIdle(exe, parsed));
            MessageBox.Show(window, error == null ? "Расписание включено." : "Не удалось включить: " + error); if (error == null) status.Text = "Задача существует. Укажи время, чтобы заменить её настройки.";
            save.IsEnabled = true; remove.IsEnabled = true;
        };
        remove.Click += async (_, _) => { save.IsEnabled = false; remove.IsEnabled = false; string error = await Task.Run(TaskSetup.UninstallIdle); MessageBox.Show(window, error == null ? "Расписание отключено." : "Не удалось отключить: " + error); if (error == null) status.Text = "Задача не найдена."; save.IsEnabled = true; remove.IsEnabled = true; };
        window.Content = panel; window.ShowDialog();
    }
}
