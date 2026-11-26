using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowGameLibrary()
    {
        var window = new Window { Title = "Мои игры", Width = 840, Height = 570, MinWidth = 600, MinHeight = 400, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) }; var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Добавь EXE игр вручную. Список хранится локально и не изменяет настройки игр. Можно добавить игру в защиту от очистки; игровые настройки Windows применяются отдельно и ко всей системе.", TextWrapping = TextWrapping.Wrap });
        var name = new TextBox { Margin = new Thickness(0, 8, 0, 8), ToolTip = "Название игры" }; top.Children.Add(name);
        var bar = new WrapPanel(); top.Children.Add(bar);
        System.Windows.Controls.Button Button(string title) { var b = new System.Windows.Controls.Button { Content = title, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 8) }; bar.Children.Add(b); return b; }
        var add = Button("Добавить EXE"); var remove = Button("Убрать из списка"); var protect = Button("Защитить от очистки"); var openProfiles = Button("Профили процессов");
        var note = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) }; top.Children.Add(note);
        var list = new System.Windows.Controls.ListBox { HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch }; root.Children.Add(list);
        System.Collections.Generic.List<GameLibrary.Game> games;
        try { games = GameLibrary.Load(GameLibrary.DefaultPath); } catch (Exception ex) { MessageBox.Show(window, ex.Message, "Ошибка списка игр", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        void Refresh() { list.ItemsSource = null; list.ItemsSource = games; }
        add.Click += (_, _) => { var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Выбери EXE игры", Filter = "Программы (*.exe)|*.exe" }; if (dlg.ShowDialog(window) != true) return; try { var next = games.Concat(new[] { new GameLibrary.Game(string.IsNullOrWhiteSpace(name.Text) ? Path.GetFileNameWithoutExtension(dlg.FileName) : name.Text, dlg.FileName) }).ToList(); GameLibrary.Save(GameLibrary.DefaultPath, next); games = GameLibrary.Load(GameLibrary.DefaultPath); Refresh(); note.Text = "Игра добавлена."; } catch (Exception ex) { note.Text = "Не удалось добавить: " + ex.Message; } };
        remove.Click += (_, _) => { if (list.SelectedItem is not GameLibrary.Game g) { note.Text = "Выбери игру."; return; } if (MessageBox.Show(window, "Убрать «" + g.Name + "» только из этого списка? Файлы игры не удаляются. Защита от очистки отдельно не изменяется.", "Удалить из списка", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return; try { var next = games.Where(x => !string.Equals(x.Executable, g.Executable, StringComparison.OrdinalIgnoreCase)).ToList(); GameLibrary.Save(GameLibrary.DefaultPath, next); games = GameLibrary.Load(GameLibrary.DefaultPath); Refresh(); note.Text = "Игра убрана из списка."; } catch (Exception ex) { note.Text = ex.Message; } };
        protect.Click += (_, _) => { if (list.SelectedItem is not GameLibrary.Game g) { note.Text = "Выбери игру."; return; } if (MessageBox.Show(window, "Добавить EXE «" + g.Name + "» в защиту очистки? Проверка приблизительная и не прерывает уже запущенные инструменты.", "Защита игры", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return; try { GameGuard.SavePaths(GameGuard.LoadPaths().Concat(new[] { g.Executable })); note.Text = "Игра добавлена в список защиты. Обнаружение не гарантировано."; } catch (Exception ex) { note.Text = "Не удалось сохранить защиту: " + ex.Message; } };
        openProfiles.Click += (_, _) => ShowProcessManager();
        Refresh(); window.Content = root; window.ShowDialog();
    }
}
