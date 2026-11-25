using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace CacheTrim;

public partial class MainWindow
{
    private void ShowReleaseHistory()
    {
        BeginPage("Что нового", "История публичных версий CacheTrim: от новых к старым. Для полной истории нужен интернет.");
        _pageContent.Children.Add(Card("Клод V2 — тестовая версия", new TextBlock { Text = "Функция находится в разработке. Возможны ошибки; работа голосового управления и управления ПК требует проверки на Windows.", Foreground = Brush(Theme.Text), TextWrapping = TextWrapping.Wrap }));
        var status = new TextBlock { Text = "Загружаем историю версий с GitHub…", Foreground = Brush(Theme.Muted), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        var list = new StackPanel();
        Wpf.Ui.Controls.Button refresh = null;
        refresh = AdvancedButton("Обновить историю", async () => await RefreshHistoryAsync());
        _pageContent.Children.Add(refresh);
        _pageContent.Children.Add(status);
        _pageContent.Children.Add(list);

        async System.Threading.Tasks.Task RefreshHistoryAsync()
        {
            refresh.IsEnabled = false;
            status.Text = "Загружаем историю версий с GitHub…";
            list.Children.Clear();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var result = await ReleaseHistory.FetchAllAsync(AppInfo.GitHubReleaseApi, AppInfo.Version, timeout.Token);
                if (result.Releases.Count == 0) throw new InvalidOperationException("Список релизов пуст.");
                status.Text = result.Complete ? "Публичные релизы GitHub. У некоторых старых версий подробного описания может не быть." : "Показана только часть истории: слишком много релизов. Попробуй открыть историю на GitHub.";
                foreach (var release in result.Releases)
                {
                    string name = "Версия " + release.Version.TrimStart('v', 'V');
                    if (release.PublishedAt.HasValue) name += " · " + release.PublishedAt.Value.ToLocalTime().ToString("dd.MM.yyyy");
                    var notes = new TextBlock { Text = string.IsNullOrWhiteSpace(release.Notes) ? "Подробное описание этого релиза отсутствует." : release.Notes.Trim(), Foreground = Brush(Theme.Text), FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
                    list.Children.Add(Card(name, notes));
                }
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is OperationCanceledException || ex is System.Text.Json.JsonException || ex is InvalidOperationException || ex is IOException)
            {
                status.Text = "Не удалось загрузить историю с GitHub. Ниже — заметки установленной версии; проверь интернет и нажми «Обновить историю».";
                string notes = "Описание этой версии недоступно.";
                try
                {
                    using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("CacheTrim.ReleaseNotes.txt");
                    if (stream != null) using (var reader = new StreamReader(stream)) notes = reader.ReadToEnd();
                }
                catch (IOException) { }
                list.Children.Add(Card("Установлена версия " + AppInfo.Version, new TextBlock { Text = notes, Foreground = Brush(Theme.Text), TextWrapping = TextWrapping.Wrap }));
                App.Log("не удалось загрузить историю версий: " + ex.Message);
            }
            finally { refresh.IsEnabled = true; }
        }
        _ = RefreshHistoryAsync();
    }
}
