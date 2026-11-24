using CacheTrim;
var modes = new[] { (1920, 1080, 60), (1920, 1080, 144), (1920, 1080, 60), (1280, 720, 240), (1920, 1080, 0) };
int[] rates = DisplayDiagnostics.Rates(modes, 1920, 1080);
if (!rates.SequenceEqual(new[] { 60, 144 })) throw new Exception("Incorrect filtering of display modes");
Console.WriteLine("OK: only distinct valid frequencies of current resolution");

if (DisplayDiagnostics.NativeStructureSize != 220) throw new Exception("Unexpected DEVMODEW structure size: " + DisplayDiagnostics.NativeStructureSize);
Console.WriteLine("OK: native display structure size");
