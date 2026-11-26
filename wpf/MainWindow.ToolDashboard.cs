using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using UiButton = Wpf.Ui.Controls.Button;

namespace CacheTrim;

public partial class MainWindow
{
    // Keep the original handlers and their confirmations: this only changes presentation.
    private UiButton ToolDashboardRow(string id, string title, string hint, UiButton button)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brush(Theme.Text), TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = hint, FontSize = 11, Foreground = Brush(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.Children.Add(text);
        var arrow = new TextBlock { Text = "→", FontSize = 18, Foreground = Brush(Theme.Accent), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 2, 0) };
        Grid.SetColumn(arrow, 1); layout.Children.Add(arrow);
        button.Content = layout;
        button.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.MinWidth = 0;
        button.MinHeight = 64;
        button.Height = double.NaN;
        button.Margin = new Thickness(0, 0, 0, 7);
        button.Padding = new Thickness(12, 9, 12, 9);
        button.CornerRadius = new CornerRadius(8);
        AutomationProperties.SetName(button, title);
        AutomationProperties.SetAutomationId(button, "tool-" + id);
        return button;
    }

    private Border ToolDashboardGroup(string number, string title, string description, params UiButton[] rows)
    {
        var stack = new StackPanel();
        var heading = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel();
        labels.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brush(Theme.Text), TextWrapping = TextWrapping.Wrap });
        labels.Children.Add(new TextBlock { Text = description, FontSize = 11, Foreground = Brush(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
        heading.Children.Add(labels);
        var badge = new Border { CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(35, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)), Padding = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 0, 0, 0), Child = new TextBlock { Text = number, Foreground = Brush(Theme.Accent), FontSize = 11, FontWeight = FontWeights.Bold } };
        Grid.SetColumn(badge, 1); heading.Children.Add(badge); stack.Children.Add(heading);
        foreach (var row in rows) stack.Children.Add(row);
        var card = new Border { Child = stack, Padding = new Thickness(18, 17, 18, 11), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), BorderBrush = Brush(Theme.Line), Background = new SolidColorBrush(Theme.IsGlass ? System.Windows.Media.Color.FromArgb(184, Theme.Surface.R, Theme.Surface.G, Theme.Surface.B) : Theme.Surface), VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetAutomationId(card, "tools-group-" + number);
        return card;
    }

    private void AddToolDashboard()
    {
        var title = new TextBlock { Text = "Инструменты", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Brush(Theme.Text), Margin = new Thickness(0, 10, 0, 4) };
        _pageContent!.Children.Add(title);
        _pageContent.Children.Add(new TextBlock { Text = "Выбери нужный раздел. Действия, меняющие систему или данные, запрашивают подтверждение.", FontSize = 12, Foreground = Brush(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14) });

        var windows = ToolDashboardGroup("01", "Windows и обслуживание", "Проверка системы и встроенные средства",
            ToolDashboardRow("windows-files", "Файлы Windows", "Проверка и попытка восстановления системных файлов", AdvancedAction("Проверить файлы Windows", "Проверит защищённые файлы и попробует восстановить повреждённые. Может занять несколько минут.", "Проверяются файлы Windows…", "sfc.exe", "/scannow", 30 * 60 * 1000)),
            ToolDashboardRow("disk-scan", "Проверка диска C:", "Онлайн-проверка файловой системы", AdvancedAction("Проверить диск C:", "Проверит файловую систему диска C: без отключения тома. Исправление ошибок не запускается.", "Проверяется диск C:…", "chkdsk.exe", "C: /scan", 60 * 60 * 1000)),
            ToolDashboardRow("resource-monitor", "Монитор ресурсов", "Процессор, память, диск и сеть в Windows", Action("Монитор ресурсов", () => OpenSystemTool("resmon.exe"))),
            ToolDashboardRow("windows-update", "Центр обновления Windows", "Открыть настройки обновлений", Action("Центр обновления Windows", () => OpenSystemTool("ms-settings:windowsupdate"))));

        var games = ToolDashboardGroup("02", "Игры и процессы", "Игровые сессии и запущенные приложения",
            ToolDashboardRow("games", "Мои игры", "Личный список игр и защита от очистки", Action("Мои игры", ShowGameLibrary)),
            ToolDashboardRow("capture", "Запись игровой сессии", "Замер кадров через отдельный PresentMon", Action("Запись игровой сессии", ShowGameCapture)),
            ToolDashboardRow("processes", "Процессы", "Список, приоритеты и ручные профили", Action("Процессы", ShowProcessManager)),
            ToolDashboardRow("affinity", "Ядра процесса", "Выбор доступных процессу CPU, не резервирование", Action("Процессоры для процесса", ShowProcessAffinity)));

        var diagnostics = ToolDashboardGroup("03", "Диагностика", "Показатели оборудования и проверки",
            ToolDashboardRow("disk-health", "Здоровье дисков", "Состояние и доступные показатели накопителей", Action("Здоровье дисков", () => ShowSystemInsight("Здоровье дисков", "Показатели физических накопителей по данным Windows. Поддержка зависит от устройства и драйвера.", SystemInsights.RunDisk))),
            ToolDashboardRow("cpu-test", "Тест процессора", "Короткий однопоточный тест и локальная история", Action("Тест процессора", () => ShowSystemInsight("Локальный тест процессора", "Трёхсекундный однопоточный тест SHA-256. Процессор будет нагружен; история сохраняется локально. Это не оценка всей системы и не измерение FPS.", LocalBenchmark.Run))),
            ToolDashboardRow("display", "Частота экранов", "Текущий режим и доступные частоты", Action("Частота экранов", ShowDisplayDiagnostics)),
            ToolDashboardRow("network", "Диагностика сети", "Проверка соединения и DNS", Action("Диагностика сети", ShowNetworkDiagnostics)),
            ToolDashboardRow("security", "Защита для античитов", "Проверка TPM и Secure Boot без изменений", Action("Защита для античитов", () => ShowSystemInsight("Защита для античитов", "Проверка Secure Boot и TPM без изменения настроек.", SystemInsights.RunSecurity))));

        var programs = ToolDashboardGroup("04", "Программы", "Каталог и поиск после удаления",
            ToolDashboardRow("packages", "Программы и магазин", "Поиск, установка и удаление через WinGet", Action("Программы и магазин", ShowPackageCatalog)),
            ToolDashboardRow("residual", "Возможные остатки", "Поиск похожих папок без удаления", Action("Поиск возможных остатков", ShowResidualSearch)));

        var grid = new Grid();
        bool wide = PageScroll.ViewportWidth >= 1050;
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (wide) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var groups = new[] { windows, games, diagnostics, programs };
        int columns = wide ? 2 : 1;
        for (int i = 0; i < (groups.Length + columns - 1) / columns; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < groups.Length; i++) {
            Grid.SetRow(groups[i], i / columns); Grid.SetColumn(groups[i], i % columns);
            groups[i].Margin = new Thickness(0, 0, wide && i % columns == 0 ? 12 : 0, 12);
            grid.Children.Add(groups[i]);
        }
        _pageContent.Children.Add(grid);
    }
}
