using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;
using MessageBox = System.Windows.MessageBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowCleanupPresets()
    {
        try
        {
            var presets = CleanupPresets.Load();
            var window = new Window { Title = "Наборы очистки", Width = 650, Height = 450, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = "Наборы сохраняют только выбранные категории. Исключения, возраст файлов, браузеры и остальные настройки остаются прежними. Применение не запускает очистку. Для расписания отдельно сохрани настройки очистки.", TextWrapping = TextWrapping.Wrap });
            var list = new ListBox { Height = 170, Margin = new Thickness(0, 10, 0, 10) }; panel.Children.Add(list);
            void Refresh() { list.Items.Clear(); foreach (string name in presets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)) list.Items.Add(name); }
            Refresh();
            var nameBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) }; panel.Children.Add(nameBox);
            var controls = new WrapPanel(); panel.Children.Add(controls);
            var save = new System.Windows.Controls.Button { Content = "Сохранить текущие категории", Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 0, 8, 0) };
            var apply = new System.Windows.Controls.Button { Content = "Применить выбранный", Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(0, 0, 8, 0) };
            var remove = new System.Windows.Controls.Button { Content = "Удалить набор", Padding = new Thickness(9, 6, 9, 6) }; controls.Children.Add(save); controls.Children.Add(apply); controls.Children.Add(remove);
            save.Click += (_, _) => { try { string name = nameBox.Text.Trim(); CleanupPresets.ValidateName(name); if (presets.ContainsKey(name) && MessageBox.Show(window, "Заменить существующий набор?", "Наборы очистки", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; presets[name] = CleanupPresets.Capture(_settings); CleanupPresets.Save(presets); Refresh(); } catch (Exception ex) { MessageBox.Show(window, ex.Message); } };
            apply.Click += (_, _) => { if (list.SelectedItem is not string name) { MessageBox.Show(window, "Выбери набор."); return; } try { CleanupPresets.Apply(_settings, presets[name]); window.Close(); ShowClean(); } catch (Exception ex) { MessageBox.Show(window, ex.Message); } };
            remove.Click += (_, _) => { if (list.SelectedItem is not string name || MessageBox.Show(window, "Удалить набор «" + name + "»?", "Наборы очистки", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return; try { presets.Remove(name); CleanupPresets.Save(presets); Refresh(); } catch (Exception ex) { MessageBox.Show(window, ex.Message); } };
            window.Content = panel; window.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Не удалось открыть наборы", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
