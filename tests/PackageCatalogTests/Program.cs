using CacheTrim;
void Check(bool ok,string name) { if (!ok) throw new Exception(name); Console.WriteLine("OK: " + name); }
Check(PackageCatalog.ValidId("Microsoft.PowerToys"), "valid ID");
foreach (var id in new[] { "", "-bad", "x;cmd", "x\ncmd", "a b", "x/../../abc", "x\"bad" }) Check(!PackageCatalog.ValidId(id), "reject unsafe ID " + id);
var a = PackageCatalog.SearchArgs("some name", "winget"); Check(a[2] == "some name" && a[4] == "winget", "search as discrete argument");
var install = PackageCatalog.InstallArgs("Microsoft.PowerToys", "winget"); Check(install.Contains("--exact") && !install.Contains("--accept-package-agreements"), "exact install without package agreement bypass");
try { PackageCatalog.InstallArgs("x;cmd", "winget"); throw new Exception("bad ID accepted"); } catch (ArgumentException) { }
try { PackageCatalog.SearchArgs("app", "unknown"); throw new Exception("bad source accepted"); } catch (ArgumentException) { }
Console.WriteLine("OK: reject unsafe parameters");

var dir = Path.Combine(Path.GetTempPath(), "ct-residual-test-" + Guid.NewGuid()); Directory.CreateDirectory(dir);
try {
 Directory.CreateDirectory(Path.Combine(dir, "ExampleApp")); Directory.CreateDirectory(Path.Combine(dir, "other"));
 var result = ResidualSearch.Find("example", new[] { dir }); Check(result.Paths.Count == 1 && Directory.Exists(result.Paths[0]), "find candidates without deleting");
 try { ResidualSearch.Find("../folder", new[] { dir }); throw new Exception("path input accepted"); } catch (ArgumentException) { }
} finally { Directory.Delete(dir, true); }
