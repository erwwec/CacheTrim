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
    private async Task RunSfcAsync()
    {
        try { new GameGuard().Check(true); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Обслуживание приостановлено", MessageBoxButton.OK, MessageBoxImage.Information); return; }
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
            SetStatus(highPerformance ? "план питания: высокая производительность" : "план питания переключён (при отсутствии копии — сбалансированный)");
            App.Log(highPerformance ? "план питания переключён: высокая производительность" : "план питания переключён: резервная копия или сбалансированный");
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
        Border hotkeyCard = Card("ГОРЯЧИЕ КЛАВИШИ", CreateHotkeySettings());
        hotkeyCard.Width = settingsCardWidth; hotkeyCard.Margin = new Thickness(0, 0, 16, 16); cards.Children.Add(hotkeyCard);
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
        DisposeHotkeys();
        _memoryTimer.Stop();
        App.LogAdded -= OnLogAdded;
    }
}
