using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;

namespace CacheTrim;
public partial class MainWindow
{
    private void ShowProcessManager()
    {
        var window = new Window { Title = "Процессы", Width = 920, Height = 600, MinWidth = 600, MinHeight = 420, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) };
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Только процессы, сведения о которых доступны Windows. Завершение может привести к потере несохранённых данных. Изменение приоритета действует до закрытия процесса и не гарантирует ускорения.", TextWrapping = TextWrapping.Wrap });
        var filter = new TextBox { Margin = new Thickness(0, 8, 0, 8), ToolTip = "Поиск по имени, PID или пути" }; top.Children.Add(filter);
        var buttons = new WrapPanel(); top.Children.Add(buttons);
        System.Windows.Controls.Button Button(string title) { var b = new System.Windows.Controls.Button { Content = title, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 8) }; buttons.Children.Add(b); return b; }
        var refresh = Button("Обновить"); var stop = Button("Завершить"); var below = Button("Приоритет ниже"); var normal = Button("Обычный приоритет"); var above = Button("Приоритет выше");
        var priority = new System.Windows.Controls.ComboBox { Width = 140, Margin = new Thickness(0, 0, 8, 8), ItemsSource = new[] { "BelowNormal", "Normal", "AboveNormal" }, SelectedIndex = 1 }; buttons.Children.Add(priority);
        var saveProfile = Button("Сохранить профиль"); var applyProfile = Button("Применить профиль");
        var note = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) }; top.Children.Add(note);
        var list = new System.Windows.Controls.ListBox { HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch }; root.Children.Add(list);
        System.Collections.Generic.List<ProcessManager.Entry> entries = new();
        void Filter() { string term = filter.Text.Trim(); list.ItemsSource = entries.Where(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || x.Path.Contains(term, StringComparison.OrdinalIgnoreCase) || x.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)).ToList(); }
        async Task Refresh() { refresh.IsEnabled = false; try { entries = await Task.Run(ProcessManager.List); Filter(); note.Text = "Доступно процессов: " + entries.Count + ". Системные и недоступные процессы могут отсутствовать."; } catch (Exception ex) { note.Text = "Не удалось получить список: " + ex.Message; } finally { refresh.IsEnabled = true; } }
        async Task Apply(string description, Action<ProcessManager.Entry> action)
        {
            if (list.SelectedItem is not ProcessManager.Entry item) { note.Text = "Сначала выбери процесс."; return; }
            string question = description + "\n\n" + item.Name + " (PID " + item.Id + ")\n" + item.Path + "\n\nПроверь, что это нужная программа. Системные процессы могут быть защищены не полностью.";
            if (MessageBox.Show(window, question, "Подтверждение действия", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            foreach (var b in new[] { stop, below, normal, above, refresh, saveProfile, applyProfile }) b.IsEnabled = false;
            try { await Task.Run(() => action(item)); note.Text = "Действие выполнено."; await Refresh(); }
            catch (Exception ex) { note.Text = "Действие не выполнено: " + ex.Message; }
            finally { foreach (var b in new[] { stop, below, normal, above, refresh, saveProfile, applyProfile }) b.IsEnabled = true; }
        }
        filter.TextChanged += (_, _) => Filter(); refresh.Click += async (_, _) => await Refresh();
        stop.Click += async (_, _) => await Apply("Принудительно завершить процесс? Несохранённые данные могут быть потеряны.", ProcessManager.Stop);
        below.Click += async (_, _) => await Apply("Установить приоритет ниже обычного?", x => ProcessManager.SetPriority(x, ProcessPriorityClass.BelowNormal));
        normal.Click += async (_, _) => await Apply("Установить обычный приоритет?", x => ProcessManager.SetPriority(x, ProcessPriorityClass.Normal));
        above.Click += async (_, _) => await Apply("Установить приоритет выше обычного? Это может замедлить другие программы.", x => ProcessManager.SetPriority(x, ProcessPriorityClass.AboveNormal));
        saveProfile.Click += (_, _) =>
        {
            if (list.SelectedItem is not ProcessManager.Entry item) { note.Text = "Сначала выбери процесс."; return; }
            try { ProcessProfiles.Save(ProcessProfiles.DefaultPath, item.Path, (string)priority.SelectedItem); note.Text = "Профиль сохранён локально для выбранного пути; автоматически он не применяется."; }
            catch (Exception ex) { note.Text = "Профиль не сохранён: " + ex.Message; }
        };
        applyProfile.Click += async (_, _) => await Apply("Применить сохранённый для этого файла приоритет?", x => ProcessManager.SetPriority(x, ProcessProfiles.Find(ProcessProfiles.DefaultPath, x.Path)));
        window.Loaded += async (_, _) => await Refresh(); window.Content = root; window.ShowDialog();
    }
}
