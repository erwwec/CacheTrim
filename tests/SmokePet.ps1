$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class PetSmokeWindows {
    public delegate bool Callback(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    public static bool HasPet(int expectedPid) {
        bool found = false;
        EnumWindows((hwnd, param) => {
            uint pid; GetWindowThreadProcessId(hwnd, out pid);
            if (pid == expectedPid && IsWindowVisible(hwnd)) {
                var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
                if (text.ToString() == "Claude Desktop Pet") found = true;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@
$exe = (Resolve-Path '.\dist\pet-publish\DesktopPet.exe').Path
# Mirror the launcher's read-only lock: verification and start must both work on Windows.
$locked = [System.IO.File]::Open($exe, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
$process = $null
try {
    $process = Start-Process -FilePath $exe -PassThru
    $found = $false
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Seconds 1
        $process.Refresh()
        if ($process.HasExited) { throw "Desktop Pet exited early: $($process.ExitCode)" }
        if ([PetSmokeWindows]::HasPet($process.Id)) { $found = $true; break }
    }
    if (-not $found) { throw 'Desktop Pet window was not found' }
    Write-Host 'Desktop Pet window appeared.'
}
finally { if ($null -ne $process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }; $locked.Dispose() }
