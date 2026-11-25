using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DesktopPet;

public partial class MainWindow
{
    private async void ComputerControl_Click(object sender, RoutedEventArgs e) => await RunManualComputerTaskAsync(false);
    private async void ComputerControlVoice_Click(object sender, RoutedEventArgs e) => await RunManualComputerTaskAsync(true);

    private async Task RunComputerTaskAsync(bool recordVoice, bool fromWake)
    {
        if (!_experimentalModel || _isClosing) return;
        if (_cloudCancellation != null) { AssistantCloudStatus.Text = "Сначала останови текущий запрос."; return; }
        if (_voiceSession != null) { AssistantCloudStatus.Text = "Сначала останови прослушивание."; return; }
        // CacheTrim normally runs elevated. Never grant experimental control its admin token.
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            { AssistantCloudStatus.Text = "Управление запрещено из программы с правами администратора. Запусти DesktopPet.exe напрямую без администратора и выбери Клод V2."; return; }
        }
        catch { AssistantCloudStatus.Text = "Не удалось проверить права процесса; управление заблокировано."; return; }
        bool automatic = AssistantAutoControl.IsChecked == true;
        bool voiceStop = AssistantVoiceStop.IsChecked == true;
        string goal = AssistantCloudText.Text.Trim();
        if (!recordVoice && (goal.Length == 0 || goal.Length > 1000)) { AssistantCloudStatus.Text = "Напиши задачу до 1000 символов."; return; }
        string key;
        try { key = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(CloudKeyPath), null, DataProtectionScope.CurrentUser)); }
        catch { AssistantCloudStatus.Text = "Сначала сохрани ключ Groq."; return; }
        string modeWarning = automatic
            ? "АВТОМАТИЧЕСКИЙ РЕЖИМ: после запуска ИИ будет нажимать, вводить текст и запускать приложения без подтверждения каждого шага. Он может ошибиться, изменить или отправить данные. Фильтр опасных действий НЕ надёжен. Следи за работой и используй кнопку остановки. "
            : "Каждый шаг потребует подтверждения. ";
        var consent = fromWake ? MessageBoxResult.Yes : MessageBox.Show((recordVoice ? "Голосовая запись до 15 секунд будет отправлена в Groq для распознавания. " : "") + "На каждом шаге снимок ОСНОВНОГО экрана целиком будет отправлен в Groq, включая видимые личные данные. " + modeWarning + (voiceStop ? "Во время задачи микрофон непрерывно слушает команды «стоп» через локальный Vosk; аудио этого режима приложение в Groq не отправляет. Голосовая остановка может не сработать. " : "Голосовая остановка выключена; доступна кнопка. ") + "Не используй пароли, коды или платёжные данные. Закрой лишние окна. Продолжить?", "Управление компьютером", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (consent != MessageBoxResult.Yes) return;
        var cts = new CancellationTokenSource(); _cloudCancellation = cts; UpdateAssistantBadge();
        var panel = new ComputerControlPanel(() => { if (!cts.IsCancellationRequested) cts.Cancel(); });
        var previous = new List<string>();
        EmergencyStopSession? stopSession = null;
        bool restoreAssistant = HideAssistantForScreenCapture();
        int voiceStopOutcome = 0; // 1 = spoken stop; 2 = recognizer unexpectedly failed
        try
        {
            SettingsPopup.IsOpen = false;
            if (recordVoice)
            {
                panel.Processing("Микрофон включён: скажи задачу целиком (до 15 секунд). Можно закончить запись кнопкой."); panel.Show();
                using (var recorder = new CloudVoiceRecorder())
                {
                    _cloudRecorder = recorder; UpdateAssistantBadge();
                    panel.SetRecordingAction(recorder.Stop);
                    byte[] audio;
                    try
                    {
                        var recording = recorder.RecordAsync(cts.Token);
                        if (fromWake) { ShowSpeech("Слушаю задачу"); try { System.Media.SystemSounds.Asterisk.Play(); } catch { /* sound is optional */ } }
                        audio = await recording;
                    }
                    finally { panel.SetRecordingAction(null); if (ReferenceEquals(_cloudRecorder, recorder)) { _cloudRecorder = null; UpdateAssistantBadge(); } }
                    cts.Token.ThrowIfCancellationRequested();
                    panel.Processing("Отправляю запись в Groq для распознавания…");
                    goal = await CloudSpeech.TranscribeAsync(CloudHttp, key, audio, cts.Token);
                }
                cts.Token.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(goal) || goal.Length > 1000) throw new InvalidOperationException("Голосовая задача пуста или слишком длинная.");
                if (MessageBox.Show("Распознанная задача:\n\n" + goal + "\n\nВыполнять именно её?", "Проверь распознавание", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    throw new OperationCanceledException();
                AssistantCloudText.Text = goal;
            }
            if (voiceStop)
            {
                try
                {
                    stopSession = new EmergencyStopSession(recognized =>
                    {
                        Interlocked.CompareExchange(ref voiceStopOutcome, recognized ? 1 : 2, 0);
                        try { cts.Cancel(); } catch (ObjectDisposedException) { }
                    });
                    stopSession.Start();
                    _computerStopListening = true;
                }
                catch (Exception ex)
                {
                    try { stopSession?.Dispose(); } catch { }
                    stopSession = null;
                    throw new InvalidOperationException(ex is InvalidOperationException ? ex.Message : "Не удалось включить голосовой «стоп». Задача не запущена; проверь микрофон или выключи голосовую остановку и используй кнопку.");
                }
            }
            panel.SetStopListeningStatus(_computerStopListening);
            cts.Token.ThrowIfCancellationRequested();
            _screenRequest = true; UpdateAssistantBadge();
            panel.Processing("Переключись на нужное окно в течение 5 секунд. Потом можно остановить задачу здесь.");
            if (!panel.IsVisible) panel.Show();
            await Task.Delay(5000, cts.Token);
            for (int i = 0; i < ComputerAgent.MaxSteps; i++)
            {
                cts.Token.ThrowIfCancellationRequested();
                panel.Hide(); await Task.Delay(300, cts.Token);
                IntPtr target = ComputerInput.GetForegroundWindow();
                if (target == IntPtr.Zero || ComputerInput.IsOurWindow(target)) throw new InvalidOperationException("Выбери нужное окно перед запуском задачи и попробуй ещё раз.");
                var screenSize = ScreenCapture.PrimarySize();
                byte[] screenshot = ScreenCapture.CapturePrimaryJpeg();
                panel.Processing("Шаг " + (i + 1) + ": снимок отправляется в Groq. Ожидаю предложение ИИ…"); panel.Show();
                var step = await ComputerAgent.NextAsync(CloudHttp, key, goal, previous, screenshot, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                if (step.Action is "done" or "ask_user")
                {
                    AssistantCloudStatus.Text = (step.Action == "done" ? "Ответ ИИ (проверь результат): " : "Нужно участие пользователя: ") + step.Reason;
                    ShowSpeech(AssistantCloudStatus.Text); return;
                }
                bool review = !automatic || ComputerSafety.NeedsReview(step);
                bool approved = await panel.WaitForActionAsync(step, screenshot, review, cts.Token);
                if (!approved) throw new OperationCanceledException();
                cts.Token.ThrowIfCancellationRequested();
                panel.Hide();
                if (!ComputerInput.IsWindow(target)) throw new InvalidOperationException("Исходное окно больше недоступно. Задача остановлена.");
                if (review)
                {
                    if (!ComputerInput.SetForegroundWindow(target)) throw new InvalidOperationException("Не удалось вернуться к исходному окну. Задача остановлена.");
                }
                else if (ComputerInput.GetForegroundWindow() != target)
                    throw new InvalidOperationException("Активное окно изменилось. Автоматическая задача остановлена.");
                await Task.Delay(200, cts.Token);
                if (ComputerInput.GetForegroundWindow() != target) throw new InvalidOperationException("Активное окно изменилось. Задача остановлена.");
                if (screenSize != ScreenCapture.PrimarySize()) throw new InvalidOperationException("Разрешение экрана изменилось. Задача остановлена.");
                if (step.Action is "click" or "double_click" or "scroll")
                {
                    byte[] current = ScreenCapture.CapturePrimaryJpeg();
                    if (!ScreenCapture.ClickAreaStable(screenshot, current, step.X, step.Y))
                        throw new InvalidOperationException("Место действия изменилось после снимка. Задача остановлена; запусти её снова.");
                }
                cts.Token.ThrowIfCancellationRequested();
                if (ComputerInput.GetForegroundWindow() != target) throw new InvalidOperationException("Активное окно изменилось. Задача остановлена.");
                string outcome;
                if (step.Action is "open_youtube" or "open_telegram" or "set_volume")
                    outcome = ComputerActions.Execute(step);
                else
                {
                    ComputerInput.Execute(step);
                    outcome = "Input sent: " + step.Action + (step.Action == "key" ? " " + step.Key : "");
                }
                previous.Add("Step " + (i + 1) + (review ? ": manually approved. " : ": automatic after task consent. ") + outcome + " Untrusted suggested intent: " + step.Reason);
                await Task.Delay(1000, cts.Token);
            }
            AssistantCloudStatus.Text = "Достигнут предел 20 шагов. Проверь результат и при необходимости запусти новую задачу.";
        }
        catch (OperationCanceledException) { AssistantCloudStatus.Text = (Volatile.Read(ref voiceStopOutcome) == 1 ? "Распознано «стоп». " : Volatile.Read(ref voiceStopOutcome) == 2 ? "Голосовая остановка отключилась, поэтому задача остановлена. " : "") + "Управление остановлено. Уже выполненные действия не отменяются."; }
        catch (Exception ex) { AssistantCloudStatus.Text = ex is InvalidOperationException ? ex.Message : "Не удалось продолжить управление. Проверь окно и доступность Groq."; }
        finally
        {
            try { stopSession?.Dispose(); } catch { }
            _computerStopListening = false;
            if (!panel.IsClosed) panel.Close();
            if (ReferenceEquals(_cloudCancellation, cts)) { _cloudCancellation = null; _screenRequest = false; }
            UpdateAssistantBadge();
            cts.Dispose();
            RestoreAssistantAfterScreenCapture(restoreAssistant);
        }
    }
}
