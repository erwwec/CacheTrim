$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class AssistantSmokeNative {
  public delegate bool Callback(IntPtr hwnd, IntPtr lParam);
  [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L, T, R, B; }
  [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder text, int count);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out Rect rect);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int x, int y, uint data, UIntPtr extra);
  public static IntPtr Find(int pid, string title) {
    IntPtr result=IntPtr.Zero;
    EnumWindows((h,p) => { uint n; GetWindowThreadProcessId(h,out n); if(n==pid && IsWindowVisible(h)) { var t=new StringBuilder(512); GetWindowText(h,t,t.Capacity); if(t.ToString()==title){result=h; return false;} } return true; },IntPtr.Zero);
    return result;
  }
  public static IntPtr[] VisibleWindows(int pid) {
    var found = new System.Collections.Generic.List<IntPtr>();
    EnumWindows((h,p) => { uint n; GetWindowThreadProcessId(h,out n); if(n==pid && IsWindowVisible(h)) found.Add(h); return true; },IntPtr.Zero);
    return found.ToArray();
  }
}
'@
function Find-Control([int]$pidValue, [string]$automationId) {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
    foreach ($h in [AssistantSmokeNative]::VisibleWindows($pidValue)) {
        try {
            $root = [System.Windows.Automation.AutomationElement]::FromHandle($h)
            if ($root) { $found = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition); if ($found) { return $found } }
        } catch [System.Windows.Automation.ElementNotAvailableException] { }
    }
    return $null
}
$exe = (Resolve-Path '.\dist\pet-publish\DesktopPet.exe').Path
$p = Start-Process -FilePath $exe -ArgumentList '--model=experimental' -PassThru
try {
    $h = [IntPtr]::Zero
    for ($i=0; $i -lt 40; $i++) { Start-Sleep -Milliseconds 500; $p.Refresh(); if ($p.HasExited) { throw "Pet exited: $($p.ExitCode)" }; $h=[AssistantSmokeNative]::Find($p.Id,'Claude Desktop Pet'); if ($h -ne [IntPtr]::Zero) { break } }
    if ($h -eq [IntPtr]::Zero) { throw 'Pet window not found' }
    $r = [AssistantSmokeNative+Rect]::new()
    if (-not [AssistantSmokeNative]::GetWindowRect($h, [ref]$r)) { throw 'Unable to read pet window bounds' }
    # Middle of the sprite within the transparent window; proportions are DPI-independent.
    [void][AssistantSmokeNative]::SetCursorPos([int]($r.L+($r.R-$r.L)*134/260), [int]($r.T+($r.B-$r.T)*236/320))
    [AssistantSmokeNative]::mouse_event(0x0008,0,0,0,[UIntPtr]::Zero)
    [AssistantSmokeNative]::mouse_event(0x0010,0,0,0,[UIntPtr]::Zero)
    $button = $null
    for ($i=0; $i -lt 20 -and -not $button; $i++) { Start-Sleep -Milliseconds 500; $button=Find-Control $p.Id 'AssistantOpenButton' }
    if (-not $button) { throw 'Assistant button missing from pet menu' }
    $size = Find-Control $p.Id 'SizeSelector'
    if (-not $size) { throw 'Size selector missing' }
    $expand = $size.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand(); Start-Sleep -Milliseconds 500
    foreach ($expected in @('50%', '100%', '150%')) {
        $c=[System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty,$expected)
        $item=$size.FindFirst([System.Windows.Automation.TreeScope]::Descendants,$c)
        if (-not $item) { throw "Size option $expected not found" }
    }
    $expand.Collapse()
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $assistant = [IntPtr]::Zero
    for ($i=0; $i -lt 20; $i++) { Start-Sleep -Milliseconds 500; $assistant=[AssistantSmokeNative]::Find($p.Id,'Клод V2 — помощник (тест)'); if ($assistant -ne [IntPtr]::Zero) { break } }
    if ($assistant -eq [IntPtr]::Zero -or $assistant -eq $h) { throw 'Separate assistant window was not shown' }
    if (-not (Find-Control $p.Id 'AssistantCloudText')) { throw 'Assistant task input missing' }
    Write-Host 'Separate assistant window and all three size options were shown.'
} finally { $p.Refresh(); if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } }
