using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NAudio.Wave;

namespace DesktopPet;
public partial class MainWindow
{
    private List<CustomCommand> _customCommands = new();
    private string? _customCommandsError;
    private readonly DispatcherTimer _customScheduleTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private CancellationTokenSource? _customCommandCancellation;
    private bool _customEditorOpen;
    private bool _customCommandPending;

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
        _customEditorOpen = true;
        try { var editor = new CustomCommandsWindow(_assistantWindow ?? this, CustomCommands.DefaultPath, _customCommands); editor.ShowDialog(); }
        finally { _customEditorOpen = false; RefreshCustomCommands(); }
    }
    private bool TryRunLocalPhrase(string? text, float confidence = 1f, bool fromVoice = false)
    {
        var builtIn = VoiceCommandCatalog.Parse(text, confidence);
        if (builtIn != PetVoiceCommand.None) { ExecuteAssistantCommand(builtIn); AssistantVoiceStatus.Text = "Команда выполнена."; return true; }
        var c = CustomCommands.Find(_customCommands, text, confidence);
        if (c == null) return false;
        RunCustomCommand(c, false, fromVoice);
        return true;
    }
    private void StartCustomSchedule()
    {
        _customScheduleTimer.Tick += CustomSchedule_Tick;
        _customScheduleTimer.Start();
    }
    private void CustomSchedule_Tick(object? sender, EventArgs e)
    {
        if (!_experimentalModel || _isClosing || _customEditorOpen || _customCommandPending || _customCommandsError != null) return;
        var now = DateTime.Now;
        try
        {
            // Serialize claim across running copies of the pet, and write the claim before executing.
            using var mutex = new Mutex(false, @"Local\ClaudeDesktopPetCustomSchedule");
            bool acquired;
            try { acquired = mutex.WaitOne(0); } catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) return;
            CustomCommand? due;
            try
            {
                var tracker = new CustomScheduleTracker(CustomCommands.ScheduleStatePath);
                due = _customCommands.FirstOrDefault(c => tracker.Due(c, now));
                if (due != null) tracker.Mark(due, now);
            }
            finally { mutex.ReleaseMutex(); }
            if (due != null) RunCustomCommand(due, true);
        }
        catch (Exception ex)
        {
            _customScheduleTimer.Stop();
            AssistantVoiceStatus.Text = "Расписание остановлено: " + ex.Message;
        }
    }
    private static bool IsElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch (Exception) { return true; } // Fail closed if elevation cannot be determined.
    }
    private async void RunCustomCommand(CustomCommand c, bool scheduled, bool fromVoice = false)
    {
        if (_customCommandPending) { AssistantVoiceStatus.Text = "Другая цепочка ещё выполняется. Можно остановить её кнопкой ниже."; return; }
        _customCommandPending = true;
        CancellationTokenSource? cancellation = null;
        try
        {
            if (c.Confirm || fromVoice || IsElevated())
            {
                string actions = string.Join(Environment.NewLine, c.Actions.Select((a, i) => (i + 1) + ". " + a));
                string prompt = (scheduled ? "По расписанию: выполнить" : "Выполнить") + " команду «" + c.Name + "»?\n\n" + actions + "\n\nПрограммы и скрипты могут получить права Клода, включая права администратора. Выполняй только доверенные действия.";
                if (MessageBox.Show(_assistantWindow ?? this, prompt, "Подтверждение своей команды", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) { AssistantVoiceStatus.Text = "Команда отменена."; return; }
            }
            if (_isClosing || !_experimentalModel) return;
            cancellation = new CancellationTokenSource(); _customCommandCancellation = cancellation;
            CustomStopButton.IsEnabled = true;
            AssistantVoiceStatus.Text = "Выполняется: " + c.Name;
            await CustomCommandExecutor.RunAsync(c.Actions, RunCustomActionAsync, cancellation.Token);
            if (!_isClosing) AssistantVoiceStatus.Text = "Цепочка выполнена: " + c.Name;
        }
        catch (OperationCanceledException) { if (!_isClosing) AssistantVoiceStatus.Text = "Цепочка остановлена. Уже запущенные программы и команды не отменены."; }
        catch (Exception ex) { if (!_isClosing) AssistantVoiceStatus.Text = "Действия остановлены: " + ex.Message; }
        finally { if (ReferenceEquals(_customCommandCancellation, cancellation)) _customCommandCancellation = null; cancellation?.Dispose(); _customCommandPending = false; if (!_isClosing) CustomStopButton.IsEnabled = false; }
    }
    private void CancelCustomCommand_Click(object sender, RoutedEventArgs e) => _customCommandCancellation?.Cancel();
    private Task RunCustomActionAsync(CustomAction action, CancellationToken token)
    {
        if (_isClosing || !_experimentalModel) throw new OperationCanceledException(token);
        return CustomCommandActions.PerformAsync(action, token);
    }
}
