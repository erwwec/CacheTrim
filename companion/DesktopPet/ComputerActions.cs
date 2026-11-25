using System;
using System.Diagnostics;
using System.IO;
using NAudio.CoreAudioApi;

namespace DesktopPet;

// Fixed actions only. No path, URL, process arguments or commands from the model.
internal static class ComputerActions
{
    internal static string Execute(ComputerStep step)
    {
        switch (step.Action)
        {
            case "open_youtube":
                if (Process.Start(new ProcessStartInfo("https://www.youtube.com/") { UseShellExecute = true }) == null)
                    return "Windows did not confirm the browser launch; verify visually.";
                return "Windows received request to open the YouTube homepage; verify visually.";
            case "open_telegram":
                string[] paths = {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Telegram Desktop", "Telegram.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Telegram Desktop", "Telegram.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Telegram Desktop", "Telegram.exe")
                };
                string? found = Array.Find(paths, File.Exists);
                if (found == null) return "Telegram Desktop not found in standard paths; use the Start menu or ask the user.";
                var launch = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; launch.ArgumentList.Add(found);
                return Process.Start(launch) == null ? "Windows did not confirm Telegram launch; verify visually." : "Windows received request to open Telegram Desktop; verify visually.";
            case "set_volume":
                if (step.Level < 0 || step.Level > 100) throw new InvalidOperationException("Недопустимый уровень громкости.");
                using (var enumerator = new MMDeviceEnumerator())
                using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
                {
                    var volume = device.AudioEndpointVolume;
                    volume.MasterVolumeLevelScalar = step.Level / 100f;
                    int actual = (int)Math.Round(volume.MasterVolumeLevelScalar * 100, MidpointRounding.AwayFromZero);
                    return "Default multimedia output master volume read back as " + actual + " percent; muted=" + volume.Mute + ".";
                }
            default: throw new InvalidOperationException("Действие недоступно.");
        }
    }
}
