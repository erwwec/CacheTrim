<# : batch
@echo off
chcp 65001 >nul
fltmc >nul 2>&1 || (powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" & exit /b)
title fln explorer tweaker
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f='%~f0'; iex ([IO.File]::ReadAllText($f,[Text.Encoding]::UTF8))"
exit /b
#>

# ================== fln explorer tweaker — Идеальный сетап Windows 2026 ==================
[Console]::OutputEncoding = [Text.Encoding]::UTF8
# Шрифт консоли с кириллицей: на системах с en-US локалью conhost берёт растровый шрифт, и русский текст превращается в кракозябры
try {
    Add-Type -Namespace FlnCon -Name Font -MemberDefinition @'
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct CFI { public uint cbSize; public uint nFont; public short X; public short Y; public int Family; public int Weight;
  [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Face; }
[DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr GetStdHandle(int h);
[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] public static extern bool SetCurrentConsoleFontEx(IntPtr h, bool max, ref CFI f);
public static void Set(string face, short size) {
  var f = new CFI(); f.cbSize = (uint)Marshal.SizeOf(f); f.Y = size; f.Family = 54; f.Weight = 400; f.Face = face;
  SetCurrentConsoleFontEx(GetStdHandle(-11), false, ref f); }
'@ -ErrorAction Stop
    [FlnCon.Font]::Set('Consolas', 16)
} catch {}
$Host.UI.RawUI.WindowTitle = 'fln explorer tweaker'

function RegGet($path, $name) { try { (Get-ItemProperty -LiteralPath $path -Name $name -ErrorAction Stop).$name } catch { $null } }
function RegSet($path, $name, $value, $type = 'DWord') {
    if (-not (Test-Path -LiteralPath $path)) { New-Item -Path $path -Force | Out-Null }
    New-ItemProperty -LiteralPath $path -Name $name -Value $value -PropertyType $type -Force | Out-Null
}
function RegDel($path, $name) { Remove-ItemProperty -LiteralPath $path -Name $name -Force -ErrorAction SilentlyContinue }

$Explorer = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer'
$Blocked  = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked'
$Classes  = 'HKLM:\SOFTWARE\Classes'
$GitVerbs = 'Directory\shell\git_gui','Directory\shell\git_shell','Directory\Background\shell\git_gui','Directory\Background\shell\git_shell',
            'LibraryFolder\background\shell\git_gui','LibraryFolder\background\shell\git_shell'

# Пункт контекстного меню от сторонней программы: блокируем его расширение, удалять ничего не нужно
function New-BlockTweak($name, $hint, $clsids) {
    @{
        Name = $name
        Hint = $hint
        Test = { $null -ne (RegGet $Blocked $clsids[0]) }.GetNewClosure()
        On   = { foreach ($c in $clsids) { RegSet $Blocked $c '' 'String' }; $global:NeedExplorer = $true }.GetNewClosure()
        Off  = { foreach ($c in $clsids) { RegDel $Blocked $c }; $global:NeedExplorer = $true }.GetNewClosure()
    }
}

# Пункт левой панели проводника
function New-SidebarTweak($name, $hint, $clsid) {
    $key = "HKCU:\Software\Classes\CLSID\$clsid"
    @{
        Name = $name
        Hint = $hint
        Test = { (RegGet $key 'System.IsPinnedToNameSpaceTree') -eq 0 }.GetNewClosure()
        On   = { RegSet $key 'System.IsPinnedToNameSpaceTree' 0; $global:NeedExplorer = $true }.GetNewClosure()
        Off  = { RegSet $key 'System.IsPinnedToNameSpaceTree' 1; $global:NeedExplorer = $true }.GetNewClosure()
    }
}

# Папка, закреплённая в быстром доступе
function Get-PinnedItem($path) {
    (New-Object -ComObject Shell.Application).Namespace('shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}').Items() |
        Where-Object { $_.Path -eq $path -and $_.ExtendedProperty('System.Home.IsPinned') } | Select-Object -First 1
}
function New-UnpinTweak($name, $folder) {
    $path = [Environment]::GetFolderPath($folder)
    @{
        Name = $name
        Hint = 'убирает папку из закреплённых в левой панели'
        Test = { -not (Get-PinnedItem $path) }.GetNewClosure()
        On   = { $i = Get-PinnedItem $path; if ($i) { $i.InvokeVerb('unpinfromhome'); Start-Sleep -Milliseconds 500 } }.GetNewClosure()
        Off  = { (New-Object -ComObject Shell.Application).Namespace($path).Self.InvokeVerb('pintohome') }.GetNewClosure()
    }
}

$Tweaks = @(
    @{
        Name = 'Context Menu: Git'
        Hint = 'убирает Open Git GUI here и Open Git Bash here'
        Test = { foreach ($v in $GitVerbs) { if (Test-Path -LiteralPath "$Classes\$v") { return ($null -ne (RegGet "$Classes\$v" 'LegacyDisable')) } }; $false }
        On   = { foreach ($v in $GitVerbs) { if (Test-Path -LiteralPath "$Classes\$v") { RegSet "$Classes\$v" 'LegacyDisable' '' 'String' } } }
        Off  = { foreach ($v in $GitVerbs) { RegDel "$Classes\$v" 'LegacyDisable' } }
    },
    (New-BlockTweak 'Context Menu: AMD' 'убирает AMD Radeon Software, AMD Link и Adrenalin' @('{5E2121EE-0300-11D4-8D3B-444553540000}')),
    (New-BlockTweak 'Context Menu: NVIDIA' 'убирает NVIDIA Control Panel (сама панель открывается из Пуска)' @('{3D1975AF-48C6-4f8e-A182-BE0E08FA86A9}')),
    (New-BlockTweak 'Context Menu: Open in Terminal' 'меню становится короче, терминал открывается и через Win+X' @('{9F156763-7844-4DC4-B2B1-901F640F5155}','{02DB545A-3E20-46DE-83A5-1329B1E88B6B}')),
    @{
        Name = 'Quick Access History: Disabled'
        Hint = 'проводник не подкидывает часто используемые папки и недавние файлы'
        Test = { ((RegGet $Explorer 'ShowFrequent') -eq 0) -and ((RegGet $Explorer 'ShowRecent') -eq 0) }
        On   = {
            RegSet $Explorer 'ShowFrequent' 0; RegSet $Explorer 'ShowRecent' 0; RegSet $Explorer 'ShowCloudFilesInQuickAccess' 0
            $global:NeedExplorer = $true
        }
        Off  = {
            RegSet $Explorer 'ShowFrequent' 1; RegSet $Explorer 'ShowRecent' 1; RegSet $Explorer 'ShowCloudFilesInQuickAccess' 1
            $global:NeedExplorer = $true
        }
    },
    (New-SidebarTweak 'Sidebar: Home' 'убирает «Главную» из левой панели' '{f874310e-b6b7-47dc-bc84-b9e6b38f5903}'),
    (New-SidebarTweak 'Sidebar: Gallery' 'убирает «Галерею» из левой панели' '{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}'),
    (New-SidebarTweak 'Sidebar: Network' 'убирает «Сеть» из левой панели' '{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}'),
    (New-UnpinTweak 'Unpin: Documents' 'MyDocuments'),
    (New-UnpinTweak 'Unpin: Pictures' 'MyPictures'),
    (New-UnpinTweak 'Unpin: Music' 'MyMusic'),
    (New-UnpinTweak 'Unpin: Videos' 'MyVideos')
)

function Show-Menu {
    Clear-Host
    Write-Host ''
    Write-Host '   ███████ ██      ███    ██   ' -ForegroundColor White -NoNewline; Write-Host 'explorer tweaker' -ForegroundColor DarkGray
    Write-Host '   ██      ██      ████   ██   ' -ForegroundColor White -NoNewline; Write-Host 'Идеальный сетап Windows 2026' -ForegroundColor DarkGray
    Write-Host '   █████   ██      ██ ██  ██' -ForegroundColor White
    Write-Host '   ██      ██      ██  ██ ██' -ForegroundColor White
    Write-Host '   ██      ███████ ██   ████' -ForegroundColor White
    Write-Host ''
    Write-Host '   ' -NoNewline; Write-Host ' ВЫКЛ ' -BackgroundColor DarkRed -ForegroundColor White -NoNewline
    Write-Host ' = как в Windows по умолчанию    ' -ForegroundColor DarkGray -NoNewline
    Write-Host ' ВКЛ ' -BackgroundColor DarkGreen -ForegroundColor White -NoNewline
    Write-Host ' = твик применён' -ForegroundColor DarkGray
    Write-Host ''
    for ($i = 0; $i -lt $Tweaks.Count; $i++) {
        $t = $Tweaks[$i]
        $on = [bool](& $t.Test)
        Write-Host ("   [{0,2}] " -f ($i + 1)) -ForegroundColor White -NoNewline
        if ($on) { Write-Host ' ВКЛ  ' -BackgroundColor DarkGreen -ForegroundColor White -NoNewline; $c = 'Green' }
        else     { Write-Host ' ВЫКЛ ' -BackgroundColor DarkRed -ForegroundColor White -NoNewline; $c = 'Red' }
        Write-Host ('  ' + $t.Name) -ForegroundColor $c
        Write-Host ('             ' + $t.Hint) -ForegroundColor DarkGray
    }
    Write-Host ''
    Write-Host '   [A] ' -ForegroundColor White -NoNewline; Write-Host 'Включить все твики' -ForegroundColor Cyan
    Write-Host '   [Q] ' -ForegroundColor White -NoNewline; Write-Host 'Выход' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '   Номер твика переключает его туда-обратно.' -ForegroundColor DarkGray
    Write-Host ''
}

function Finish {
    if ($global:NeedExplorer) {
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
        $global:NeedExplorer = $false
    }
}

if ($env:FLN_TWEAKER_TEST) {
    foreach ($t in $Tweaks) { '{0,-35} {1}' -f $t.Name, [bool](& $t.Test) }
    return
}

$global:NeedExplorer = $false
while ($true) {
    Show-Menu
    $choice = (Read-Host '   Выбор').Trim().ToUpper()
    if ($choice -in 'Q', 'Й') { break }
    if ($choice -in 'A', 'Ф') {
        foreach ($t in $Tweaks) { if (-not (& $t.Test)) { & $t.On } }
    } elseif ($choice -match '^\d+$' -and [int]$choice -ge 1 -and [int]$choice -le $Tweaks.Count) {
        $t = $Tweaks[[int]$choice - 1]
        if (& $t.Test) { & $t.Off } else { & $t.On }
    }
    Finish
}

Clear-Host
Write-Host '   Готово.' -ForegroundColor Green
Start-Sleep 2
