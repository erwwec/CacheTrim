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
        checks.Children.Add(Action("Монитор ресурсов", () => OpenSystemTool("resmon.exe")));
        checks.Children.Add(Action("Центр обновления Windows", () => OpenSystemTool("ms-settings:windowsupdate")));
        modules.Children.Add(AdvancedModule("ПРОВЕРКА И ИНСТРУМЕНТЫ", "Диагностика без изменения системных параметров.", checks));

        modules.Children.Add(AdvancedModule("ОТЧЁТ ДЛЯ ПОМОЩИ", "Сохранит сводку Windows, памяти, дисков и включённых настроек CacheTrim. Отчёт остаётся на этом компьютере, личные пути и список программ не добавляются.",
            AdvancedButton("Сохранить системный отчёт", async () => await ExportSystemReportAsync())));

        _pageContent!.Children.Add(modules);
    }

    private void ShowPet()
    {
        BeginPage("Питомец рабочего стола", "Запускай Клода прямо из CacheTrim — питомец остаётся поверх рабочего стола и окон.");
        WrapPanel cards = new();
        cards.Children.Add(AdvancedModule("DESKTOP PET", "При первом запуске CacheTrim скачает питомца из GitHub и проверит файл по SHA-256. Затем Клод появится на рабочем столе отдельным прозрачным окном.",
            AdvancedButton("Запустить питомца", async () => await LaunchDesktopPetAsync(), true)));
        cards.Children.Add(AdvancedModule("УПРАВЛЕНИЕ ПИТОМЦЕМ", "Перетаскивай Клода левой кнопкой мыши. Нажми правой кнопкой по нему, чтобы открыть меню: там настраиваются размер, реплики, режим «Не отвлекать», коллекция и реакция на звук. Клик по спящему питомцу его разбудит.",
            AdvancedButton("Как настроить Клода", async () => await ShowPetHelpAsync())));
        _pageContent!.Children.Add(cards);
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
            : "CacheTrim восстановит прежний план питания, включит обратно параметры игровых записей и снимет изменения игрового профиля CacheTrim.";
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
            else MessageBox.Show(this, enabled ? "Игровой профиль применён. Если некоторые параметры не вступили в силу сразу, перезагрузите компьютер." : "Стандартные настройки восстановлены.", title, MessageBoxButton.OK, MessageBoxImage.Information);
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

    private async Task RunSfcAsync()
    {
        if (!Confirm("Проверка Windows", "Запустить sfc /scannow? Это может занять несколько минут.")) return;
        await RunBusyAsync("проверяются системные файлы…", async () =>
        {
            string output = string.Empty;
            int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\sfc.exe", "/scannow", out output, 30 * 60 * 1000));
            App.Log("SFC завершён · код " + code + " · " + output);
            MessageBox.Show(this, "Код завершения: " + code + "\nПодробности записаны в журнал.", "Проверка системных файлов", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private async Task ApplyPowerPlanAsync(bool highPerformance)
    {
        string target = highPerformance ? "Высокая производительность" : "прежний план питания";
        string details = highPerformance
            ? "Переключить Windows на план «Высокая производительность»? Он может повысить нагрев и расход батареи. CacheTrim сохранит текущий план и восстановит его по кнопке «Восстановить прежний план»."
            : "CacheTrim восстановит план, который был активен до включения высокой производительности. Если резервная копия не найдена, будет выбран план «Сбалансированный».";
        if (!ConfirmRisk("План питания", details)) return;
        Tweak tweak = Tweaks.All().First(item => item.Name == "План «Высокая производительность»");
        await RunBusyAsync("меняется план питания…", async () =>
        {
            string? error = await Task.Run(() => tweak.Set(highPerformance));
            if (error != null) throw new InvalidOperationException(error);
            SetStatus(highPerformance ? "план питания: высокая производительность" : "прежний план питания восстановлен");
            App.Log(highPerformance ? "план питания переключён: высокая производительность" : "прежний план питания восстановлен");
        });
    }

    private void ShowStartup()
    {
        BeginPage("Автозагрузка", "Отключённые записи сохраняются в резерве и могут быть восстановлены.");
        List<Autostart.Entry> entries = Autostart.List();
        if (entries.Count == 0) { _pageContent!.Children.Add(Card("ЗАПИСИ НЕ НАЙДЕНЫ", new TextBlock { Text = "В автозагрузке нет записей.", Foreground = Brush(Theme.Muted) })); return; }
        StackPanel rows = new();
        foreach (Autostart.Entry entry in entries)
        {
            CheckBox check = new() { IsChecked = entry.Enabled, VerticalAlignment = VerticalAlignment.Center };
            check.Checked += async (_, _) => { if (!_suppressAutostartEvents) await SetAutostartAsync(entry, check, true); };
            check.Unchecked += async (_, _) => { if (!_suppressAutostartEvents) await SetAutostartAsync(entry, check, false); };
            string state = entry.Enabled ? string.Empty : " · отключено, запись в резерве";
            rows.Children.Add(Row(entry.Name, Autostart.SourceLabel(entry.Origin) + " · " + Ellipsize(entry.Command, 110) + state + (entry.RequiresAdmin ? " · админ" : string.Empty), check));
        }
        _pageContent!.Children.Add(Card("ЗАПИСИ АВТОЗАПУСКА · " + entries.Count, rows));
        WrapPanel taskActions = new();
        taskActions.Children.Add(Action("Включить очистку при входе", async () => await SetScheduledCleanAsync(true), true));
        taskActions.Children.Add(Action("Отключить задачу", async () => await SetScheduledCleanAsync(false)));
        _pageContent.Children.Add(Card("ЗАДАЧА CACHETrim", taskActions));
    }

    private async Task SetAutostartAsync(Autostart.Entry entry, CheckBox check, bool enabled)
    {
        if (_suppressAutostartEvents) return;
        if (_busy)
        {
            SetCheckSilently(check, !enabled, false);
            SetStatus("дождитесь завершения текущей операции");
            return;
        }

        _busy = true;
        check.IsEnabled = false;
        SetStatus(enabled ? "восстанавливается автозагрузка…" : "отключается автозагрузка…");
        try
        {
            string? error = await Task.Run(() => Autostart.SetEnabled(entry, enabled));
            if (error != null)
            {
                SetCheckSilently(check, !enabled, false);
                MessageBox.Show(this, error, "Не удалось изменить автозагрузку", MessageBoxButton.OK, MessageBoxImage.Warning);
                App.Log("ошибка автозагрузки " + entry.Name + ": " + error);
                SetStatus("автозагрузка не изменена");
            }
            else
            {
                entry.Enabled = enabled;
                App.Log((enabled ? "восстановлена автозагрузка: " : "отключена автозагрузка: ") + entry.Name);
                SetStatus("автозагрузка обновлена");
            }
        }
        catch (Exception ex)
        {
            SetCheckSilently(check, !enabled, false);
            App.Log("ошибка автозагрузки " + entry.Name + ": " + ex.Message);
            MessageBox.Show(this, ex.Message, "Не удалось изменить автозагрузку", MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus("автозагрузка не изменена");
        }
        finally
        {
            check.IsEnabled = true;
            _busy = false;
        }
    }

    private void ShowLog()
    {
        BeginPage("Журнал", "История операций и ошибки CacheTrim.");
        _logBox = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new WpfFontFamily("Consolas"), FontSize = 12, MinHeight = 500, Background = Brush(Theme.SurfaceAlt), Foreground = Brush(Theme.Text), BorderThickness = new Thickness(0), Padding = new Thickness(14), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        try { if (File.Exists(Config.HistoryPath)) _logBox.Text = string.Join(Environment.NewLine, File.ReadLines(Config.HistoryPath).TakeLast(700)); } catch { }
        _pageContent!.Children.Add(Card("ИСТОРИЯ ОПЕРАЦИЙ", _logBox));
        WrapPanel actions = new();
        actions.Children.Add(Action("Открыть файл журнала", () => OpenPath(Config.HistoryPath)));
        actions.Children.Add(Action("Открыть папку", () => OpenPath(Config.LogDir)));
        _pageContent.Children.Add(actions);
    }

    private void ShowSettings()
    {
        BeginPage("Настройки", "Темы, поведение очистки и обновления приложения.");
        double viewport = PageScroll.ViewportWidth > 0 ? PageScroll.ViewportWidth : Math.Max(680, ActualWidth - 224);
        double available = Math.Max(600, viewport - 52);
        double settingsCardWidth = available >= 1050 ? (available - 16) / 2 : Math.Min(700, available);
        WrapPanel cards = new() { HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch, VerticalAlignment = System.Windows.VerticalAlignment.Top };
        StackPanel appearance = new();
        WpfComboBox themes = new() { Width = 260, Height = 34, SelectedIndex = Theme.ToIndex(_settings.ThemeName), Margin = new Thickness(0, 4, 0, 8) };
        themes.Items.Add("Индиго"); themes.Items.Add("Океан"); themes.Items.Add("Графит"); themes.Items.Add("Жидкое стекло"); themes.Items.Add("Фиолетовый"); themes.Items.Add("Розовый");
        appearance.Children.Add(Row("Тема оформления", "Жидкое стекло размывает фон рабочего стола за окном.", themes));
        themes.SelectionChanged += (_, _) =>
        {
            _settings.ThemeName = Theme.FromIndex(themes.SelectedIndex);
            if (Theme.IsGlassName(_settings.ThemeName) && string.IsNullOrEmpty(_settings.AccentColorHex)) _settings.AccentColorHex = "#8FCBFF";
            Theme.Apply(_settings.ThemeName, _settings.AccentColorHex);
            ApplyTheme();
            ShowSettings();
        };

        WpfComboBox densityMode = new() { Width = 260, Height = 34, SelectedIndex = Math.Max(0, Math.Min(2, _settings.UiDensity)), Margin = new Thickness(0, 4, 0, 8) };
        densityMode.Items.Add("Компактно"); densityMode.Items.Add("Стандартно"); densityMode.Items.Add("Свободно");
        densityMode.SelectionChanged += (_, _) =>
        {
            _settings.UiDensity = densityMode.SelectedIndex;
            SaveConfig(_settings, "плотность интерфейса сохранена", "плотность интерфейса изменена");
            ShowSettings();
        };
        appearance.Children.Add(Row("Плотность интерфейса", "Компактно — больше строк на экране; свободно — крупнее отступы.", densityMode));

        if (Theme.IsGlassName(_settings.ThemeName))
        {
            if (!IsWindowsTransparencyEnabled())
            {
                appearance.Children.Add(new TextBlock
                {
                    Text = "Windows отключила эффекты прозрачности, поэтому фон сейчас сплошной. Включи их, чтобы увидеть Acrylic-размытие.",
                    FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8),
                    Foreground = Brush(Color.FromRgb(255, 176, 120))
                });
                appearance.Children.Add(Action("Включить эффекты прозрачности Windows", EnableWindowsTransparency, true));
            }
            appearance.Children.Add(new TextBlock
            {
                Text = "Desktop Acrylic доступен в Windows 11 22H2 и новее при включённой прозрачности. На других системах используется сплошной фон. Силу размытия задаёт Windows.",
                FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10), Foreground = Brush(Theme.Muted)
            });
            CheckBox backgroundBlur = new()
            {
                Content = "Размывать фон окна",
                IsChecked = _settings.GlassBackgroundBlur > 0,
                Margin = new Thickness(0, 0, 0, 8)
            };
            backgroundBlur.Checked += (_, _) => { _settings.GlassBackgroundBlur = 100; UpdateGlassEffect(); };
            backgroundBlur.Unchecked += (_, _) => { _settings.GlassBackgroundBlur = 0; UpdateGlassEffect(); };
            appearance.Children.Add(backgroundBlur);

            Slider density = new() { Minimum = 0, Maximum = 100, Value = _settings.GlassDensity, Width = 260, TickFrequency = 5 };
            TextBlock densityValue = new() { Text = _settings.GlassDensity + "%", Width = 48, VerticalAlignment = VerticalAlignment.Center, Foreground = Brush(Theme.Accent) };
            density.ValueChanged += (_, e) => { _settings.GlassDensity = (int)e.NewValue; densityValue.Text = _settings.GlassDensity + "%"; UpdateGlassEffect(); };
            appearance.Children.Add(Row("Плотность тонировки стекла", "Насколько заметен выбранный оттенок.", SliderWithValue(density, densityValue)));
        }

        WrapPanel colorActions = new() { MaxWidth = Math.Max(440, settingsCardWidth - 250) };
        colorActions.Children.Add(ColorAction("Голубой", () => SetAccentColor("#4EA8FF")));
        colorActions.Children.Add(ColorAction("Фиолетовый", () => SetAccentColor("#B499FF")));
        colorActions.Children.Add(ColorAction("Розовый", () => SetAccentColor("#FF8FB7")));
        colorActions.Children.Add(ColorAction("Янтарный", () => SetAccentColor("#FFB86B")));
        colorActions.Children.Add(ColorAction("Свой цвет…", PickGlassColor));
        appearance.Children.Add(Row("Цвет акцента и стекла", "Выбери свой цвет, он меняет оттенок стеклянной поверхности.", colorActions));
        appearance.Children.Add(new TextBlock { Text = "Текущий цвет: " + (_settings.AccentColorHex.Length > 0 ? _settings.AccentColorHex : Theme.DefaultAccentHex(_settings.ThemeName)), FontSize = 11, Foreground = Brush(Theme.Muted), Margin = new Thickness(2, 0, 0, 8) });
        WrapPanel appearanceActions = new();
        appearanceActions.Children.Add(Action("Сохранить оформление", () => SaveConfig(_settings, "оформление сохранено", "оформление сохранено"), true));
        appearanceActions.Children.Add(Action("Сбросить цвет", () => { _settings.AccentColorHex = string.Empty; Theme.Apply(_settings.ThemeName, string.Empty); ApplyTheme(); ShowSettings(); }));
        appearance.Children.Add(appearanceActions);
        Border appearanceCard = Card("ОФОРМЛЕНИЕ", appearance); appearanceCard.Width = settingsCardWidth; appearanceCard.Margin = new Thickness(0, 0, 16, 16); cards.Children.Add(appearanceCard);

        StackPanel behavior = new();
        TextBox delay = new() { Text = _settings.DelaySeconds.ToString(), Width = 180, Height = 32 };
        TextBox age = new() { Text = _settings.TempAgeHours.ToString(), Width = 180, Height = 32 };
        behavior.Children.Add(Row("Задержка автоочистки после входа", "Секунды; 20–60 дают Windows загрузить службы.", delay));
        behavior.Children.Add(Row("Temp-файлы старше", "Часы. 0 — удалить все незанятые temp-файлы.", age));
        behavior.Children.Add(Action("Сохранить параметры", () =>
        {
            if (int.TryParse(delay.Text, out int d)) _settings.DelaySeconds = Math.Clamp(d, 0, 600);
            if (int.TryParse(age.Text, out int a)) _settings.TempAgeHours = Math.Clamp(a, 0, 8760);
            SaveConfig(_settings, "параметры сохранены", "параметры очистки сохранены");
        }, true));
        Border behaviorCard = Card("ПОВЕДЕНИЕ ОЧИСТКИ", behavior); behaviorCard.Width = settingsCardWidth; behaviorCard.Margin = new Thickness(0, 0, 0, 16); cards.Children.Add(behaviorCard);

        StackPanel updater = new();
        updater.Children.Add(new TextBlock { Text = "Версия " + AppInfo.Version + " · GitHub Releases · SHA-256", Foreground = Brush(Theme.Muted), FontSize = 12 });
        updater.Children.Add(Action("Проверить обновления", async () => await CheckUpdatesAsync()));
        Border updaterCard = Card("ОБНОВЛЕНИЯ", updater); updaterCard.Width = settingsCardWidth; updaterCard.Margin = new Thickness(0, 0, 16, 16); cards.Children.Add(updaterCard);

        WrapPanel scheduled = new();
        scheduled.Children.Add(Action("Включить автоочистку", async () => await SetScheduledCleanAsync(true), true));
        scheduled.Children.Add(Action("Снять задачу", async () => await SetScheduledCleanAsync(false)));
        Border scheduledCard = Card("АВТООЧИСТКА ПРИ ВХОДЕ", scheduled); scheduledCard.Width = settingsCardWidth; scheduledCard.Margin = new Thickness(0, 0, 0, 16); cards.Children.Add(scheduledCard);

        WrapPanel windowsLinks = new();
        windowsLinks.Children.Add(CompactAction("Экран", () => OpenWindowsSettings("ms-settings:display")));
        windowsLinks.Children.Add(CompactAction("Хранилище", () => OpenWindowsSettings("ms-settings:storagesense")));
        windowsLinks.Children.Add(CompactAction("Сеть", () => OpenWindowsSettings("ms-settings:network")));
        windowsLinks.Children.Add(CompactAction("Персонализация", () => OpenWindowsSettings("ms-settings:personalization")));
        Border windowsLinksCard = Card("ПАРАМЕТРЫ WINDOWS", windowsLinks); windowsLinksCard.Width = settingsCardWidth; windowsLinksCard.Margin = new Thickness(0, 0, 16, 16); cards.Children.Add(windowsLinksCard);

        StackPanel localFiles = new();
        localFiles.Children.Add(new TextBlock { Text = "Настройки: " + Config.ConfigPath + "\nЖурнал: " + Config.HistoryPath, Foreground = Brush(Theme.Muted), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        WrapPanel fileActions = new();
        fileActions.Children.Add(CompactAction("Открыть журнал", () => OpenPath(Config.HistoryPath)));
        fileActions.Children.Add(CompactAction("Открыть папку данных", () => OpenPath(Config.LogDir)));
        localFiles.Children.Add(fileActions);
        Border filesCard = Card("ДАННЫЕ CACHETRIM", localFiles); filesCard.Width = settingsCardWidth; filesCard.Margin = new Thickness(0, 0, 0, 16); cards.Children.Add(filesCard);
        _pageContent.Children.Add(cards);
    }

    private Wpf.Ui.Controls.Button ColorAction(string title, Action action)
    {
        Wpf.Ui.Controls.Button button = Action(title, action);
        button.Width = 124;
        button.MinWidth = 0;
        button.Height = 34;
        button.Padding = new Thickness(7, 4, 7, 4);
        button.Margin = new Thickness(0, 2, 6, 2);
        return button;
    }

    private Wpf.Ui.Controls.Button CompactAction(string title, Action action)
    {
        Wpf.Ui.Controls.Button button = Action(title, action);
        button.Width = 166;
        button.MinWidth = 0;
        button.Height = 36;
        button.Padding = new Thickness(8, 5, 8, 5);
        button.Margin = new Thickness(0, 2, 6, 2);
        return button;
    }

    private static StackPanel SliderWithValue(Slider slider, TextBlock value) => new() { Orientation = Orientation.Horizontal, Children = { slider, value } };

    private void SetAccentColor(string hex)
    {
        _settings.AccentColorHex = hex;
        Theme.Apply(_settings.ThemeName, _settings.AccentColorHex);
        ApplyTheme();
        ShowSettings();
    }

    private void PickGlassColor()
    {
        using System.Windows.Forms.ColorDialog picker = new() { FullOpen = true, Color = System.Drawing.Color.FromArgb(Theme.Accent.A, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B) };
        if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        Color color = Color.FromArgb(255, picker.Color.R, picker.Color.G, picker.Color.B);
        _settings.AccentColorHex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        Theme.Apply(_settings.ThemeName, _settings.AccentColorHex);
        ApplyTheme();
        ShowSettings();
    }

    private async Task CheckUpdatesAsync()
    {
        if (_busy)
        {
            SetStatus("сначала дождитесь завершения текущей операции");
            return;
        }
        _busy = true;
        SetStatus("проверяю GitHub Releases…");
        try
        {
            UpdateInfo info = await Task.Run(UpdateService.CheckLatest);
            if (!info.Available) { MessageBox.Show(this, "Установлена последняя версия CacheTrim " + AppInfo.Version + ".", "Обновлений нет", MessageBoxButton.OK, MessageBoxImage.Information); SetStatus("установлена последняя версия"); return; }
            if (!Confirm("Доступно обновление " + info.Version, "Скачать проверенную SHA-256 сборку и перезапустить CacheTrim?"))
            {
                SetStatus("обновление отложено");
                return;
            }
            SetStatus("скачиваю и проверяю обновление " + info.Version + "…");
            string exe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            await Task.Run(() => UpdateService.DownloadAndLaunch(info, exe, Environment.ProcessId));
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex) { SetStatus("ошибка проверки обновления"); MessageBox.Show(this, ex.Message, "Не удалось проверить обновления", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { _busy = false; }
    }

    private async Task SetScheduledCleanAsync(bool enabled)
    {
        string exe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        await RunBusyAsync(enabled ? "включается автоочистка…" : "снимается задача автоочистки…", async () =>
        {
            string? error = await Task.Run(() => enabled ? TaskSetup.Install(exe, _settings.DelaySeconds) : TaskSetup.Uninstall());
            if (error != null) throw new InvalidOperationException(error);
            SetStatus(enabled ? "автоочистка при входе включена" : "автоочистка при входе отключена");
            App.Log(enabled ? "задача автоочистки включена" : "задача автоочистки снята");
        });
    }

    private async Task ShowDiagnosticsAsync()
    {
        try
        {
            Native.MemStats s = await Task.Run(Native.GetMemoryStats);
            string disks = string.Join(Environment.NewLine, DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).Select(d => d.Name + "  " + Cleaner.FormatBytes(d.AvailableFreeSpace) + " свободно из " + Cleaner.FormatBytes(d.TotalSize)));
            string report = "Windows " + Environment.OSVersion.Version + " · " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit") +
                "\nПроцессоры: " + Environment.ProcessorCount + "\nПроцессы: " + s.ProcessCount + " · Дескрипторы: " + s.HandleCount +
                "\nОЗУ: " + Cleaner.FormatBytes((long)s.TotalPhys) + " всего · " + Cleaner.FormatBytes((long)s.AvailPhys) + " доступно" +
                "\nStandby: " + Cleaner.FormatBytes((long)s.Standby) + " · кэш: " + Cleaner.FormatBytes((long)s.Cache) + "\n\nДИСКИ\n" + disks +
                "\n\nЗадача автоочистки: " + (TaskSetup.IsInstalled() ? "включена" : "выключена") + "\nПрава администратора: " + (Native.IsElevated ? "есть" : "нет");
            MessageBox.Show(this, report, "Диагностика системы", MessageBoxButton.OK, MessageBoxImage.Information);
            App.Log("сформирован диагностический отчёт");
        }
        catch (Exception ex)
        {
            App.Log("ошибка диагностики: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Не удалось выполнить диагностику", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task ExportSystemReportAsync()
    {
        await RunBusyAsync("собирается системный отчёт…", async () =>
        {
            Native.MemStats memory = await Task.Run(Native.GetMemoryStats);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(documents))
                documents = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string reportFolder = System.IO.Path.Combine(documents, "CacheTrim", "Reports");
            Directory.CreateDirectory(reportFolder);
            string reportPath = System.IO.Path.Combine(reportFolder, "CacheTrim-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".txt");
            List<string> activeTweaks = await Task.Run(() => Tweaks.All().Where(item => item.IsOn()).Select(item => "- " + item.Name).ToList());
            List<string> disks = new();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                        disks.Add("- " + drive.Name.TrimEnd('\\', ':') + ": " + Cleaner.FormatBytes(drive.AvailableFreeSpace) + " свободно из " + Cleaner.FormatBytes(drive.TotalSize));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            string report = "CacheTrim — системный отчёт\r\n" +
                "Создан: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" +
                "Версия CacheTrim: " + AppInfo.Version + "\r\n" +
                "Windows: " + Environment.OSVersion.Version + "\r\n" +
                "Разрядность Windows: " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit") + "\r\n" +
                "Логических процессоров: " + Environment.ProcessorCount + "\r\n" +
                "Память: " + Cleaner.FormatBytes((long)memory.TotalPhys) + " всего; " + Cleaner.FormatBytes((long)memory.AvailPhys) + " доступно; " + Cleaner.FormatBytes((long)(memory.TotalPhys > memory.AvailPhys ? memory.TotalPhys - memory.AvailPhys : 0)) + " занято\r\n" +
                "Запущено процессов: " + memory.ProcessCount + "\r\n" +
                "Права администратора: " + (Native.IsElevated ? "есть" : "нет") + "\r\n" +
                "Автоочистка при входе: " + (TaskSetup.IsInstalled() ? "включена" : "выключена") + "\r\n\r\n" +
                "ДИСКИ\r\n" + (disks.Count == 0 ? "Нет доступных фиксированных дисков." : string.Join("\r\n", disks)) + "\r\n\r\n" +
                "ВКЛЮЧЁННЫЕ НАСТРОЙКИ CACHETRIM\r\n" + (activeTweaks.Count == 0 ? "Нет включённых настроек." : string.Join("\r\n", activeTweaks)) + "\r\n";
            File.WriteAllText(reportPath, report, new System.Text.UTF8Encoding(false));
            App.Log("сохранён системный отчёт без списка программ и личных путей");
            MessageBoxResult result = MessageBox.Show(this, "Отчёт сохранён:\n" + reportPath + "\n\nОткрыть папку с отчётом?", "Системный отчёт готов", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes) OpenPath(reportFolder);
            SetStatus("системный отчёт сохранён");
        });
    }

    private void OpenWindowsSettings(string uri)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Не удалось открыть параметры Windows", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void OpenPath(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path)) { Directory.CreateDirectory(Config.LogDir); path = Config.LogDir; }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }
        catch (Exception ex) { App.Log("не удалось открыть путь: " + ex.Message); }
    }

    private static string Ellipsize(string value, int length) => value.Length <= length ? value : value[..(length - 1)] + "…";

    private void OnLogAdded(string line)
    {
        try
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (!IsLoaded || Dispatcher.HasShutdownStarted || _activePage != "log" || _logBox == null) return;
                    _logBox.AppendText(line + Environment.NewLine);
                    _logBox.ScrollToEnd();
                }
                catch (Exception) { }
            }));
        }
        catch (Exception) { }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _memoryTimer.Stop();
        App.LogAdded -= OnLogAdded;
    }
}
