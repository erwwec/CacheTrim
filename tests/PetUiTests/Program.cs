using System.Reflection;
using System.Windows;
using DesktopPet;
internal static class Program
{
    [STAThread] static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var pet = new MainWindow();
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
        finally { pet.Close(); app.Shutdown(); }
    }
}
