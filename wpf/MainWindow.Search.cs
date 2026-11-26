using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace CacheTrim;

public partial class MainWindow
{
    private sealed class SearchCommand(MainWindow owner) : ICommand
    {
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => owner.OpenSearch();
    }

    private static readonly (string Label, string Keywords, string Page)[] SearchEntries =
    [
        ("Память и её очистка", "озу оперативная standby кэш", "memory"),
        ("Очистка файлов", "временные файлы браузеры корзина кэш предпросмотр исключения", "clean"),
        ("Оптимизация Windows", "твики настройки игры производительность", "optimize"),
        ("План питания", "ноутбук высокая производительность", "advanced"),
        ("Проверка Windows и диска", "sfc dism chkdsk восстановление", "advanced"),
        ("Диагностика сети", "интернет пинг dns подключение", "advanced"),
        ("Частота экранов", "монитор герцы экран", "advanced"),
        ("Тест процессора", "бенчмарк скорость cpu история", "advanced"),
        ("Здоровье дисков", "накопители smart температура износ ошибки", "advanced"),
        ("Защита для античитов", "secure boot tpm античит безопасность", "advanced"),
        ("Питомец Клод и аниме-аватар", "компаньон песни текст музыка", "pet"),
        ("Автозагрузка", "запуск приложения включить отключить", "startup"),
        ("Журнал", "лог ошибки история", "log"),
        ("Что нового", "история версий обновления изменения changelog релизы", "whatsnew"),
        ("Оформление и обновления", "тема жидкое стекло цвет версии настройки", "settings"),
        ("Расписание очистки", "таймер автоматическая чистка", "clean"),
    ];

    private void OpenSearch_Click(object sender, RoutedEventArgs e) => OpenSearch();
    private void OpenSearch()
    {
        var window = new Window { Title = "Поиск функций", Width = 560, Height = 480, MinWidth = 420, MinHeight = 300,
            Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) };
        var query = new System.Windows.Controls.TextBox { FontSize = 15, Padding = new Thickness(8), ToolTip = "Название функции или ключевые слова" };
        DockPanel.SetDock(query, Dock.Top); root.Children.Add(query);
        var openButton = new System.Windows.Controls.Button { Content = "Открыть раздел", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        DockPanel.SetDock(openButton, Dock.Bottom); root.Children.Add(openButton);
        var results = new System.Windows.Controls.ListBox { Margin = new Thickness(0, 10, 0, 0) }; root.Children.Add(results);
        void Refresh() {
            var words = query.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            results.Items.Clear();
            foreach (var item in SearchEntries.Where(x => words.All(w => (x.Label + " " + x.Keywords).Contains(w, StringComparison.OrdinalIgnoreCase))))
                results.Items.Add(new ListBoxItem { Content = item.Label, Tag = item.Page, Padding = new Thickness(8) });
            if (results.Items.Count > 0) results.SelectedIndex = 0;
        }
        void OpenSelected() {
            if (results.SelectedItem is ListBoxItem item && item.Tag is string page) { window.Close(); Navigate(page); }
        }
        query.TextChanged += (_, _) => Refresh();
        query.KeyDown += (_, e) => { if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; } else if (e.Key == Key.Down && results.Items.Count > 0) { results.Focus(); } };
        openButton.Click += (_, _) => OpenSelected();
        results.MouseDoubleClick += (_, _) => OpenSelected();
        results.KeyDown += (_, e) => { if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; } };
        window.Content = root; Refresh(); window.Loaded += (_, _) => query.Focus(); window.ShowDialog();
    }
}
