using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace DesktopPet;
internal static class CustomCommandActions
{
    internal static async Task PerformAsync(CustomAction a, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        switch (a.Type)
        {
            case "delay": await Task.Delay(TimeSpan.FromSeconds(int.Parse(a.Target, System.Globalization.CultureInfo.InvariantCulture)), token); break;
            case "sound": await PlayCommandSoundAsync(a.Target, token); break;
            case "app":
                if (!File.Exists(a.Target)) throw new FileNotFoundException("Программа не найдена: " + a.Target);
                Process.Start(new ProcessStartInfo { FileName = a.Target, UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(a.Target)! }); break;
            case "file":
                if (!File.Exists(a.Target)) throw new FileNotFoundException("Файл не найден: " + a.Target);
                Process.Start(new ProcessStartInfo { FileName = a.Target, UseShellExecute = true }); break;
            case "folder":
                if (!Directory.Exists(a.Target)) throw new DirectoryNotFoundException("Папка не найдена: " + a.Target);
                Process.Start(new ProcessStartInfo { FileName = a.Target, UseShellExecute = true }); break;
            case "url": Process.Start(new ProcessStartInfo { FileName = a.Target, UseShellExecute = true }); break;
            case "search": Process.Start(new ProcessStartInfo { FileName = "https://www.google.com/search?q=" + Uri.EscapeDataString(a.Target), UseShellExecute = true }); break;
            case "cmd": case "powershell":
                string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
                string executable = a.Type == "cmd" ? Path.Combine(system, "cmd.exe") : Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe");
                var start = new ProcessStartInfo { FileName = executable, UseShellExecute = false };
                if (a.Type == "cmd") { start.Arguments = "/D /S /C \"" + a.Target + "\""; }
                else { start.ArgumentList.Add("-NoLogo"); start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-EncodedCommand"); start.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(a.Target))); }
                using (var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить команду."))
                { await process.WaitForExitAsync(token); if (process.ExitCode != 0) throw new InvalidOperationException("Команда завершилась с кодом " + process.ExitCode + "."); }
                break;
            default: throw new InvalidDataException("Неизвестное действие.");
        }
    }
    private static async Task PlayCommandSoundAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Звуковой файл не найден: " + path);
        using var reader = new AudioFileReader(path);
        if (reader.TotalTime > TimeSpan.FromMinutes(10)) throw new InvalidDataException("Звук должен быть не длиннее 10 минут.");
        using var player = new WaveOutEvent();
        var done = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.PlaybackStopped += (_, e) => done.TrySetResult(e.Exception);
        player.Init(reader);
        using var registration = token.Register(() => { try { player.Stop(); } catch (Exception) { } done.TrySetCanceled(token); });
        token.ThrowIfCancellationRequested(); player.Play();
        var error = await done.Task;
        token.ThrowIfCancellationRequested();
        if (error != null) throw new InvalidOperationException("Ошибка воспроизведения звука.", error);
    }
}
