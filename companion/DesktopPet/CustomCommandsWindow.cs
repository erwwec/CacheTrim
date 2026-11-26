using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace DesktopPet;

internal sealed class CustomCommandsWindow : Window
{
    private readonly string _path;
    private List<CustomCommand> _commands;
    private readonly ListBox _list = new() { MinWidth = 175, MinHeight = 270, Margin = new Thickness(0, 0, 12, 0) };
    private readonly TextBox _name = new() { MaxLength = 80, Margin = new Thickness(0, 2, 0, 8) };
    private readonly TextBox _phrases = new() { AcceptsReturn = true, Height = 65, MaxLength = 810, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 2, 0, 8) };
    private readonly CheckBox _enabled = new() { Content = "Команда включена", IsChecked = true, Foreground = Brushes.White, Margin = new Thickness(0, 2, 0, 8) };
    private readonly ListBox _actions = new() { MinHeight = 100, MaxHeight = 140, Margin = new Thickness(0, 2, 0, 6) };
    private readonly TextBox _url = new() { ToolTip = "http:// или https://", Margin = new Thickness(0, 3, 0, 4) };
    private readonly TextBlock _status = new() { Foreground = Brushes.LightGoldenrodYellow, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private int _editing = -1;
    internal CustomCommandsWindow(Window owner, string path, List<CustomCommand> commands)
    {
        _path = path; _commands = commands.ToList(); Owner = owner; Title = "Свои команды Клода"; Width = 820; Height = 620; MinWidth = 650; MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new SolidColorBrush(Color.FromRgb(35, 27, 47)); Foreground = Brushes.White;
        var root = new Grid { Margin = new Thickness(16) }; root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) }); root.ColumnDefinitions.Add(new ColumnDefinition());
        var left = new DockPanel(); Grid.SetColumn(left, 0); root.Children.Add(left);
        var leftButtons = new StackPanel(); DockPanel.SetDock(leftButtons, Dock.Bottom); left.Children.Add(leftButtons); left.Children.Add(_list);
        Button AddButton(Panel panel, string text, RoutedEventHandler handler) { var b = new Button { Content = text, Margin = new Thickness(0, 4, 4, 0), Padding = new Thickness(8, 5, 8, 5) }; b.Click += handler; panel.Children.Add(b); return b; }
        AddButton(leftButtons, "Новая команда", (_, _) => ClearEditor());
        AddButton(leftButtons, "Удалить выбранную", (_, _) => DeleteSelected());
        _list.SelectionChanged += (_, _) => { if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _commands.Count) ShowCommand(_list.SelectedIndex); };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(scroll, 1); root.Children.Add(scroll);
        var right = new StackPanel(); scroll.Content = right;
        void Label(string text) => right.Children.Add(new TextBlock { Text = text, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        Label("Название"); right.Children.Add(_name); Label("Фразы активации — каждая с новой строки"); right.Children.Add(_phrases); right.Children.Add(_enabled);
        Label("Цепочка действий — выполняется сверху вниз после подтверждения"); right.Children.Add(_actions);
        var actionButtons = new WrapPanel(); right.Children.Add(actionButtons);
        AddButton(actionButtons, "Добавить программу…", (_, _) => { var dialog = new OpenFileDialog { Title = "Выбери программу", Filter = "Программы (*.exe)|*.exe" }; if (dialog.ShowDialog(this) == true) _actions.Items.Add(new CustomAction("app", dialog.FileName)); });
        AddButton(actionButtons, "Убрать действие", (_, _) => { int i = _actions.SelectedIndex; if (i >= 0) _actions.Items.RemoveAt(i); });
        AddButton(actionButtons, "↑", (_, _) => MoveAction(-1)); AddButton(actionButtons, "↓", (_, _) => MoveAction(1));
        Label("Ссылка (http или https)"); right.Children.Add(_url); AddButton(right, "Добавить ссылку", (_, _) => { if (!string.IsNullOrWhiteSpace(_url.Text)) { _actions.Items.Add(new CustomAction("url", _url.Text.Trim())); _url.Clear(); } });
        AddButton(right, "Сохранить команду", (_, _) => SaveEditor());
        right.Children.Add(new TextBlock { Text = "Команды хранятся на этом ПК. При запуске всегда будет показано подтверждение. Голос работает только через «Слушать одну команду» и при поддержке слов локальной моделью; расписания нет.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray, Margin = new Thickness(0, 8, 0, 0) });
        right.Children.Add(_status); Content = root; RefreshList();
    }
    private void RefreshList() { _list.ItemsSource = null; _list.ItemsSource = _commands; }
    private void ClearEditor() { _editing = -1; _list.SelectedIndex = -1; _name.Clear(); _phrases.Clear(); _enabled.IsChecked = true; _actions.Items.Clear(); _url.Clear(); _status.Text = "Новая команда. Сначала сохрани изменения."; }
    private void ShowCommand(int i) { _editing = i; var c = _commands[i]; _name.Text = c.Name; _phrases.Text = string.Join(Environment.NewLine, c.Phrases); _enabled.IsChecked = c.Enabled; _actions.Items.Clear(); foreach (var a in c.Actions) _actions.Items.Add(a); _url.Clear(); _status.Text = "Измени и нажми «Сохранить команду»."; }
    private void MoveAction(int delta) { int i = _actions.SelectedIndex, j = i + delta; if (i < 0 || j < 0 || j >= _actions.Items.Count) return; var action = _actions.Items[i]; _actions.Items.RemoveAt(i); _actions.Items.Insert(j, action); _actions.SelectedIndex = j; }
    private void SaveEditor()
    {
        try
        {
            var c = new CustomCommand(_name.Text, _phrases.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(), _enabled.IsChecked == true, _actions.Items.Cast<CustomAction>().ToList());
            var next = _commands.ToList(); int selected = _editing; if (selected >= 0 && selected < next.Count) next[selected] = c; else { selected = next.Count; next.Add(c); }
            CustomCommands.Save(_path, next); _commands = CustomCommands.Load(_path); RefreshList(); _list.SelectedIndex = selected; _status.Text = "Команда сохранена.";
        }
        catch (Exception ex) { _status.Text = "Не удалось сохранить: " + ex.Message; }
    }
    private void DeleteSelected()
    {
        int i = _list.SelectedIndex; if (i < 0 || i >= _commands.Count) { _status.Text = "Выбери команду в списке."; return; }
        if (MessageBox.Show(this, "Удалить команду «" + _commands[i].Name + "»? Программы и файлы не удаляются.", "Удалить команду", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try { var next = _commands.ToList(); next.RemoveAt(i); CustomCommands.Save(_path, next); _commands = CustomCommands.Load(_path); RefreshList(); ClearEditor(); _status.Text = "Команда удалена."; }
        catch (Exception ex) { _status.Text = "Не удалось удалить: " + ex.Message; }
    }
}
