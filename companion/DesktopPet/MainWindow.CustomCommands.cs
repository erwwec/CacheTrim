using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace DesktopPet;
public partial class MainWindow
{
    private List<CustomCommand> _customCommands = new();
    private string? _customCommandsError;
    private void RefreshCustomCommands()
    {
        try { _customCommands = CustomCommands.Load(CustomCommands.DefaultPath); _customCommandsError = null; }
        catch (Exception ex) { _customCommands = new(); _customCommandsError = ex.Message; }
        AssistantCommandSelector.ItemsSource = VoiceCommandCatalog.Phrases.Concat(_customCommands.Where(c => c.Enabled).SelectMany(c => c.Phrases)).ToArray();
        if (_customCommandsError != null) AssistantVoiceStatus.Text = _customCommandsError;
    }
    private void CustomCommands_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        if (_voiceSession != null) StopVoice();
        RefreshCustomCommands();
        if (_customCommandsError != null) { MessageBox.Show(_assistantWindow ?? this, _customCommandsError, "Свои команды", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var editor = new CustomCommandsWindow(_assistantWindow ?? this, CustomCommands.DefaultPath, _customCommands); editor.ShowDialog(); RefreshCustomCommands();
    }
    private bool TryRunLocalPhrase(string? text, float confidence = 1f)
    {
        var builtIn = VoiceCommandCatalog.Parse(text, confidence);
        if (builtIn != PetVoiceCommand.None) { ExecuteAssistantCommand(builtIn); AssistantVoiceStatus.Text = "Команда выполнена."; return true; }
        var c = CustomCommands.Find(_customCommands, text, confidence);
        if (c == null) return false;
        string actions = string.Join(Environment.NewLine, c.Actions.Select((a, i) => (i + 1) + ". " + a));
        string prompt = "Выполнить команду «" + c.Name + "»?\n\n" + actions + "\n\nПрограммы могут запуститься с правами Клода, включая права администратора. Выполняй только доверенные действия.";
        if (MessageBox.Show(_assistantWindow ?? this, prompt, "Подтверждение своей команды", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { AssistantVoiceStatus.Text = "Команда отменена."; return true; }
        try
        {
            foreach (var a in c.Actions)
            {
                if (a.Type == "app")
                {
                    if (!File.Exists(a.Target)) throw new FileNotFoundException("Программа не найдена: " + a.Target);
                    Process.Start(new ProcessStartInfo { FileName = a.Target, UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(a.Target)! });
                }
                else Process.Start(new ProcessStartInfo { FileName = a.Target, UseShellExecute = true });
            }
            AssistantVoiceStatus.Text = "Команда запущена: " + c.Name;
        }
        catch (Exception ex) { AssistantVoiceStatus.Text = "Действия остановлены: " + ex.Message; }
        return true;
    }
}
