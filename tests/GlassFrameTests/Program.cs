using System.Reflection;
using System.Windows;
using System.Windows.Shell;
using CacheTrim;
internal static class Program
{
    [STAThread] static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var asm = typeof(MainWindow).Assembly;
        var appType = asm.GetType("CacheTrim.App")!;
        var settings = appType.GetProperty("Settings", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var settingsType = settings.GetType();
        settingsType.GetField("ThemeName")!.SetValue(settings, "LiquidGlass");
        settingsType.GetField("GlassBackgroundBlur")!.SetValue(settings, 100);
        var window = new MainWindow();
        try {
            window.Show();
            var chrome = WindowChrome.GetWindowChrome(window);
            if (chrome == null || chrome.GlassFrameThickness != WindowChrome.GlassFrameCompleteThickness)
                throw new Exception("Full glass frame was not applied to WindowChrome.");
            settingsType.GetField("ThemeName")!.SetValue(settings, "Graphite");
            var theme = asm.GetType("CacheTrim.Theme")!;
            theme.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, new object[] { "Graphite", "" });
            typeof(MainWindow).GetMethod("ApplyTheme", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            if (WindowChrome.GetWindowChrome(window).GlassFrameThickness != new Thickness(0))
                throw new Exception("Glass frame was not cleared when switching to a solid theme.");
            Console.WriteLine("WindowChrome uses the full frame for Acrylic and clears it on theme change.");
        } finally { window.Close(); app.Shutdown(); }
    }
}
