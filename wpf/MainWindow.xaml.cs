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
    private bool _isBusy;
    private bool _busy
    {
        get => _isBusy;
        set
        {
            _isBusy = value;
            BusyBanner.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }
    private bool _suppressColor;
    private bool _suppressTweakEvents;
    private bool _suppressAutostartEvents;
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
    private Rect _windowRestoreBounds;
    private bool _coversTaskbar;
    private int _lastBackdropResult = int.MinValue;
    private bool _pageResizeQueued;
    private bool _preservePageScroll;
    private double _lastPageViewportWidth;

    public MainWindow(int startPage = 0)
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding(new SearchCommand(this), new KeyGesture(Key.F, ModifierKeys.Control)));
        BrandVersion.Text = "SYSTEM TOOLS · " + AppInfo.Version;
        PageScroll.SizeChanged += OnPageViewportSizeChanged;
        _settings = App.Settings;
        Theme.Apply(_settings.ThemeName, _settings.AccentColorHex);
        ApplyTheme();
        BuildNavigation();
        App.LogAdded += OnLogAdded;
        _memoryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _memoryTimer.Tick += delegate { RefreshMemory(); };
        _memoryTimer.Start();
        string[] pages = { "memory", "clean", "optimize", "startup", "log", "settings", "advanced", "pet" };
        Navigate(pages[Math.Max(0, Math.Min(pages.Length - 1, startPage))]);
        RefreshMemory();
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        WindowSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(92, 20, 25, 33)) : Brush(Theme.Bg);
        TitleSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(50, 28, 34, 44)) : Brush(Theme.Surface);
        NavigationSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(72, 23, 29, 39)) : Brush(Theme.Surface);
        BusyBanner.Background = Brush(Theme.Surface);
        BusyBanner.BorderBrush = Brush(Theme.Accent);
        StatusSurface.Background = Theme.IsGlass ? new SolidColorBrush(Color.FromArgb(58, 27, 33, 43)) : Brush(Theme.Surface);
        ApplyTheme();
        RefreshMemory();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e) => MaximizeButton.Content = WindowState == WindowState.Maximized || _coversTaskbar ? "❐" : "□";
    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            _coversTaskbar = false;
            MaximizeButton.Content = "□";
            return;
        }
        if (_coversTaskbar)
        {
            _coversTaskbar = false;
            Left = _windowRestoreBounds.Left;
            Top = _windowRestoreBounds.Top;
            Width = _windowRestoreBounds.Width;
            Height = _windowRestoreBounds.Height;
            MaximizeButton.Content = "□";
            return;
        }

        _windowRestoreBounds = new Rect(Left, Top, Width, Height);
        System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
        Matrix fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        WpfPoint origin = fromDevice.Transform(new WpfPoint(screen.Bounds.Left, screen.Bounds.Top));
        Vector size = fromDevice.Transform(new Vector(screen.Bounds.Width, screen.Bounds.Height));
        Left = origin.X;
        Top = origin.Y;
        Width = size.X;
        Height = size.Y;
        _coversTaskbar = true;
        MaximizeButton.Content = "❐";
    }
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
        AddNav("Питомец", "◆", "pet");
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
            case "pet": ShowPet(); break;
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
        if (!_preservePageScroll)
            Dispatcher.BeginInvoke(new Action(() => PageScroll.ScrollToTop()), DispatcherPriority.Loaded);
    }

    private void OnPageViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ResizeDashboardChart();
        double width = PageScroll.ViewportWidth;
        if (width < 1 || _pageResizeQueued || _busy || Keyboard.FocusedElement is TextBox)
            return;
        if (_lastPageViewportWidth > 0 && Math.Abs(width - _lastPageViewportWidth) < 24)
            return;

        if (_activePage != "clean" && _activePage != "optimize" && _activePage != "advanced" && _activePage != "settings")
        {
            _lastPageViewportWidth = width;
            return;
        }

        _lastPageViewportWidth = width;
        _pageResizeQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _pageResizeQueued = false;
            if (_busy || Keyboard.FocusedElement is TextBox)
                return;

            double offset = PageScroll.VerticalOffset;
            _preservePageScroll = true;
            switch (_activePage)
            {
                case "clean": ShowClean(); break;
                case "optimize": ShowOptimize(); break;
                case "advanced": ShowAdvanced(); break;
                case "settings": ShowSettings(); break;
            }
            _preservePageScroll = false;
            Dispatcher.BeginInvoke(new Action(() => PageScroll.ScrollToVerticalOffset(offset)), DispatcherPriority.Loaded);
        }), DispatcherPriority.Loaded);
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

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        if (_busy) BusyOperationText.Text = text;
    }

    private bool SaveConfig(Config config, string successStatus, string logMessage)
    {
        string error = config.Save();
        if (error != null)
        {
            SetStatus("не удалось сохранить настройки");
            MessageBox.Show(this, error, "Не удалось сохранить настройки CacheTrim", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        App.Log(logMessage);
        SetStatus(successStatus);
        return true;
    }

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
        BusyBanner.Background = Brush(Theme.Surface);
        BusyBanner.BorderBrush = Brush(Theme.Accent);
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

    // DWM draws behind the WPF client area only when both the HWND target and
    // the glass frame are transparent. Keep a solid fallback on older systems.
    private void UpdateGlassEffect()
    {
        if (!IsLoaded) return;
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        HwndSource? source = HwndSource.FromHwnd(hwnd);
        bool transparency = IsWindowsTransparencyEnabled();
        bool supported = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
        bool requested = Theme.IsGlass && _settings.GlassBackgroundBlur > 0;
        bool tryAcrylic = requested && transparency && supported && source?.CompositionTarget != null;
        bool acrylicApplied = false;
        int result = 0;

        if (tryAcrylic)
        {
            source!.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;
            GlassMargins margins = new() { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            int frameResult = DwmExtendFrameIntoClientArea(hwnd, ref margins);
            int dark = 1;
            _ = DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
            int backdrop = 3; // DWMSBT_TRANSIENTWINDOW (Desktop Acrylic)
            result = frameResult == 0 ? DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int)) : frameResult;
            acrylicApplied = result == 0;
        }

        if (!acrylicApplied)
        {
            // Restore both DWM and WPF state when switching themes or on failure.
            if (supported)
            {
                int none = 1; // DWMSBT_NONE
                _ = DwmSetWindowAttribute(hwnd, 38, ref none, sizeof(int));
            }
            GlassMargins noGlass = new();
            _ = DwmExtendFrameIntoClientArea(hwnd, ref noGlass);
            if (source?.CompositionTarget != null)
                source.CompositionTarget.BackgroundColor = Theme.Bg;
        }

        Background = acrylicApplied ? System.Windows.Media.Brushes.Transparent : Brush(Theme.Bg);
        if (acrylicApplied)
        {
            // Accent influences the tint without compromising contrast of text.
            Color accent = Theme.Accent;
            byte alpha = (byte)(28 + Math.Clamp(_settings.GlassDensity, 0, 100) * 0.72);
            WindowSurface.Background = new SolidColorBrush(Color.FromArgb(alpha,
                (byte)(20 * 0.8 + accent.R * 0.2),
                (byte)(25 * 0.8 + accent.G * 0.2),
                (byte)(33 * 0.8 + accent.B * 0.2)));
        }
        else WindowSurface.Background = Brush(Theme.Bg);

        if (tryAcrylic && result != 0 && result != _lastBackdropResult)
            App.Log("Не удалось включить Acrylic (DWM, код " + result + "); используется сплошной фон");
        _lastBackdropResult = result;
        if (Theme.IsGlass)
        {
            if (!requested) SetStatus("Размытие фона выключено");
            else if (!transparency) SetStatus("Включите эффекты прозрачности в Windows · сплошной фон");
            else if (!supported) SetStatus("Acrylic требует Windows 11 22H2 или новее · сплошной фон");
            else if (!acrylicApplied) SetStatus("Не удалось включить Acrylic · сплошной фон");
            else SetStatus("Acrylic Windows включён");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GlassMargins
    {
        public int Left, Right, Top, Bottom;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref GlassMargins margins);

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
        Wpf.Ui.Controls.CardControl card = new() { Content = content, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(16), CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Theme.IsGlass ? Color.FromArgb(200, Theme.Surface.R, Theme.Surface.G, Theme.Surface.B) : Theme.Surface), BorderBrush = Brush(Theme.Line), BorderThickness = new Thickness(1) };
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
        try { new GameGuard().Check(true); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Обслуживание приостановлено", MessageBoxButton.OK, MessageBoxImage.Information); return; }
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
        WrapPanel cards = new() { HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Top };
        _cleanCardWidth = twoColumns ? (available - 16) / 2 : available;
        _cleanLeftColumn = new StackPanel { Width = _cleanCardWidth, Margin = new Thickness(0, 0, twoColumns ? 12 : 0, 0), VerticalAlignment = System.Windows.VerticalAlignment.Top };
        _cleanRightColumn = twoColumns ? new StackPanel { Width = _cleanCardWidth, VerticalAlignment = System.Windows.VerticalAlignment.Top } : _cleanLeftColumn;
        cards.Children.Add(_cleanLeftColumn);
        if (twoColumns) cards.Children.Add(_cleanRightColumn);
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
        actions.Children.Add(Action("Сохранить настройки", () => SaveConfig(c, "настройки сохранены", "настройки очистки сохранены"), true));
        actions.Children.Add(Action("Предпросмотр очистки", async () => await PreviewCleanupAsync(c.Clone())));
        actions.Children.Add(Action("Исключения и правила", ShowCleanupOptions));
        actions.Children.Add(Action("Наборы очистки", ShowCleanupPresets));
        actions.Children.Add(Action("Поиск файлов", ShowFileReview));
        actions.Children.Add(Action("Защита во время игр", ShowGameGuard));
        actions.Children.Add(Action("Расписание очистки", ShowIdleSchedule));
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
                            Action("Восстановить прежний план", async () => await ApplyPowerPlanAsync(false))
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
            SetCheckSilently(check, !desired, true);
            return;
        }
        if (_busy)
        {
            SetCheckSilently(check, !desired, true);
            SetStatus("дождитесь завершения текущей операции");
            return;
        }

        _busy = true;
        check.IsEnabled = false;
        SetStatus(desired ? "применяются настройки…" : "возвращаются настройки…");
        try
        {
            string? error = await Task.Run(() => tweak.Set(desired));
            if (error != null)
            {
                SetCheckSilently(check, !desired, true);
                MessageBox.Show(this, error, "Не удалось применить", MessageBoxButton.OK, MessageBoxImage.Warning);
                App.Log("ошибка твика " + tweak.Name + ": " + error);
                SetStatus("настройка не изменена");
            }
            else
            {
                App.Log((desired ? "включён твик: " : "отключён твик: ") + tweak.Name + (tweak.NeedsSignOut ? " · нужен выход из системы" : string.Empty));
                SetStatus("настройка применена");
            }
        }
        catch (Exception ex)
        {
            SetCheckSilently(check, !desired, true);
            App.Log("ошибка твика " + tweak.Name + ": " + ex.Message);
            MessageBox.Show(this, ex.Message, "Не удалось применить", MessageBoxButton.OK, MessageBoxImage.Warning);
            SetStatus("настройка не изменена");
        }
        finally
        {
            check.IsEnabled = true;
            _busy = false;
        }
    }

    private void SetCheckSilently(CheckBox check, bool value, bool tweak)
    {
        if (tweak) _suppressTweakEvents = true;
        else _suppressAutostartEvents = true;
        try { check.IsChecked = value; }
        finally
        {
            if (tweak) _suppressTweakEvents = false;
            else _suppressAutostartEvents = false;
        }
    }

}
