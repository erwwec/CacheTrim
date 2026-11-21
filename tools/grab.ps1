param(
    [Parameter(Mandatory=$true)][string]$Process,
    [Parameter(Mandatory=$true)][string]$Out
)
# Captures a single top-level window by process name via PrintWindow (ignores z-order).
# Kept outside %TEMP% on purpose: CacheTrim cleans %TEMP% for real.
Add-Type -AssemblyName System.Drawing
$code = @'
using System;using System.Runtime.InteropServices;using System.Drawing;
public class Cap {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  public struct R{public int L,T,Rr,B;}
  public static string Grab(IntPtr h, string path) {
    R r; GetWindowRect(h, out r);
    int w = r.Rr - r.L, hh = r.B - r.T;
    if (w <= 0 || hh <= 0) return "window not measurable";
    Bitmap bmp = new Bitmap(w, hh);
    Graphics g = Graphics.FromImage(bmp);
    PrintWindow(h, g.GetHdc(), 2);
    g.Dispose();
    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    bmp.Dispose();
    return path + " " + w + "x" + hh;
  }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing
$p = Get-Process -Name $Process -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
[Cap]::Grab($p.MainWindowHandle, $Out)
