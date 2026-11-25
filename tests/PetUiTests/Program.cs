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
            var selector = (System.Windows.Controls.ComboBox)Element("ModelSelector");
            var sprite = (System.Windows.Controls.Image)Element("PetSprite");
            selector.SelectedIndex = 0;
            Check(Element("AssistantOpenButton").Visibility == Visibility.Collapsed, "assistant hidden in original model");
            Check(type.GetField("_voiceSession", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet) == null, "microphone not started automatically");
            Check(type.GetField("_cloudCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet) == null, "cloud assistant not started automatically");
            var classic = sprite.Source;
            selector.SelectedIndex = 1;
            Check(Element("AssistantOpenButton").Visibility == Visibility.Visible, "assistant visible in V2");
            var sizes = (System.Windows.Controls.ComboBox)Element("SizeSelector");
            Check(sizes.Items.Count == 3, "only three pet sizes are offered");
            sizes.SelectedIndex = 0; Check((double)type.GetField("_uiScale", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)! == 0.5, "50 percent size selected");
            sizes.SelectedIndex = 1; Check((double)type.GetField("_uiScale", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)! == 1.0, "100 percent size selected");
            sizes.SelectedIndex = 2; Check((double)type.GetField("_uiScale", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)! == 1.5, "150 percent size selected");
            ((System.Windows.Controls.Button)Element("AssistantOpenButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var assistantWindow = (Window)type.GetField("_assistantWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!;
            Check(assistantWindow != pet && assistantWindow.IsVisible && assistantWindow.Content == Element("AssistantContent"), "assistant opens in a separate window");
            var commands = (System.Windows.Controls.ComboBox)Element("AssistantCommandSelector");
            Check(commands.Items.Count == 8, "local command list is populated");
            commands.SelectedIndex = 0;
            Check(((System.Windows.Controls.TextBox)Element("AssistantCommandText")).Text == "привет клод", "selected command fills input");
            var providers = (System.Windows.Controls.ComboBox)Element("AssistantProvider");
            var speechChoice = (System.Windows.Controls.ComboBox)Element("AssistantSpeechProvider");
            providers.SelectedIndex = 1; speechChoice.SelectedIndex = 1;
            Check(File.ReadAllLines(Path.Combine(tempDir, "settings.ini"))[18] == "1" && File.ReadAllLines(Path.Combine(tempDir, "settings.ini"))[19] == "1", "provider and speech choice are persisted");
            var entry = (System.Windows.Controls.TextBox)Element("AssistantCloudText");
            entry.ApplyTemplate();
            Check(entry.Background.ToString() == "#FF30263C" && entry.Template.FindName("PART_ContentHost", entry) != null, "input has a dark editable template");
            providers.ApplyTemplate();
            Check(providers.Template.FindName("PART_Popup", providers) != null, "provider menu has a dropdown");
            assistantWindow.Close();
            Check(!assistantWindow.IsVisible, "closing assistant hides it safely");
            Check(sprite.Source != null && !ReferenceEquals(sprite.Source, classic), "test model is distinct from the original");
            Check(File.ReadAllLines(Path.Combine(tempDir, "settings.ini"))[17] == "True", "test model selection is saved");
            type.GetField("_experimentalModel", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pet, false);
            type.GetMethod("LoadSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, null);
            Check((bool)type.GetField("_experimentalModel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!, "test model selection is restored");
            var cloudCancellation = new System.Threading.CancellationTokenSource();
            type.GetField("_cloudCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pet, cloudCancellation);
            type.GetMethod("UpdateAssistantBadge", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, null);
            Check(Element("AssistantBadge").Visibility == Visibility.Visible, "assistant activity is visible on the pet");
            selector.SelectedIndex = 0;
            Check(Element("AssistantBadge").Visibility == Visibility.Collapsed && type.GetField("_cloudCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet) == null, "switch to classic cancels cloud and hides activity");
            Check(Element("AssistantOpenButton").Visibility == Visibility.Collapsed && type.GetField("_voiceSession", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet) == null, "switching to original stops voice and hides assistant");
            Check(ReferenceEquals(sprite.Source, classic), "original model can be restored without replacement");
            var sections = new[] { "AppearanceSection", "RemindersSection", "CommandsSection", "BehaviorSection", "MusicSection" };
            Check(sections.All(name => Element(name) is System.Windows.Controls.Expander), "all pet menu groups exist");
            var music = (System.Windows.Controls.Expander)Element("MusicSection");
            music.ApplyTemplate();
            var content = (System.Windows.Controls.ContentPresenter)music.Template.FindName("SectionContent", music);
            Check(content != null && content.Visibility == Visibility.Collapsed, "music settings initially collapsed");
            music.IsExpanded = true; music.ApplyTemplate();
            Check(content.Visibility == Visibility.Visible, "music settings can be expanded");
            music.IsExpanded = false;
            Check(Element("BringSecretButton") != null && Element("LyricsStatusButton") != null, "secret and lyrics diagnostic buttons exist");
            type.GetField("_reminderHoldUntil", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pet, DateTime.UtcNow.AddMinutes(1));
            type.GetMethod("BringSecret_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, new object[] { pet, new RoutedEventArgs() });
            var speechText = (System.Windows.Controls.TextBlock)Element("SpeechText");
            Check(speechText.Text.StartsWith("Принёс секрет:") && Element("SpeechBubble").Visibility == Visibility.Visible, "manual secret appears even during a reminder");
            Check((DateTime)type.GetField("_lyricsHoldUntil", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)! > DateTime.UtcNow, "lyrics wait while secret is shown");
            type.GetMethod("StopAudioCapture", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, null);
            type.GetField("_audioEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pet, true);
            type.GetField("_audioAvailable", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(pet, false);
            type.GetMethod("Audio_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, new object[] { pet, new RoutedEventArgs() });
            Check(!(bool)type.GetField("_audioEnabled", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!, "unavailable audio reaction can still be switched off");
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
