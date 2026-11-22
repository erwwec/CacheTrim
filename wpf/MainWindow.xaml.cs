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

public partial class MainWindow : Window
{
    private readonly Config _settings;
    private readonly List<double> _memoryHistory = new();
    private readonly DispatcherTimer _memoryTimer;
    private string _activePage = "memory";
    private bool _busy;
    private bool _suppressColor;
    private bool _suppressTweakEvents;
    private string _optimizationModule = "Windows";
    private StackPanel? _pageContent;
    private System.Windows.Controls.Panel? _pageCards;
    private Canvas? _chart;
    private TextBlock? _memoryUsed;
    private TextBlock? _memoryStandby;
    private TextBlock? _memoryCache;
    private TextBlock? _memoryCommit;
    private TextBlock? _memoryPercent;
    private readonly Border[] _metricTracks = new Border[4];
    private readonly Border[] _metricFills = new Border[4];
    private readonly double[] _metricRatios = new double[4];
    private TextBox? _logBox;
    private StackPanel? _cleanLeftColumn;
    private StackPanel? _cleanRightColumn;
    private double _cleanCardWidth;
    private int _lastBackdropResult = int.MinValue;

    public MainWindow(int startPage = 0)
    {
        InitializeComponent();
        PageScroll.SizeChanged += delegate { ResizeDashboardChart(); };
        _settings = App.Settings;
        Theme.Apply(_settings.ThemeName, _settings.AccentColorHex);
        ApplyTheme();
        BuildNavigation();
        App.LogAdded += OnLogAdded;
        _memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _memoryTimer.Tick += delegate { RefreshMemory(); };
        _memoryTimer.Start();
        string[] pages = { "memory", "clean", "optimize", "startup", "log", "settings", "advanced" };
        Navigate(pages[Math.Max(0, Math.Min(pages.Length - 1, startPage))]);
        RefreshMemory();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        WindowSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(92, 20, 25, 33)) : Brush(Theme.Bg);
        TitleSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(50, 28, 34, 44)) : Brush(Theme.Surface);
        NavigationSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(72, 23, 29, 39)) : Brush(Theme.Surface);
        StatusSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(58, 27, 33, 43)) : Brush(Theme.Surface);
        ApplyTheme();
        RefreshMemory();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e) => MaximizeButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (e.ClickCount == 2) { OnMaximizeClick(sender, e); return; }
        try { DragMove(); } catch { }
    }

    private void BuildNavigation()
    {
        NavigationItems.Children.Clear();
        AddNav("Память", "☷", "memory");
        AddNav("Очистка", "◇", "clean");
        AddNav("Оптимизация", "⚙", "optimize");
        AddNav("Расширенные", "⚡", "advanced");
        AddNav("Автозагрузка", "⏻", "startup");
        AddNav("Журнал", "≡", "log");
        AddNav("Настройки", "⚙", "settings");
    }

    private void AddNav(string title, string icon, string page)
    {
        Wpf.Ui.Controls.Button button = new()
        {
            Content = new DockPanel { LastChildFill = true, Children =
            {
                new TextBlock { Text = icon, Width = 28, FontSize = 17, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center },
                new TextBlock { Text = title, FontSize = 13, VerticalAlignment = VerticalAlignment.Center }
            }},
            Appearance = ControlAppearance.Transparent,
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
            Padding = new Thickness(10, 0, 10, 0),
            Height = 42,
            Margin = new Thickness(0, 2, 0, 2),
            Tag = page
        };
        button.Click += (_, _) => Navigate(page);
        NavigationItems.Children.Add(button);
        if (page == _activePage) SetNavSelected(button, true);
    }

    private void Navigate(string page)
    {
        if (page == _activePage && _pageContent != null) return;
        _activePage = page;
        foreach (Wpf.Ui.Controls.Button b in NavigationItems.Children.OfType<Wpf.Ui.Controls.Button>())
            SetNavSelected(b, Equals(b.Tag, page));
        switch (page)
        {
            case "clean": ShowClean(); break;
            case "optimize": ShowOptimize(); break;
            case "advanced": ShowAdvanced(); break;
            case "startup": ShowStartup(); break;
            case "log": ShowLog(); break;
            case "settings": ShowSettings(); break;
            default: ShowMemory(); break;
        }
    }

    private static void SetNavSelected(Wpf.Ui.Controls.Button button, bool selected)
    {
        button.Appearance = ControlAppearance.Transparent;
        button.Background = selected ? Brush(Theme.SurfaceAlt) : System.Windows.Media.Brushes.Transparent;
        button.Foreground = Brush(Theme.Text);
        button.BorderBrush = selected ? Brush(Theme.Accent) : System.Windows.Media.Brushes.Transparent;
        button.BorderThickness = selected ? new Thickness(3, 0, 0, 0) : new Thickness(3, 0, 0, 0);
        button.Opacity = selected ? 1.0 : 0.82;
        button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private void BeginPage(string title, string subtitle)
    {
        StackPanel stack = new() { Margin = new Thickness(0, 0, 0, 16), HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        stack.Children.Add(new StackPanel { Margin = new Thickness(0, 0, 0, 8), Children =
        {
            new TextBlock { Text = title, FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = Brush(Theme.Text) },
            new TextBlock { Text = subtitle, FontSize = 12, Foreground = Brush(Theme.Muted), TextWrapping = TextWrapping.Wrap }
        }});
        _pageContent = stack;
        _pageCards = null;
        _chart = null;
        _memoryUsed = _memoryStandby = _memoryCache = _memoryCommit = _memoryPercent = null;
        _logBox = null;
        PageHost.Content = stack;
    }

    private Border Card(string title, UIElement content)
    {
        int density = Math.Max(0, Math.Min(2, _settings.UiDensity));
        double padding = density == 0 ? 14 : density == 2 ? 22 : 18;
        double gap = density == 0 ? 8 : density == 2 ? 16 : 12;
        StackPanel body = new() { Orientation = Orientation.Vertical };
        body.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Brush(Theme.Accent), Margin = new Thickness(0, 0, 0, gap)
        });
        body.Children.Add(content);
        Border card = new()
        {
            Child = body, Margin = new Thickness(0, 0, 0, gap), Padding = new Thickness(padding),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Theme.IsGlass ? Color.FromArgb(184, Theme.Surface.R, Theme.Surface.G, Theme.Surface.B) : Theme.Surface),
            BorderBrush = Brush(Theme.Line), BorderThickness = new Thickness(1),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Top
        };
        return card;
    }

    private StackPanel Row(string title, string hint, FrameworkElement control)
    {
        int density = Math.Max(0, Math.Min(2, _settings.UiDensity));
        double rowHeight = density == 0 ? 46 : density == 2 ? 66 : 54;
        double rowMargin = density == 0 ? 2 : density == 2 ? 6 : 4;
        Grid row = new() { MinHeight = rowHeight, Margin = new Thickness(0, rowMargin, 0, rowMargin) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        StackPanel labels = new() { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brush(Theme.Text) });
        if (!string.IsNullOrWhiteSpace(hint)) labels.Children.Add(new TextBlock { Text = hint, FontSize = 10, Foreground = Brush(Theme.Muted), TextWrapping = TextWrapping.Wrap });
        row.Children.Add(labels);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return new StackPanel { Children = { new Border { BorderBrush = Brush(Theme.Line), BorderThickness = new Thickness(0, 0, 0, 1), Child = row } } };
    }

    private Wpf.Ui.Controls.Button Action(string title, Action action, bool primary = false)
    {
        Wpf.Ui.Controls.Button button = new()
        {
            Content = title,
            Appearance = ControlAppearance.Transparent,
            Padding = new Thickness(16, 8, 16, 8),
            MinWidth = 132,
            Margin = new Thickness(0, 0, 8, 0),
            CornerRadius = new CornerRadius(7),
            Background = Brush(primary ? Theme.Accent : Theme.SurfaceAlt),
            Foreground = Brush(primary ? OnAccent(Theme.Accent) : Theme.Text),
            BorderBrush = Brush(primary ? Theme.Accent : Theme.Line),
            BorderThickness = new Thickness(1)
        };
        button.IsEnabledChanged += delegate { button.Opacity = button.IsEnabled ? 1.0 : 0.72; };
        button.MouseEnter += delegate
        {
            if (!button.IsEnabled) return;
            button.Background = Brush(primary ? Theme.Accent : Theme.Line);
            button.BorderBrush = Brush(Theme.Accent);
        };
        button.MouseLeave += delegate
        {
            button.Background = Brush(primary ? Theme.Accent : Theme.SurfaceAlt);
            button.BorderBrush = Brush(primary ? Theme.Accent : Theme.Line);
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static SolidColorBrush Brush(Color c) => new(c);

    private static Color OnAccent(Color color)
    {
        double luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
        return luminance > 0.56 ? Color.FromRgb(12, 20, 30) : Color.FromRgb(248, 250, 253);
    }

    private void SetStatus(string text) => StatusText.Text = text;

    private async Task RunBusyAsync(string label, Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        SetStatus(label);
        try { await action(); SetStatus("операция завершена"); }
        catch (Exception ex) { App.Log("ошибка: " + ex.Message); MessageBox.Show(this, ex.Message, "CacheTrim", MessageBoxButton.OK, MessageBoxImage.Error); SetStatus("операция завершилась с ошибкой"); }
        finally { _busy = false; }
    }

    private bool Confirm(string title, string message) => MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private bool ConfirmRisk(string title, string details)
    {
        return MessageBox.Show(this,
            "Это потенциально опасная функция. Вы уверены, что хотите её включить?\n\n" + details,
            title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void EnableWindowsTransparency()
    {
        if (!Confirm("Включить эффекты прозрачности Windows?",
            "Acrylic не работает, потому что эффекты прозрачности сейчас выключены в Windows. Включение затронет оформление всей системы и может немного увеличить нагрузку на графику. Изменение можно отменить здесь или в параметрах Windows.")) return;
        try
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                if (key == null) throw new InvalidOperationException("Не удалось открыть параметры оформления Windows.");
                key.SetValue("EnableTransparency", 1, RegistryValueKind.DWord);
            }
            SendMessageTimeout(new IntPtr(0xffff), 0x001A, IntPtr.Zero, "ImmersiveColorSet", 0x0002, 2000, out _);
            UpdateGlassEffect();
            SetStatus("эффекты прозрачности Windows включены");
            ShowSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Не удалось включить эффекты прозрачности", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ApplyTheme()
    {
        Background = Theme.IsGlass ? System.Windows.Media.Brushes.Transparent : Brush(Theme.Bg);
        WindowSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(92, 20, 25, 33)) : Brush(Theme.Bg);
        TitleSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(50, 28, 34, 44)) : Brush(Theme.Surface);
        NavigationSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(72, 23, 29, 39)) : Brush(Theme.Surface);
        StatusSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(58, 27, 33, 43)) : Brush(Theme.Surface);
        AdminText.Foreground = Brush(Theme.Accent);
        StatusAdmin.Foreground = Brush(Theme.Accent);
        foreach (Wpf.Ui.Controls.Button button in NavigationItems.Children.OfType<Wpf.Ui.Controls.Button>())
            SetNavSelected(button, Equals(button.Tag, _activePage));
        Application.Current.Resources["SystemAccentColor"] = Theme.Accent;
        Application.Current.Resources["SystemAccentColorBrush"] = Brush(Theme.Accent);
        Application.Current.Resources["SystemAccentColorPrimaryBrush"] = Brush(Theme.Accent);
        Application.Current.Resources["AccentFillColorDefaultBrush"] = Brush(Theme.Accent);
        UpdateGlassEffect();
    }

    private void UpdateGlassEffect()
    {
        if (!IsLoaded) return;
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        bool windowsTransparency = IsWindowsTransparencyEnabled();
        bool requestAcrylic = Theme.IsGlass && _settings.GlassBackgroundBlur > 0 && windowsTransparency;
        int backdrop = requestAcrylic ? 3 : 1;
        int result = DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
        if (result != _lastBackdropResult && result != 0 && Theme.IsGlass)
            App.Log("Windows не включила системное размытие окна (DWM, код " + result + ")");
        _lastBackdropResult = result;
        byte alpha = (byte)(Theme.IsGlass && requestAcrylic && result == 0 ? 28 + _settings.GlassDensity * 0.72 : 255);
        WindowSurface.Background = Theme.IsGlass
            ? new SolidColorBrush(Color.FromArgb(alpha, 20, 25, 33))
            : Brush(Theme.Bg);
        if (Theme.IsGlass && IsLoaded)
        {
            if (!windowsTransparency) SetStatus("Размытие выключено в Windows: включите эффекты прозрачности");
            else if (result != 0) SetStatus("Windows не поддержала Acrylic · показан сплошной фон");
            else SetStatus(requestAcrylic ? "Acrylic Windows включён" : "Размытие фона выключено");
        }
    }

    private static bool IsWindowsTransparencyEnabled()
    {
        try
        {
            object value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "EnableTransparency", 1);
            return Convert.ToInt32(value) != 0;
        }
        catch { return false; }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);


    private void ShowMemory()
    {
        BeginPage("Память", "Живые показатели Windows и быстрые действия.");
        Grid metrics = new() { Margin = new Thickness(0, 0, 0, 2) };
        for (int i = 0; i < 4; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _memoryUsed = AddMetric(metrics, 0, "ЗАНЯТО ОЗУ", "использовано из всего объёма", Color.FromRgb(229, 106, 116));
        _memoryStandby = AddMetric(metrics, 1, "БЫСТРО ОСВОБОДИТЬ", "резерв памяти Windows", Theme.Accent);
        _memoryCache = AddMetric(metrics, 2, "КЭШ WINDOWS", "данные для быстрого доступа", Color.FromRgb(93, 169, 238));
        _memoryCommit = AddMetric(metrics, 3, "ВИРТУАЛЬНАЯ ПАМЯТЬ", "занято из доступного лимита", Color.FromRgb(175, 137, 255));
        _pageContent!.Children.Add(metrics);

        Grid graph = new() { Height = Math.Max(270, PageScroll.ViewportHeight - 330) };
        graph.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        graph.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid header = new() { Margin = new Thickness(4, 0, 4, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = "ЗАГРУЗКА ПАМЯТИ · ПОСЛЕДНИЕ 3 МИНУТЫ", FontSize = 10, Foreground = Brush(Theme.Muted), VerticalAlignment = VerticalAlignment.Center });
        _memoryPercent = new TextBlock { Text = "—", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brush(Theme.Accent) };
        Grid.SetColumn(_memoryPercent, 1); header.Children.Add(_memoryPercent);
        graph.Children.Add(header);
        _chart = new Canvas { ClipToBounds = true };
        Grid.SetRow(_chart, 1); graph.Children.Add(_chart);
        _chart.SizeChanged += (_, _) => DrawChart();
        PageScroll.Dispatcher.BeginInvoke(new Action(ResizeDashboardChart), DispatcherPriority.Loaded);
        _pageContent.Children.Add(Card("ПАМЯТЬ · 3 МИНУТЫ", graph));
        WrapPanel buttons = new() { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Action("Очистить память", async () => await PurgeMemoryAsync(), true));
        buttons.Children.Add(Action("Полный прогон", async () => await RunFullScanAsync()));
        buttons.Children.Add(Action("Диагностика системы", async () => await ShowDiagnosticsAsync()));
        _pageContent.Children.Add(buttons);
        RefreshMemory();
    }

    private void ResizeDashboardChart()
    {
        if (_activePage != "memory" || _chart == null) return;
        Grid graph = _chart.Parent as Grid;
        if (graph != null && PageScroll.ViewportHeight > 0)
            graph.Height = Math.Max(270, PageScroll.ViewportHeight - 330);
    }

    private TextBlock AddMetric(Grid grid, int column, string label, string hint, Color accent)
    {
        StackPanel content = new();
        content.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = Brush(Theme.Muted) });
        TextBlock value = new() { Text = "—", FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Brush(Theme.Text) };
        content.Children.Add(value);
        content.Children.Add(new TextBlock { Text = hint, FontSize = 10, Foreground = Brush(Theme.Muted) });
        Border fill = new() { Width = 0, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Background = Brush(accent) };
        Border line = new() { Height = 4, Background = Brush(Theme.Line), Margin = new Thickness(0, 5, 0, 0), Child = fill, ClipToBounds = true };
        _metricTracks[column] = line;
        _metricFills[column] = fill;
        line.SizeChanged += (_, _) => UpdateMetricBar(column);
        content.Children.Add(line);
        Wpf.Ui.Controls.CardControl card = new() { Content = content, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(16), CornerRadius = new CornerRadius(10), Background = Brush(Theme.Surface), BorderBrush = Brush(Theme.Line), BorderThickness = new Thickness(1) };
        Grid.SetColumn(card, column); grid.Children.Add(card);
        return value;
    }

    private void RefreshMemory()
    {
        try
        {
            Native.MemStats stats = Native.GetMemoryStats();
            ulong total = Math.Max(1, stats.TotalPhys);
            double used = (double)(total - Math.Min(total, stats.AvailPhys)) / total;
            _memoryHistory.Add(used);
            if (_memoryHistory.Count > 120) _memoryHistory.RemoveAt(0);
            ulong usedBytes = total - Math.Min(total, stats.AvailPhys);
            if (_memoryUsed != null) _memoryUsed.Text = Cleaner.FormatBytes((long)usedBytes) + " из " + Cleaner.FormatBytes((long)total);
            if (_memoryStandby != null) _memoryStandby.Text = Cleaner.FormatBytes((long)stats.Standby);
            if (_memoryCache != null) _memoryCache.Text = Cleaner.FormatBytes((long)stats.Cache);
            ulong commitLimit = Math.Max(1, stats.CommitLimit);
            if (_memoryCommit != null) _memoryCommit.Text = Cleaner.FormatBytes((long)stats.CommitUsed) + " из " + Cleaner.FormatBytes((long)commitLimit);
            SetMetricRatio(0, used);
            SetMetricRatio(1, (double)stats.Standby / total);
            SetMetricRatio(2, (double)stats.Cache / total);
            SetMetricRatio(3, (double)stats.CommitUsed / commitLimit);
            if (_memoryPercent != null) _memoryPercent.Text = Math.Round(used * 100).ToString("0") + "%";
            DrawChart();
        }
        catch (Exception ex) { SetStatus("ошибка показателей памяти: " + ex.Message); }
    }

    private void SetMetricRatio(int index, double ratio)
    {
        _metricRatios[index] = Math.Max(0, Math.Min(1, ratio));
        UpdateMetricBar(index);
    }

    private void UpdateMetricBar(int index)
    {
        Border track = _metricTracks[index];
        Border fill = _metricFills[index];
        if (track == null || fill == null) return;
        double ratio = _metricRatios[index];
        fill.Width = track.ActualWidth * ratio;
        Color barColor = index == 0 && ratio >= 0.90 ? Color.FromRgb(239, 91, 105)
            : index == 0 && ratio >= 0.75 ? Color.FromRgb(255, 181, 92)
            : Theme.Accent;
        fill.Background = Brush(barColor);
    }

    private void DrawChart()
    {
        if (_chart == null || _chart.ActualWidth < 40 || _chart.ActualHeight < 40) return;
        _chart.Children.Clear();
        double width = _chart.ActualWidth, height = _chart.ActualHeight;
        for (int i = 0; i < 5; i++)
        {
            double y = i * (height - 1) / 4;
            _chart.Children.Add(new Line { X1 = 0, X2 = width, Y1 = y, Y2 = y, Stroke = Brush(Color.FromArgb(50, Theme.Muted.R, Theme.Muted.G, Theme.Muted.B)), StrokeThickness = 1 });
        }
        if (_memoryHistory.Count < 2) return;
        Polyline line = new() { Stroke = Brush(Theme.Accent), StrokeThickness = 2.5, StrokeLineJoin = PenLineJoin.Round };
        for (int i = 0; i < _memoryHistory.Count; i++)
            line.Points.Add(new WpfPoint(width * i / (_memoryHistory.Count - 1), height - _memoryHistory[i] * (height - 12) - 6));
        _chart.Children.Add(line);
    }

    private async Task PurgeMemoryAsync()
    {
        if (!Confirm("Очистка памяти", "Windows освободит выбранные списки страниц. Это не удаляет пользовательские файлы.")) return;
        await RunBusyAsync("очищается память…", async () =>
        {
            Config c = _settings.Clone();
            await Task.Run(() =>
            {
                if (c.WorkingSets) LogPurge(Native.Purge(Native.PurgeTarget.WorkingSets), "рабочие наборы");
                if (c.Modified) LogPurge(Native.Purge(Native.PurgeTarget.ModifiedList), "изменённые страницы");
                if (c.Standby) LogPurge(Native.Purge(Native.PurgeTarget.StandbyList), "standby-список");
                if (c.LowStandby) LogPurge(Native.Purge(Native.PurgeTarget.LowPriorityStandby), "низкоприоритетный standby");
                if (c.FileCache) LogPurge(Native.FlushSystemFileCache(), "системный кэш файлов");
            });
            RefreshMemory();
        });
    }

    private async Task RunFullScanAsync()
    {
        if (!Confirm("Полный прогон", "Запустить очистку выбранных категорий и памяти? Занятые файлы будут пропущены.")) return;
        Config c = _settings.Clone();
        await RunBusyAsync("выполняется полный прогон…", async () =>
        {
            await Task.Run(() =>
            {
                if (c.WorkingSets) LogPurge(Native.Purge(Native.PurgeTarget.WorkingSets), "рабочие наборы");
                if (c.Modified) LogPurge(Native.Purge(Native.PurgeTarget.ModifiedList), "изменённые страницы");
                if (c.Standby) LogPurge(Native.Purge(Native.PurgeTarget.StandbyList), "standby-список");
                if (c.LowStandby) LogPurge(Native.Purge(Native.PurgeTarget.LowPriorityStandby), "низкоприоритетный standby");
                if (c.FileCache) LogPurge(Native.FlushSystemFileCache(), "системный кэш файлов");
                Cleaner cleaner = new(App.Log); cleaner.Run(c);
                App.Log("полный прогон завершён · удалено " + Cleaner.FormatBytes(cleaner.BytesFreed) + " · пропущено " + cleaner.FilesSkipped);
            });
            RefreshMemory();
        });
    }

    private void LogPurge(string? error, string label) => App.Log(error == null ? "готово: " + label : "ошибка " + label + ": " + error);

    private void ShowClean()
    {
        BeginPage("Очистка", "Выбери категории очистки. Занятые файлы пропускаются.");
        double viewport = PageScroll.ViewportWidth > 0 ? PageScroll.ViewportWidth : Math.Max(720, ActualWidth - 224);
        double available = Math.Max(600, viewport - 52);
        bool twoColumns = available >= 760;
        Grid cards = new() { HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch, VerticalAlignment = System.Windows.VerticalAlignment.Top };
        cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (twoColumns) cards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _cleanCardWidth = twoColumns ? (available - 16) / 2 : available;
        _cleanLeftColumn = new StackPanel { Margin = new Thickness(0, 0, twoColumns ? 12 : 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Top };
        _cleanRightColumn = twoColumns ? new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Top } : _cleanLeftColumn;
        cards.Children.Add(_cleanLeftColumn);
        if (twoColumns) { Grid.SetColumn(_cleanRightColumn, 1); cards.Children.Add(_cleanRightColumn); }
        Config c = _settings;
        AddCleanGroup("ПАМЯТЬ", new[]
        {
            Opt("Рабочие наборы процессов", "Освобождает неиспользуемую память.", () => c.WorkingSets, v => c.WorkingSets = v),
            Opt("Изменённые страницы", "Сбрасывает изменённые страницы.", () => c.Modified, v => c.Modified = v),
            Opt("Standby-список", "Очищает закэшированные страницы.", () => c.Standby, v => c.Standby = v),
            Opt("Низкоприоритетный standby", "Очищает низкоприоритетные страницы.", () => c.LowStandby, v => c.LowStandby = v),
            Opt("Системный кэш файлов", "Обрезает кэш файловой системы.", () => c.FileCache, v => c.FileCache = v)
        });
        AddCleanGroup("СЕТЬ", new[]
        {
            Opt("DNS-кэш", "Очищает записи DNS.", () => c.Dns, v => c.Dns = v),
            Opt("ARP-кэш", "Сбрасывает таблицу адресов сети.", () => c.Arp, v => c.Arp = v),
            Opt("NetBIOS-кэш", "Обновляет кэш NetBIOS.", () => c.Netbios, v => c.Netbios = v)
        });
        if (twoColumns)
        {
            _cleanLeftColumn.Children.Add(Card("ПЕРЕД ЗАПУСКОМ", new TextBlock
            {
                Text = "Закрой игры и браузеры, если очищаешь их кэш. CacheTrim пропускает занятые файлы и не удаляет личные документы. Кэш шейдеров игры создадут заново при следующем запуске.",
                TextWrapping = TextWrapping.Wrap, Foreground = Brush(Theme.Muted), FontSize = 12
            }));
        }
        AddCleanGroup("ДИСК И КЭШИ", new[]
        {
            Opt("Временные файлы", "Temp-папки пользователя и Windows.", () => c.Temp, v => c.Temp = v),
            Opt("Кэши браузеров", "Chromium и поддерживаемые браузеры.", () => c.Browsers, v => c.Browsers = v),
            Opt("Кэш шейдеров", "Direct3D, NVIDIA, AMD и Intel.", () => c.D3dCache, v => c.D3dCache = v),
            Opt("Эскизы проводника", "Кэш миниатюр и значков.", () => c.Thumbs, v => c.Thumbs = v),
            Opt("Отчёты об ошибках", "Очереди Windows Error Reporting.", () => c.Wer, v => c.Wer = v),
            Opt("Дампы памяти", "MEMORY.DMP и минидампы.", () => c.Dumps, v => c.Dumps = v),
            Opt("Кэш Windows Update", "Скачанные файлы обновлений.", () => c.UpdateCache, v => c.UpdateCache = v),
            Opt("Кэши UWP", "Временные данные Store-приложений.", () => c.UwpCache, v => c.UwpCache = v),
            Opt("Prefetch", "Файлы запуска старше семи дней.", () => c.Prefetch, v => c.Prefetch = v),
            Opt("Старые системные логи", "Журналы обслуживания старше недели.", () => c.WinLogs, v => c.WinLogs = v),
            Opt("Корзина", "Очищает корзину Windows.", () => c.Recycle, v => c.Recycle = v)
        });
        WrapPanel actions = new();
        actions.Children.Add(Action("Сохранить настройки", () => { c.Save(); App.Log("настройки очистки сохранены"); SetStatus("настройки сохранены"); }, true));
        actions.Children.Add(Action("Запустить очистку", async () => await RunCleanerAsync(c.Clone())));
        _cleanLeftColumn = null;
        _cleanRightColumn = null;
        _pageContent!.Children.Add(cards);
        _pageContent.Children.Add(actions);
    }

    private static (string Label, string Hint, Func<bool> Get, Action<bool> Set) Opt(string label, string hint, Func<bool> get, Action<bool> set) => (label, hint, get, set);

    private void AddCleanGroup(string title, (string Label, string Hint, Func<bool> Get, Action<bool> Set)[] items)
    {
        StackPanel rows = new();
        foreach (var item in items)
        {
            CheckBox check = new() { IsChecked = item.Get(), VerticalAlignment = VerticalAlignment.Center };
            check.Checked += (_, _) => item.Set(true); check.Unchecked += (_, _) => item.Set(false);
            rows.Children.Add(Row(item.Label, item.Hint, check));
        }
        Border card = Card(title, rows);
        card.Width = _cleanCardWidth;
        card.Margin = new Thickness(0, 0, 0, 12);
        StackPanel target = title == "ДИСК И КЭШИ" ? _cleanRightColumn : _cleanLeftColumn;
        if (target != null) target.Children.Add(card); else _pageContent!.Children.Add(card);
    }

    private async Task RunCleanerAsync(Config c)
    {
        if (!Confirm("Запустить очистку", "CacheTrim обработает выбранные категории. Занятые файлы будут пропущены.")) return;
        await RunBusyAsync("выполняется очистка…", async () =>
        {
            await Task.Run(() => { Cleaner cleaner = new(App.Log); cleaner.Run(c); App.Log("очистка завершена · удалено " + Cleaner.FormatBytes(cleaner.BytesFreed) + " · пропущено " + cleaner.FilesSkipped); });
            RefreshMemory();
        });
    }

    private void ShowOptimize()
    {
        BeginPage("Оптимизация", "Обратимые настройки Windows и игр.");
        WrapPanel tabs = new();
        tabs.Children.Add(Action("Windows", () => { _optimizationModule = "Windows"; ShowOptimize(); }, _optimizationModule == "Windows"));
        tabs.Children.Add(Action("Игры", () => { _optimizationModule = "Игры"; ShowOptimize(); }, _optimizationModule == "Игры"));
        _pageContent!.Children.Add(tabs);
        WrapPanel cards = new() { ItemWidth = 720, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
        List<Tweak> items = Tweaks.All().Where(t => t.Module == _optimizationModule).ToList();
        foreach (string group in items.Select(t => t.Group).Distinct())
        {
            StackPanel rows = new();
            foreach (Tweak tweak in items.Where(t => t.Group == group))
            {
                CheckBox check = new() { IsChecked = tweak.IsOn(), ToolTip = tweak.Hint, VerticalAlignment = VerticalAlignment.Center };
                check.Checked += async (_, _) => await ApplyTweakAsync(tweak, check, true);
                check.Unchecked += async (_, _) => await ApplyTweakAsync(tweak, check, false);
                string hint = tweak.Hint + (tweak.NeedsSignOut ? " · нужен выход из системы" : string.Empty);
                rows.Children.Add(Row(tweak.Name, hint, check));
            }
            Border card = Card(group.ToUpperInvariant(), rows);
            card.Width = 700;
            cards.Children.Add(card);
        }
        if (_optimizationModule == "Windows")
        {
            WrapPanel tasks = new();
            tasks.Children.Add(Action("Обслуживание дисков", async () => await RunDiskMaintenanceAsync()));
            tasks.Children.Add(Action("Проверить системные файлы", async () => await RunSfcAsync()));
            Border maintenance = Card("ОБСЛУЖИВАНИЕ", tasks);
            maintenance.Width = 700;
            cards.Children.Add(maintenance);
        }
        else
        {
            WrapPanel tools = new();
            tools.Children.Add(Action("Графические параметры", () => OpenWindowsSettings("ms-settings:display-advancedgraphics")));
            tools.Children.Add(Action("Игровой режим", () => OpenWindowsSettings("ms-settings:gaming-gamemode")));
            tools.Children.Add(Action("Параметры захвата", () => OpenWindowsSettings("ms-settings:gaming-captures")));
            Border gameTools = Card("ПОЛЕЗНЫЕ НАСТРОЙКИ ИГР", tools);
            gameTools.Width = 700;
            cards.Children.Add(gameTools);
            Border power = Card("ПИТАНИЕ ВО ВРЕМЯ ИГРЫ", new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Высокая производительность держит более отзывчивый план питания, но на ноутбуке быстрее расходует заряд.", TextWrapping = TextWrapping.Wrap, Foreground = Brush(Theme.Muted), Margin = new Thickness(0, 0, 0, 12) },
                    new WrapPanel
                    {
                        Children =
                        {
                            Action("Высокая производительность", async () => await ApplyPowerPlanAsync(true), true),
                            Action("Сбалансированный", async () => await ApplyPowerPlanAsync(false))
                        }
                    }
                }
            });
            power.Width = 700;
            cards.Children.Add(power);
        }
        _pageContent.Children.Add(cards);
    }

    private async Task ApplyTweakAsync(Tweak tweak, CheckBox check, bool desired)
    {
        if (_suppressTweakEvents) return;
        if (tweak.PotentiallyRisky && desired && !ConfirmRisk(tweak.Name, tweak.Hint))
        {
            _suppressTweakEvents = true;
            check.IsChecked = false;
            _suppressTweakEvents = false;
            return;
        }
        check.IsEnabled = false;
        string? error = await Task.Run(() => tweak.Set(desired));
        check.IsEnabled = true;
        if (error != null) { check.IsChecked = !desired; MessageBox.Show(this, error, "Не удалось применить", MessageBoxButton.OK, MessageBoxImage.Warning); App.Log("ошибка твика " + tweak.Name + ": " + error); }
        else App.Log((desired ? "включён твик: " : "отключён твик: ") + tweak.Name + (tweak.NeedsSignOut ? " · нужен выход из системы" : string.Empty));
    }

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
            AdvancedAction("Удалить старые компоненты", "DISM удалит заменённые версии системных компонентов, чтобы освободить место. После этого нельзя будет удалить соответствующие старые обновления Windows. Операция может занять много времени.", "Очищаются старые компоненты Windows…", "Dism.exe", "/Online /Cleanup-Image /StartComponentCleanup")));

        modules.Children.Add(AdvancedModule("ПРОИЗВОДИТЕЛЬНОСТЬ WINDOWS", "Высокая производительность уменьшает энергосбережение процессора. На ноутбуке быстрее расходуется заряд. Вернуть обычный режим можно этой же карточкой.",
            AdvancedButton("Включить высокую производительность", async () => await ApplyPowerPlanAsync(true), true),
            AdvancedButton("Вернуть сбалансированный режим", async () => await ApplyPowerPlanAsync(false))));

        WrapPanel checks = new();
        checks.Children.Add(AdvancedAction("Проверить файлы Windows", "Проверит защищённые файлы и попробует восстановить повреждённые. Может занять несколько минут.", "Проверяются файлы Windows…", "sfc.exe", "/scannow"));
        checks.Children.Add(AdvancedAction("Проверить диск C:", "Проверит файловую систему диска C: без отключения тома. Исправление ошибок не запускается.", "Проверяется диск C:…", "chkdsk.exe", "C: /scan"));
        checks.Children.Add(Action("Монитор ресурсов", () => OpenSystemTool("resmon.exe")));
        checks.Children.Add(Action("Центр обновления Windows", () => OpenSystemTool("ms-settings:windowsupdate")));
        modules.Children.Add(AdvancedModule("ПРОВЕРКА И ИНСТРУМЕНТЫ", "Диагностика без изменения системных параметров.", checks));

        _pageContent!.Children.Add(modules);
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
            : "CacheTrim вернёт сбалансированный план питания, включит обратно параметры игровых записей и снимет изменения игрового профиля CacheTrim.";
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
            int code = await Task.Run(() => Tweaks.Run(ps, "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'CacheTrim before changes' -RestorePointType 'MODIFY_SETTINGS'\"", out output));
            App.Log("точка восстановления · код " + code + " · " + output);
            MessageBox.Show(this, code == 0 ? "Точка восстановления создана." : "Windows не создала точку восстановления. Проверьте, включена ли защита системы.\n\n" + output, "Точка восстановления", MessageBoxButton.OK, code == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
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

    private Wpf.Ui.Controls.Button AdvancedAction(string label, string warning, string busy, string executable, string arguments)
    {
        Wpf.Ui.Controls.Button button = Action(label, async () => await RunAdvancedCommandAsync(label, warning, busy, executable, arguments));
        button.ToolTip = warning;
        return button;
    }

    private async Task RunAdvancedCommandAsync(string title, string warning, string busy, string executable, string arguments)
    {
        if (!ConfirmRisk(title, warning)) return;
        await RunBusyAsync(busy, async () =>
        {
            string output = string.Empty;
            string path = System.IO.Path.Combine(Environment.SystemDirectory, executable);
            int code = await Task.Run(() => Tweaks.Run(path, arguments, out output));
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
            int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\powercfg.exe", "/energy /duration 30 /output \"" + reportPath + "\"", out output));
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
            int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\Dism.exe", "/Online /Cleanup-Image /RestoreHealth", out output));
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
                int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\defrag.exe", "\"" + drive.Name.TrimEnd('\\') + "\" /O", out output));
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
            int code = await Task.Run(() => Tweaks.Run(Environment.SystemDirectory + "\\sfc.exe", "/scannow", out output));
            App.Log("SFC завершён · код " + code + " · " + output);
            MessageBox.Show(this, "Код завершения: " + code + "\nПодробности записаны в журнал.", "Проверка системных файлов", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private async Task ApplyPowerPlanAsync(bool highPerformance)
    {
        string target = highPerformance ? "Высокая производительность" : "Сбалансированный";
        if (!ConfirmRisk("План питания", "Переключить Windows на план «" + target + "»? Высокая производительность может повысить нагрев и расход батареи. Вернуться к сбалансированному плану можно в CacheTrim.")) return;
        Tweak tweak = Tweaks.All().First(item => item.Name == "План «Высокая производительность»");
        string? error = await Task.Run(() => tweak.Set(highPerformance));
        if (error != null) MessageBox.Show(this, error, "Не удалось изменить план питания", MessageBoxButton.OK, MessageBoxImage.Warning);
        else { SetStatus("план питания: " + target); App.Log("план питания переключён: " + target); }
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
            check.Checked += async (_, _) => await SetAutostartAsync(entry, check, true);
            check.Unchecked += async (_, _) => await SetAutostartAsync(entry, check, false);
            rows.Children.Add(Row(entry.Name, Autostart.SourceLabel(entry.Origin) + " · " + Ellipsize(entry.Command, 110) + (entry.RequiresAdmin ? " · админ" : string.Empty), check));
        }
        _pageContent!.Children.Add(Card("ЗАПИСИ АВТОЗАПУСКА · " + entries.Count, rows));
        WrapPanel taskActions = new();
        taskActions.Children.Add(Action("Включить очистку при входе", async () => await SetScheduledCleanAsync(true), true));
        taskActions.Children.Add(Action("Отключить задачу", async () => await SetScheduledCleanAsync(false)));
        _pageContent.Children.Add(Card("ЗАДАЧА CACHETrim", taskActions));
    }

    private async Task SetAutostartAsync(Autostart.Entry entry, CheckBox check, bool enabled)
    {
        check.IsEnabled = false;
        string? error = await Task.Run(() => Autostart.SetEnabled(entry, enabled));
        check.IsEnabled = true;
        if (error != null) { check.IsChecked = !enabled; MessageBox.Show(this, error, "Не удалось изменить автозагрузку", MessageBoxButton.OK, MessageBoxImage.Warning); }
        else { entry.Enabled = enabled; App.Log((enabled ? "восстановлена автозагрузка: " : "отключена автозагрузка: ") + entry.Name); }
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
            _settings.Save();
            SetStatus("плотность интерфейса сохранена");
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
                Text = "Системное размытие Desktop Acrylic. Его силу задаёт Windows; плотность оттенка и цвет настраиваются ниже.",
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
        appearanceActions.Children.Add(Action("Сохранить оформление", () => { _settings.Save(); SetStatus("оформление сохранено"); App.Log("оформление сохранено"); }, true));
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
            _settings.Save(); App.Log("параметры очистки сохранены"); SetStatus("параметры сохранены");
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
        SetStatus("проверяю GitHub Releases…");
        try
        {
            UpdateInfo info = await Task.Run(UpdateService.CheckLatest);
            if (!info.Available) { MessageBox.Show(this, "Установлена последняя версия CacheTrim " + AppInfo.Version + ".", "Обновлений нет", MessageBoxButton.OK, MessageBoxImage.Information); SetStatus("установлена последняя версия"); return; }
            if (Confirm("Доступно обновление " + info.Version, "Скачать проверенную SHA-256 сборку и перезапустить CacheTrim?"))
            {
                string exe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                await Task.Run(() => UpdateService.DownloadAndLaunch(info, exe, Environment.ProcessId));
                System.Windows.Application.Current.Shutdown();
            }
        }
        catch (Exception ex) { SetStatus("ошибка проверки обновления"); MessageBox.Show(this, ex.Message, "Не удалось проверить обновления", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task SetScheduledCleanAsync(bool enabled)
    {
        string exe = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
        string? error = await Task.Run(() => enabled ? TaskSetup.Install(exe, _settings.DelaySeconds) : TaskSetup.Uninstall());
        if (error != null) MessageBox.Show(this, error, "Не удалось изменить задачу", MessageBoxButton.OK, MessageBoxImage.Warning);
        else { SetStatus(enabled ? "автоочистка при входе включена" : "автоочистка при входе отключена"); App.Log(enabled ? "задача автоочистки включена" : "задача автоочистки снята"); }
    }

    private async Task ShowDiagnosticsAsync()
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
        Dispatcher.BeginInvoke(() =>
        {
            if (_activePage != "log" || _logBox == null) return;
            _logBox.AppendText(line + Environment.NewLine);
            _logBox.ScrollToEnd();
        });
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _memoryTimer.Stop();
        App.LogAdded -= OnLogAdded;
    }
}
