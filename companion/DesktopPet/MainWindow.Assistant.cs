using System;
using System.Windows;

namespace DesktopPet;

public partial class MainWindow
{
    private void StopVoice()
    {
        var old = _voiceSession;
        _voiceSession = null; // Invalidate callbacks before stopping the engine.
        try { old?.Dispose(); } catch (Exception) { /* shutdown must remain safe */ }
        AssistantListenButton.Content = "Слушать одну команду";
        AssistantVoiceStatus.Text = "Микрофон выключен";
        UpdateAssistantBadge();
    }

    private void AssistantListen_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        if (_wakeEnabled) { AssistantVoiceStatus.Text = "Сначала выключи вызов «Джарвис»."; return; }
        if (_voiceSession != null) { StopVoice(); return; }
        if (_cloudCancellation != null) { AssistantVoiceStatus.Text = "Сначала останови облачный запрос."; return; }
        if (MessageBox.Show(this,
            "Включить микрофон для одной команды? Постоянного прослушивания нет. Запись не сохраняется и не отправляется в облако; фраза распознаётся локальной моделью Vosk.",
            "Голосовые команды Клода V2", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        VoiceCommandSession? session = null;
        try
        {
            session = new VoiceCommandSession((text, confidence, error) =>
            {
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
                try { Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_isClosing || !_experimentalModel || !ReferenceEquals(_voiceSession, session)) return;
                    StopVoice();
                    if (error != null) { AssistantVoiceStatus.Text = error; return; }
                    var command = VoiceCommandCatalog.Parse(text, confidence);
                    if (command == PetVoiceCommand.None) { AssistantVoiceStatus.Text = "Команда не распознана. Попробуй ещё раз или введи текст."; return; }
                    AssistantVoiceStatus.Text = "Распознано: " + text;
                    ExecuteAssistantCommand(command);
                })); } catch (InvalidOperationException) { }
            });
            _voiceSession = session;
            UpdateAssistantBadge();
            AssistantVoiceStatus.Text = "Слушаю одну команду…";
            AssistantListenButton.Content = "Остановить прослушивание";
            session.Start();
        }
        catch (Exception ex)
        {
            StopVoice();
            AssistantVoiceStatus.Text = ex is InvalidOperationException
                ? ex.Message : "Не удалось включить распознавание. Проверь микрофон, разрешения и папку VoiceModel рядом с приложением; можно вводить команды текстом.";
        }
    }

    private void AssistantText_Click(object sender, RoutedEventArgs e)
    {
        if (!_experimentalModel || _isClosing) return;
        var command = VoiceCommandCatalog.Parse(AssistantCommandText.Text);
        if (command == PetVoiceCommand.None) { AssistantVoiceStatus.Text = "Нет такой команды. Выбери фразу из списка."; return; }
        AssistantVoiceStatus.Text = "Команда выполнена.";
        ExecuteAssistantCommand(command);
    }

    private void ExecuteAssistantCommand(PetVoiceCommand command)
    {
        if (!_experimentalModel || _isClosing) return;
        _reminderHoldUntil = DateTime.MinValue;
        _lyricsHoldUntil = DateTime.UtcNow.AddSeconds(6);
        switch (command)
        {
            case PetVoiceCommand.Hello: ShowSpeech("Привет! Я Клод V2."); break;
            case PetVoiceCommand.Time: ShowSpeech("Сейчас " + DateTime.Now.ToString("HH:mm")); break;
            case PetVoiceCommand.Date: ShowSpeech("Сегодня " + DateTime.Now.ToString("dd.MM.yyyy")); break;
            case PetVoiceCommand.Jump: Jump_Click(this, new RoutedEventArgs()); ShowSpeech("Прыгаю!"); break;
            case PetVoiceCommand.Secret: BringSecret_Click(this, new RoutedEventArgs()); break;
            case PetVoiceCommand.Downloads: OpenDownloads_Click(this, new RoutedEventArgs()); break;
            case PetVoiceCommand.Desktop: ShowDesktop_Click(this, new RoutedEventArgs()); break;
        }
    }
}
