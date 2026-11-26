using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
namespace CacheTrim;
public partial class MainWindow
{
    private void ShowProcessAffinity()
    {
        var window = new Window { Title = "Процессоры для процесса", Width = 900, Height = 690, MinWidth = 620, MinHeight = 480, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brush(Theme.Bg), Foreground = Brush(Theme.Text) };
        var root = new DockPanel { Margin = new Thickness(18) }; var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "Выбор разрешённых логических процессоров для одного запущенного процесса. Это не резервирование ядер: другие программы тоже могут их использовать. Ограничение может ухудшить работу. Поддерживается только одна группа процессоров; восстановление — для того же запуска процесса.", TextWrapping = TextWrapping.Wrap });
        var bar = new WrapPanel { Margin = new Thickness(0,8,0,8) }; top.Children.Add(bar);
        System.Windows.Controls.Button Button(string title) { var b = new System.Windows.Controls.Button { Content=title, Padding=new Thickness(9,5,9,5), Margin=new Thickness(0,0,8,8) }; bar.Children.Add(b); return b; }
        var refresh = Button("Обновить процессы"); var inspect = Button("Показать ядра"); var apply = Button("Применить"); var restore = Button("Вернуть исходную маску");
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,8) }; top.Children.Add(status);
        var cpus = new WrapPanel(); top.Children.Add(cpus);
        var list = new System.Windows.Controls.ListBox { HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch }; root.Children.Add(list);
        ProcessManager.Entry selected = null; var boxes = new Dictionary<int, System.Windows.Controls.CheckBox>();
        async Task Refresh() { refresh.IsEnabled=false; try { list.ItemsSource=await Task.Run(ProcessManager.List); selected=null; cpus.Children.Clear(); boxes.Clear(); } catch(Exception ex) { status.Text=ex.Message; } finally { refresh.IsEnabled=true; } }
        inspect.Click += (_,_) => { if (list.SelectedItem is not ProcessManager.Entry e) { status.Text="Выбери процесс."; return; } try { var state=ProcessAffinity.Read(e); selected=e; cpus.Children.Clear(); boxes.Clear(); for(int i=0;i<64;i++) { ulong bit=1UL<<i; if ((state.SystemMask & bit)==0) continue; var box=new System.Windows.Controls.CheckBox { Content="CPU "+i, IsChecked=(state.ProcessMask&bit)!=0, Margin=new Thickness(0,0,12,8) }; boxes.Add(i,box); cpus.Children.Add(box); } status.Text="Текущая маска процесса; возможны дополнительные ограничения Windows или драйвера."; } catch(Exception ex) { selected=null; cpus.Children.Clear(); boxes.Clear(); status.Text=ex.Message; } };
        apply.Click += (_,_) => { if (selected == null || list.SelectedItem is not ProcessManager.Entry chosen || chosen != selected) { status.Text="Выбери процесс и сначала покажи его ядра."; return; } ulong mask=0; foreach(var pair in boxes) if (pair.Value.IsChecked==true) mask |= 1UL<<pair.Key; if (MessageBox.Show(window,"Изменить маску процессоров для "+selected.Name+" (PID "+selected.Id+")? Это может снизить производительность. Прежняя маска сохраняется для ручного восстановления.","Подтверждение",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes) return; try { ProcessAffinity.Apply(selected,mask); status.Text="Маска изменена; прежняя сохранена для этого запуска процесса."; } catch(Exception ex) { status.Text="Маска не изменена: "+ex.Message; } };
        restore.Click += (_,_) => { if (list.SelectedItem is not ProcessManager.Entry e) { status.Text="Выбери процесс."; return; } if (MessageBox.Show(window,"Вернуть сохранённую исходную маску для "+e.Name+" (PID "+e.Id+")?", "Восстановление", MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return; try { ProcessAffinity.Restore(e); status.Text="Сохранённая маска восстановлена."; } catch(Exception ex) { status.Text="Не удалось восстановить: "+ex.Message; } };
        refresh.Click += async (_,_) => await Refresh(); window.Loaded += async (_,_) => await Refresh(); window.Content=root; window.ShowDialog();
    }
}
