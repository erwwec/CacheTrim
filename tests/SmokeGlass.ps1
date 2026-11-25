$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class GlassSmoke {
    public delegate bool Callback(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);
    public static IntPtr FindMain(int expectedPid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, param) => {
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            if (pid == expectedPid && IsWindowVisible(hwnd)) {
                var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
                if (text.ToString() == "CacheTrim") { found = hwnd; return false; }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@
$settings = Join-Path $env:APPDATA 'CacheTrim'
New-Item -ItemType Directory -Path $settings -Force | Out-Null
# Use a complete, valid cleanup configuration: partial files are now rejected for safety.
$cleanupFlags = @('working_sets', 'standby', 'low_standby', 'modified', 'file_cache', 'temp', 'browsers', 'wer', 'update_cache', 'd3d_cache', 'thumbnails', 'dumps', 'windows_logs', 'uwp_cache', 'recycle', 'prefetch', 'dns', 'arp', 'netbios')
$config = @($cleanupFlags | ForEach-Object { "$_=0" }) + @('temp_age_hours=24', 'theme=LiquidGlass', 'glass_background_blur=100', 'glass_density=24')
Set-Content -LiteralPath (Join-Path $settings 'config.ini') -Value $config -Encoding utf8
New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Force | Out-Null
Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name EnableTransparency -Value 1 -Type DWord
$exe = (Resolve-Path '.\dist\verify\CacheTrim.exe').Path
$process = Start-Process -FilePath $exe -PassThru
try {
    $hwnd = [IntPtr]::Zero
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Seconds 1
        $process.Refresh()
        if ($process.HasExited) { throw "CacheTrim exited early: $($process.ExitCode)" }
        $hwnd = [GlassSmoke]::FindMain($process.Id)
        if ($hwnd -ne [IntPtr]::Zero) { break }
    }
    if ($hwnd -eq [IntPtr]::Zero) { throw 'Main window not found' }
    Start-Sleep -Seconds 3
    $value = 0
    $hr = [GlassSmoke]::DwmGetWindowAttribute($hwnd, 38, [ref]$value, 4)
    if ($hr -ne 0 -or $value -ne 3) { throw "Desktop Acrylic not active: HRESULT=$hr backdrop=$value" }
    Write-Host 'DWM reports Desktop Acrylic is active.'
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
