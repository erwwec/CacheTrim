using CacheTrim;
using System.Diagnostics;
if (args.Contains("--child")) { Thread.Sleep(TimeSpan.FromSeconds(60)); return; }

ProcessAffinity.ValidateMask(3,15);
foreach (var m in new ulong[] {0,16,32}) { try { ProcessAffinity.ValidateMask(m,15); throw new Exception("bad mask accepted"); } catch(ArgumentException) {} }
string dir=Path.Combine(Path.GetTempPath(),"ct-affinity-"+Guid.NewGuid()); Directory.CreateDirectory(dir);
try { string path=Path.Combine(dir,"backups.json"); var e=new ProcessManager.Entry(123,"game",Path.Combine(dir,"game.exe"),1234,1,0,"Normal");
 ProcessAffinity.SaveOriginal(path,e,15); ProcessAffinity.SaveOriginal(path,e,3);
 if(ProcessAffinity.Load(path).Single().OriginalMask!=15) throw new Exception("original overwritten");
 if(ProcessAffinity.Matches(ProcessAffinity.Load(path).Single(),e with { StartUtcTicks=1235 })) throw new Exception("PID reuse accepted");
 File.WriteAllText(path,"broken"); try { ProcessAffinity.SaveOriginal(path,e,3); throw new Exception("corrupt data overwritten"); } catch(System.Text.Json.JsonException) { }
 if(File.ReadAllText(path)!="broken") throw new Exception("corrupt data lost");
 Console.WriteLine("OK: mask validation and process-specific original backup");
} finally { Directory.Delete(dir,true); }

if (OperatingSystem.IsWindows())
{
    string temp = Path.Combine(Path.GetTempPath(), "ct-affinity-integration-" + Guid.NewGuid()); Directory.CreateDirectory(temp);
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(ProcessAffinity).Assembly.Location);
    start.ArgumentList.Add("--child");
    using var child = Process.Start(start) ?? throw new Exception("Failed to start child process");
    try
    {
        ProcessManager.Entry entry = null;
        for (int n=0;n<50 && entry == null;n++) { entry = ProcessManager.List().FirstOrDefault(x => x.Id == child.Id); if (entry == null) Thread.Sleep(100); }
        if (entry == null) throw new Exception("Child process unavailable");
        ProcessManager.SetPriority(entry, ProcessPriorityClass.Normal);
        var state = ProcessAffinity.Read(entry);
        string backup = Path.Combine(temp, "backup.json");
        ProcessAffinity.Apply(entry, state.ProcessMask, backup);
        ProcessAffinity.Restore(entry, backup);
        if (ProcessAffinity.Read(entry).ProcessMask != state.ProcessMask) throw new Exception("Affinity was not restored");
        ProcessManager.Stop(entry);
        if (!child.WaitForExit(5000)) throw new Exception("Child did not exit");
        Console.WriteLine("OK: own child process priority, affinity backup/restore, and stop");
    }
    finally { if (!child.HasExited) { child.Kill(true); child.WaitForExit(); } Directory.Delete(temp, true); }
}
