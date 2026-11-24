using System.Reflection;
using System.IO;
using System.Windows;
using DesktopPet;
internal static class Program
{
    [STAThread] static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var pet = new MainWindow();
        string tempDir = Path.Combine(Path.GetTempPath(), "CacheTrimPetTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        typeof(MainWindow).GetField("_settingsPath", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pet, Path.Combine(tempDir, "settings.ini"));
        try
        {
            pet.Show();
            var type = typeof(MainWindow);
            var speech = type.GetMethod("ShowSpeech", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var hide = type.GetMethod("HideLyricSpeech", BindingFlags.Instance | BindingFlags.NonPublic)!;
            UIElement Element(string name) => (UIElement)type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!;
            void Show(string text, bool lyrics) => speech.Invoke(pet, new object[] { text, lyrics });
            void Hide() => hide.Invoke(pet, null);
            void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("OK: " + label); }
            Check(Element("BringSecretButton") != null && Element("LyricsStatusButton") != null, "secret and lyrics diagnostic buttons exist");
            type.GetMethod("BringSecret_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, new object[] { pet, new RoutedEventArgs() });
            var speechText = (System.Windows.Controls.TextBlock)Element("SpeechText");
            Check(speechText.Text.StartsWith("Принёс секрет:") && Element("SpeechBubble").Visibility == Visibility.Visible, "manual secret appears in thought bubble");
            Check((DateTime)type.GetField("_lyricsHoldUntil", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)! > DateTime.UtcNow, "lyrics wait while secret is shown");
            var parse = type.GetMethod("ParseSetting", BindingFlags.Static | BindingFlags.NonPublic)!;
            double Parse(string value) => (double)parse.Invoke(null, new object[] { new[] { value }, 0, 0.6, 0.5, 2.0 })!;
            Check(Parse("NaN") == 0.6 && Parse("Infinity") == 0.6, "invalid floating point settings use safe defaults");
            Check(Parse("9") == 2.0 && Parse("0.1") == 0.5, "settings are clamped to allowed bounds");
            Show("Ordinary pet phrase", false); Hide();
            Check(Element("SpeechBubble").Visibility == Visibility.Visible && Element("ThoughtDotLarge").Visibility == Visibility.Visible, "hiding lyrics preserves ordinary speech");
            Show("Song line", true); Hide();
            Check(Element("SpeechBubble").Visibility == Visibility.Collapsed && Element("ThoughtDotLarge").Visibility == Visibility.Collapsed && Element("ThoughtDotSmall").Visibility == Visibility.Collapsed, "hiding lyrics removes cloud and dots together");
            Show("Song line", true); Show("Different pet phrase", false); Hide();
            Check(Element("SpeechBubble").Visibility == Visibility.Visible && Element("ThoughtDotSmall").Visibility == Visibility.Visible, "a newer normal phrase is not erased");
        }
        finally { pet.Close(); app.Shutdown(); Directory.Delete(tempDir, true); }
    }
}
