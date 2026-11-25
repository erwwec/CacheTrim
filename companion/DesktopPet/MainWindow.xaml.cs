using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using System.IO;
using System.Text;
using NAudio.Wave;
using NAudio.Dmo;

namespace DesktopPet;

public partial class MainWindow : Window
{
    private enum Activity { Roaming, Working, Phone, Sleeping, Tea, Reading, Stretching, DeskNap }
    private enum WorkPhase { Typing, Checking, Thinking }
    private const double ModelLeft = 74;
    private const double ModelTop = 180;
    private const double ModelRight = 194;
    private const double ModelBottom = 292;
    private static readonly ImageSource IdleSprite = LoadSprite("claude_idle.png");
    private static readonly ImageSource WorkSprite = LoadSprite("claude_work.png");
    private static readonly ImageSource PhoneSprite = LoadSprite("claude_phone.png");
    private static readonly ImageSource SleepSprite = LoadSprite("claude_sleep.png");
    private static readonly ImageSource ExperimentalSprite = LoadExperimentalSprite();

    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private double _petOpacity = 1;
    private double _savedLeft = double.NaN, _savedTop = double.NaN;
    private int _reminderMinutes;
    private string _reminderText = "Пора сделать перерыв!";
    private DateTime _nextReminder = DateTime.MaxValue;
    private DateTime _reminderHoldUntil = DateTime.MinValue;
    private readonly DispatcherTimer _motionTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private readonly DispatcherTimer _lyricsTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private LyricsService? _lyricsService;
    private CancellationTokenSource _lyricsCancellation = new();
    private bool _lyricsEnabled;
    private bool _lyricsBusy;
    private string _lyricsLastLine = "";
    private string _lyricsStatus = "Проверка ещё не выполнялась.";
    private DateTime _lyricsHoldUntil = DateTime.MinValue;
    private bool _speechIsLyrics;
    private DateTime _nextLyricsAttemptAt = DateTime.MinValue;
    private readonly DispatcherTimer _audioTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _platformScanTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly Random _random = new();
    private readonly List<Rect> _platforms = new();
    private readonly List<int> _collectionItems = new();
    private readonly List<string> _customPhrases = new();
    private readonly string _settingsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeDesktopPet", "settings.ini");
    private WasapiLoopbackCapture? _loopbackCapture;
    private bool _audioAvailable;
    private bool _isClosing;
    private bool _initializing = true;
    private bool _experimentalModel;
    private double _pendingAudioLevel;
    private double _audioLevel;
    private double _velocityX;
    private double _velocityY;
    private double _phase;
    private double _uiScale = 0.6;
    private Point _dragOffset;
    private Point _lastPointer;
    private DateTime _lastPointerAt;
    private bool _audioEnabled = true;
    private bool _quietMode;
    private bool _surprisesEnabled = true;
    private bool _favoriteSpotEnabled;
    private bool _collectionEnabled = true;
    private bool _routineEnabled = true;
    private bool _activityTransition;
    private double _chatterLevel = 1;
    private double _jumpRate = 1;
    private DateTime _nextCollectionAt = DateTime.UtcNow.AddMinutes(7);
    private DateTime _nextMicroActionAt = DateTime.UtcNow.AddSeconds(22);
    private DateTime _nextSurpriseAt = DateTime.UtcNow.AddMinutes(5);
    private int _microAction;
    private bool _dragging;
    private bool _onSurface = true;
    private DateTime _nextIdlePollAt;
    private DateTime _nextKeyboardScanAt;
    private DateTime _lastKeyboardActivityAt = DateTime.MinValue;
    private DateTime _nextWorkPhaseAt;
    private DateTime _speechEndsAt;
    private bool _forcedRoamingAfterThrow;
    private bool _thrownResting;
    private DateTime _throwHoldUntil;
    private bool _miniSceneActive;
    private WorkPhase _workPhase;
    private bool _musicDetected;
    private int _audioAboveCount;
    private int _audioBelowCount;
    private Activity _activity = Activity.Roaming;

    public MainWindow()
    {
        LoadSettings();
        // Explicit launch choices override the saved appearance; ordinary autostart/hotkey keeps it.
        bool modelOverridden = false;
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (arg.Equals("--model=classic", StringComparison.OrdinalIgnoreCase)) { _experimentalModel = false; modelOverridden = true; }
            if (arg.Equals("--model=experimental", StringComparison.OrdinalIgnoreCase)) { _experimentalModel = true; modelOverridden = true; }
        }
        InitializeComponent();
        ModelSelector.SelectedIndex = _experimentalModel ? 1 : 0;
        PetCanvas.LayoutTransform = new ScaleTransform(_uiScale, _uiScale);
        Width = 260 * _uiScale;
        Height = 320 * _uiScale;
        SizeLabel.Text = $"Размер: {_uiScale:P0}";
        ChatterSlider.Value = _chatterLevel;
        JumpSlider.Value = _jumpRate;
        SizeSlider.Value = _uiScale;
        OpacitySlider.Value = _petOpacity;
        PetCanvas.Opacity = _petOpacity;
        OpacityLabel.Text = $"Непрозрачность: {_petOpacity:P0}";
        ReminderInterval.SelectedIndex = _reminderMinutes switch { 30 => 1, 60 => 2, 120 => 3, _ => 0 };
        ReminderText.Text = _reminderText;
        RefreshAutostartLabel();
        _nextReminder = _reminderMinutes > 0 ? DateTime.UtcNow.AddMinutes(_reminderMinutes) : DateTime.MaxValue;
        _reminderTimer.Tick += Reminder_Tick;
        _reminderTimer.Start();
        ChatterLabel.Text = ChatterLabelFor(_chatterLevel);
        JumpLabel.Text = JumpLabelFor(_jumpRate);
        QuietButton.Content = _quietMode ? "Не отвлекать: вкл" : "Не отвлекать: выкл";
        SurpriseButton.Content = _surprisesEnabled ? "Секреты: вкл" : "Секреты: выкл";
        SeatButton.Content = _favoriteSpotEnabled ? "Место: угол экрана" : "Место: свободно";
        CollectionButton.Content = _collectionEnabled ? "Коллекция: вкл" : "Коллекция: выкл";
        RoutineButton.Content = _routineEnabled ? "Распорядок: вкл" : "Распорядок: выкл";
        AudioButton.Content = _audioEnabled ? "Реакция на звук: вкл" : "Реакция на звук: выкл";
        LyricsButton.Content = _lyricsEnabled ? "Текст песни: вкл" : "Текст песни: выкл";
        PhraseTextBox.Text = string.Join(Environment.NewLine, _customPhrases);
        Left = SystemParameters.WorkArea.Right - ModelRight * _uiScale - 28;
        Top = SystemParameters.WorkArea.Bottom - ModelBottom * _uiScale;
        if (double.IsFinite(_savedLeft) && double.IsFinite(_savedTop)) {
            var area = SystemParameters.WorkArea;
            Left = Math.Clamp(_savedLeft, area.Left - ModelLeft * _uiScale, Math.Max(area.Left - ModelLeft * _uiScale, area.Right - ModelRight * _uiScale));
            Top = Math.Clamp(_savedTop, area.Top - ModelTop * _uiScale, Math.Max(area.Top - ModelTop * _uiScale, area.Bottom - ModelBottom * _uiScale));
        }
        _initializing = false;
        if (modelOverridden) SaveSettings();
        _nextIdlePollAt = DateTime.UtcNow;
        ScheduleNextLeap(DateTime.UtcNow);
        _motionTimer.Tick += Motion_Tick;
        _audioTimer.Tick += Audio_Tick;
        _lyricsTimer.Tick += Lyrics_Tick;
        if (_lyricsEnabled) _lyricsTimer.Start();
        _platformScanTimer.Tick += (_, _) => RefreshPlatforms();
        _motionTimer.Start();
        _audioTimer.Start();
        RefreshPlatforms();
        _platformScanTimer.Start();
        if (_audioEnabled && !StartAudioCapture())
        {
            AudioButton.Content = "Звук недоступен — выключить";
        }
        SetActivity(Activity.Roaming, false);
    }

    private void Motion_Tick(object? sender, EventArgs e)
    {
        if (_dragging || SettingsPopup.IsOpen) return;
        var dt = _motionTimer.Interval.TotalSeconds;
        var area = SystemParameters.WorkArea;
        var footOffset = ModelBottom * _uiScale;
        var visualLeft = ModelLeft * _uiScale;
        var visualRight = ModelRight * _uiScale;
        var visualTop = ModelTop * _uiScale;
        var now = DateTime.UtcNow;

        if (now >= _nextKeyboardScanAt)
        {
            _nextKeyboardScanAt = now.AddMilliseconds(50);
            for (var key = 0x08; key <= 0xFE; key++)
            {
                if ((GetAsyncKeyState(key) & 0x8001) != 0)
                {
                    _lastKeyboardActivityAt = now;
                    break;
                }
            }
        }

        if (now >= _nextIdlePollAt)
        {
            _nextIdlePollAt = now.AddSeconds(1);
            var idle = GetUserIdleDuration();
            var nextActivity = idle >= TimeSpan.FromMinutes(2)
                ? Activity.Sleeping
                : now - _lastKeyboardActivityAt < TimeSpan.FromSeconds(5) ? Activity.Working : Activity.Roaming;
            if (_forcedRoamingAfterThrow)
            {
                if (now >= _throwHoldUntil)
                    _forcedRoamingAfterThrow = false;
                else nextActivity = Activity.Roaming;
            }
            if (_miniSceneActive)
            {
                if (now >= _activityTransitionEndsAt) _miniSceneActive = false;
                else nextActivity = _activity;
            }
            if (!_miniSceneActive && !_forcedRoamingAfterThrow && _activityTransition) { if (now >= _activityTransitionEndsAt) _activityTransition = false; }
            else if (!_miniSceneActive && _activity != nextActivity) SetActivity(nextActivity, true);
        }

        if (_activity == Activity.Working && now >= _nextWorkPhaseAt)
        {
            _workPhase = (WorkPhase)(((int)_workPhase + 1) % 3);
            // Do not reset the phase clock when the user briefly moves the mouse.
            _nextWorkPhaseAt = now.AddSeconds(_random.Next(6, 10));
        }

        // Spontaneous hops with gravity, momentum, and screen-edge rebounds.
        if (_activity == Activity.Roaming && !_miniSceneActive && !_forcedRoamingAfterThrow && now >= _nextLeapAt && _onSurface)
        {
            if (_surprisesEnabled && now >= _nextSurpriseAt && _random.NextDouble() < 0.18)
            {
                _nextSurpriseAt = now.AddMinutes(_random.Next(6, 13));
                if (ShouldSpeak())
                    ShowSpeech(_random.Next(3) switch { 0 => "Нашёл тут кое-что!", 1 => "Псс… у меня есть секрет.", _ => "Проверка рабочего места завершена." });
            }
            _velocityY = -(_random.NextDouble() * 250 + 850) * Math.Sqrt(_jumpRate);
            _velocityX = (_random.NextDouble() * 2 - 1) * 280 * Math.Sqrt(_jumpRate);
            _onSurface = false;
            ScheduleNextLeap(now);
        }
        if (_activity == Activity.Roaming && !_miniSceneActive && !_forcedRoamingAfterThrow && _onSurface && now >= _nextMicroActionAt)
        {
            _microAction = _random.Next(5);
            _nextMicroActionAt = now.AddSeconds(_random.Next(16, 39));
            if (_microAction == 4) StartMiniScene(ChooseMiniScene());
        }

        var previousTop = Top;
        var previousBottom = previousTop + footOffset;
        _velocityY += 1450 * dt;
        var nextTop = Top + _velocityY * dt;
        var nextBottom = nextTop + footOffset;
        if (_favoriteSpotEnabled && !_forcedRoamingAfterThrow && !_thrownResting && _onSurface && Left < area.Right - ModelRight * _uiScale - 30)
            _velocityX = Math.Min(110, _velocityX + 160 * dt);
        Left += _velocityX * dt;

        // Land on the first visible application surface the pet crosses while falling.
        var landingSurface = area.Bottom;
        if (_velocityY > 0)
        {
            foreach (var platform in _platforms)
            {
                var overlaps = Left + visualRight > platform.Left && Left + visualLeft < platform.Right;
                if (overlaps && previousBottom <= platform.Top + 3 && nextBottom >= platform.Top && platform.Top < landingSurface)
                    landingSurface = platform.Top;
            }
        }
        if (nextBottom >= landingSurface && previousBottom <= landingSurface + 3)
        {
            nextTop = landingSurface - footOffset;
            if (_velocityY > 550) { _velocityY = -_velocityY * 0.24; _onSurface = false; }
            else { _velocityY = 0; _onSurface = true; }
            _velocityX *= 0.78;
            if (Math.Abs(_velocityX) < 12) _velocityX = 0;
        }
        if (_onSurface && _collectionEnabled && now >= _nextCollectionAt)
        {
            _nextCollectionAt = now.AddMinutes(_random.Next(8, 15));
            if (_random.NextDouble() < 0.7) AddCollectionItem();
        }
        Top = nextTop;
        if (Top + visualTop < area.Top) { Top = area.Top - visualTop; _velocityY = Math.Abs(_velocityY) * 0.55; }
        if (Left + visualLeft < area.Left) { Left = area.Left - visualLeft; _velocityX = Math.Abs(_velocityX) * 0.96; }
        if (Left + visualRight > area.Right) { Left = area.Right - visualRight; _velocityX = -Math.Abs(_velocityX) * 0.96; }

        _phase += 0.12 + _audioLevel * 0.18;
        var micro = _onSurface && _activity is not (Activity.Sleeping or Activity.Tea or Activity.Reading or Activity.Stretching or Activity.DeskNap)
            ? _microAction switch { 0 => Math.Max(0, Math.Sin(_phase * 0.32)) * 2.5, 1 => Math.Max(0, Math.Sin(_phase * 0.22)) * 4, 2 => (Math.Sin(_phase * 0.55) + 1) * 1.5, _ => 0 }
            : 0;
        var workBob = _activity == Activity.Working
            ? _workPhase switch { WorkPhase.Typing => Math.Sin(_phase * 0.9) * 1.8, WorkPhase.Checking => Math.Sin(_phase * 0.22) * 1.2, _ => Math.Sin(_phase * 0.35) * 1.6 }
            : 0;
        var bob = Math.Max(0, Math.Sin(_phase)) * (2 + _audioLevel * 7) + micro + workBob;
        var transforms = new TransformGroup();
        transforms.Children.Add(new TranslateTransform(0, -bob));
        var sceneLean = _activity == Activity.Stretching ? Math.Sin(_phase * 0.18) * 11 : _activity == Activity.Reading ? Math.Sin(_phase * 0.11) * 2 : 0;
        var workLean = _activity == Activity.Working && _workPhase == WorkPhase.Checking ? Math.Sin(_phase * 0.18) * 3 : 0;
        transforms.Children.Add(new RotateTransform(Math.Clamp(_velocityX / 24, -28, 28) + (_microAction == 3 && _onSurface ? Math.Sin(_phase * 0.14) * 3 : 0) + sceneLean + workLean));
        PetSprite.RenderTransformOrigin = new Point(0.5, 0.5);
        PetSprite.RenderTransform = transforms;
        SpeechBubble.RenderTransform = new TranslateTransform(0, -bob);
        ThoughtDotLarge.RenderTransform = new TranslateTransform(0, -bob);
        ThoughtDotSmall.RenderTransform = new TranslateTransform(0, -bob);
        PropGlyph.Visibility = _activity is Activity.Tea or Activity.Reading ? Visibility.Visible : Visibility.Collapsed;
        PropGlyph.Text = _activity == Activity.Tea ? "☕" : "📖";
        PropGlyph.RenderTransform = new TranslateTransform(0, Math.Sin(_phase * 0.16) * 2);
        if (now >= _speechEndsAt)
        {
            SpeechBubble.Visibility = Visibility.Collapsed;
            ThoughtDotLarge.Visibility = Visibility.Collapsed;
            ThoughtDotSmall.Visibility = Visibility.Collapsed;
            _speechIsLyrics = false;
        }
    }

    private DateTime _nextLeapAt = DateTime.UtcNow.AddSeconds(3);
    private DateTime _activityTransitionEndsAt;

    private void ScheduleNextLeap(DateTime from)
    {
        var seconds = Math.Clamp((_random.NextDouble() * 5 + 4) / _jumpRate, 2.0, 15.0);
        _nextLeapAt = from.AddSeconds(seconds);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo { public uint Size; public uint Time; }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    private static TimeSpan GetUserIdleDuration()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var elapsed = unchecked(GetTickCount() - info.Time);
        return TimeSpan.FromMilliseconds(elapsed);
    }

    private bool StartAudioCapture()
    {
        try
        {
            _loopbackCapture = new WasapiLoopbackCapture();
            _loopbackCapture.DataAvailable += Loopback_DataAvailable;
            _loopbackCapture.RecordingStopped += Loopback_RecordingStopped;
            _loopbackCapture.StartRecording();
            _audioAvailable = true;
            return true;
        }
        catch
        {
            if (_loopbackCapture is not null)
            {
                _loopbackCapture.DataAvailable -= Loopback_DataAvailable;
                _loopbackCapture.RecordingStopped -= Loopback_RecordingStopped;
                _loopbackCapture.Dispose();
                _loopbackCapture = null;
            }
            _audioAvailable = false;
            return false;
        }
    }

    private void Loopback_RecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_isClosing) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_isClosing || !ReferenceEquals(sender, _loopbackCapture)) return;
            var stoppedCapture = _loopbackCapture;
            _loopbackCapture = null;
            if (stoppedCapture is not null)
            {
                stoppedCapture.DataAvailable -= Loopback_DataAvailable;
                stoppedCapture.RecordingStopped -= Loopback_RecordingStopped;
                stoppedCapture.Dispose();
            }
            _audioAvailable = false;
            _audioLevel = 0;
            Interlocked.Exchange(ref _pendingAudioLevel, 0);
            _musicDetected = false;
            _audioAboveCount = 0;
            _audioBelowCount = 0;
            AudioButton.Content = "Звук недоступен — выключить";
        });
    }

    private static ImageSource LoadSprite(string fileName)
    {
        var uri = new Uri($"pack://application:,,,/DesktopPet;component/Assets/{fileName}", UriKind.Absolute);
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = uri;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static ImageSource LoadExperimentalSprite()
    {
        using var resource = typeof(MainWindow).Assembly.GetManifestResourceStream("DesktopPet.ExperimentalSprite")
            ?? throw new InvalidOperationException("В сборке отсутствует тестовая модель Клода.");
        using var reader = new StreamReader(resource);
        byte[] png = Convert.FromBase64String(reader.ReadToEnd());
        using var stream = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private ImageSource SpriteFor(Activity activity, bool miniScene = false)
    {
        // The prototype currently has one pose; animation and behavior still run.
        if (_experimentalModel) return ExperimentalSprite;
        if (miniScene) return activity switch
        {
            Activity.DeskNap => SleepSprite,
            Activity.Stretching => WorkSprite,
            _ => PhoneSprite
        };
        return activity switch
        {
            Activity.Working or Activity.DeskNap or Activity.Stretching => WorkSprite,
            Activity.Phone or Activity.Tea or Activity.Reading => PhoneSprite,
            Activity.Sleeping => SleepSprite,
            _ => IdleSprite
        };
    }

    private void ModelSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || ModelSelector.SelectedIndex < 0) return;
        _experimentalModel = ModelSelector.SelectedIndex == 1;
        PetSprite.Source = SpriteFor(_activity, _miniSceneActive);
        SaveSettings();
    }

    private void RefreshPlatforms()
    {
        var rects = new List<Rect>();
        var dpi = VisualTreeHelper.GetDpi(this);
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || IsIconic(handle)) return true;
            GetWindowThreadProcessId(handle, out var processId);
            if (processId == (uint)Environment.ProcessId || GetWindowTextLength(handle) == 0) return true;
            if (GetWindowRect(handle, out var bounds))
            {
                var width = (bounds.Right - bounds.Left) / dpi.DpiScaleX;
                var height = (bounds.Bottom - bounds.Top) / dpi.DpiScaleY;
                if (width >= 120 && height >= 70)
                    rects.Add(new Rect(bounds.Left / dpi.DpiScaleX, bounds.Top / dpi.DpiScaleY, width, height));
            }
            return true;
        }, IntPtr.Zero);
        _platforms.Clear();
        _platforms.AddRange(rects);
        if (_onSurface)
        {
            var feet = Top + ModelBottom * _uiScale;
            if (feet < SystemParameters.WorkArea.Bottom - 3)
            {
                var left = Left + ModelLeft * _uiScale;
                var right = Left + ModelRight * _uiScale;
                var stillSupported = rects.Exists(r => Math.Abs(r.Top - feet) <= 4 && r.Left < right && r.Right > left);
                if (!stillSupported)
                {
                    _onSurface = false;
                    var center = Left + (ModelLeft + ModelRight) * _uiScale / 2;
                    var nearby = rects
                        .Where(r => r.Top < feet + 10 && r.Top > feet - 90 && Math.Abs((r.Left + r.Right) / 2 - center) < 170)
                        .OrderBy(r => Math.Abs((r.Left + r.Right) / 2 - center) + Math.Abs(r.Top - feet) * 0.35)
                        .FirstOrDefault();
                    if (nearby.Width > 0)
                    {
                        var deltaX = (nearby.Left + nearby.Right) / 2 - center;
                        _velocityY = nearby.Top >= feet - 8 ? -310 : -430;
                        _velocityX = Math.Clamp(deltaX * 1.4, -230, 230);
                        if (ShouldSpeak()) ShowSpeech(ChooseCustomPhrase("Окно закрылось — переберусь сюда."));
                    }
                    else _velocityY = 0;
                }
            }
        }
    }

    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", EntryPoint = "EnumWindows")]
    private static extern bool EnumWindowsNative(EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr handle);
    [DllImport("user32.dll", EntryPoint = "GetWindowRect")] private static extern bool GetWindowRectNative(IntPtr handle, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    private static bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter) => EnumWindowsNative(callback, parameter);
    private static bool GetWindowRect(IntPtr handle, out NativeRect rect) => GetWindowRectNative(handle, out rect);

    private Activity ChooseMiniScene()
    {
        var hour = DateTime.Now.Hour;
        if (_routineEnabled && hour is >= 6 and < 11 && _random.NextDouble() < 0.65) return Activity.Stretching;
        if (_routineEnabled && (hour >= 20 || hour < 5) && _random.NextDouble() < 0.65) return Activity.DeskNap;
        return _random.Next(5) switch
        {
            0 => Activity.Tea,
            1 => Activity.Reading,
            2 => Activity.Stretching,
            3 => Activity.DeskNap,
            _ => Activity.Phone
        };
    }

    private bool ShouldSpeak() => !_quietMode && _chatterLevel > 0 && _random.NextDouble() < _chatterLevel / 2.0;

    private static string ChatterLabelFor(double value) => value < 0.2 ? "Реплики: выкл" : value > 1.5 ? "Реплики: часто" : value > 0.75 ? "Реплики: обычно" : "Реплики: редко";
    private static string JumpLabelFor(double value) => value < 0.8 ? "Прыжки: редко" : value > 1.6 ? "Прыжки: часто" : "Прыжки: обычно";

    private string ChooseCustomPhrase(string fallback) =>
        _customPhrases.Count == 0 ? fallback : _customPhrases[_random.Next(_customPhrases.Count)];

    private void AddCollectionItem()
    {
        _collectionItems.Add(_random.Next(4));
        while (_collectionItems.Count > 8) _collectionItems.RemoveAt(0);
        SaveSettings();
        if (ShouldSpeak()) ShowSpeech(ChooseCustomPhrase("Нашёл для коллекции маленькую вещь!"));
    }

    private void SetActivity(Activity activity, bool speak)
    {
        var changed = _activity != activity;
        _activity = activity;
        if (changed && activity == Activity.Working && _nextWorkPhaseAt < DateTime.UtcNow)
        {
            _nextWorkPhaseAt = DateTime.UtcNow.AddSeconds(_random.Next(2, 5));
        }
        if (changed && activity != Activity.Sleeping) ScheduleNextLeap(DateTime.UtcNow);
        PetSprite.Source = SpriteFor(activity);
        PropGlyph.Visibility = activity is Activity.Tea or Activity.Reading ? Visibility.Visible : Visibility.Collapsed;
        PropGlyph.Text = activity == Activity.Tea ? "☕" : "📖";

        if (!speak || !changed) return;
        var line = activity switch
        {
            Activity.Working => "Пойду проверю свой ноутбук.",
            Activity.Phone => "Ты отошёл. Проверю телефон.",
            Activity.Sleeping => "Тебя давно нет. Я немного посплю.",
            Activity.Tea => "Устрою маленький перерыв на чай.",
            Activity.Reading => "Пока тихо, почитаю.",
            Activity.Stretching => "Пора размяться!",
            Activity.DeskNap => "Только минутку прикрою глаза…",
            _ => "Проснулся!"
        };
        if (ShouldSpeak()) ShowSpeech(ChooseCustomPhrase(line));
    }

    private void StartMiniScene(Activity scene)
    {
        _miniSceneActive = true;
        _activityTransition = true;
        _activityTransitionEndsAt = DateTime.UtcNow.AddSeconds(scene == Activity.DeskNap ? 5 : 4);
        _activity = scene;
        PetSprite.Source = SpriteFor(scene, miniScene: true);
        PropGlyph.Text = scene == Activity.Tea ? "☕" : scene == Activity.Reading ? "📖" : "";
        PropGlyph.Visibility = scene is Activity.Tea or Activity.Reading ? Visibility.Visible : Visibility.Collapsed;
        var sceneLine = scene switch { Activity.Tea => "Перерыв на чай.", Activity.Reading => "Почитаю чуть-чуть.", Activity.Stretching => "Разминаюсь!", _ => "Пять минут отдыха…" };
        if (ShouldSpeak() && _random.NextDouble() < 0.7) ShowSpeech(ChooseCustomPhrase(sceneLine));
    }

    private void Audio_Tick(object? sender, EventArgs e)
    {
        if (!_audioEnabled || !_audioAvailable) { _audioLevel *= 0.8; return; }
        var latest = Interlocked.Exchange(ref _pendingAudioLevel, 0);
        _audioLevel = Math.Max(latest, _audioLevel * 0.58);
        UpdateMusicComments();
    }

    private void Loopback_DataAvailable(object? sender, WaveInEventArgs e)
    {
        var capture = _loopbackCapture;
        if (capture is null || !ReferenceEquals(sender, capture) || e.BytesRecorded == 0) return;
        var format = capture.WaveFormat;
        var bytesPerSample = (format.BitsPerSample + 7) / 8;
        if (bytesPerSample is < 2 or > 4) return;
        var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            (format is WaveFormatExtensible extensible && extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT);
        double sum = 0;
        var count = 0;
        for (var i = 0; i + bytesPerSample <= e.BytesRecorded; i += bytesPerSample)
        {
            double sample;
            if (isFloat && bytesPerSample == 4)
                sample = BitConverter.ToSingle(e.Buffer, i);
            else if (bytesPerSample == 2)
                sample = BitConverter.ToInt16(e.Buffer, i) / 32768.0;
            else if (bytesPerSample == 3)
            {
                var raw = e.Buffer[i] | (e.Buffer[i + 1] << 8) | (e.Buffer[i + 2] << 16);
                if ((raw & 0x800000) != 0) raw |= unchecked((int)0xFF000000);
                sample = raw / 8388608.0;
            }
            else
                sample = BitConverter.ToInt32(e.Buffer, i) / 2147483648.0;
            if (!double.IsFinite(sample)) continue;
            sum += sample * sample;
            count++;
        }
        if (count > 0 && ReferenceEquals(capture, _loopbackCapture)) Interlocked.Exchange(ref _pendingAudioLevel, Math.Sqrt(sum / count));
    }

    private void UpdateMusicComments()
    {
        if (_audioLevel > 0.025) _audioAboveCount++;
        else _audioAboveCount = 0;
        if (_audioLevel < 0.01) _audioBelowCount++;
        else _audioBelowCount = 0;

        if (!_musicDetected && _audioAboveCount >= 3)
        {
            _musicDetected = true;
            if (!_quietMode && _chatterLevel > 0)
                ShowSpeech(_chatterLevel > 1.5 ? "Музыка! Давай подвигаться!" : "О, слышу музыку!");
            if (_activity == Activity.Roaming && !_miniSceneActive && !_forcedRoamingAfterThrow && _onSurface)
            {
                _velocityY = -Math.Min(760, 540 + _audioLevel * 700);
                _velocityX = (_random.NextDouble() * 2 - 1) * 210;
                _onSurface = false;
                ScheduleNextLeap(DateTime.UtcNow);
            }
        }
        else if (_musicDetected && _audioBelowCount >= 18)
        {
            _musicDetected = false;
            _audioAboveCount = 0;
        }
    }

    private void ShowSpeech(string text, bool isLyrics = false)
    {
        if (DateTime.UtcNow < _reminderHoldUntil) return;
        _speechIsLyrics = isLyrics;
        SpeechText.Text = text;
        SpeechBubble.Visibility = Visibility.Visible;
        ThoughtDotLarge.Visibility = Visibility.Visible;
        ThoughtDotSmall.Visibility = Visibility.Visible;
        _speechEndsAt = DateTime.UtcNow.AddSeconds(6);
    }

    private void HideLyricSpeech()
    {
        _lyricsLastLine = "";
        if (!_speechIsLyrics) return;
        _speechIsLyrics = false;
        SpeechBubble.Visibility = Visibility.Collapsed;
        ThoughtDotLarge.Visibility = Visibility.Collapsed;
        ThoughtDotSmall.Visibility = Visibility.Collapsed;
        _speechEndsAt = DateTime.MinValue;
    }

    private void Pet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (SettingsPopup.IsOpen) return;
        _activityTransition = false;
        SetActivity(Activity.Roaming, _activity == Activity.Sleeping);
        var element = (UIElement)sender;
        _dragging = true;
        _thrownResting = false;
        _forcedRoamingAfterThrow = false;
        _miniSceneActive = false;
        _dragOffset = e.GetPosition(this);
        _lastPointer = PointToScreen(_dragOffset);
        _lastPointerAt = DateTime.UtcNow;
        _velocityX = 0;
        _velocityY = 0;
        _onSurface = false;
        element.CaptureMouse();
        e.Handled = true;
    }

    private void Pet_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var pointer = PointToScreen(e.GetPosition(this));
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastPointerAt).TotalSeconds;
        if (elapsed > 0.002)
        {
            var instantX = (pointer.X - _lastPointer.X) / elapsed;
            var instantY = (pointer.Y - _lastPointer.Y) / elapsed;
            _velocityX = Math.Clamp(_velocityX * 0.35 + instantX * 0.65, -1800, 1800);
            _velocityY = Math.Clamp(_velocityY * 0.35 + instantY * 0.65, -1800, 1800);
            _lastPointer = pointer;
            _lastPointerAt = now;
        }
        Left = pointer.X - _dragOffset.X;
        Top = pointer.Y - _dragOffset.Y;
        e.Handled = true;
    }

    private void Pet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
        _velocityX = Math.Clamp(_velocityX * 1.6, -2800, 2800);
        _velocityY = Math.Clamp(_velocityY * 1.6, -2800, 2800);
        _forcedRoamingAfterThrow = true;
        _thrownResting = true;
        _throwHoldUntil = DateTime.UtcNow.AddSeconds(6);
        _miniSceneActive = false;
        ScheduleNextLeap(DateTime.UtcNow);
        e.Handled = true;
    }

    private void Pet_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (SettingsPopup.IsOpen) SettingsPopup.IsOpen = false;
        else OpenSettingsPopup();
        e.Handled = true;
    }

    private void OpenSettingsPopup()
    {
        var area = SystemParameters.WorkArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        Point targetTop;
        try { targetTop = PetSprite.PointToScreen(new Point(0, 0)); }
        catch { targetTop = PointToScreen(new Point(0, 0)); }
        var topDip = targetTop.Y / dpi.DpiScaleY;
        var bottomDip = topDip + PetSprite.ActualHeight * _uiScale;
        var spaceAbove = Math.Max(0, topDip - area.Top - 8);
        var spaceBelow = Math.Max(0, area.Bottom - bottomDip - 8);
        var useAbove = spaceAbove >= spaceBelow;
        SettingsPopup.Placement = useAbove
            ? System.Windows.Controls.Primitives.PlacementMode.Top
            : System.Windows.Controls.Primitives.PlacementMode.Bottom;
        Menu.Measure(new Size(Menu.Width, double.PositiveInfinity));
        var available = Math.Max(150, Math.Max(spaceAbove, spaceBelow));
        // Leave room for expanding sections without trapping them below the viewport.
        MenuScroll.MaxHeight = Math.Min(640, available);
        MenuScroll.ScrollToTop();
        SettingsPopup.IsOpen = true;
    }

    private void Jump_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = false;
        _activityTransition = false;
        SetActivity(Activity.Roaming, false);
        _velocityY = -1100 * Math.Sqrt(_jumpRate);
        _velocityX = (_random.NextDouble() * 2 - 1) * 300;
        _onSurface = false;
        _forcedRoamingAfterThrow = true;
        _thrownResting = false;
        _throwHoldUntil = DateTime.UtcNow.AddSeconds(6);
        _miniSceneActive = false;
        ScheduleNextLeap(DateTime.UtcNow);
    }

    private void CloseMenu_Click(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = false;

    private void StopAudioCapture()
    {
        var capture = _loopbackCapture;
        _loopbackCapture = null;
        _audioAvailable = false;
        if (capture is null) return;
        capture.DataAvailable -= Loopback_DataAvailable;
        capture.RecordingStopped -= Loopback_RecordingStopped;
        try { capture.StopRecording(); } catch { }
        capture.Dispose();
    }

    private void Audio_Click(object sender, RoutedEventArgs e)
    {
        if (_audioEnabled)
        {
            _audioEnabled = false;
            StopAudioCapture();
            _audioLevel = 0;
            Interlocked.Exchange(ref _pendingAudioLevel, 0);
            _musicDetected = false;
            _audioAboveCount = 0;
            _audioBelowCount = 0;
        }
        else
        {
            Interlocked.Exchange(ref _pendingAudioLevel, 0);
            if (!StartAudioCapture())
            {
                AudioButton.Content = "Звук недоступен — выключить";
                return;
            }
            _audioEnabled = true;
        }
        AudioButton.Content = _audioEnabled ? "Реакция на звук: вкл" : "Реакция на звук: выкл";
        SaveSettings();
    }

    private void Lyrics_Click(object sender, RoutedEventArgs e)
    {
        if (!_lyricsEnabled)
        {
            var result = MessageBox.Show(this, "Чтобы найти синхронизированный текст, название песни, исполнитель, альбом и длительность (если доступны) будут отправлены сервису LRCLIB через интернет. Аудиозапись не отправляется. Работает только с совместимыми медиаплеерами и не для всех песен. Включить?", "Текст песни", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result != MessageBoxResult.Yes) return;
            _lyricsEnabled = true;
            _nextLyricsAttemptAt = DateTime.MinValue;
            _lyricsTimer.Start();
        }
        else
        {
            _lyricsEnabled = false;
            _lyricsTimer.Stop();
            _lyricsCancellation.Cancel();
            _lyricsCancellation.Dispose();
            _lyricsCancellation = new CancellationTokenSource();
            _lyricsService?.Dispose();
            _lyricsService = null;
            HideLyricSpeech();
        }
        LyricsButton.Content = _lyricsEnabled ? "Текст песни: вкл" : "Текст песни: выкл";
        SaveSettings();
    }

    private async void Lyrics_Tick(object? sender, EventArgs e)
    {
        if (!_lyricsEnabled || _quietMode || _lyricsBusy || _isClosing || DateTime.UtcNow < _nextLyricsAttemptAt || DateTime.UtcNow < _lyricsHoldUntil) return;
        _lyricsBusy = true;
        var token = _lyricsCancellation.Token;
        try
        {
            _lyricsService ??= new LyricsService();
            string? line = await _lyricsService.CurrentLineAsync(token);
            if (token.IsCancellationRequested || !_lyricsEnabled || _quietMode || _isClosing) return;
            _lyricsStatus = _lyricsService.Status;
            if (DateTime.UtcNow < _lyricsHoldUntil) return;
            if (string.IsNullOrWhiteSpace(line)) { HideLyricSpeech(); return; }
            if (line.Length > 180) line = line[..180] + "…";
            if (line != _lyricsLastLine || !_speechIsLyrics || SpeechBubble.Visibility != Visibility.Visible || SpeechText.Text != line)
            {
                _lyricsLastLine = line;
                ShowSpeech(line, isLyrics: true);
            }
            _speechEndsAt = DateTime.UtcNow.AddSeconds(3);
        }
        catch (Exception) { if (!token.IsCancellationRequested && !_isClosing) { _lyricsStatus = "Не удалось проверить проигрыватель или связаться с LRCLIB. Проверь интернет и повтори позже."; HideLyricSpeech(); _nextLyricsAttemptAt = DateTime.UtcNow.AddSeconds(5); } }
        finally { _lyricsBusy = false; }
    }

    private void ChatterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing) return;
        _chatterLevel = e.NewValue;
        ChatterLabel.Text = ChatterLabelFor(e.NewValue);
        SaveSettings();
    }

    private void JumpSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing) return;
        _jumpRate = e.NewValue;
        JumpLabel.Text = JumpLabelFor(e.NewValue);
        ScheduleNextLeap(DateTime.UtcNow);
        SaveSettings();
    }

    private void Quiet_Click(object sender, RoutedEventArgs e)
    {
        _quietMode = !_quietMode;
        QuietButton.Content = _quietMode ? "Не отвлекать: вкл" : "Не отвлекать: выкл";
        if (_quietMode)
        {
            SpeechBubble.Visibility = Visibility.Collapsed;
            ThoughtDotLarge.Visibility = Visibility.Collapsed;
            ThoughtDotSmall.Visibility = Visibility.Collapsed;
            _speechIsLyrics = false;
            _lyricsLastLine = "";
        }
        SaveSettings();
    }

    private void BringSecret_Click(object sender, RoutedEventArgs e)
    {
        // An explicit request shows the surprise even when automatic chatter is disabled.
        int item = _random.Next(4);
        _collectionItems.Add(item);
        while (_collectionItems.Count > 8) _collectionItems.RemoveAt(0);
        SaveSettings();
        _lyricsHoldUntil = DateTime.UtcNow.AddSeconds(6);
        // An explicit user request must not be silently suppressed by a reminder.
        _reminderHoldUntil = DateTime.MinValue;
        Jump_Click(sender, e);
        string surprise = item switch { 0 => "звёздочка ⭐", 1 => "камешек 💎", 2 => "клевер 🍀", _ => "подарок 🎁" };
        ShowSpeech("Принёс секрет: " + surprise);
    }

    private void LyricsStatus_Click(object sender, RoutedEventArgs e)
    {
        string status = !_lyricsEnabled ? "Текст песни выключен. Сначала включи его в этом меню и подтверди запрос к LRCLIB." :
            _quietMode ? "Включён режим «Не отвлекать»: он скрывает текст. Выключи его для показа песен." : _lyricsStatus;
        MessageBox.Show(this, status + "\n\nПлеер должен передавать название и исполнителя в управление мультимедиа Windows. Текст находится не для всех песен. Аудио не отправляется.", "Текст песни", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Surprise_Click(object sender, RoutedEventArgs e)
    {
        _surprisesEnabled = !_surprisesEnabled;
        SurpriseButton.Content = _surprisesEnabled ? "Секреты: вкл" : "Секреты: выкл";
        SaveSettings();
    }

    private void Seat_Click(object sender, RoutedEventArgs e)
    {
        _favoriteSpotEnabled = !_favoriteSpotEnabled;
        SeatButton.Content = _favoriteSpotEnabled ? "Место: угол экрана" : "Место: свободно";
        SaveSettings();
    }

    private void Collection_Click(object sender, RoutedEventArgs e)
    {
        _collectionEnabled = !_collectionEnabled;
        CollectionButton.Content = _collectionEnabled ? "Коллекция: вкл" : "Коллекция: выкл";
        if (_collectionEnabled && _nextCollectionAt < DateTime.UtcNow) _nextCollectionAt = DateTime.UtcNow.AddMinutes(2);
        SaveSettings();
    }

    private void Routine_Click(object sender, RoutedEventArgs e)
    {
        _routineEnabled = !_routineEnabled;
        RoutineButton.Content = _routineEnabled ? "Распорядок: вкл" : "Распорядок: выкл";
        SaveSettings();
    }

    private void SavePhrases_Click(object sender, RoutedEventArgs e)
    {
        _customPhrases.Clear();
        _customPhrases.AddRange(PhraseTextBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)
            .Take(12)
            .Select(line => line.Length > 64 ? line[..64] : line));
        PhraseTextBox.Text = string.Join(Environment.NewLine, _customPhrases);
        SaveSettings();
        if (!_quietMode) ShowSpeech(_customPhrases.Count > 0 ? "Сохранил твои фразы." : "Вернул мои обычные фразы.");
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            var values = File.ReadAllLines(_settingsPath);
            _uiScale = ParseSetting(values, 0, _uiScale, 0.5, 2);
            _chatterLevel = ParseSetting(values, 1, _chatterLevel, 0, 2);
            _jumpRate = ParseSetting(values, 2, _jumpRate, 0.5, 2);
            _quietMode = values.Length > 3 && bool.TryParse(values[3], out var quiet) && quiet;
            _surprisesEnabled = values.Length <= 4 || !bool.TryParse(values[4], out var surprise) || surprise;
            _favoriteSpotEnabled = values.Length > 5 && bool.TryParse(values[5], out var favorite) && favorite;
            _audioEnabled = values.Length <= 6 || !bool.TryParse(values[6], out var audio) || audio;
            _collectionEnabled = values.Length <= 7 || !bool.TryParse(values[7], out var collection) || collection;
            _routineEnabled = values.Length <= 8 || !bool.TryParse(values[8], out var routine) || routine;
            _petOpacity = ParseSetting(values, 12, 1, 0.35, 1);
            if (values.Length > 13 && int.TryParse(values[13], out int minutes) && minutes is 30 or 60 or 120) _reminderMinutes = minutes;
            if (values.Length > 14 && values[14].Length < 500) {
                try {
                    string text = Encoding.UTF8.GetString(Convert.FromBase64String(values[14])).Trim();
                    if (text.Length > 0) _reminderText = text.Length > 80 ? text[..80] : text;
                } catch (FormatException) { }
            }
            _savedLeft = ParseSetting(values, 15, double.NaN, -100000, 100000);
            _savedTop = ParseSetting(values, 16, double.NaN, -100000, 100000);
            _experimentalModel = values.Length > 17 && bool.TryParse(values[17], out var experimental) && experimental;
            _lyricsEnabled = values.Length > 11 && bool.TryParse(values[11], out var lyrics) && lyrics;
            if (values.Length > 9)
                _collectionItems.AddRange(values[9].Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out var item) ? item : -1).Where(item => item is >= 0 and < 4).Take(8));
            if (values.Length > 10 && values[10].Length < 1400)
            {
                var savedPhrases = Encoding.UTF8.GetString(Convert.FromBase64String(values[10]));
                _customPhrases.AddRange(savedPhrases.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(line => line.Length > 0).Take(12).Select(line => line.Length > 64 ? line[..64] : line));
            }
        }
        catch { }
    }

    private static double ParseSetting(string[] values, int index, double fallback, double min, double max) =>
        values.Length > index && double.TryParse(values[index], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed) ? Math.Clamp(parsed, min, max) : fallback;

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_settingsPath)!);
            var tempPath = _settingsPath + ".tmp";
            var savedPhrases = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, _customPhrases)));
            var values = new[]
            {
                _uiScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _chatterLevel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _jumpRate.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _quietMode.ToString(), _surprisesEnabled.ToString(), _favoriteSpotEnabled.ToString(), _audioEnabled.ToString(),
                _collectionEnabled.ToString(), _routineEnabled.ToString(), string.Join(',', _collectionItems), savedPhrases, _lyricsEnabled.ToString(),
                _petOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture), _reminderMinutes.ToString(),
                Convert.ToBase64String(Encoding.UTF8.GetBytes(_reminderText)),
                _savedLeft.ToString(System.Globalization.CultureInfo.InvariantCulture), _savedTop.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _experimentalModel.ToString()
            };
            File.WriteAllLines(tempPath, values);
            File.Move(tempPath, _settingsPath, true);
        }
        catch { }
    }

    private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing) return;
        if (!IsInitialized || PetCanvas is null) return;
        var newScale = e.NewValue;
        var oldScale = _uiScale;
        var footPosition = Top + ModelBottom * oldScale;
        var modelCenterX = Left + (ModelLeft + ModelRight) / 2 * oldScale;
        _uiScale = newScale;
        PetCanvas.LayoutTransform = new ScaleTransform(newScale, newScale);
        Width = 260 * newScale;
        Height = 320 * newScale;
        Top = footPosition - ModelBottom * newScale;
        Left = modelCenterX - (ModelLeft + ModelRight) / 2 * newScale;
        SizeLabel.Text = $"Размер: {newScale:P0}";
        SaveSettings();
    }

    private void RememberPosition_Click(object sender, RoutedEventArgs e)
    {
        _savedLeft = Left; _savedTop = Top; SaveSettings();
        SettingsPopup.IsOpen = false;
        if (!_quietMode) ShowSpeech("Положение сохранено!");
    }
    private void ResetPosition_Click(object sender, RoutedEventArgs e)
    {
        _savedLeft = _savedTop = double.NaN; SaveSettings();
        SettingsPopup.IsOpen = false;
        if (!_quietMode) ShowSpeech("Стартовое положение сброшено.");
    }

    private const string AutostartKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AutostartName = "CacheTrim Claude Desktop Pet";
    private string MyAutostartCommand() => "\"" + Environment.ProcessPath + "\"";
    private void RefreshAutostartLabel()
    {
        try {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(AutostartKey);
            string? current = key?.GetValue(AutostartName) as string;
            PetAutostartButton.Content = string.Equals(current, MyAutostartCommand(), StringComparison.OrdinalIgnoreCase)
                ? "Автозапуск: вкл" : current == null ? "Автозапуск: выкл" : "Автозапуск: другая запись";
        } catch { PetAutostartButton.Content = "Автозапуск недоступен"; }
    }
    private void PetAutostart_Click(object sender, RoutedEventArgs e)
    {
        try {
            if (string.IsNullOrWhiteSpace(Environment.ProcessPath)) throw new InvalidOperationException("Не удалось определить файл Клода.");
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(AutostartKey)
                ?? throw new InvalidOperationException("Нет доступа к автозагрузке пользователя.");
            string? current = key.GetValue(AutostartName) as string;
            string own = MyAutostartCommand();
            if (!PetPolicy.CanChangeAutostart(current, own))
                throw new InvalidOperationException("Запись автозапуска изменилась. Проверь её в разделе «Автозагрузка» CacheTrim.");
            if (current != null) key.DeleteValue(AutostartName, false);
            else {
                if (MessageBox.Show(this, "Запускать Клода при входе в Windows? Он будет запускаться от текущего пользователя напрямую, без проверки CacheTrim при каждом входе. Отключить можно этой же кнопкой.",
                    "Автозапуск Клода", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
                key.SetValue(AutostartName, own, Microsoft.Win32.RegistryValueKind.String);
            }
            RefreshAutostartLabel();
        } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Автозапуск Клода"); }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing || PetCanvas == null) return;
        _petOpacity = Math.Clamp(e.NewValue, 0.35, 1);
        PetCanvas.Opacity = _petOpacity;
        OpacityLabel.Text = $"Непрозрачность: {_petOpacity:P0}";
        SaveSettings();
    }

    private void SaveReminder_Click(object sender, RoutedEventArgs e)
    {
        _reminderMinutes = ReminderInterval.SelectedIndex switch { 1 => 30, 2 => 60, 3 => 120, _ => 0 };
        _reminderText = ReminderText.Text.Trim();
        if (_reminderText.Length == 0) _reminderText = "Пора сделать перерыв!";
        ReminderText.Text = _reminderText;
        _nextReminder = _reminderMinutes > 0 ? DateTime.UtcNow.AddMinutes(_reminderMinutes) : DateTime.MaxValue;
        SaveSettings();
        SettingsPopup.IsOpen = false;
        if (!_quietMode) { _lyricsHoldUntil = DateTime.UtcNow.AddSeconds(6); ShowSpeech(_reminderMinutes == 0 ? "Напоминания выключены." : "Напоминание сохранено!"); }
    }

    private void Reminder_Tick(object? sender, EventArgs e)
    {
        if (!PetPolicy.ReminderDue(_reminderMinutes, _nextReminder, DateTime.UtcNow, _quietMode, SettingsPopup.IsOpen)) return;
        _nextReminder = DateTime.UtcNow.AddMinutes(_reminderMinutes);
        _lyricsHoldUntil = DateTime.UtcNow.AddSeconds(8);
        ShowSpeech(_reminderText);
        _reminderHoldUntil = DateTime.UtcNow.AddSeconds(8);
        _speechEndsAt = DateTime.UtcNow.AddSeconds(8);
    }

    private void ShowDesktop_Click(object sender, RoutedEventArgs e)
    {
        try {
            Type? type = Type.GetTypeFromProgID("Shell.Application");
            if (type == null) throw new InvalidOperationException("Команда Windows недоступна.");
            object shell = Activator.CreateInstance(type)!;
            type.InvokeMember("ToggleDesktop", System.Reflection.BindingFlags.InvokeMethod, null, shell, null);
            SettingsPopup.IsOpen = false;
        } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Не удалось показать рабочий стол"); }
    }

    private void OpenDownloads_Click(object sender, RoutedEventArgs e)
    {
        try {
            var start = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = false };
            start.ArgumentList.Add("shell:Downloads");
            System.Diagnostics.Process.Start(start);
            SettingsPopup.IsOpen = false;
        } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Не удалось открыть загрузки"); }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _isClosing = true;
        _reminderTimer.Stop(); _motionTimer.Stop(); _audioTimer.Stop(); _platformScanTimer.Stop(); _lyricsTimer.Stop();
        _lyricsCancellation.Cancel(); _lyricsCancellation.Dispose(); _lyricsService?.Dispose();
        StopAudioCapture();
        base.OnClosed(e);
    }
}
