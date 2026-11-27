using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace DesktopPet;

internal sealed partial class CustomCommandsWindow : Window
{
    // Templates are inert until the user explicitly saves them as a custom command.
    private static readonly CustomCommand[] Templates = {
        new("Яндекс", new() { "открой яндекс" }, true, new() { new("url", "https://yandex.ru/") }),
        new("YouTube", new() { "открой ютуб" }, true, new() { new("url", "https://www.youtube.com/") }),
        new("ВКонтакте", new() { "открой вконтакте" }, true, new() { new("url", "https://vk.com/") }),
        new("Переводчик", new() { "открой переводчик" }, true, new() { new("url", "https://translate.google.com/") }),
        new("Google", new() { "открой гугл" }, true, new() { new("url", "https://www.google.com/") }),
        new("Закрыть окно", new() { "закрой окно" }, true, new() { new("window", "close") }),
        new("На весь экран (F11)", new() { "на весь экран" }, true, new() { new("keys", "F11") }),
        new("Развернуть окно", new() { "разверни окно" }, true, new() { new("window", "maximize") }),
        new("Нормальное окно", new() { "восстанови окно" }, true, new() { new("window", "restore") }),
        new("Свернуть окно", new() { "сверни окно" }, true, new() { new("window", "minimize") }),
        new("Вставить", new() { "вставь" }, true, new() { new("keys", "Ctrl+V") }),
        new("Скопировать", new() { "скопируй" }, true, new() { new("keys", "Ctrl+C") }),
        new("Отменить ввод", new() { "отмени ввод" }, true, new() { new("keys", "Ctrl+Z") }),
        new("Другое окно", new() { "другое окно" }, true, new() { new("keys", "Alt+Tab") })
    };
    private readonly string _path;
    private List<CustomCommand> _commands;
    private int _editing = -1;
    private bool _loading;
    private string _pendingType = "url";

    internal CustomCommandsWindow(Window owner, string path, List<CustomCommand> commands)
    {
        _path = path;
        _commands = commands.ToList();
        Owner = owner;
        InitializeComponent();
        RefreshList();
        ClearEditor();
        ShowEditor();
    }

    private void RefreshList()
    {
        _loading = true;
        string query = _searchCommands?.Text.Trim() ?? "";
        var shown = _commands.Where(c => query.Length == 0 || c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || c.Phrases.Any(p => p.Contains(query, StringComparison.CurrentCultureIgnoreCase)) || c.OptionalPhrases.Any(p => p.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToList();
        _list.ItemsSource = null;
        _list.ItemsSource = shown;
        _templates.ItemsSource = Templates.Where(c => query.Length == 0 || c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || c.Phrases.Any(p => p.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        EmptyCommands.Visibility = shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTitle.Text = _commands.Count == 0 ? "Пока нет своих команд" : "Ничего не найдено";
        EmptyHint.Text = _commands.Count == 0 ? "Создай команду и добавь действия" : "Измени поисковый запрос";
        _loading = false;
    }

    private void SearchCommands_Changed(object sender, TextChangedEventArgs e) { if (!_loading && _list != null && _templates != null && EmptyCommands != null) RefreshList(); }

    private void ClearEditor()
    {
        _loading = true;
        _editing = -1;
        _list.SelectedIndex = -1; _templates.SelectedIndex = -1;
        _name.Clear(); _phrases.Clear(); _optionalPhrases.Clear(); _enabled.IsChecked = true;
        _confirm.IsChecked = true; _scheduleEnabled.IsChecked = false; _scheduleTime.Text = "09:00"; _scheduleDays.SelectedIndex = 0;
        _actions.Items.Clear(); _url.Clear(); LinkEditor.Visibility = Visibility.Collapsed;
        EditorHeading.Text = "НОВАЯ КОМАНДА";
        DeleteButton.Visibility = Visibility.Collapsed;
        _status.Text = "";
        _loading = false;
        UpdateEmptyActions();
    }

    private void ShowCommand(int index)
    {
        if (index < 0 || index >= _commands.Count) return;
        _loading = true;
        _editing = index;
        var command = _commands[index];
        _name.Text = command.Name;
        _phrases.Text = string.Join(Environment.NewLine, command.Phrases);
        _enabled.IsChecked = command.Enabled;
        _confirm.IsChecked = command.Confirm; _optionalPhrases.Text = string.Join(Environment.NewLine, command.OptionalPhrases);
        _scheduleEnabled.IsChecked = command.ScheduleEnabled; _scheduleTime.Text = command.ScheduleTime;
        _scheduleDays.SelectedIndex = Math.Max(0, Array.IndexOf(CustomCommands.ScheduleOptions, command.ScheduleDays));
        _actions.Items.Clear();
        foreach (var action in command.Actions) _actions.Items.Add(action);
        _url.Clear(); LinkEditor.Visibility = Visibility.Collapsed;
        EditorHeading.Text = "РЕДАКТИРОВАНИЕ КОМАНДЫ";
        DeleteButton.Visibility = Visibility.Visible;
        _status.Text = "";
        _loading = false;
        UpdateEmptyActions();
    }

    private List<string> Phrases() => _phrases.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    private List<string> OptionalPhrases() => _optionalPhrases.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    private string ScheduleDays() => (_scheduleDays.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "daily";
    private bool HasUnsaved()
    {
        if (!string.IsNullOrWhiteSpace(_url.Text)) return true;
        if (_editing < 0 || _editing >= _commands.Count)
            return !string.IsNullOrWhiteSpace(_name.Text) || Phrases().Count > 0 || OptionalPhrases().Count > 0 || _actions.Items.Count > 0 || _enabled.IsChecked != true || _confirm.IsChecked != true || _scheduleEnabled.IsChecked == true || _scheduleTime.Text != "09:00" || ScheduleDays() != "daily";
        var c = _commands[_editing];
        return _name.Text != c.Name || !Phrases().SequenceEqual(c.Phrases) || !OptionalPhrases().SequenceEqual(c.OptionalPhrases) || (_enabled.IsChecked == true) != c.Enabled || (_confirm.IsChecked == true) != c.Confirm || (_scheduleEnabled.IsChecked == true) != c.ScheduleEnabled || _scheduleTime.Text != c.ScheduleTime || ScheduleDays() != c.ScheduleDays || !_actions.Items.Cast<CustomAction>().SequenceEqual(c.Actions);
    }
    private bool ConfirmDiscard() => !HasUnsaved() || MessageBox.Show(this, "Отменить несохранённые изменения?", "Свои команды", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    protected override void OnClosing(CancelEventArgs e)
    {
        if (EditorView.Visibility == Visibility.Visible && !ConfirmDiscard()) e.Cancel = true;
        base.OnClosing(e);
    }
    private void ShowList()
    {
        ListView.Visibility = Visibility.Visible; EditorView.Visibility = Visibility.Collapsed;
        ListTab.Style = (Style)FindResource("PrimaryButton"); EditorTab.Style = (Style)FindResource("ButtonBase");
        DeleteButton.Visibility = SaveButton.Visibility = CancelButton.Visibility = Visibility.Collapsed;
        _status.Text = "";
    }
    private void ShowEditor()
    {
        ListView.Visibility = Visibility.Collapsed; EditorView.Visibility = Visibility.Visible;
        ListTab.Style = (Style)FindResource("ButtonBase"); EditorTab.Style = (Style)FindResource("PrimaryButton");
        SaveButton.Visibility = CancelButton.Visibility = Visibility.Visible;
        DeleteButton.Visibility = _editing >= 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1) DragMove(); }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ListTab_Click(object sender, RoutedEventArgs e) { if (EditorView.Visibility != Visibility.Visible || ConfirmDiscard()) { ClearEditor(); ShowList(); } }
    private void EditorTab_Click(object sender, RoutedEventArgs e) { if (EditorView.Visibility != Visibility.Visible) { ClearEditor(); ShowEditor(); } }
    private void New_Click(object sender, RoutedEventArgs e) { if (EditorView.Visibility != Visibility.Visible || ConfirmDiscard()) { ClearEditor(); ShowEditor(); } }
    private void Cancel_Click(object sender, RoutedEventArgs e) { if (ConfirmDiscard()) { ClearEditor(); ShowList(); } }
    private void Command_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && _list.SelectedItem is CustomCommand selected) { int index = _commands.IndexOf(selected); if (index >= 0) { ShowCommand(index); ShowEditor(); } }
    }
    private void Template_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _templates.SelectedItem is not CustomCommand template) return;
        ClearEditor();
        _name.Text = template.Name; _phrases.Text = string.Join(Environment.NewLine, template.Phrases);
        foreach (var action in template.Actions) _actions.Items.Add(action);
        EditorHeading.Text = "ШАБЛОН — СОХРАНИ КОМАНДУ"; UpdateEmptyActions(); ShowEditor();
    }
    private void UpdateEmptyActions() { if (EmptyActions != null && _actions != null) EmptyActions.Visibility = _actions.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed; }
    private void Actions_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateEmptyActions();
    private void MoveAction(int delta)
    {
        int i = _actions.SelectedIndex, j = i + delta;
        if (i < 0 || j < 0 || j >= _actions.Items.Count) return;
        var action = _actions.Items[i]; _actions.Items.RemoveAt(i); _actions.Items.Insert(j, action); _actions.SelectedIndex = j;
        UpdateEmptyActions();
    }
    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveAction(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveAction(1);
    private void RemoveAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && ItemsControl.ContainerFromElement(_actions, button) is ListBoxItem item)
        {
            int index = _actions.ItemContainerGenerator.IndexFromContainer(item);
            if (index >= 0) _actions.Items.RemoveAt(index);
        }
        UpdateEmptyActions();
    }
    private void AddAction_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_url.Text)) { _status.Text = "Сначала добавь действие или очисти поле."; return; }
        var picker = new ActionPickerWindow(this);
        if (picker.ShowDialog() == true && picker.Result is CustomAction action)
        {
            _actions.Items.Add(action); _actions.SelectedIndex = _actions.Items.Count - 1; _status.Text = ""; UpdateEmptyActions();
        }
    }
    private void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Выбери программу", Filter = "Программы (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) == true) { _actions.Items.Add(new CustomAction("app", dialog.FileName)); _actions.SelectedIndex = _actions.Items.Count - 1; UpdateEmptyActions(); }
    }
    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Выбери файл", Filter = "Все файлы (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) { _actions.Items.Add(new CustomAction("file", dialog.FileName)); _actions.SelectedIndex = _actions.Items.Count - 1; UpdateEmptyActions(); }
    }
    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Выбери папку" };
        if (dialog.ShowDialog(this) == true) { _actions.Items.Add(new CustomAction("folder", dialog.FolderName)); _actions.SelectedIndex = _actions.Items.Count - 1; UpdateEmptyActions(); }
    }
    private void ShowInput(string type, string title, string hint)
    {
        if (!string.IsNullOrWhiteSpace(_url.Text)) { _status.Text = "Сначала добавь действие или очисти поле."; return; }
        _pendingType = type; ActionEntryTitle.Text = title; _url.Tag = hint; _url.Clear(); LinkEditor.Visibility = Visibility.Visible; _url.Focus();
    }
    private void ShowLink_Click(object sender, RoutedEventArgs e) => ShowInput("url", "Адрес ссылки (http или https)", "https://example.com");
    private void ShowSearch_Click(object sender, RoutedEventArgs e) => ShowInput("search", "Поиск Google: запрос откроется в браузере", "Например: расписание поездов");
    private void ShowDelay_Click(object sender, RoutedEventArgs e) => ShowInput("delay", "Пауза в секундах (1–3600)", "Например: 5");
    private void ShowCmd_Click(object sender, RoutedEventArgs e) => ShowInput("cmd", "Команда CMD: выполняется с правами Клода", "Введи команду CMD в одну строку");
    private void ShowPowerShell_Click(object sender, RoutedEventArgs e) => ShowInput("powershell", "Команда PowerShell: выполняется с правами Клода", "Введи команду PowerShell в одну строку");
    private void AddSound_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Выбери звук WAV или MP3 до 10 минут", Filter = "Звуковые файлы (*.wav;*.mp3)|*.wav;*.mp3" };
        if (dialog.ShowDialog(this) == true) { _actions.Items.Add(new CustomAction("sound", dialog.FileName)); _actions.SelectedIndex = _actions.Items.Count - 1; UpdateEmptyActions(); }
    }
    private void AddLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var action = new CustomAction(_pendingType, _url.Text.Trim());
            var checkedAction = CustomCommands.Validate(new[] { new CustomCommand("Проверка", new() { "проверка действия" }, true, new() { action }) })[0].Actions[0];
            _actions.Items.Add(checkedAction); _actions.SelectedIndex = _actions.Items.Count - 1;
            _url.Clear(); LinkEditor.Visibility = Visibility.Collapsed; _status.Text = ""; UpdateEmptyActions();
        }
        catch (Exception ex) { _status.Text = "Не удалось добавить действие: " + ex.Message; }
    }
    private void Save_Click(object sender, RoutedEventArgs e) => SaveEditor();
    private void SaveEditor()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_url.Text)) { _status.Text = "Сначала добавь действие в цепочку или очисти поле."; return; }
            var command = new CustomCommand(_name.Text, Phrases(), _enabled.IsChecked == true, _actions.Items.Cast<CustomAction>().ToList())
            {
                Id = _editing >= 0 && _editing < _commands.Count ? _commands[_editing].Id : "",
                Confirm = _confirm.IsChecked == true, OptionalPhrases = OptionalPhrases(), ScheduleEnabled = _scheduleEnabled.IsChecked == true,
                ScheduleTime = _scheduleTime.Text.Trim(), ScheduleDays = ScheduleDays()
            };
            if (!command.Confirm && (_editing < 0 || _commands[_editing].Confirm || !_commands[_editing].Actions.SequenceEqual(command.Actions) || (!_commands[_editing].ScheduleEnabled && command.ScheduleEnabled)))
            {
                if (MessageBox.Show(this, "Команда будет запускаться без подтверждения, в том числе по расписанию. Программы и скрипты получат права Клода, возможно администратора. Продолжить?", "Запуск без подтверждения", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            }
            var next = _commands.ToList(); int selected = _editing;
            if (selected >= 0 && selected < next.Count) next[selected] = command; else { selected = next.Count; next.Add(command); }
            CustomCommands.Save(_path, next); _commands = CustomCommands.Load(_path); RefreshList();
            _loading = true; _list.SelectedItem = _commands[selected]; _loading = false;
            ShowCommand(selected); _status.Text = "Команда сохранена.";
        }
        catch (Exception ex) { _status.Text = "Не удалось сохранить: " + ex.Message; }
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        int i = _editing; if (i < 0 || i >= _commands.Count) return;
        if (MessageBox.Show(this, "Удалить команду «" + _commands[i].Name + "»? Программы и файлы не удаляются.", "Удалить команду", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { var next = _commands.ToList(); next.RemoveAt(i); CustomCommands.Save(_path, next); _commands = CustomCommands.Load(_path); RefreshList(); ClearEditor(); ShowList(); _status.Text = "Команда удалена."; }
        catch (Exception ex) { _status.Text = "Не удалось удалить: " + ex.Message; }
    }
}
