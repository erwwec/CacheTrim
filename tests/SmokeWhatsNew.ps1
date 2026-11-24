$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class SmokeWindows {
    public delegate bool Callback(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    public static string[] Titles(int expectedPid) {
        var titles = new List<string>();
        EnumWindows((hwnd, param) => {
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            if (pid == expectedPid && IsWindowVisible(hwnd)) {
                var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
                if (text.Length > 0) titles.Add(text.ToString());
            }
            return true;
        }, IntPtr.Zero);
        return titles.ToArray();
    }
}
'@
$exe = (Resolve-Path '.\dist\verify\CacheTrim.exe').Path
$process = Start-Process -FilePath $exe -ArgumentList '--updated-from=2.1.12' -PassThru
try {
    $found = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Seconds 1
        $process.Refresh()
        if ($process.HasExited) { throw "CacheTrim exited early: $($process.ExitCode)" }
        $titles = [SmokeWindows]::Titles($process.Id)
        if ($titles -contains 'Что нового в CacheTrim') { $found = $true; break }
    }
    if (-not $found) { throw "Update dialog was not found. Visible windows: $($titles -join ', ')" }
    Write-Host 'Update dialog appeared.'
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}
