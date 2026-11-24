using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowDisplayDiagnostics()
    {
        var window = new Window { Title = "Частота экранов", Width = 720, Height = 520, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) };
        var footer = new System.Windows.Controls.Button { Content = "Открыть параметры дисплея Windows", Padding = new Thickness(12, 6, 12, 6), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Click += (_, _) => OpenSystemTool("ms-settings:display-advanced");
        var result = new StringBuilder();
        result.AppendLine("Частота и доступные режимы по данным Windows, только для текущего разрешения. Значения целочисленные и могут округляться. Доступность зависит от монитора, кабеля, драйвера и других настроек; это не гарантия, что каждый режим можно включить. Ничего не меняется."); result.AppendLine();
        try
        {
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                try { var r = DisplayDiagnostics.Read(screen.DeviceName); result.AppendLine(r.Name + (screen.Primary ? " (основной)" : "")); result.AppendLine(r.Width + " × " + r.Height + ", текущая частота: " + (r.CurrentHz > 1 ? r.CurrentHz + " Гц" : "неизвестно")); result.AppendLine("Режимы Windows: " + (r.ReportedHz.Length > 0 ? string.Join(", ", r.ReportedHz.Select(x => x + " Гц")) : "неизвестны")); result.AppendLine(); }
                catch (Exception ex) { result.AppendLine(screen.DeviceName + ": " + ex.Message); }
            }
        }
        catch (Exception ex) { result.AppendLine("Не удалось получить экраны: " + ex.Message); }
        root.Children.Add(new TextBox { Text = result.ToString(), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(10) });
        window.Content = root; window.ShowDialog();
    }
}
