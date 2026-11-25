using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DesktopPet;

public partial class MainWindow
{
    private static readonly HttpClient CloudHttp = new() { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 524288 };
    private CancellationTokenSource? _cloudCancellation;
    private CloudVoiceRecorder? _cloudRecorder;
    private bool _screenRequest;
    private void UpdateAssistantBadge()
    {
        bool listening = _voiceSession != null || (_cloudCancellation != null && _cloudRecorder != null);
        bool working = _cloudCancellation != null;
        AssistantBadge.Visibility = listening || working ? Visibility.Visible : Visibility.Collapsed;
        AssistantBadgeText.Text = listening ? "● МИКРОФОН" : _screenRequest ? "● ЭКРАН → ИИ" : "● ИИ РАБОТАЕТ";
    }
    private static string CloudKeyPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeDesktopPet", "groq-key.dat");

    private void SaveCloudKey_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        var key = AssistantApiKey.Password.Trim();
        if (key.Length < 8 || key.Length > 256 || key.Contains('\n') || key.Contains('\r')) { AssistantCloudStatus.Text = "Введи корректный ключ Groq."; return; }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CloudKeyPath)!);
            File.WriteAllBytes(CloudKeyPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser));
            AssistantApiKey.Clear();
            AssistantCloudStatus.Text = "Ключ сохранён для этой учётной записи Windows. Проверь, что тариф Groq бесплатный.";
        }
        catch { AssistantCloudStatus.Text = "Не удалось сохранить ключ в защищённом хранилище Windows."; }
    }
    private void DeleteCloudKey_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        StopCloud();
        try { File.Delete(CloudKeyPath); AssistantApiKey.Clear(); AssistantCloudStatus.Text = "Сохранённый ключ удалён с этого ПК."; }
        catch { AssistantCloudStatus.Text = "Не удалось удалить ключ."; }
    }
    private void StopCloud()
    {
        var cts = _cloudCancellation;
        _cloudCancellation = null;
        _screenRequest = false;
        if (cts != null) { cts.Cancel(); cts.Dispose(); }
        _cloudRecorder?.Stop();
        UpdateAssistantBadge();
        AssistantCloudButton.Content = "Спросить ИИ";
        AssistantScreenButton.Content = "Спросить ИИ об экране";
        AssistantCloudVoiceButton.Content = "Сказать задачу ИИ";
        AssistantCloudStatus.Text = "ИИ не запущен";
    }
    private void AskCloud_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        if (_cloudCancellation != null) { StopCloud(); return; }
        if (_voiceSession != null) { AssistantCloudStatus.Text = "Сначала останови локальное прослушивание."; return; }
        string question = AssistantCloudText.Text.Trim();
        if (string.IsNullOrWhiteSpace(question)) { AssistantCloudStatus.Text = "Напиши задачу."; return; }
        _ = RunCloudAsync(question, false);
    }
    private async void AskScreen_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        if (_cloudCancellation != null) { StopCloud(); return; }
        if (_voiceSession != null) { AssistantCloudStatus.Text = "Сначала останови локальное прослушивание."; return; }
        string question = AssistantCloudText.Text.Trim();
        if (string.IsNullOrWhiteSpace(question)) { AssistantCloudStatus.Text = "Напиши, что посмотреть на экране."; return; }
        string key;
        try { key = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(CloudKeyPath), null, DataProtectionScope.CurrentUser)); }
        catch { AssistantCloudStatus.Text = "Сначала сохрани ключ бесплатного аккаунта Groq."; return; }
        var cts = new CancellationTokenSource();
        _cloudCancellation = cts;
        _screenRequest = true;
        UpdateAssistantBadge();
        AssistantCloudButton.Content = "Остановить запрос";
        AssistantScreenButton.Content = "Остановить просмотр";
        AssistantCloudStatus.Text = "Делаю и отправляю снимок основного экрана в Groq…";
        try
        {
            // Hide the menu so it doesn't cover the requested screen; give the popup time to close.
            SettingsPopup.IsOpen = false;
            await Task.Delay(250, cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            byte[] image = ScreenCapture.CapturePrimaryJpeg();
            string answer = await ScreenVision.AskAsync(CloudHttp, key, question, image, cts.Token);
            if (!ReferenceEquals(_cloudCancellation, cts) || _isClosing || !_experimentalModel) return;
            _reminderHoldUntil = DateTime.MinValue; _lyricsHoldUntil = DateTime.UtcNow.AddSeconds(8);
            ShowSpeech(answer); AssistantCloudStatus.Text = answer;
        }
        catch (OperationCanceledException) { if (ReferenceEquals(_cloudCancellation, cts)) AssistantCloudStatus.Text = "Просмотр остановлен или истекло время ожидания."; }
        catch (Exception ex) { if (ReferenceEquals(_cloudCancellation, cts)) AssistantCloudStatus.Text = ex is InvalidOperationException ? ex.Message : "Не удалось снять экран или связаться с Groq."; }
        finally
        {
            if (ReferenceEquals(_cloudCancellation, cts)) { _cloudCancellation = null; _screenRequest = false; cts.Dispose(); UpdateAssistantBadge(); AssistantCloudButton.Content = "Спросить ИИ"; AssistantScreenButton.Content = "Спросить ИИ об экране"; }
        }
    }
    private void CloudVoice_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        if (_cloudRecorder != null) { _cloudRecorder.Stop(); return; }
        if (_cloudCancellation != null) return;
        if (_voiceSession != null) { AssistantCloudStatus.Text = "Сначала останови локальное прослушивание."; return; }
        _ = RunCloudAsync(null, true);
    }
    private async Task RunCloudAsync(string? question, bool recordVoice)
    {
        string key;
        try { key = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(CloudKeyPath), null, DataProtectionScope.CurrentUser)); }
        catch { AssistantCloudStatus.Text = "Сначала сохрани ключ бесплатного аккаунта Groq."; return; }
        var cts = new CancellationTokenSource();
        _cloudCancellation = cts;
        UpdateAssistantBadge();
        AssistantCloudButton.Content = "Остановить запрос";
        try
        {
            if (recordVoice)
            {
                AssistantCloudStatus.Text = "Микрофон включён: говори (до 15 секунд). Нажми ещё раз, чтобы закончить.";
                AssistantCloudVoiceButton.Content = "Завершить запись";
                using (var recorder = new CloudVoiceRecorder())
                {
                    _cloudRecorder = recorder;
                    UpdateAssistantBadge();
                    byte[] audio;
                    try { audio = await recorder.RecordAsync(cts.Token); }
                    finally { if (ReferenceEquals(_cloudRecorder, recorder)) { _cloudRecorder = null; UpdateAssistantBadge(); } }
                    if (!ReferenceEquals(_cloudCancellation, cts)) return;
                    AssistantCloudVoiceButton.Content = "Сказать задачу ИИ";
                    AssistantCloudStatus.Text = "Отправляю голос в Groq для распознавания…";
                    question = await CloudSpeech.TranscribeAsync(CloudHttp, key, audio, cts.Token);
                    if (!ReferenceEquals(_cloudCancellation, cts)) return;
                    AssistantCloudText.Text = question;
                }
            }
            AssistantCloudStatus.Text = "Запрос отправлен в Groq…";
            var answer = await CloudAssistant.AskAsync(CloudHttp, key, question!, async (name, arguments, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                if (!_experimentalModel || _isClosing || !ReferenceEquals(_cloudCancellation, cts)) return "Действие отменено.";
                // AskAsync starts on the UI thread and resumes there; do not execute without the UI thread.
                if (!Dispatcher.CheckAccess()) throw new InvalidOperationException();
                switch (name)
                {
                    case "open_youtube":
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.youtube.com/") { UseShellExecute = true });
                        return "Открыта главная страница YouTube. Видео не выбиралось.";
                    case "open_telegram":
                        string[] candidates = {
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Telegram Desktop", "Telegram.exe"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Telegram Desktop", "Telegram.exe"),
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Telegram Desktop", "Telegram.exe")
                        };
                        string? telegram = Array.Find(candidates, File.Exists);
                        if (telegram == null) return "Telegram Desktop не найден в стандартных папках. Приложение не запущено.";
                        var launch = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = true };
                        launch.ArgumentList.Add(telegram);
                        if (System.Diagnostics.Process.Start(launch) == null) return "Не удалось передать Telegram на запуск.";
                        return "Windows получила запрос запустить Telegram Desktop; запуск не подтверждён.";
                    case "create_presentation":
                        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        if (string.IsNullOrWhiteSpace(documents) || !Path.IsPathFullyQualified(documents)) return "Не удалось найти папку Документы. Презентация не создана.";
                        using (var parsed = System.Text.Json.JsonDocument.Parse(arguments))
                        {
                            ct.ThrowIfCancellationRequested();
                            var folder = Path.Combine(documents, "CacheTrim", "Презентации");
                            var file = PresentationBuilder.Create(parsed.RootElement, folder);
                            return "Создана редактируемая текстовая презентация в Документы/CacheTrim/Презентации: " + Path.GetFileName(file) + ". Проверь факты: текст подготовлен ИИ.";
                        }
                    case "get_time": return "Местная дата и время: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                    case "pet_jump": ExecuteAssistantCommand(PetVoiceCommand.Jump); return "Клод прыгнул.";
                    case "pet_secret": ExecuteAssistantCommand(PetVoiceCommand.Secret); return "Клод принёс секрет.";
                    case "open_downloads":
                        var start = new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = false };
                        start.ArgumentList.Add("shell:Downloads");
                        System.Diagnostics.Process.Start(start);
                        return "Открыта папка загрузок.";
                    default: return "Действие недоступно.";
                }
            }, cts.Token);
            if (!ReferenceEquals(_cloudCancellation, cts) || _isClosing || !_experimentalModel) return;
            _reminderHoldUntil = DateTime.MinValue; _lyricsHoldUntil = DateTime.UtcNow.AddSeconds(8);
            ShowSpeech(answer); AssistantCloudStatus.Text = answer;
        }
        catch (OperationCanceledException) { if (ReferenceEquals(_cloudCancellation, cts)) AssistantCloudStatus.Text = "Запрос остановлен или истекло время ожидания."; }
        catch (Exception ex) { if (ReferenceEquals(_cloudCancellation, cts)) AssistantCloudStatus.Text = ex is InvalidOperationException ? ex.Message : "Не удалось связаться с Groq. Проверь интернет и ключ."; }
        finally
        {
            if (ReferenceEquals(_cloudCancellation, cts)) { _cloudCancellation = null; cts.Dispose(); UpdateAssistantBadge(); AssistantCloudButton.Content = "Спросить ИИ"; AssistantCloudVoiceButton.Content = "Сказать задачу ИИ"; }
        }
    }
}
