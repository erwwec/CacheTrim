using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DesktopPet;

public partial class MainWindow
{
    private WakeWordSession? _wakeSession;
    private bool _wakeEnabled;
    private bool _wakeBusy;

    private void WakeWord_Click(object sender, RoutedEventArgs e)
    {
        if (_wakeEnabled) { DisableWakeWord(); return; }
        if (!_experimentalModel || _isClosing) return;
        if (_cloudCancellation != null || _voiceSession != null) { AssistantCloudStatus.Text = "Сначала останови текущую задачу и прослушивание."; return; }
        bool elevated;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            _ = LoadKey(SelectedAiProvider);
            if (!LocalSpeech) _ = LoadKey(AiProvider.Groq);
        }
        catch (InvalidOperationException ex) { AssistantCloudStatus.Text = ex.Message; return; }
        catch { AssistantCloudStatus.Text = "Не удалось проверить права процесса; голосовой вызов не включён."; return; }
        if (MessageBox.Show(ComputerSafety.AdministratorWarning(elevated) + "Пока режим включён, микрофон постоянно слушает слово «Джарвис» локально. Возможны ложные срабатывания. После каждого срабатывания приложение запишет до 15 секунд. " + (LocalSpeech ? "Речь распознаётся локально. " : "Запись отправляется в Groq для распознавания. ") + "Текст и снимки экрана могут отправляться в " + AiProviderConfig.Name(SelectedAiProvider) + ". Работа с ПК может идти автоматически. Не оставляй режим без присмотра; закрой личные данные. Режим выключается кнопкой и не включается автоматически после перезапуска. Включить?", "Голосовой вызов Джарвиса", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _wakeEnabled = true; StartWakeEngine();
    }

    private void StartWakeEngine(bool reportStatus = true)
    {
        if (!_wakeEnabled || _wakeBusy || _wakeSession != null || _isClosing || !_experimentalModel) return;
        WakeWordSession? session = null;
        try
        {
            session = new WakeWordSession(recognized =>
            {
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
                try { Dispatcher.BeginInvoke(new Action(async () =>
                {
                    if (!ReferenceEquals(_wakeSession, session) || !_wakeEnabled || _isClosing) return;
                    SuspendWakeEngine();
                    if (!recognized)
                    {
                        DisableWakeWord(); AssistantCloudStatus.Text = "Прослушивание «Джарвис» отключилось. Проверь микрофон и файлы локальной модели."; return;
                    }
                    if (_cloudCancellation != null || _voiceSession != null)
                    {
                        AssistantCloudStatus.Text = "Джарвис услышан, но уже выполняется другая задача."; StartWakeEngine(false); return;
                    }
                    _wakeBusy = true;
                    try
                    {
                        AssistantCloudStatus.Text = "Джарвис услышан. Скажи задачу после сигнала.";
                        await Task.Delay(350);
                        if (_wakeEnabled && !_isClosing && _experimentalModel) await RunComputerTaskAsync(true, true);
                    }
                    catch { AssistantCloudStatus.Text = "Не удалось запустить голосовую задачу. Проверь микрофон и попробуй снова."; }
                    finally
                    {
                        _wakeBusy = false;
                        if (_wakeEnabled && !_isClosing && _experimentalModel) StartWakeEngine(false);
                    }
                })); } catch (InvalidOperationException) { }
            });
            _wakeSession = session; session.Start();
            WakeWordButton.Content = "Выключить вызов «Джарвис»";
            if (reportStatus) AssistantCloudStatus.Text = "Микрофон слушает слово «Джарвис».";
            UpdateAssistantBadge();
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_wakeSession, session)) _wakeSession = null;
            try { session?.Dispose(); } catch { }
            _wakeEnabled = false; WakeWordButton.Content = "Включить вызов «Джарвис»"; UpdateAssistantBadge();
            AssistantCloudStatus.Text = ex is InvalidOperationException ? ex.Message : "Не удалось запустить вызов. Проверь микрофон, разрешения и папку VoiceModel.";
        }
    }
    private void SuspendWakeEngine()
    {
        var old = _wakeSession; _wakeSession = null;
        try { old?.Dispose(); } catch { }
        UpdateAssistantBadge();
    }
    private void DisableWakeWord()
    {
        _wakeEnabled = false; SuspendWakeEngine();
        WakeWordButton.Content = "Включить вызов «Джарвис»";
        AssistantCloudStatus.Text = "Голосовой вызов выключен.";
    }
    private async Task RunManualComputerTaskAsync(bool voice)
    {
        if (_wakeBusy) { AssistantCloudStatus.Text = "Голосовой вызов уже занят задачей."; return; }
        _wakeBusy = true; SuspendWakeEngine();
        try { await RunComputerTaskAsync(voice, false); }
        finally { _wakeBusy = false; if (_wakeEnabled && !_isClosing && _experimentalModel) StartWakeEngine(false); }
    }
}
