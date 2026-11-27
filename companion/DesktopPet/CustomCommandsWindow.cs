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
    private readonly string _path;
    private List<CustomCommand> _commands;
    private int _editing = -1;
    private bool _loading;

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
        _list.ItemsSource = null;
        _list.ItemsSource = _commands;
        EmptyCommands.Visibility = _commands.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;
    }

    private void ClearEditor()
    {
        _loading = true;
        _editing = -1;
        _list.SelectedIndex = -1;
        _name.Clear(); _phrases.Clear(); _enabled.IsChecked = true;
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
    private bool HasUnsaved()
    {
        if (!string.IsNullOrWhiteSpace(_url.Text)) return true;
        if (_editing < 0 || _editing >= _commands.Count)
            return !string.IsNullOrWhiteSpace(_name.Text) || Phrases().Count > 0 || _actions.Items.Count > 0 || _enabled.IsChecked != true;
        var c = _commands[_editing];
        return _name.Text != c.Name || !Phrases().SequenceEqual(c.Phrases) || (_enabled.IsChecked == true) != c.Enabled || !_actions.Items.Cast<CustomAction>().SequenceEqual(c.Actions);
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
        if (!_loading && _list.SelectedIndex >= 0) { ShowCommand(_list.SelectedIndex); ShowEditor(); }
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
    private void AddProgram_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Выбери программу", Filter = "Программы (*.exe)|*.exe" };
        if (dialog.ShowDialog(this) == true) { _actions.Items.Add(new CustomAction("app", dialog.FileName)); _actions.SelectedIndex = _actions.Items.Count - 1; UpdateEmptyActions(); }
    }
    private void ShowLink_Click(object sender, RoutedEventArgs e) { LinkEditor.Visibility = Visibility.Visible; _url.Focus(); }
    private void AddLink_Click(object sender, RoutedEventArgs e)
    {
        string value = _url.Text.Trim();
        if (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        { _status.Text = "Введи ссылку http или https без логина и пароля."; return; }
        _actions.Items.Add(new CustomAction("url", uri.AbsoluteUri)); _actions.SelectedIndex = _actions.Items.Count - 1;
        _url.Clear(); LinkEditor.Visibility = Visibility.Collapsed; _status.Text = ""; UpdateEmptyActions();
    }
    private void Save_Click(object sender, RoutedEventArgs e) => SaveEditor();
    private void SaveEditor()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_url.Text)) { _status.Text = "Сначала добавь ссылку в цепочку или очисти поле."; return; }
            var command = new CustomCommand(_name.Text, Phrases(), _enabled.IsChecked == true, _actions.Items.Cast<CustomAction>().ToList());
            var next = _commands.ToList(); int selected = _editing;
            if (selected >= 0 && selected < next.Count) next[selected] = command; else { selected = next.Count; next.Add(command); }
            CustomCommands.Save(_path, next); _commands = CustomCommands.Load(_path); RefreshList();
            _loading = true; _list.SelectedIndex = selected; _loading = false;
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
