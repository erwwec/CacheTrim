using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace DesktopPet;
internal sealed partial class ActionPickerWindow : Window
{
    private sealed record Choice(string Category, string Title, string Type, string Description, string Hint, string Preset = "", string Browse = "")
    { public string Display => Category + "  ·  " + Title; }
    private static readonly Choice[] Choices = {
        new("Запуск", "Запустить программу", "app", "Запустить локальную программу EXE.", "Полный путь к программе.", Browse: "app"),
        new("Запуск", "Открыть файл", "file", "Открыть локальный файл в приложении по умолчанию.", "Полный путь к файлу.", Browse: "file"),
        new("Запуск", "Открыть папку", "folder", "Открыть локальную папку.", "Полный путь к папке.", Browse: "folder"),
        new("Запуск", "Открыть ссылку", "url", "Открыть ссылку в браузере по умолчанию.", "Адрес http или https."),
        new("Запуск", "Поиск в интернете", "search", "Открыть результаты поиска Google; запрос передаётся Google.", "Поисковый запрос."),
        new("Запуск", "Первый результат поиска", "search_first", "Попытаться открыть первый результат через Google «Мне повезёт». Google может показать обычную страницу поиска или проверку вместо перехода.", "Поисковый запрос передаётся Google."),
        new("Запуск", "Команда CMD", "cmd", "Выполнить текст в CMD с правами Клода. Используй только доверенные команды.", "Команда в одной строке."),
        new("Запуск", "PowerShell", "powershell", "Выполнить текст PowerShell с правами Клода. Используй только доверенные команды.", "Команда в одной строке."),
        new("Цепочка", "Пауза", "delay", "Подождать до следующего действия; паузу можно отменить.", "Целое число секунд от 1 до 3600."),
        new("Цепочка", "Звук", "sound", "Проиграть WAV или MP3 до 10 минут.", "Путь к звуковому файлу.", Browse: "sound"),
        new("Клавиши", "Сочетание клавиш", "keys", "После подтверждения переключись на нужное окно. Клавиши отправляются активному окну через 5 секунд.", "Например Ctrl+Shift+S, F11 или Alt+Tab."),
        new("Клавиши", "Копировать", "keys", "Отправить Ctrl+C в выбранное активное окно после отсчёта.", "", "Ctrl+C"),
        new("Клавиши", "Вставить", "keys", "Отправить Ctrl+V в выбранное активное окно после отсчёта.", "", "Ctrl+V"),
        new("Клавиши", "Отменить ввод", "keys", "Отправить Ctrl+Z в выбранное активное окно после отсчёта.", "", "Ctrl+Z"),
        new("Окна", "Закрыть окно", "window", "Запросить закрытие выбранного активного окна. Приложение может спросить о сохранении.", "", "close"),
        new("Окна", "Свернуть окно", "window", "Свернуть выбранное активное окно после отсчёта.", "", "minimize"),
        new("Окна", "Развернуть окно", "window", "Развернуть выбранное активное окно после отсчёта.", "", "maximize"),
        new("Окна", "Восстановить окно", "window", "Восстановить размер выбранного активного окна после отсчёта.", "", "restore"),
        new("Окна", "На весь экран (F11)", "keys", "Отправить F11 выбранному окну. Работает только в приложениях, поддерживающих F11.", "", "F11"),
        new("Окна", "Другое окно (Alt+Tab)", "keys", "Переключить окно через Alt+Tab после отсчёта.", "", "Alt+Tab")
    };
    internal CustomAction? Result { get; private set; }
    internal ActionPickerWindow(Window owner) { Owner = owner; InitializeComponent(); FilterChanged(this, null!); }
    private void HeaderMouseDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) DragMove(); }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void FilterChanged(object sender, TextChangedEventArgs e)
    {
        if (_choices == null || _filter == null) return;
        string q = _filter.Text.Trim();
        _choices.ItemsSource = Choices.Where(c => q.Length == 0 || c.Display.Contains(q, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        _choices.SelectedIndex = _choices.Items.Count > 0 ? 0 : -1;
    }
    private void ChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_description == null || _parameter == null) return;
        var c = _choices.SelectedItem as Choice;
        _description.Text = c?.Description ?? "Действие не найдено."; _hint.Text = c?.Hint ?? ""; _parameter.Text = "";
        bool show = c != null && c.Preset.Length == 0;
        _parameter.Visibility = _parameterLabel.Visibility = _hint.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        _browse.Visibility = show && c!.Browse.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _add.IsEnabled = c != null; _error.Text = "";
    }
    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        if (_choices.SelectedItem is not Choice c) return;
        if (c.Browse == "folder") { var folder = new OpenFolderDialog { Title = "Выбери папку" }; if (folder.ShowDialog(this) == true) _parameter.Text = folder.FolderName; return; }
        string filter = c.Browse switch { "app" => "Программы (*.exe)|*.exe", "sound" => "Звуковые файлы (*.wav;*.mp3)|*.wav;*.mp3", _ => "Все файлы (*.*)|*.*" };
        var dialog = new OpenFileDialog { Filter = filter }; if (dialog.ShowDialog(this) == true) _parameter.Text = dialog.FileName;
    }
    private CustomAction BuildAction()
    {
        if (_choices.SelectedItem is not Choice c) throw new InvalidOperationException("Выбери действие.");
        var candidate = new CustomAction(c.Type, c.Preset.Length > 0 ? c.Preset : _parameter.Text.Trim());
        return CustomCommands.Validate(new[] { new CustomCommand("Проверка", new() { "проверка действия" }, true, new() { candidate }) })[0].Actions[0];
    }
    private void AddClick(object sender, RoutedEventArgs e)
    {
        if (_choices.SelectedItem is not Choice c) return;
        try
        {
            Result = BuildAction();
            DialogResult = true;
        }
        catch (Exception ex) { _error.Text = ex.Message; }
    }
}
