using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DesktopPet;

static class Program
{
    static async Task Main()
    {
        var assembly = typeof(MainWindow).Assembly;
        var actionType = assembly.GetType("DesktopPet.CustomAction")!;
        var performer = assembly.GetType("DesktopPet.CustomCommandActions")!.GetMethod("PerformAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        Task Run(string type, string target, CancellationToken token = default) => (Task)performer.Invoke(null, new[] { Activator.CreateInstance(actionType, new object[] { type, target })!, token })!;
        var dir = Path.Combine(Path.GetTempPath(), "Claude Action Test " + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var cmdFile = Path.Combine(dir, "cmd output.txt");
            await Run("cmd", "echo command-ok>\"" + cmdFile + "\"");
            if (!File.ReadAllText(cmdFile).Contains("command-ok")) throw new Exception("CMD quoting failed");
            Console.WriteLine("OK: CMD action with quoted path");
            var psFile = Path.Combine(dir, "powershell output.txt");
            await Run("powershell", "Set-Content -LiteralPath '" + psFile.Replace("'", "''") + "' -Value 'тест ok' -Encoding UTF8");
            if (!File.ReadAllText(psFile).Contains("тест ok")) throw new Exception("PowerShell Unicode or quoting failed");
            Console.WriteLine("OK: PowerShell action with Unicode and quoted path");
            using var cts = new CancellationTokenSource(150);
            try { await Run("delay", "30", cts.Token); throw new Exception("Delay did not cancel"); }
            catch (OperationCanceledException) { Console.WriteLine("OK: pause can be cancelled"); }
            try { await Run("sound", Path.Combine(dir, "missing.wav")); throw new Exception("Missing audio not reported"); }
            catch (FileNotFoundException) { Console.WriteLine("OK: missing audio fails safely"); }
        }
        finally { Directory.Delete(dir, true); }
    }
}
