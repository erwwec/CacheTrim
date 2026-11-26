using CacheTrim;
using Microsoft.Win32;

static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("OK " + name); }
static void Throws<T>(Action action, string name) where T : Exception { try { action(); throw new Exception("Expected " + typeof(T).Name); } catch (T) { Check(true, name); } }
string dir = Path.Combine(Path.GetTempPath(), "CacheTrimToolkitTest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
try {
    Check(SystemToolkit.ValidPackageId("Microsoft.PowerToys") && SystemToolkit.ValidPackageId("9NBLGGH4NNS1"), "ordinary package IDs accepted");
    Check(!SystemToolkit.ValidPackageId("evil & calc") && !SystemToolkit.ValidPackageId("--source") && !SystemToolkit.ValidPackageId(""), "argument injection rejected");
    Check(!SystemToolkit.ValidSearch("a\nb") && SystemToolkit.ValidSearch("media player"), "search validated");
    var start = SystemToolkit.Winget("winget.exe", new[] { "search", "--query", "a & b" }, true);
    Check(start.ArgumentList.Count == 3 && start.ArgumentList[2] == "a & b" && !start.UseShellExecute, "search stays a single argument without shell");
    var result = await SystemToolkit.BenchmarkAsync(1, CancellationToken.None);
    Check(result.Iterations > 0 && result.Seconds >= 1 && result.Score > 0, "benchmark measures real work");
    string history = Path.Combine(dir, "history.json"); SystemToolkit.SaveBenchmark(result, history); SystemToolkit.SaveBenchmark(result with { Score = result.Score + 1 }, history);
    Check(SystemToolkit.ReadBenchmarks(history).Count == 2 && SystemToolkit.RenderBenchmarkHistory(SystemToolkit.ReadBenchmarks(history)).Contains("место 1"), "history and local ranking");
    File.WriteAllText(history, "{broken"); Throws<InvalidDataException>(() => SystemToolkit.SaveBenchmark(result, history), "corrupt history not overwritten"); Check(File.ReadAllText(history) == "{broken", "corrupt history preserved");
    using (var canceled = new CancellationTokenSource()) { canceled.Cancel(); try { await SystemToolkit.BenchmarkAsync(1, canceled.Token); throw new Exception("cancel failed"); } catch (OperationCanceledException) { Check(true, "benchmark cancellation"); } }
    string steam = Path.Combine(dir, "steam"); string apps = Path.Combine(steam, "steamapps"); Directory.CreateDirectory(apps);
    File.WriteAllText(Path.Combine(apps, "appmanifest_1.acf"), "\"AppState\" { \"appid\" \"570\" \"name\" \"Dota 2\" }");
    File.WriteAllText(Path.Combine(apps, "appmanifest_2.acf"), "\"AppState\" { \"appid\" \"bad\" \"name\" \"Invalid\" }");
    var games = GamingToolkit.DiscoverSteam(steam); Check(games.Count == 1 && games[0].Name == "Dota 2" && games[0].AppId == 570, "Steam game manifests parsed");
    string epic = Path.Combine(dir, "epic"); Directory.CreateDirectory(epic);
    File.WriteAllText(Path.Combine(epic, "game.item"), System.Text.Json.JsonSerializer.Serialize(new { DisplayName = "Example game", InstallLocation = Path.Combine(dir, "game") }));
    File.WriteAllText(Path.Combine(epic, "broken.item"), "{broken");
    Check(GamingToolkit.DiscoverEpic(epic).Single().Name == "Example game", "Epic manifests parsed and corrupt files skipped");
    string[] csv = GamingToolkit.ParseCsvLine("a,\"b,c\",\"x\"\"y\""); Check(csv.SequenceEqual(new[] { "a", "b,c", "x\"y" }), "quoted CSV parsed");
    string data = "Application,SwapChainAddress,MsBetweenPresents\nGame,1,10\nGame,1,20\nGame,1,NA\nGame,2,100\n";
    var summary = GamingToolkit.SummarizeCsv(new StringReader(data)); Check(summary.Frames == 2 && Math.Abs(summary.AveragePresentedFps - 1000.0 / 15) < 0.01 && summary.P99FrameTimeMs == 20 && summary.SwapChain == "1", "frame summary selects dominant swapchain");
    Throws<InvalidDataException>(() => GamingToolkit.SummarizeCsv(new StringReader("Application\nGame\n")), "missing CSV metrics reported");
    var id = new ManagedProcessTools.ProcessIdentity(10, 1, "game", Path.Combine(dir, "game.exe"));
    Check(ManagedProcessTools.Same(id, id with { }) && !ManagedProcessTools.Same(id, id with { StartUtcTicks = 2 }), "PID reuse identity rejected");
    Check(ManagedProcessTools.IsProtected(id with { Pid = Environment.ProcessId }, dir, Environment.ProcessId), "own process protected");
    var pb = new ManagedProcessTools.ProcessBackup(id, System.Diagnostics.ProcessPriorityClass.Normal, 3, System.Diagnostics.ProcessPriorityClass.High, 1);
    Check(ManagedProcessTools.MatchesKnown(pb, System.Diagnostics.ProcessPriorityClass.High, 1) && ManagedProcessTools.MatchesKnown(pb, System.Diagnostics.ProcessPriorityClass.Normal, 3) && !ManagedProcessTools.MatchesKnown(pb, System.Diagnostics.ProcessPriorityClass.Idle, 1), "process rollback rejects external changes");
    Check(InterruptTools.ValidInstance(@"VEN_1234&DEV_5678\instance") && !InterruptTools.ValidInstance(@"..\foo\bar"), "device paths constrained");
    if (OperatingSystem.IsWindows()) {
        string key = @"Software\CacheTrimToolkitTests\" + Guid.NewGuid().ToString("N"); string instance = @"device\instance"; string backup = Path.Combine(dir, "irq.json");
        try {
            using (Registry.CurrentUser.CreateSubKey(key + "\\" + instance)) { }
            InterruptTools.ApplyAt(Registry.CurrentUser, key, backup, instance, 2);
            using (var k = Registry.CurrentUser.OpenSubKey(key + "\\" + instance + @"\Device Parameters\Interrupt Management\Affinity Policy")) Check((int)k!.GetValue("DevicePolicy")! == 2, "test-only interrupt policy written");
            InterruptTools.RestoreAt(Registry.CurrentUser, key, backup, instance);
            using (var k = Registry.CurrentUser.OpenSubKey(key + "\\" + instance + @"\Device Parameters\Interrupt Management\Affinity Policy")) Check(k!.GetValue("DevicePolicy") == null, "absent value restored without deleting key");
            using (var k = Registry.CurrentUser.CreateSubKey(key + "\\" + instance + @"\Device Parameters\Interrupt Management\Affinity Policy")) { k.SetValue("DevicePolicy", 3, RegistryValueKind.DWord); k.SetValue("Other", 42, RegistryValueKind.DWord); }
            InterruptTools.ApplyAt(Registry.CurrentUser, key, backup, instance, 1);
            InterruptTools.ApplyAt(Registry.CurrentUser, key, backup, instance, 2);
            InterruptTools.RestoreAt(Registry.CurrentUser, key, backup, instance);
            using (var k = Registry.CurrentUser.OpenSubKey(key + "\\" + instance + @"\Device Parameters\Interrupt Management\Affinity Policy")) Check((int)k!.GetValue("DevicePolicy")! == 3 && (int)k.GetValue("Other")! == 42, "original policy and unrelated value preserved");
            InterruptTools.ApplyAt(Registry.CurrentUser, key, backup, instance, 1);
            using (var k = Registry.CurrentUser.CreateSubKey(key + "\\" + instance + @"\Device Parameters\Interrupt Management\Affinity Policy")) k.SetValue("DevicePolicy", 5, RegistryValueKind.DWord);
            Throws<InvalidOperationException>(() => InterruptTools.RestoreAt(Registry.CurrentUser, key, backup, instance), "external changes are not overwritten");
        } finally { Registry.CurrentUser.DeleteSubKeyTree(key, false); }
    }
} finally { Directory.Delete(dir, true); }
