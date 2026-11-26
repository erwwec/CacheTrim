using CacheTrim;
var windows = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FakeWindows"));
var app = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Apps", "app.exe"));
var entry = new ProcessManager.Entry(100, "app", app, 1234, 1, 0, "Normal");
void Allowed(ProcessManager.Entry selected, ProcessManager.Entry current, int self = 99, int session = 1) => ProcessManager.Validate(selected, current, self, session, windows);
void Rejected(ProcessManager.Entry selected, ProcessManager.Entry current, int self = 99, int session = 1) { try { Allowed(selected, current, self, session); } catch (InvalidOperationException) { return; } throw new Exception("Expected rejection"); }
Allowed(entry, entry);
Rejected(entry, entry with { StartUtcTicks = 4321 });
Rejected(entry, entry with { Path = app + ".other" });
Rejected(entry, entry, self: 100);
Rejected(entry, entry, session: 2);
var system = entry with { Path = Path.Combine(windows, "system32", "system.exe") };
Rejected(system, system);
Allowed(entry with { Path = windows + "-other/app.exe" }, entry with { Path = windows + "-other/app.exe" });
Console.WriteLine("OK: process identity, session, self and Windows paths protected");

var dir = Path.Combine(Path.GetTempPath(), "cachetrim-profiles-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
try {
    string file = Path.Combine(dir, "profiles.json");
    ProcessProfiles.Save(file, app, "BelowNormal");
    if (ProcessProfiles.Find(file, app) != System.Diagnostics.ProcessPriorityClass.BelowNormal) throw new Exception("profile roundtrip");
    try { ProcessProfiles.Save(file, app, "RealTime"); throw new Exception("unsafe priority accepted"); } catch (ArgumentException) { }
    File.WriteAllText(file, "broken");
    try { ProcessProfiles.Save(file, app, "Normal"); throw new Exception("broken profile accepted"); } catch (System.Text.Json.JsonException) { }
    if (File.ReadAllText(file) != "broken") throw new Exception("corrupt profile overwritten");
    Console.WriteLine("OK: profiles roundtrip, unsafe priorities blocked, corrupt data retained");
} finally { Directory.Delete(dir, true); }
