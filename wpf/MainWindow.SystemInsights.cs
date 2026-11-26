using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace CacheTrim;
public partial class MainWindow
{
    private void ShowSystemInsight(string title, string description, Func<string> read)
    {
        var window = new Window { Title = title, Width = 740, Height = 540, MinWidth = 480, MinHeight = 320, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) };
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap });
        var button = new System.Windows.Controls.Button { Content = "Проверить", HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 10, 0, 10) }; top.Children.Add(button);
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10) }; root.Children.Add(output);
        button.Click += async (_, _) => { button.IsEnabled = false; output.Text = "Запрашиваем данные Windows…"; try { output.Text = await Task.Run(read); } catch (Exception ex) { output.Text = "Не удалось выполнить проверку: " + ex.Message; } finally { button.IsEnabled = true; } };
        window.Content = root; window.ShowDialog();
    }
}
