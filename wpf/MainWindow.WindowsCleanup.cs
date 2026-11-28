using System;
using System.Windows;
using System.Windows.Controls;

namespace CacheTrim;

public partial class MainWindow
{
    private void OpenCleanupLink(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Не удалось открыть страницу GitHub", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ShowWindowsCleanup()
    {
        BeginPage("Очистка Windows", "Каталог внешних инструментов для настройки Windows. Кнопки открывают только официальные страницы GitHub — CacheTrim ничего не скачивает и не запускает автоматически.");

        WrapPanel tools = new() { HorizontalAlignment = System.Windows.HorizontalAlignment.Left };

        tools.Children.Add(AdvancedModule("WIN11DEBLOAT",
            "PowerShell-инструмент для удаления предустановленных приложений и настройки отдельных функций Windows. Изменения могут затронуть систему и профиль пользователя. Перед запуском изучи инструкции проекта, создай резервную копию и проверь доступный откат.",
            AdvancedButton("Официальный репозиторий", () => OpenCleanupLink("https://github.com/Raphire/Win11Debloat"), true),
            AdvancedButton("Открыть релизы", () => OpenCleanupLink("https://github.com/Raphire/Win11Debloat/releases/latest"))));

        tools.Children.Add(AdvancedModule("WINUTIL",
            "Инструмент для установки программ, обслуживания и изменения настроек Windows. Некоторые действия требуют прав администратора и могут влиять на всю систему — сначала проверь описание выбранного действия на официальной странице.",
            AdvancedButton("Официальный репозиторий", () => OpenCleanupLink("https://github.com/ChrisTitusTech/winutil"), true),
            AdvancedButton("Открыть релизы", () => OpenCleanupLink("https://github.com/ChrisTitusTech/winutil/releases/latest"))));

        tools.Children.Add(AdvancedModule("TINY11 BUILDER — ПРОГРАММА ДЛЯ УСТАНОВКИ WINDOWS",
            "Создаёт облегчённый установочный образ Windows 11, который затем можно использовать для чистой установки. Это не очистка уже установленной Windows. Перед выбором пресета изучи, какие компоненты и функции он удаляет; образ сначала лучше проверить в виртуальной машине.",
            AdvancedButton("Официальный репозиторий", () => OpenCleanupLink("https://github.com/ntdevlabs/tiny11builder"), true),
            AdvancedButton("Открыть релизы", () => OpenCleanupLink("https://github.com/ntdevlabs/tiny11builder/releases/latest"))));

        _pageContent!.Children.Add(tools);
    }
}
