using CacheTrim;
static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("OK: " + name); }
var disk = SystemInsights.FormatDisk("[{\"Name\":\"SSD\",\"Media\":\"SSD\",\"Health\":\"Healthy\",\"Temperature\":33,\"Wear\":0,\"Hours\":null,\"ReadErrors\":3}]");
Check(disk.Contains("SSD") && disk.Contains("33 °C") && disk.Contains("0 %") && disk.Contains("нет данных") && disk.Contains("чтения: 3"), "disk fields and nulls");
Check(SystemInsights.FormatDisk("[]").Contains("не вернула физических дисков"), "empty disks");
Check(SystemInsights.FormatSecurity("{\"SecureBoot\":true,\"TpmPresent\":false,\"TpmReady\":null}").Contains("Secure Boot: включён\nTPM: не обнаружен\nГотовность TPM: неизвестно"), "security states");
try { SystemInsights.FormatDisk("{}"); throw new Exception("object accepted"); } catch (FormatException) { Console.WriteLine("OK: invalid root rejected"); }

string dir = Path.Combine(Path.GetTempPath(), "cachetrim-insights-" + Guid.NewGuid());
Directory.CreateDirectory(dir);
try
{
    string path = Path.Combine(dir, "history.json");
    var first = new LocalBenchmark.Result(DateTimeOffset.UtcNow, 123.5);
    Check(LocalBenchmark.AddAndSave(path, first).Count == 1, "benchmark history created");
    Check(LocalBenchmark.AddAndSave(path, first).Count == 2, "benchmark history appended");
    Check(LocalBenchmark.Format(first, new[] { first }).Contains("не онлайн-рейтинг"), "benchmark labels scope");
    File.WriteAllText(path, "broken");
    try { LocalBenchmark.AddAndSave(path, first); throw new Exception("corrupt history accepted"); } catch (System.Text.Json.JsonException) { }
    Check(File.ReadAllText(path) == "broken", "corrupt history preserved");
}
finally { Directory.Delete(dir, true); }
