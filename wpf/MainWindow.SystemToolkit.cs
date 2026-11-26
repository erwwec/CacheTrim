using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using MessageBox = System.Windows.MessageBox;

namespace CacheTrim;

public partial class MainWindow
{
    private sealed record ProcessItem(ManagedProcessTools.ProcessIdentity Identity)
    {
        public override string ToString() => $"{Identity.Name}  ·  PID {Identity.Pid}  ·  {Identity.Path}";
    }
    private static List<ProcessItem> AvailableProcesses()
    {
        var rows = new List<ProcessItem>();
        foreach (var p in Process.GetProcesses()) {
            using (p) { try { var id = ManagedProcessTools.Identify(p); if (!ManagedProcessTools.IsProtected(id, Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.ProcessId)) rows.Add(new(id)); } catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or ArgumentException) {} }
        }
        return rows.OrderBy(x => x.Identity.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    private Window ToolWindow(string title, double height = 630) => new() { Title = title, Width = 900, Height = height, MinWidth = 650, MinHeight = 400, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
    private Button ToolButton(string text) => new() { Content = text, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 4, 8, 6), Background = Brush(Theme.SurfaceAlt), Foreground = Brush(Theme.Text), BorderBrush = Brush(Theme.Accent) };
    private TextBox ToolInput() => new() { Background = Brush(Theme.SurfaceAlt), Foreground = Brush(Theme.Text), CaretBrush = Brush(Theme.Text), BorderBrush = Brush(Theme.Accent), Padding = new Thickness(6) };
    private TextBox ToolOutput() => new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brush(Theme.SurfaceAlt), Foreground = Brush(Theme.Text), Padding = new Thickness(9) };
    private TextBlock ToolLabel(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brush(Theme.Text), Margin = new Thickness(0, 0, 0, 8) };
    private static void AddTop(DockPanel root, UIElement child) { DockPanel.SetDock(child, Dock.Top); root.Children.Add(child); }
    private static void ShowFailure(Window window, Exception ex) => MessageBox.Show(window, ex.Message, "CacheTrim", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void ShowSystemToolkit()
    {
        BeginPage("Игры и диагностика", "Дополнительные инструменты запускаются только по запросу. Показатели и функции зависят от Windows и оборудования.");
        var cards = new WrapPanel();
        cards.Children.Add(AdvancedModule("БЕНЧМАРК CPU", "Локальный тест SHA-256 с результатом, историей и рейтингом на этом ПК. Не является тестом GPU или прогнозом FPS.", AdvancedButton("Запустить бенчмарк", ShowBenchmark)));
        cards.Children.Add(AdvancedModule("ЗДОРОВЬЕ ДИСКОВ", "Температура, износ, часы и ошибки по данным Windows. Драйверы могут предоставлять не все показатели.", AdvancedButton("Проверить диски", ShowDiskHealth)));
        cards.Children.Add(AdvancedModule("ПРОЦЕССЫ И ЯДРА", "Список процессов, приоритет, ограничение доступных ядер, профили и восстановление исходных параметров. Это не эксклюзивное резервирование ядер.", AdvancedButton("Управление процессами", ShowProcessManager)));
        cards.Children.Add(AdvancedModule("МОНИТОРИНГ ИГР", "Запись кадров выбранного процесса через официальный PresentMon. При первом использовании файл загружается из GitHub после согласия.", AdvancedButton("Записать игровую сессию", ShowGameCapture)));
        cards.Children.Add(AdvancedModule("МОИ ИГРЫ", "Поиск установленных игр Steam и Epic Games по локальным файлам. Другие магазины пока не поддерживаются.", AdvancedButton("Показать игры", ShowGames)));
        cards.Children.Add(AdvancedModule("ПРОГРАММЫ", "Поиск, установка и удаление через WinGet и Microsoft Store; поиск возможных остатков без удаления.", AdvancedButton("Открыть каталог программ", ShowPrograms)));
        cards.Children.Add(AdvancedModule("ПРЕРЫВАНИЯ PCI", "Просмотр и ручная смена политики распределения прерываний PCI с резервной копией исходного значения. Это рискованно и может потребовать перезагрузки.", AdvancedButton("Настройки прерываний", ShowInterrupts)));
        cards.Children.Add(AdvancedModule("АНТИЧИТ: ПРОВЕРКА ЗАЩИТЫ", "Проверка Secure Boot, TPM и VBS без изменения настроек. Не гарантирует совместимость с конкретной игрой.", AdvancedButton("Проверить защиту", ShowAntiCheatStatus)));
        _pageContent!.Children.Add(cards);
    }

    private void ShowBenchmark()
    {
        var window = ToolWindow("Бенчмарк CPU", 560); var root = new DockPanel { Margin = new Thickness(18) }; window.Content = root;
        AddTop(root, ToolLabel("Однопоточный локальный тест SHA-256 на 5 секунд нагружает процессор и может нагревать его. Сравнивай результаты в одинаковых условиях. Это не оценка игр и не мировой рейтинг."));
        var buttons = new WrapPanel(); AddTop(root, buttons); var run = ToolButton("Запустить тест"); buttons.Children.Add(run);
        var output = ToolOutput(); root.Children.Add(output); string path = Path.Combine(SystemToolkit.DataDirectory, "benchmark-history.json");
        try { output.Text = SystemToolkit.RenderBenchmarkHistory(SystemToolkit.ReadBenchmarks(path)); } catch (Exception ex) { output.Text = ex.Message; }
        var cts = new CancellationTokenSource(); window.Closed += (_, _) => cts.Cancel();
        run.Click += async (_, _) => {
            run.IsEnabled = false; output.Text = "Выполняется тест…";
            try { var result = await SystemToolkit.BenchmarkAsync(5, cts.Token); SystemToolkit.SaveBenchmark(result, path); output.Text = "Тест завершён. История и рейтинг только на этом ПК:\n\n" + SystemToolkit.RenderBenchmarkHistory(SystemToolkit.ReadBenchmarks(path)); }
            catch (OperationCanceledException) { } catch (Exception ex) { output.Text = "Ошибка: " + ex.Message; } finally { run.IsEnabled = true; }
        };
        window.ShowDialog();
    }
    private void ShowReadOnlyDiagnostic(string title, string intro, string script)
    {
        var window = ToolWindow(title, 600); var root = new DockPanel { Margin = new Thickness(18) }; window.Content = root;
        AddTop(root, ToolLabel(intro)); var b = ToolButton("Обновить"); AddTop(root, b); var output = ToolOutput(); root.Children.Add(output);
        var cts = new CancellationTokenSource(); window.Closed += (_, _) => cts.Cancel();
        async Task Refresh() { b.IsEnabled = false; output.Text = "Считывание данных…"; try { output.Text = await SystemToolkit.ReadPowerShellAsync(script, cts.Token); } catch (OperationCanceledException) { output.Text = "Остановлено или истекло время ожидания."; } catch (Exception ex) { output.Text = "Не удалось получить данные: " + ex.Message; } finally { b.IsEnabled = true; } }
        b.Click += async (_, _) => await Refresh(); window.Loaded += async (_, _) => await Refresh(); window.ShowDialog();
    }
    private void ShowDiskHealth() => ShowReadOnlyDiagnostic("Здоровье дисков", "Данные считываются из Windows. Не каждый накопитель и драйвер сообщает все параметры; ничего не изменяется.", SystemToolkit.DiskScript);
    private void ShowAntiCheatStatus() => ShowReadOnlyDiagnostic("Античит: проверка защиты", "Статус Secure Boot, TPM и VBS. Это не античит и не гарантия работы конкретной игры; настройки не изменяются.", SystemToolkit.SecurityScript);

    private void ShowProcessManager()
    {
        var window = ToolWindow("Процессы и ядра", 690); var root = new DockPanel { Margin = new Thickness(18) }; window.Content = root;
        AddTop(root, ToolLabel("Изменения влияют только на выбранный запущенный процесс, могут ухудшить работу или вызвать потерю данных. Системные процессы защищены. Ядра здесь ограничивают доступ процесса, но не резервируются исключительно для него."));
        var filter = ToolInput(); filter.Margin = new Thickness(0, 0, 0, 6); filter.ToolTip = "Поиск по имени, пути или PID"; AddTop(root, filter);
        var list = new System.Windows.Controls.ListBox { Height = 200, Background = Brush(Theme.SurfaceAlt), Foreground = Brush(Theme.Text) }; AddTop(root, list);
        var refresh = ToolButton("Обновить список"); AddTop(root, refresh);
        var values = new WrapPanel(); AddTop(root, values); values.Children.Add(ToolLabel("Приоритет:"));
        var priority = new ComboBox { Width = 150, Margin = new Thickness(6, 0, 12, 0), ItemsSource = new[] { ProcessPriorityClass.Idle, ProcessPriorityClass.BelowNormal, ProcessPriorityClass.Normal, ProcessPriorityClass.AboveNormal, ProcessPriorityClass.High }, SelectedIndex = 2 }; values.Children.Add(priority);
        values.Children.Add(ToolLabel("Маска ядер (HEX):")); var mask = ToolInput(); mask.Width = 150; mask.Margin = new Thickness(6, 0, 0, 0); values.Children.Add(mask);
        var actions = new WrapPanel(); AddTop(root, actions); var apply = ToolButton("Применить"); var restore = ToolButton("Вернуть исходное"); var save = ToolButton("Сохранить профиль"); var load = ToolButton("Применить профиль"); var kill = ToolButton("Завершить процесс");
        foreach (var b in new[] { apply, restore, save, load, kill }) actions.Children.Add(b);
        var status = ToolOutput(); status.Text = "Выбери процесс. Маска указывает логические процессоры в текущей группе; изменения для систем с более чем 64 логическими процессорами не поддерживаются."; root.Children.Add(status);
        List<ProcessItem> all = new();
        void Filter() { string q = filter.Text.Trim(); list.ItemsSource = all.Where(x => x.ToString().Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToList(); }
        void Refresh() { all = AvailableProcesses(); Filter(); status.Text = $"Найдено доступных процессов: {all.Count}."; }
        ProcessItem Selected() => list.SelectedItem as ProcessItem ?? throw new InvalidOperationException("Выбери процесс.");
        refresh.Click += (_, _) => Refresh(); filter.TextChanged += (_, _) => Filter();
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is not ProcessItem item) return; try { using var p = ManagedProcessTools.OpenChecked(item.Identity); priority.SelectedItem = p.PriorityClass; mask.Text = unchecked((ulong)p.ProcessorAffinity.ToInt64()).ToString("X"); status.Text = "Выбран: " + item.Identity.Path; } catch (Exception ex) { status.Text = ex.Message; } };
        void Operation(Action action) { try { action(); } catch (Exception ex) { ShowFailure(window, ex); } }
        bool Confirm(string detail) => MessageBox.Show(window, detail, "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        apply.Click += (_, _) => Operation(() => {
            var item = Selected(); if (priority.SelectedItem is not ProcessPriorityClass pri || !ulong.TryParse(mask.Text.Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ulong bits) || bits == 0) throw new ArgumentException("Укажи допустимый приоритет и ненулевую HEX-маску ядер.");
            if (Environment.ProcessorCount < 64 && (bits >> Environment.ProcessorCount) != 0) throw new ArgumentException("Маска содержит несуществующие ядра.");
            if (!Confirm($"Изменить приоритет и ядра процесса {item.Identity.Name} (PID {item.Identity.Pid})? Сначала сохранятся исходные настройки.")) return;
            ManagedProcessTools.Change(item.Identity, pri, unchecked((long)bits)); status.Text = "Изменено. Восстановить можно кнопкой «Вернуть исходное», пока этот процесс работает.";
        });
        restore.Click += (_, _) => Operation(() => { var item = Selected(); ManagedProcessTools.Restore(item.Identity); status.Text = "Исходные параметры процесса восстановлены."; });
        save.Click += (_, _) => Operation(() => { ManagedProcessTools.SaveProfile(Selected().Identity); status.Text = "Профиль для EXE сохранён локально. Автоматически не применяется."; });
        load.Click += (_, _) => Operation(() => { var item = Selected(); if (Confirm("Применить сохранённый профиль к " + item.Identity.Name + "?")) { ManagedProcessTools.ApplyProfile(item.Identity); status.Text = "Профиль применён; исходные параметры сохранены."; } });
        kill.Click += (_, _) => Operation(() => { var item = Selected(); if (!Confirm("Завершить " + item.Identity.Name + "? Несохранённые данные могут быть потеряны.")) return; using var p = ManagedProcessTools.OpenChecked(item.Identity); p.Kill(false); status.Text = "Отправлен запрос завершения."; Refresh(); });
        Refresh(); window.ShowDialog();
    }

    private void ShowGameCapture()
    {
        var window = ToolWindow("Игровой мониторинг", 600); var root = new DockPanel { Margin = new Thickness(18) }; window.Content = root;
        AddTop(root, ToolLabel("Сначала запусти игру, затем выбери её процесс. При первом замере с GitHub загрузится официальный PresentMon 2.3.1 (проверка SHA-256). Запись использует ETW, может быть ограничена античитом и не измеряет FPS, который видит глаз. CSV остаётся на компьютере."));
        var choices = new ComboBox { Margin = new Thickness(0, 0, 0, 8), MaxDropDownHeight = 260 }; AddTop(root, choices);
        var buttons = new WrapPanel(); AddTop(root, buttons); var refresh = ToolButton("Обновить процессы"); var run = ToolButton("Записать 30 секунд"); var cancel = ToolButton("Остановить"); cancel.IsEnabled = false; buttons.Children.Add(refresh); buttons.Children.Add(run); buttons.Children.Add(cancel);
        var output = ToolOutput(); root.Children.Add(output); CancellationTokenSource active = null;
        void Refresh() { choices.ItemsSource = AvailableProcesses(); if (choices.Items.Count > 0) choices.SelectedIndex = 0; }
        refresh.Click += (_, _) => Refresh(); cancel.Click += (_, _) => active?.Cancel(); window.Closed += (_, _) => active?.Cancel();
        run.Click += async (_, _) => {
            if (choices.SelectedItem is not ProcessItem item) { MessageBox.Show(window, "Выбери процесс игры."); return; }
            if (MessageBox.Show(window, "Записать 30 секунд для " + item.Identity.Name + "? Если PresentMon ещё не загружен, он будет скачан с GitHub. Использование ETW может конфликтовать с защитой игры.", "Игровой мониторинг", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            using var cts = new CancellationTokenSource(); active = cts; run.IsEnabled = refresh.IsEnabled = false; cancel.IsEnabled = true; output.Text = "Идёт запись кадров…";
            try { var result = await GamingToolkit.CaptureAsync(item.Identity, 30, cts.Token); output.Text = $"Запись завершена. Основная цепочка кадров: {result.Summary.SwapChain}\nКадров: {result.Summary.Frames}\nСредняя частота Present: {result.Summary.AveragePresentedFps:N1} кадров/с\n99-й процентиль интервала между Present: {result.Summary.P99FrameTimeMs:N1} мс\n\nЭто частота отправки кадров, не обязательно отображённый FPS.\nФайл CSV: {result.Csv}"; }
            catch (OperationCanceledException) { output.Text = "Запись остановлена или истекло время ожидания."; } catch (Exception ex) { output.Text = "Не удалось выполнить замер: " + ex.Message; }
            finally { active = null; run.IsEnabled = refresh.IsEnabled = true; cancel.IsEnabled = false; }
        };
        Refresh(); window.ShowDialog();
    }
    private void ShowGames()
    {
        var window = ToolWindow("Мои игры", 560); var root = new DockPanel { Margin = new Thickness(18) }; window.Content = root;
        AddTop(root, ToolLabel("Игры Steam и Epic по локальным манифестам: список может содержать инструменты или устаревшие записи. Другие магазины пока не поддерживаются. Настройки для конкретных игр не меняются."));
        var refresh = ToolButton("Обновить"); AddTop(root, refresh); var output = ToolOutput(); root.Children.Add(output);
        void Refresh() { try {
            string path = GamingToolkit.GetSteamRoot(); var lines = new List<string>();
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) lines.AddRange(GamingToolkit.DiscoverSteam(path).Select(g => $"Steam · {g.Name} · AppID {g.AppId} · {g.Library}"));
            lines.AddRange(GamingToolkit.DiscoverEpic(GamingToolkit.GetEpicManifestFolder()).Select(g => $"Epic · {g.Name} · {g.InstallLocation}"));
            output.Text = lines.Count == 0 ? "Игры Steam и Epic не найдены в локальных манифестах." : string.Join(Environment.NewLine, lines);
        } catch (Exception ex) { output.Text = "Не удалось прочитать список: " + ex.Message; } }
        refresh.Click += (_, _) => Refresh(); Refresh(); window.ShowDialog();
    }
    private void ShowPrograms()
    {
        var window = ToolWindow("Программы и магазин", 700); var root = new DockPanel { Margin = new Thickness(18) }; window.Content = root;
        AddTop(root, ToolLabel("Каталог и список программ используют WinGet из «Установщика приложений» Microsoft. Источники и установщики сторонние. Удаление может затронуть данные программы; поиск остатков только показывает пути и ничего не удаляет."));
        var query = ToolInput(); query.Margin = new Thickness(0, 0, 0, 6); query.ToolTip = "Название программы или часть имени папки для поиска остатков"; AddTop(root, query);
        var source = new ComboBox { Width = 200, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, ItemsSource = new[] { "winget", "msstore" }, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 6) }; AddTop(root, source);
        var row = new WrapPanel(); AddTop(root, row); var search = ToolButton("Искать в каталоге"); var installed = ToolButton("Установленные"); var leftovers = ToolButton("Найти папки-остатки"); row.Children.Add(search); row.Children.Add(installed); row.Children.Add(leftovers);
        var id = ToolInput(); id.Margin = new Thickness(0, 4, 0, 6); id.ToolTip = "Точный ID пакета из результата поиска или списка"; AddTop(root, ToolLabel("Точный ID пакета из результата:")); AddTop(root, id);
        var actions = new WrapPanel(); AddTop(root, actions); var install = ToolButton("Установить по ID"); var uninstall = ToolButton("Удалить по ID"); actions.Children.Add(install); actions.Children.Add(uninstall);
        var output = ToolOutput(); root.Children.Add(output); var cts = new CancellationTokenSource(); window.Closed += (_, _) => cts.Cancel();
        async Task Read(string[] args) { search.IsEnabled = installed.IsEnabled = false; output.Text = "Выполняется запрос WinGet…"; try { output.Text = await SystemToolkit.RunWingetReadAsync(args, cts.Token); } catch (OperationCanceledException) { output.Text = "Остановлено или истекло время ожидания."; } catch (Exception ex) { output.Text = ex.Message; } finally { search.IsEnabled = installed.IsEnabled = true; } }
        search.Click += async (_, _) => { string term = query.Text.Trim(); if (!SystemToolkit.ValidSearch(term)) { MessageBox.Show(window, "Введи название длиной до 100 символов."); return; } await Read(new[] { "search", "--source", (string)source.SelectedItem, "--query", term, "--disable-interactivity" }); };
        installed.Click += async (_, _) => await Read(new[] { "list", "--disable-interactivity" });
        leftovers.Click += (_, _) => { string term = query.Text.Trim(); if (!SystemToolkit.ValidSearch(term) || term.Length < 3) { MessageBox.Show(window, "Введи не менее трёх символов названия."); return; } var found = new List<string>(); foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) }) try { found.AddRange(Directory.EnumerateDirectories(folder).Where(p => Path.GetFileName(p).Contains(term, StringComparison.OrdinalIgnoreCase)).Take(100)); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { found.Add("Не удалось прочитать " + folder + ": " + ex.Message); } output.Text = "Совпадение имени не доказывает, что папка относится к программе. Ничего не удалено.\n\n" + (found.Count == 0 ? "Папки не найдены." : string.Join(Environment.NewLine, found)); };
        void Start(bool remove) { try {
            string packageId = id.Text.Trim(); if (!SystemToolkit.ValidPackageId(packageId)) throw new ArgumentException("Укажи точный ID пакета из WinGet: буквы, цифры, точка, дефис или подчёркивание.");
            if (MessageBox.Show(window, (remove ? "Удалить " : "Установить ") + packageId + " через WinGet? Сторонний установщик получит права администратора и может изменять или удалять данные. Проверь издателя и условия перед продолжением.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            string path = SystemToolkit.WingetPath(); if (!File.Exists(path)) throw new FileNotFoundException("WinGet не найден. Установи «Установщик приложений» из Microsoft Store.");
            var args = new List<string> { remove ? "uninstall" : "install", "--id", packageId, "--exact", "--interactive" }; if (!remove) { args.Add("--source"); args.Add((string)source.SelectedItem); }
            using var launched = Process.Start(SystemToolkit.Winget(path, args, false)) ?? throw new InvalidOperationException("Не удалось открыть WinGet."); output.Text = "Команда запущена в отдельном окне. Результат и запросы установщика проверяй в этом окне.";
        } catch (Exception ex) { ShowFailure(window, ex); } }
        install.Click += (_, _) => Start(false); uninstall.Click += (_, _) => Start(true); window.ShowDialog();
    }
    private void ShowInterrupts()
    {
        var window = ToolWindow("Прерывания PCI", 600); var root = new DockPanel { Margin = new Thickness(18) }; window.Content = root;
        AddTop(root, ToolLabel("Риск: неправильная политика может ухудшить работу устройства или системы. Windows рекомендует штатную политику; изменения могут примениться только после перезапуска устройства или ПК. Не меняй без резервной копии и точки восстановления. Это не измерение задержки и не настройка MSI."));
        var list = new System.Windows.Controls.ListBox { Height = 225, Background = Brush(Theme.SurfaceAlt), Foreground = Brush(Theme.Text) }; AddTop(root, list);
        var choices = new ComboBox { Width = 390, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, ItemsSource = new[] { "0 — стандартная политика Windows", "1 — близкие процессоры", "2 — один близкий процессор", "3 — все процессоры", "5 — распределить сообщения" }, SelectedIndex = 0 }; AddTop(root, choices);
        var buttons = new WrapPanel(); AddTop(root, buttons); var refresh = ToolButton("Обновить устройства"); var apply = ToolButton("Изменить политику"); var restore = ToolButton("Восстановить исходную"); buttons.Children.Add(refresh); buttons.Children.Add(apply); buttons.Children.Add(restore);
        var output = ToolOutput(); root.Children.Add(output);
        void Refresh() { try { var rows = InterruptTools.ListPci(); list.ItemsSource = rows; output.Text = $"Обнаружено устройств PCI: {rows.Count}. Не все драйверы поддерживают эту политику."; } catch (Exception ex) { output.Text = ex.Message; } }
        InterruptTools.Device Selected() => list.SelectedItem as InterruptTools.Device ?? throw new InvalidOperationException("Выбери устройство.");
        refresh.Click += (_, _) => Refresh();
        apply.Click += (_, _) => { try { var item = Selected(); int policy = new[] { 0, 1, 2, 3, 5 }[choices.SelectedIndex]; if (MessageBox.Show(window, "Изменить политику прерываний устройства «" + item.Name + "»? Существующее значение будет сохранено. Это может вызвать нестабильность; изменение не гарантирует ускорение.", "Опасное изменение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return; InterruptTools.Apply(item, policy); output.Text = "Значение записано. Действие зависит от драйвера; может потребоваться перезагрузка. Откат доступен для этого устройства."; } catch (Exception ex) { ShowFailure(window, ex); } };
        restore.Click += (_, _) => { try { var item = Selected(); if (MessageBox.Show(window, "Вернуть сохранённую политику для «" + item.Name + "»?", "Восстановление", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return; InterruptTools.Restore(item); output.Text = "Исходное значение восстановлено. Может потребоваться перезагрузка."; } catch (Exception ex) { ShowFailure(window, ex); } };
        Refresh(); window.ShowDialog();
    }

}
