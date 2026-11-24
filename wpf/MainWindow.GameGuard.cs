using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowGameGuard()
    {
        try
        {
            var window = new Window { Title = "Защита во время игр", Width = 740, Height = 470, Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = "Очистка останавливается, если Windows сообщает о полноэкранном приложении или запущена выбранная игра. Системное обслуживание не запускается во время игры, но уже начатые инструменты Windows автоматически не прерываются. Укажи EXE каждой игры: обнаружение зависит от Windows и не гарантировано.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            var paths = new TextBox { Text = string.Join(Environment.NewLine, GameGuard.LoadPaths()), AcceptsReturn = true, Height = 210, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
            panel.Children.Add(paths);
            var buttons = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            var add = new System.Windows.Controls.Button { Content = "Добавить EXE игры", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0) };
            var save = new System.Windows.Controls.Button { Content = "Сохранить список", Padding = new Thickness(10, 5, 10, 5) }; buttons.Children.Add(add); buttons.Children.Add(save); panel.Children.Add(buttons);
            add.Click += (_, _) => { var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Выбери EXE игры", Filter = "Программы (*.exe)|*.exe", Multiselect = true }; if (dialog.ShowDialog(window) == true) paths.Text = string.Join(Environment.NewLine, paths.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Concat(dialog.FileNames)); };
            save.Click += (_, _) => { try { GameGuard.SavePaths(paths.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0)); window.Close(); } catch (Exception ex) { MessageBox.Show(window, ex.Message, "Не удалось сохранить", MessageBoxButton.OK, MessageBoxImage.Warning); } };
            window.Content = panel; window.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Защита игр", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
