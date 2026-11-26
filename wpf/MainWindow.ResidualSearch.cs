using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowResidualSearch()
    {
        var window = new Window { Title = "Поиск возможных остатков", Width = 800, Height = 550, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) }; var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Поиск папок, название которых содержит введённое слово, в Program Files и AppData текущего пользователя. Совпадения могут быть данными других программ. Ничего не удаляется; список не гарантирует обнаружения всех остатков. Не удаляй папки без проверки и резервной копии.", TextWrapping = TextWrapping.Wrap });
        var input = new TextBox { Margin = new Thickness(0, 8, 0, 8), ToolTip = "Часть названия программы, от 4 символов" }; top.Children.Add(input);
        var search = new System.Windows.Controls.Button { Content = "Найти возможные папки", Padding = new Thickness(10, 5, 10, 5), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) }; top.Children.Add(search);
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; root.Children.Add(output);
        search.Click += async (_, _) => { search.IsEnabled = false; output.Text = "Поиск…"; try { string term = input.Text; var r = await Task.Run(() => ResidualSearch.Find(term, ResidualSearch.DefaultRoots())); var text = new StringBuilder("Совпадения по названию, не доказанные остатки:\n\n"); foreach (var path in r.Paths) text.AppendLine(path); if (r.Paths.Count == 0) text.AppendLine("Совпадений нет."); text.AppendLine("\nНедоступных папок поиска: " + r.UnavailableRoots + (r.Truncated ? "; список ограничен." : ".")); output.Text = text.ToString(); } catch (Exception ex) { output.Text = "Поиск не выполнен: " + ex.Message; } finally { search.IsEnabled = true; } };
        window.Content = root; window.ShowDialog();
    }
}
