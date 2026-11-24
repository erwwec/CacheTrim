using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using Orientation = System.Windows.Controls.Orientation;
using WpfPoint = System.Windows.Point;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfComboBox = System.Windows.Controls.ComboBox;
using Application = System.Windows.Application;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Color = System.Windows.Media.Color;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using UiButton = Wpf.Ui.Controls.Button;
using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;

namespace CacheTrim;

public partial class MainWindow
{
    private void ShowAdvanced()
    {
        BeginPage("Расширенные функции", "Профили производительности и системные операции. Перед применением CacheTrim покажет, что изменится.");

        WrapPanel modules = new() { HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
        modules.Children.Add(AdvancedModule("ИГРОВОЙ ПРОФИЛЬ", "Включает игровой режим Windows, план высокой производительности и отключает фоновые игровые записи и ограничение частоты CPU. Может увеличить нагрев и расход батареи.",
            AdvancedButton("Применить игровой профиль", async () => await ApplyGamingProfileAsync(true), true),
            AdvancedButton("Вернуть стандартные настройки", async () => await ApplyGamingProfileAsync(false))));

        modules.Children.Add(AdvancedModule("ЗАЩИТА ПЕРЕД ИЗМЕНЕНИЯМИ", "Создаёт точку восстановления Windows. Её можно использовать, чтобы откатить системные настройки. Windows может запретить создание, если защита системы выключена или точка уже недавно создавалась.",
            AdvancedButton("Создать точку восстановления", async () => await CreateRestorePointAsync(), true)));

        modules.Children.Add(AdvancedModule("ИСПРАВИТЬ ПОДКЛЮЧЕНИЕ", "Сбрасывает Winsock и сетевые параметры TCP/IP, затем очищает DNS. Может помочь при проблемах с сетью. После завершения потребуется перезагрузка; VPN и нестандартные сетевые настройки могут потребовать повторной настройки.",
            AdvancedButton("Сбросить сетевые настройки", async () => await ResetNetworkAsync(), true)));

        modules.Children.Add(AdvancedModule("ОЧИСТКА ВИДЕОКАРТЫ", "Удаляет временные кэши шейдеров DirectX, NVIDIA, AMD и Intel. Игры создадут их заново; первый запуск после очистки может загружаться дольше.",
            AdvancedButton("Очистить кэш шейдеров", async () => await CleanAdvancedCachesAsync(true, false), true)));

        modules.Children.Add(AdvancedModule("ОСВОБОДИТЬ МЕСТО", "Удаляет только загруженные файлы обновлений, которые Windows сможет скачать повторно. Установленные обновления и системные компоненты не затрагиваются.",
            AdvancedButton("Очистить загрузки обновлений", async () => await CleanAdvancedCachesAsync(false, true), true)));

        modules.Children.Add(AdvancedModule("ВОССТАНОВИТЬ WINDOWS", "DISM проверит хранилище компонентов и скачает исправные файлы через Windows Update, если найдёт повреждение. Операция может занять длительное время.",
            AdvancedButton("Восстановить компоненты Windows", async () => await RestoreWindowsImageAsync(), true)));

        modules.Children.Add(AdvancedModule("ОЧИСТИТЬ СИСТЕМНЫЕ КОМПОНЕНТЫ", "Удаляет устаревшие версии компонентов Windows после обновлений. После очистки нельзя будет удалить соответствующие старые обновления.",
            AdvancedAction("Удалить старые компоненты", "DISM удалит заменённые версии системных компонентов, чтобы освободить место. После этого нельзя будет удалить соответствующие старые обновления Windows. Операция может занять много времени.", "Очищаются старые компоненты Windows…", "Dism.exe", "/Online /Cleanup-Image /StartComponentCleanup", 2 * 60 * 60 * 1000)));

        modules.Children.Add(AdvancedModule("ПРОИЗВОДИТЕЛЬНОСТЬ WINDOWS", "Высокая производительность уменьшает энергосбережение процессора. На ноутбуке быстрее расходуется заряд. Вернуть прежний план можно этой же карточкой.",
            AdvancedButton("Включить высокую производительность", async () => await ApplyPowerPlanAsync(true), true),
            AdvancedButton("Восстановить прежний план", async () => await ApplyPowerPlanAsync(false))));

        WrapPanel checks = new();
        checks.Children.Add(AdvancedAction("Проверить файлы Windows", "Проверит защищённые файлы и попробует восстановить повреждённые. Может занять несколько минут.", "Проверяются файлы Windows…", "sfc.exe", "/scannow", 30 * 60 * 1000));
        checks.Children.Add(AdvancedAction("Проверить диск C:", "Проверит файловую систему диска C: без отключения тома. Исправление ошибок не запускается.", "Проверяется диск C:…", "chkdsk.exe", "C: /scan", 60 * 60 * 1000));
        checks.Children.Add(Action("Диагностика сети", ShowNetworkDiagnostics));
        checks.Children.Add(Action("Частота экранов", ShowDisplayDiagnostics));
        checks.Children.Add(Action("Монитор ресурсов", () => OpenSystemTool("resmon.exe")));
        checks.Children.Add(Action("Центр обновления Windows", () => OpenSystemTool("ms-settings:windowsupdate")));
        modules.Children.Add(AdvancedModule("ПРОВЕРКА И ИНСТРУМЕНТЫ", "Диагностика без изменения системных параметров.", checks));

        modules.Children.Add(AdvancedModule("ОТЧЁТ ДЛЯ ПОМОЩИ", "Сохранит сводку Windows, памяти, дисков и включённых настроек CacheTrim. Отчёт остаётся на этом компьютере, личные пути и список программ не добавляются.",
            AdvancedButton("Сохранить системный отчёт", async () => await ExportSystemReportAsync())));

        _pageContent!.Children.Add(modules);
    }

    private void ShowPet()
    {
        BeginPage("Питомец рабочего стола", "Выбери компаньона: он откроется отдельным окном поверх рабочего стола.");
        WrapPanel cards = new();
        WpfComboBox choice = new() { Width = 250, MinHeight = 34, ToolTip = "Выбери компаньона" };
        choice.Items.Add("Клод");
        choice.Items.Add("Аниме-аватар");
        choice.SelectedIndex = 0;
        StackPanel progress = new() { Width = 420, Visibility = Visibility.Collapsed };
        progress.Children.Add(new TextBlock { Text = "Проверяем и запускаем…", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brush(Theme.Text), TextWrapping = TextWrapping.Wrap });
        progress.Children.Add(new TextBlock { Text = "Первый запуск может занять несколько минут.", FontSize = 12, Foreground = Brush(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 7) });
        progress.Children.Add(new System.Windows.Controls.ProgressBar { Height = 7, IsIndeterminate = true });
        UiButton launch = null;
        launch = AdvancedButton("Запустить выбранного", async () =>
        {
            if (_busy) { MessageBox.Show(this, "Дождись окончания текущей операции.", "CacheTrim", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            launch.IsEnabled = false;
            choice.IsEnabled = false;
            progress.Visibility = Visibility.Visible;
            try
            {
                if (choice.SelectedIndex == 0) await LaunchDesktopPetAsync();
                else await LaunchAnimeAvatarAsync();
            }
            finally { progress.Visibility = Visibility.Collapsed; choice.IsEnabled = true; launch.IsEnabled = true; }
        }, true);
        cards.Children.Add(AdvancedModule("ВЫБОР КОМПАНЬОНА", "Клод скачивается из GitHub; нужен интернет. Аниме-аватар входит в обновление и запускается отдельно. Их функции различаются.", choice, launch, progress));
        cards.Children.Add(AdvancedModule("УПРАВЛЕНИЕ КЛОДОМ", "Клод: перетаскивай левой кнопкой мыши; правой открывай меню, включая настройки песен. У аниме-аватара своё меню.",
            AdvancedButton("Как настроить Клода", async () => await ShowPetHelpAsync()),
            AdvancedButton("Сведения об аниме-аватаре", ShowAnimeAvatarNotices)));
        _pageContent!.Children.Add(cards);
    }

    private void ShowAnimeAvatarNotices()
    {
        using var input = typeof(MainWindow).Assembly.GetManifestResourceStream("CacheTrim.AnimeAvatar.Notices.txt");
        if (input == null) { MessageBox.Show(this, "Сведения об аватаре недоступны.", "Аниме-аватар"); return; }
        using var reader = new StreamReader(input);
        MessageBox.Show(this, reader.ReadToEnd(), "Сведения об аниме-аватаре", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task LaunchAnimeAvatarAsync()
    {
        if (MessageBox.Show(this, "Аниме-аватар — отдельная программа, предоставленная владельцем проекта. Её исходный код и цифровая подпись отсутствуют, безопасность не подтверждена. Она может запуститься с правами администратора, как CacheTrim. Продолжить?", "Аниме-аватар", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunBusyAsync("проверяется аниме-аватар…", async () =>
        {
            string path = await Task.Run(AnimeAvatarPackage.EnsureExtracted);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(path)! });
            App.Log("Запрошен запуск аниме-аватара из обновления");
        });
    }

    private async Task LaunchDesktopPetAsync()
    {
        if (System.Diagnostics.Process.GetProcessesByName("DesktopPet").Length > 0)
        {
            MessageBox.Show(this, "Питомец уже запущен. Найди его на рабочем столе или нажми по нему правой кнопкой, чтобы открыть настройки.", "Desktop Pet", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunBusyAsync("проверяется и запускается питомец…", async () =>
        {
            UpdateInfo release = await Task.Run(UpdateService.CheckLatest);
            if (string.IsNullOrWhiteSpace(release.DesktopPetDownloadUrl) || string.IsNullOrWhiteSpace(release.DesktopPetChecksumUrl))
                throw new InvalidOperationException("В последнем GitHub-релизе пока нет файла Desktop Pet. Проверь подключение и повтори попытку позже.");
            string petPath = await Task.Run(() => UpdateService.EnsureDesktopPet(release));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(petPath) { UseShellExecute = true });
            App.Log("Desktop Pet проверен и запущен из вкладки CacheTrim");
        });
    }

    private Task ShowPetHelpAsync()
    {
        MessageBox.Show(this,
            "Клод — отдельный питомец рабочего стола, поэтому он остаётся виден, когда CacheTrim свёрнут.\n\n" +
            "• Перетащи его левой кнопкой.\n" +
            "• Нажми правой кнопкой, чтобы открыть настройки.\n" +
            "• Нажми на спящего Клода, чтобы разбудить.\n\n" +
            "Параметры питомца хранятся отдельно от настроек CacheTrim.",
            "Управление Desktop Pet", MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    internal void ShowWhatsNew(string previousVersion, bool useStagedNotes)
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            string notes = string.Empty;
            if (useStagedNotes)
            {
                string notesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CacheTrim", "UpdateCache", "WhatsNew.txt");
                try { if (File.Exists(notesPath)) notes = File.ReadAllText(notesPath, new System.Text.UTF8Encoding(false)); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            // The legacy updater does not download notes; never reuse a stale cache.
            if (string.IsNullOrWhiteSpace(notes))
            {
                try
                {
                    using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("CacheTrim.ReleaseNotes.txt");
                    if (stream != null) using (var reader = new StreamReader(stream)) notes = reader.ReadToEnd();
                }
                catch (IOException) { }
            }
            // Ask GitHub for every skipped public release. If offline, show current notes
            // and explicitly say that the full history could not be retrieved.
            try
            {
                using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                string history = await ReleaseHistory.FetchAsync(AppInfo.GitHubReleaseApi, previousVersion, AppInfo.Version, timeout.Token);
                if (!string.IsNullOrWhiteSpace(history)) notes = history;
                else notes = "Не удалось загрузить полную историю пропущенных версий. Ниже — изменения текущей версии.\n\n" + notes;
            }
            catch (Exception ex)
            {
                notes = "Не удалось загрузить полную историю пропущенных версий. Ниже — изменения текущей версии.\n\n" + notes;
                App.Log("история обновлений недоступна: " + ex.Message);
            }
            if (!IsLoaded) return;
            WhatsNewWindow window = new(previousVersion, AppInfo.Version, notes) { Owner = this };
            window.ShowDialog();
            WhatsNewState.MarkShown(AppInfo.Version);
            App.Log("показано окно изменений после обновления " + previousVersion + " → " + AppInfo.Version);
        }), DispatcherPriority.ApplicationIdle);
    }

    private Border AdvancedModule(string title, string description, params FrameworkElement[] actions)
    {
        StackPanel content = new();
        content.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = Brush(Theme.Muted), FontSize = 12, Margin = new Thickness(0, 0, 0, 12) });
        StackPanel actionList = new();
        foreach (FrameworkElement action in actions)
        {
            action.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            action.Margin = new Thickness(0, 3, 0, 3);
            actionList.Children.Add(action);
        }
        content.Children.Add(actionList);
        Border module = Card(title, content);
        module.Width = 500;
        module.Margin = new Thickness(0, 0, 12, 12);
        module.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        return module;
    }

    private Wpf.Ui.Controls.Button AdvancedButton(string title, Action action, bool primary = false)
    {
        Wpf.Ui.Controls.Button button = Action(title, action, primary);
        button.MinWidth = 250;
        button.Height = 38;
        return button;
    }

    private async Task ApplyGamingProfileAsync(bool enabled)
    {
        string title = enabled ? "Игровой профиль" : "Стандартные настройки";
        string details = enabled
            ? "CacheTrim изменит план питания, включит игровой режим Windows, отключит фоновую запись клипов и системное ограничение CPU для фоновых задач. Ноутбук может сильнее нагреваться и быстрее разряжаться. Часть изменений потребует перезапуска Windows."
            : "CacheTrim применит стандартные значения параметров игрового профиля. Они могут отличаться от твоих прежних настроек. План питания восстанавливается из копии; если её нет, будет выбран «Сбалансированный».";
        if (!ConfirmRisk(title, details)) return;
        await RunBusyAsync(enabled ? "включается игровой профиль…" : "возвращаются стандартные параметры…", async () =>
        {
            string[] names = { "План «Высокая производительность»", "Включить игровой режим Windows", "Отключить системный Game DVR", "Отключить запись игровых клипов", "Не ограничивать скорость процессора в фоне" };
            List<string> errors = new();
            await Task.Run(() =>
            {
                List<Tweak> all = Tweaks.All();
                foreach (string name in names)
                {
                    Tweak tweak = all.FirstOrDefault(item => item.Name == name);
                    if (tweak == null) { errors.Add("Функция не найдена: " + name); continue; }
                    string error = tweak.Set(enabled);
                    if (error != null) errors.Add(name + ": " + error);
                    else App.Log((enabled ? "игровой профиль: включено — " : "игровой профиль: сброшено — ") + name);
                }
            });
            if (errors.Count > 0) MessageBox.Show(this, string.Join("\n", errors), "Часть параметров не изменилась", MessageBoxButton.OK, MessageBoxImage.Warning);
            else MessageBox.Show(this, enabled ? "Игровой профиль применён. Если некоторые параметры не вступили в силу сразу, перезагрузите компьютер." : "Стандартные значения применены. Они могут отличаться от твоих прежних настроек.", title, MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private async Task CreateRestorePointAsync()
    {
        if (!ConfirmRisk("Точка восстановления", "Windows сохранит текущее состояние системных файлов и реестра. Создание может быть недоступно, если защита системы отключена или недавно уже создавалась точка.")) return;
        await RunBusyAsync("создаётся точка восстановления…", async () =>
        {
            string ps = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            string output = string.Empty;
            int code = await Task.Run(() => Tweaks.Run(ps, "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'CacheTrim before changes' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop\"", out output, 10 * 60 * 1000));
            App.Log("точка восстановления · код " + code + " · " + output);
            MessageBox.Show(this, code == 0 ? "Windows обработала запрос. Проверьте в защите системы, появилась ли новая точка восстановления: Windows может пропустить её создание." : "Windows не создала точку восстановления. Проверьте, включена ли защита системы.\n\n" + output, "Точка восстановления", MessageBoxButton.OK, code == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async Task ResetNetworkAsync()
    {
        try { new GameGuard().Check(true); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Обслуживание приостановлено", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!ConfirmRisk("Сброс сетевых настроек", "Сбросит Winsock и TCP/IP, очистит DNS-кэш и может отключить пользовательские настройки VPN или статического IP. После операции перезагрузите компьютер.")) return;
        await RunBusyAsync("сбрасываются сетевые настройки…", async () =>
        {
            List<string> results = new();
            string netsh = System.IO.Path.Combine(Environment.SystemDirectory, "netsh.exe");
            string output = string.Empty;
            int winsock = await Task.Run(() => Tweaks.Run(netsh, "winsock reset", out output)); results.Add("Winsock: " + winsock + " " + output);
            int tcpip = await Task.Run(() => Tweaks.Run(netsh, "int ip reset", out output)); results.Add("TCP/IP: " + tcpip + " " + output);
            int dns = await Task.Run(() => Tweaks.Run(System.IO.Path.Combine(Environment.SystemDirectory, "ipconfig.exe"), "/flushdns", out output)); results.Add("DNS: " + dns + " " + output);
            foreach (string line in results) App.Log(line);
            bool ok = winsock == 0 && tcpip == 0 && dns == 0;
            MessageBox.Show(this, string.Join("\n", results) + "\n\nПерезагрузите компьютер, чтобы завершить сброс.", "Сброс сети", MessageBoxButton.OK, ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async Task CleanAdvancedCachesAsync(bool shaders, bool updates)
    {
        string title = shaders ? "Очистка кэша видеокарты" : "Очистка загрузок Windows Update";
        string details = shaders
            ? "Удалит временные кэши шейдеров GPU. Игры пересоздадут их автоматически, но первый запуск может быть медленнее."
            : "Удалит только повторно загружаемые пакеты обновлений Windows. Установленные обновления останутся на месте.";
        if (!ConfirmRisk(title, details)) return;
        await RunBusyAsync(shaders ? "очищается кэш видеокарты…" : "очищаются загрузки обновлений…", async () =>
        {
            Cleaner cleaner = new(App.Log);
            Config config = new()
            {
                WorkingSets = false, Standby = false, LowStandby = false, Modified = false, FileCache = false,
                Temp = false, Browsers = false, Wer = false, UpdateCache = updates, D3dCache = shaders,
                Thumbs = false, Dumps = false, WinLogs = false, UwpCache = false, Recycle = false, Prefetch = false,
                Dns = false, Arp = false, Netbios = false
            };
            await Task.Run(() => cleaner.Run(config));
            App.Log(title + " завершена · освобождено " + Cleaner.FormatBytes(cleaner.BytesFreed));
            MessageBox.Show(this, "Освобождено: " + Cleaner.FormatBytes(cleaner.BytesFreed) + "\nЗанятые файлы пропущены: " + cleaner.FilesSkipped, title, MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private Wpf.Ui.Controls.Button AdvancedAction(string label, string warning, string busy, string executable, string arguments, int timeoutMilliseconds = 30000)
    {
        Wpf.Ui.Controls.Button button = Action(label, async () => await RunAdvancedCommandAsync(label, warning, busy, executable, arguments, timeoutMilliseconds));
        button.ToolTip = warning;
        return button;
    }

    private async Task RunAdvancedCommandAsync(string title, string warning, string busy, string executable, string arguments, int timeoutMilliseconds)
    {
        try { new GameGuard().Check(true); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Обслуживание приостановлено", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!ConfirmRisk(title, warning)) return;
        await RunBusyAsync(busy, async () =>
        {
            string output = string.Empty;
            string path = System.IO.Path.Combine(Environment.SystemDirectory, executable);
            int code = await Task.Run(() => Tweaks.Run(path, arguments, out output, timeoutMilliseconds));
            App.Log(title + " · код " + code + " · " + output);
            MessageBox.Show(this, "Код завершения: " + code + "\nПодробности записаны в журнал CacheTrim.", title + (code == 0 ? " — готово" : " — завершено с сообщением"), MessageBoxButton.OK, code == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async Task CreateEnergyReportAsync()
    {
        string reportPath = System.IO.Path.Combine(Config.LogDir, "energy-report.html");
        string warning = "Windows в течение 30 секунд соберёт сведения о настройках питания и сохранит HTML-отчёт. Настройки не меняются. Отчёт хранится локально на этом компьютере.";
        if (!ConfirmRisk("Создание отчёта об энергопотреблении", warning)) return;
        await RunBusyAsync("создаётся отчёт о питании…", async () =>
        {
            Directory.CreateDirectory(Config.LogDir);
            string output = string.Empty;
            int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\powercfg.exe", "/energy /duration 30 /output \"" + reportPath + "\"", out output, 2 * 60 * 1000));
            App.Log("отчёт о питании · код " + code + " · " + output);
            if (code == 0 && File.Exists(reportPath)) OpenPath(reportPath);
            else MessageBox.Show(this, "Не удалось создать отчёт. Код завершения: " + code + "\n" + output, "Отчёт об энергопотреблении", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private async Task FlushDnsAsync()
    {
        await RunBusyAsync("очищается DNS-кэш…", async () =>
        {
            string output = string.Empty;
            int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\ipconfig.exe", "/flushdns", out output));
            App.Log("очистка DNS-кэша · код " + code + " · " + output);
            if (code != 0) MessageBox.Show(this, output, "Не удалось очистить DNS-кэш", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private async Task RestoreWindowsImageAsync()
    {
        try { new GameGuard().Check(true); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Обслуживание приостановлено", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!ConfirmRisk("Восстановление компонентов Windows", "DISM проверит хранилище компонентов и при необходимости скачает замену через Windows Update. Операция может занять много времени.")) return;
        await RunBusyAsync("восстанавливаются компоненты Windows…", async () =>
        {
            string output = string.Empty;
            int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\Dism.exe", "/Online /Cleanup-Image /RestoreHealth", out output, 2 * 60 * 60 * 1000));
            App.Log("DISM RestoreHealth · код " + code + " · " + output);
            MessageBox.Show(this, "Код завершения: " + code + "\nПодробности записаны в журнал CacheTrim.", "Восстановление Windows завершено", MessageBoxButton.OK, code == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private void OpenSystemTool(string fileName)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Не удалось запустить инструмент Windows", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async Task RunDiskMaintenanceAsync()
    {
        try { new GameGuard().Check(true); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Обслуживание приостановлено", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!Confirm("Оптимизация дисков", "Windows выберет подходящую операцию для каждого фиксированного диска. Запустить?")) return;
        await RunBusyAsync("обслуживание дисков…", async () =>
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                string output = string.Empty;
                int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\defrag.exe", "\"" + drive.Name.TrimEnd('\\') + "\" /O", out output, 2 * 60 * 60 * 1000));
                App.Log("обслуживание диска " + drive.Name + " · код " + code + " · " + output);
            }
        });
    }

}
