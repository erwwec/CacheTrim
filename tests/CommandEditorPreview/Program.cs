using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet;

internal static class Preview
{
    [STAThread]
    private static void Main(string[] args)
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var owner = new Window { Width = 1, Height = 1, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Left = -5000, Top = -5000, WindowStartupLocation = WindowStartupLocation.Manual };
        owner.Show();
        var assembly = typeof(MainWindow).Assembly;
        var editorType = assembly.GetType("DesktopPet.CustomCommandsWindow")!;
        var commandType = assembly.GetType("DesktopPet.CustomCommand")!;
        var actionType = assembly.GetType("DesktopPet.CustomAction")!;
        var list = Activator.CreateInstance(typeof(List<>).MakeGenericType(commandType))!;
        var window = (Window)Activator.CreateInstance(editorType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { owner, Path.Combine(Path.GetTempPath(), "preview-commands-do-not-save.json"), list }, null)!;
        object Field(string name) => editorType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        ((TextBox)Field("_name")).Text = "Запусти игру";
        ((TextBox)Field("_phrases")).Text = "запусти игру\nоткрой игру";
        var actions = (ListBox)Field("_actions");
        actions.Items.Add(Activator.CreateInstance(actionType, new object[] { "app", @"C:\Games\Game.exe" })!);
        actions.Items.Add(Activator.CreateInstance(actionType, new object[] { "url", "https://example.com/" })!);
        actions.SelectedIndex = 0;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = 20; window.Top = 20;
        window.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        timer.Tick += (_, _) =>
        {
            timer.Stop(); window.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using (var stream = File.Create(output)) encoder.Save(stream);
            // Clear the sample so closing does not prompt to discard it.
            ((TextBox)Field("_name")).Clear(); ((TextBox)Field("_phrases")).Clear(); actions.Items.Clear();
            application.Shutdown();
        };
        timer.Start(); application.Run();
    }
}
