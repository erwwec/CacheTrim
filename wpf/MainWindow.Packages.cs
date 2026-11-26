using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;

namespace CacheTrim;
public partial class MainWindow
{
    private void ShowPackageCatalog()
    {
        var window = new Window { Title = "Программы и магазин", Width = 900, Height = 690, MinWidth = 620, MinHeight = 450, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(16) }; var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Поиск и управление через установленный в Windows WinGet. Данные запрашиваются из выбранного источника. Проверяй издателя и ID: установка и удаление выполняются сторонними установщиками и могут менять файлы. Лицензии пакетов автоматически не принимаются.", TextWrapping = TextWrapping.Wrap });
        var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; top.Children.Add(row);
        var source = new System.Windows.Controls.ComboBox { Width = 115, ItemsSource = new[] { "winget", "msstore" }, SelectedIndex = 0, Margin = new Thickness(0, 0, 8, 8) }; row.Children.Add(source);
        var query = new TextBox { Width = 220, ToolTip = "Название программы для поиска", Margin = new Thickness(0, 0, 8, 8) }; row.Children.Add(query);
        System.Windows.Controls.Button Button(string title) { var b = new System.Windows.Controls.Button { Content = title, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 8) }; row.Children.Add(b); return b; }
        var search = Button("Найти"); var installed = Button("Установленные");
        var row2 = new WrapPanel(); top.Children.Add(row2); var id = new TextBox { Width = 220, ToolTip = "Точный ID из результатов", Margin = new Thickness(0, 0, 8, 8) }; row2.Children.Add(id);
        var install = new System.Windows.Controls.Button { Content = "Установить по ID", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 8) }; row2.Children.Add(install);
        var remove = new System.Windows.Controls.Button { Content = "Удалить по ID", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 8) }; row2.Children.Add(remove);
        top.Children.Add(new TextBlock { Text = "Скопируй точный ID из результатов в поле выше. Удаление выполняет деинсталлятор программы; данные могут быть удалены. Результаты списка не гарантируют наличие всех приложений.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        var output = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }; root.Children.Add(output);
        async Task Run(string[] args)
        {
            foreach (var b in new[] { search, installed, install, remove }) b.IsEnabled = false;
            output.Text = "Ожидание WinGet… Установка и удаление могут занять несколько минут.";
            try { output.Text = await PackageCatalog.RunAsync(args); } catch (Exception ex) { output.Text = "Не удалось выполнить команду: " + ex.Message; }
            finally { foreach (var b in new[] { search, installed, install, remove }) b.IsEnabled = true; }
        }
        bool ConfirmSource() => MessageBox.Show(window, "WinGet может связаться с источниками и принять условия их использования (--accept-source-agreements). Продолжить?", "Источники WinGet", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        search.Click += async (_, _) => { try { var a = PackageCatalog.SearchArgs(query.Text, (string)source.SelectedItem); if (ConfirmSource()) await Run(a); } catch (Exception ex) { output.Text = ex.Message; } };
        installed.Click += async (_, _) => { if (ConfirmSource()) await Run(PackageCatalog.ListArgs()); };
        install.Click += async (_, _) => { try { var a = PackageCatalog.InstallArgs(id.Text.Trim(), (string)source.SelectedItem); if (MessageBox.Show(window, "Установить пакет " + id.Text.Trim() + " из " + source.SelectedItem + "? WinGet может принять условия источника и скачать сторонний установщик. Проверь издателя и пакет заранее.", "Подтверждение установки", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await Run(a); } catch (Exception ex) { output.Text = ex.Message; } };
        remove.Click += async (_, _) => { try { var a = PackageCatalog.UninstallArgs(id.Text.Trim()); if (MessageBox.Show(window, "Удалить " + id.Text.Trim() + "? Деинсталлятор может удалить настройки и данные. Это действие нельзя автоматически отменить.", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) await Run(a); } catch (Exception ex) { output.Text = ex.Message; } };
        window.Content = root; window.ShowDialog();
    }
}
