using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;

namespace CacheTrim;

public partial class MainWindow
{
    private void ShowCleanupOptions()
    {
        CleanupOptions options;
        try { options = CleanupOptions.Load(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Настройки очистки", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        var window = new Window { Title = "Правила очистки", Width = 760, Height = 740, MinWidth = 620, MinHeight = 500,
            Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var content = new StackPanel { Margin = new Thickness(24) };
        content.Children.Add(new TextBlock { Text = "Браузеры", FontSize = 19, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "Будут проверяться кэши только выбранных браузеров. Закрой браузер перед очисткой.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8), Foreground = Brush(Theme.Muted) });
        var browserBoxes = new Dictionary<string, CheckBox>();
        var browsers = new WrapPanel();
        foreach (var name in CleanupOptions.BrowserNames)
        {
            var box = new CheckBox { Content = name, IsChecked = options.IncludesBrowser(name), Margin = new Thickness(0, 4, 18, 5) };
            browserBoxes[name] = box;
            browsers.Children.Add(box);
        }
        content.Children.Add(browsers);
        content.Children.Add(new TextBlock { Text = "Исключения", FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 0) });
        content.Children.Add(new TextBlock { Text = "Полный путь к файлу или папке — по одному на строку. Эти пути не будут очищаться. Пока есть исключения, очистка корзины пропускается целиком.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8), Foreground = Brush(Theme.Muted) });
        var excludes = new TextBox { AcceptsReturn = true, Text = string.Join(Environment.NewLine, options.ExcludedPaths), Height = 112,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        content.Children.Add(excludes);
        content.Children.Add(new TextBlock { Text = "Минимальный возраст файлов", FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 18, 0, 0) });
        content.Children.Add(new TextBlock { Text = "Часы: 0 означает без ограничения. Для Temp и системных категорий сохраняются также уже заданные более строгие ограничения. Корзина очищается Windows целиком.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8), Foreground = Brush(Theme.Muted) });
        var ages = new Dictionary<string, TextBox>();
        var categories = new (string Key, string Label)[] { ("temp", "Временные файлы"), ("browsers", "Кэш браузеров"), ("shaders", "Кэш шейдеров"), ("thumbnails", "Эскизы"), ("reports", "Отчёты об ошибках"), ("dumps", "Дампы"), ("updates", "Кэш Windows Update"), ("uwp", "Кэши UWP"), ("prefetch", "Prefetch"), ("logs", "Системные журналы") };
        foreach (var category in categories)
        {
            var box = new TextBox { Text = options.AgeFor(category.Key).ToString(), Width = 100, Margin = new Thickness(8, 3, 0, 3) };
            ages[category.Key] = box;
            var row = new DockPanel(); DockPanel.SetDock(box, Dock.Right); row.Children.Add(box);
            row.Children.Add(new TextBlock { Text = category.Label, VerticalAlignment = VerticalAlignment.Center }); content.Children.Add(row);
        }
        var save = new System.Windows.Controls.Button { Content = "Сохранить", Padding = new Thickness(18, 8, 18, 8), HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 18, 0, 0) };
        save.Click += (_, _) => {
            try
            {
                var next = new CleanupOptions();
                next.ExcludedPaths = excludes.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                next.Browsers = browserBoxes.Where(x => x.Value.IsChecked == true).Select(x => x.Key).ToList();
                foreach (var pair in ages)
                {
                    if (!int.TryParse(pair.Value.Text, out int hours) || hours < 0 || hours > 87600) throw new ArgumentException("Возраст должен быть целым числом от 0 до 87600 часов.");
                    next.MinimumAgeHours[pair.Key] = hours;
                }
                next.Save(); App.Log("правила очистки сохранены"); window.Close();
            }
            catch (Exception ex) { MessageBox.Show(window, ex.Message, "Не удалось сохранить", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        content.Children.Add(save);
        window.Content = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        window.ShowDialog();
    }

    private async Task PreviewCleanupAsync(Config config)
    {
        await RunBusyAsync("подготавливается предпросмотр очистки…", async () => {
            Cleaner cleaner = await Task.Run(() => { var scan = new Cleaner(_ => { }); scan.Preview(config); return scan; });
            var text = new StringBuilder();
            text.AppendLine("Кандидаты: " + cleaner.FilesDeleted + " файлов, " + Cleaner.FormatBytes(cleaner.BytesFreed));
            text.AppendLine("Это оценка: занятые и недоступные файлы могут быть пропущены при очистке. Список ограничен 3000 путями.");
            text.AppendLine("Память, сеть и очистка корзины в список файлов не входят. Ничего ещё не удалено.");
            text.AppendLine();
            foreach (string path in cleaner.PreviewPaths) text.AppendLine(path);
            var window = new Window { Title = "Предпросмотр очистки", Width = 880, Height = 650, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            window.Content = new TextBox { Text = text.ToString(), IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(12) };
            window.ShowDialog();
        });
    }
}
