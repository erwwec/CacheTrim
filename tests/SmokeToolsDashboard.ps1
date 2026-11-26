$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class ToolDashboardSmoke {
    public delegate bool Callback(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
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
$exe = (Resolve-Path '.\dist\verify\CacheTrim.exe').Path
$process = Start-Process -FilePath $exe -PassThru
try {
    $hwnd = [IntPtr]::Zero
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Seconds 1; $process.Refresh()
        if ($process.HasExited) { throw "CacheTrim exited early: $($process.ExitCode)" }
        $hwnd = [ToolDashboardSmoke]::FindMain($process.Id)
        if ($hwnd -ne [IntPtr]::Zero) { break }
    }
    if ($hwnd -eq [IntPtr]::Zero) { throw 'Main window not found' }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    function FindId($id) {
        $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
        return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    }
    $nav = $null
    for ($i = 0; $i -lt 20 -and $null -eq $nav; $i++) { $nav = FindId 'nav-advanced'; if ($null -eq $nav) { Start-Sleep -Milliseconds 500 } }
    if ($null -eq $nav) { throw 'Advanced navigation button not found' }
    $invoke = $nav.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invoke.Invoke()
    $first = $null
    for ($i = 0; $i -lt 20 -and $null -eq $first; $i++) { $first = FindId 'tool-windows-files'; if ($null -eq $first) { Start-Sleep -Milliseconds 500 } }
    $ids = @('windows-files','disk-scan','resource-monitor','windows-update','games','capture','processes','affinity','disk-health','cpu-test','display','network','security','packages','residual')
    foreach ($id in $ids) {
        $element = FindId ('tool-' + $id)
        if ($null -eq $element) { throw "Missing tool $id" }
        if (-not $element.Current.IsEnabled -or $element.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button) { throw "Tool $id is not an enabled button" }
        if ([string]::IsNullOrWhiteSpace($element.Current.Name)) { throw "Tool $id has no accessible name" }
    }
    Write-Host "Tools dashboard: $($ids.Count) named buttons found; no tool actions invoked."
}
finally { if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force } }
