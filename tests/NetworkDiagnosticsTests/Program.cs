using CacheTrim;
static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("OK: " + label); }
Check(NetworkDiagnostics.ValidateTarget(" example.com ") == "example.com", "host accepted and trimmed");
Check(NetworkDiagnostics.ValidateTarget("127.0.0.1") == "127.0.0.1", "IP address accepted");
foreach (var invalid in new[] { "", "https://example.com/path", "example.com:80", "abc\nxyz", new string('a', 254) })
{
    bool rejected = false; try { NetworkDiagnostics.ValidateTarget(invalid); } catch (ArgumentException) { rejected = true; }
    Check(rejected, "invalid target rejected");
}
