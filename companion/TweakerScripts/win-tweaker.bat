<# : batch
@echo off
chcp 65001 >nul
fltmc >nul 2>&1 || (powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs" & exit /b)
title fln tweaker
powershell -NoProfile -ExecutionPolicy Bypass -Command "$f='%~f0'; iex ([IO.File]::ReadAllText($f,[Text.Encoding]::UTF8))"
exit /b
#>

# ================== fln tweaker — Идеальный сетап Windows 2026 ==================
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
$Host.UI.RawUI.WindowTitle = 'fln tweaker'

function RegGet($path, $name) { try { (Get-ItemProperty -LiteralPath $path -Name $name -ErrorAction Stop).$name } catch { $null } }
function RegSet($path, $name, $value, $type = 'DWord') {
    if (-not (Test-Path -LiteralPath $path)) { New-Item -Path $path -Force | Out-Null }
    New-ItemProperty -LiteralPath $path -Name $name -Value $value -PropertyType $type -Force | Out-Null
}
function RegDel($path, $name) { Remove-ItemProperty -LiteralPath $path -Name $name -Force -ErrorAction SilentlyContinue }

$HighPerf ='8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'
$Balanced = '381b4222-f694-41f0-9685-ff5bb260df2e'
$CDM      = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
$CdmKeys  = 'SubscribedContent-338388Enabled','SubscribedContent-338389Enabled','SubscribedContent-353698Enabled',
            'SystemPaneSuggestionsEnabled','SilentInstalledAppsEnabled','SoftLandingEnabled'

$Tweaks = @(
    @{
        Name = 'Power Plan: High Performance'
        Hint = 'полезно для игровых ноутбуков'
        Test = { (powercfg /getactivescheme) -match $HighPerf }
        On   = {
            powercfg /setactive $HighPerf 2>$null
            if ($LASTEXITCODE -ne 0) { powercfg -duplicatescheme $HighPerf $HighPerf | Out-Null; powercfg /setactive $HighPerf }
        }
        Off  = { powercfg /setactive $Balanced }
    },
    @{
        Name = 'Background UWP Apps: Disabled'
        Hint = 'фоновые приложения Windows не работают'
        Test = { (RegGet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications' 'GlobalUserDisabled') -eq 1 }
        On   = {
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications' 'GlobalUserDisabled' 1
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' 'BackgroundAppGlobalToggle' 0
        }
        Off  = {
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications' 'GlobalUserDisabled' 0
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' 'BackgroundAppGlobalToggle' 1
        }
    },
    @{
        Name = 'Delivery Optimization: Disabled'
        Hint = 'ПК не раздаёт обновления чужим компьютерам'
        Test = { (RegGet 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization' 'DODownloadMode') -eq 0 }
        On   = { RegSet 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization' 'DODownloadMode' 0 }
        Off  = { RegDel 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization' 'DODownloadMode' }
    },
    @{
        Name = 'Telemetry & Ads: Disabled'
        Hint = 'телеметрия, рекламный ID, советы и реклама в системе'
        Test = {
            ((RegGet 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry') -eq 0) -and
            ((RegGet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled') -eq 0)
        }
        On   = {
            RegSet 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry' 0
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled' 0
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy' 'TailoredExperiencesWithDiagnosticDataEnabled' 0
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' 'Start_IrisRecommendations' 0
            foreach ($k in $CdmKeys) { RegSet $CDM $k 0 }
            Stop-Service DiagTrack -Force -ErrorAction SilentlyContinue
            Set-Service DiagTrack -StartupType Disabled -ErrorAction SilentlyContinue
        }
        Off  = {
            RegDel 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' 'AllowTelemetry'
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled' 1
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy' 'TailoredExperiencesWithDiagnosticDataEnabled' 1
            RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' 'Start_IrisRecommendations' 1
            foreach ($k in $CdmKeys) { RegSet $CDM $k 1 }
            Set-Service DiagTrack -StartupType Automatic -ErrorAction SilentlyContinue
            Start-Service DiagTrack -ErrorAction SilentlyContinue
        }
    },
    @{
        Name = 'Menu Show Delay: 400ms -> 20ms'
        Hint = 'меню по правой кнопке открывается мгновенно'
        Test = { (RegGet 'HKCU:\Control Panel\Desktop' 'MenuShowDelay') -eq '20' }
        On   = { RegSet 'HKCU:\Control Panel\Desktop' 'MenuShowDelay' '20' 'String' }
        Off  = { RegSet 'HKCU:\Control Panel\Desktop' 'MenuShowDelay' '400' 'String' }
    },
    @{
        Name = 'Wallpaper Compression: Disabled'
        Hint = 'обои без сжатия качества (было 85%)'
        Test = { (RegGet 'HKCU:\Control Panel\Desktop' 'JPEGImportQuality') -eq 100 }
        On   = { RegSet 'HKCU:\Control Panel\Desktop' 'JPEGImportQuality' 100 }
        Off  = { RegDel 'HKCU:\Control Panel\Desktop' 'JPEGImportQuality' }
    }
)

function Show-Menu {
    Clear-Host
    Write-Host ''
    Write-Host '   ███████ ██      ███    ██   ' -ForegroundColor White -NoNewline; Write-Host 'tweaker' -ForegroundColor DarkGray
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
        Write-Host ("   [{0}] " -f ($i + 1)) -ForegroundColor White -NoNewline
        if ($on) { Write-Host ' ВКЛ  ' -BackgroundColor DarkGreen -ForegroundColor White -NoNewline; $c = 'Green' }
        else     { Write-Host ' ВЫКЛ ' -BackgroundColor DarkRed -ForegroundColor White -NoNewline; $c = 'Red' }
        Write-Host ('  ' + $t.Name) -ForegroundColor $c
        Write-Host ('            ' + $t.Hint) -ForegroundColor DarkGray
    }
    Write-Host ''
    Write-Host '   [F] ' -ForegroundColor White -NoNewline; Write-Host 'Применить' -ForegroundColor Yellow -NoNewline
    Write-Host ' — все настройки файла ответов (для тех, кто не переустанавливал Windows)' -ForegroundColor DarkGray
    Write-Host '   [X] ' -ForegroundColor White -NoNewline; Write-Host 'Удалить Xbox и Game Bar' -ForegroundColor Magenta -NoNewline
    Write-Host ' — не для Ryzen 7900X3D / 7950X3D / 9900X3D / 9950X3D' -ForegroundColor DarkGray
    Write-Host '   [A] ' -ForegroundColor White -NoNewline; Write-Host 'Включить все твики' -ForegroundColor Cyan
    Write-Host '   [Q] ' -ForegroundColor White -NoNewline; Write-Host 'Выход' -ForegroundColor DarkGray
    Write-Host ''
    Write-Host '   Номер твика переключает его туда-обратно.' -ForegroundColor DarkGray
    Write-Host ''
}

# Всё из autounattend.xml, что можно сделать на уже установленной Windows.
# Не переносится: обход требований и аккаунта при установке, имя ПК, часовой пояс,
# обои, акцентный цвет и очистка закрепов (чтобы не стереть то, что у человека уже есть).
function Invoke-AnswerFile {
    function Step($text) { Write-Host "   > $text" -ForegroundColor DarkGray }
    $adv = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'

    Step 'Удаление встроенных приложений'
    $packages = 'Microsoft.Microsoft3DViewer','Microsoft.BingSearch','Microsoft.WindowsCamera','Clipchamp.Clipchamp',
        'Microsoft.WindowsAlarms','Microsoft.Copilot','Microsoft.549981C3F5F10','Microsoft.Windows.DevHome',
        'MicrosoftCorporationII.MicrosoftFamily','Microsoft.WindowsFeedbackHub','Microsoft.Edge.GameAssist',
        'Microsoft.GetHelp','Microsoft.Getstarted','microsoft.windowscommunicationsapps','Microsoft.WindowsMaps',
        'Microsoft.MixedReality.Portal','Microsoft.BingNews','Microsoft.MicrosoftOfficeHub','Microsoft.Office.OneNote',
        'Microsoft.OutlookForWindows','Microsoft.MSPaint','Microsoft.People','Microsoft.PowerAutomateDesktop',
        'MicrosoftCorporationII.QuickAssist','Microsoft.SkypeApp','Microsoft.ScreenSketch',
        'Microsoft.MicrosoftSolitaireCollection','Microsoft.MicrosoftStickyNotes','MicrosoftTeams','MSTeams',
        'Microsoft.Todos','Microsoft.WindowsSoundRecorder','Microsoft.Wallet','Microsoft.BingWeather',
        'Microsoft.YourPhone','Microsoft.ZuneVideo','Microsoft.Windows.Ai.Copilot.Provider'
    $provisioned = Get-AppxProvisionedPackage -Online
    foreach ($p in $packages) {
        Get-AppxPackage -AllUsers -Name $p -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
        $provisioned | Where-Object DisplayName -eq $p | Remove-AppxProvisionedPackage -Online -AllUsers -ErrorAction SilentlyContinue | Out-Null
    }

    Step 'Удаление компонентов Windows (может занять несколько минут)'
    $caps = 'Language.Handwriting','Browser.InternetExplorer','MathRecognizer','OneCoreUAP.OneSync','OpenSSH.Client',
        'Microsoft.Windows.PowerShell.ISE','App.Support.QuickAssist','Microsoft.Windows.SnippingTool','Language.Speech',
        'Language.TextToSpeech','App.StepsRecorder','Hello.Face.18967','Hello.Face.Migration.18967','Hello.Face.20134',
        'Media.WindowsMediaPlayer','Microsoft.Windows.WordPad'
    Get-WindowsCapability -Online | Where-Object { $_.State -eq 'Installed' -and ($_.Name -split '~')[0] -in $caps } |
        Remove-WindowsCapability -Online -ErrorAction SilentlyContinue | Out-Null
    foreach ($f in 'MediaPlayback','Microsoft-RemoteDesktopConnection','Recall','Microsoft-SnippingTool') {
        $feat = Get-WindowsOptionalFeature -Online -FeatureName $f -ErrorAction SilentlyContinue
        if ($feat -and $feat.State -eq 'Enabled') { Disable-WindowsOptionalFeature -Online -FeatureName $f -Remove -NoRestart -ErrorAction SilentlyContinue | Out-Null }
    }

    Step 'OneDrive, Teams, Outlook — удаление и запрет переустановки'
    foreach ($exe in "$env:windir\System32\OneDriveSetup.exe", "$env:windir\SysWOW64\OneDriveSetup.exe") {
        if (Test-Path $exe) { Start-Process $exe '/uninstall' -Wait -ErrorAction SilentlyContinue }
    }
    RegDel 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' 'OneDrive'
    RegSet 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Communications' 'ConfigureChatAutoInstall' 0
    Remove-Item 'HKLM:\Software\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\DevHomeUpdate', 'HKLM:\Software\Microsoft\WindowsUpdate\Orchestrator\UScheduler_Oobe\OutlookUpdate' -Force -ErrorAction SilentlyContinue
    RegSet 'HKLM:\Software\Policies\Microsoft\Windows\CloudContent' 'DisableCloudOptimizedContent' 1

    Step 'Безопасность: UAC, SmartScreen, Smart App Control, шифрование'
    RegSet 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' 'EnableLUA' 0
    RegSet 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' 'VerifiedAndReputablePolicyState' 0
    RegSet 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer' 'SmartScreenEnabled' 'Off' 'String'
    foreach ($n in 'ServiceEnabled','NotifyMalicious','NotifyPasswordReuse','NotifyUnsafeApp') { RegSet 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WTDS\Components' $n 0 }
    RegSet 'HKLM:\SOFTWARE\Policies\Microsoft\Windows Defender Security Center\Systray' 'HideSystray' 1
    RegSet 'HKCU:\Software\Microsoft\Edge\SmartScreenEnabled' '(default)' 0
    RegSet 'HKCU:\Software\Microsoft\Edge\SmartScreenPuaEnabled' '(default)' 0
    RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AppHost' 'EnableWebContentEvaluation' 0
    RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AppHost' 'PreventOverride' 0
    RegSet 'HKLM:\SYSTEM\CurrentControlSet\Control\BitLocker' 'PreventDeviceEncryption' 1
    net.exe accounts /maxpwage:UNLIMITED | Out-Null

    Step 'Edge, Copilot, виджеты, Bing в поиске, игровая запись'
    RegSet 'HKLM:\Software\Policies\Microsoft\Edge\Recommended' 'BackgroundModeEnabled' 0
    RegSet 'HKLM:\Software\Policies\Microsoft\Edge\Recommended' 'StartupBoostEnabled' 0
    RegSet 'HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot' 'TurnOffWindowsCopilot' 1
    RegSet 'HKLM:\SOFTWARE\Policies\Microsoft\Dsh' 'AllowNewsAndInterests' 0
    RegSet 'HKCU:\Software\Policies\Microsoft\Windows\Explorer' 'DisableSearchBoxSuggestions' 1
    RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR' 'AppCaptureEnabled' 0

    Step 'Приватность (как «отключить всё» при установке)'
    RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' 'Enabled' 0
    RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy' 'TailoredExperiencesWithDiagnosticDataEnabled' 0
    RegSet 'HKCU:\Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy' 'HasAccepted' 0
    RegSet 'HKCU:\Software\Microsoft\InputPersonalization' 'RestrictImplicitInkCollection' 1
    RegSet 'HKCU:\Software\Microsoft\InputPersonalization' 'RestrictImplicitTextCollection' 1
    RegSet 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location' 'Value' 'Deny' 'String'

    Step 'Проводник и панель задач'
    RegSet 'HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32' '(default)' '' 'String'
    RegSet $adv 'HideFileExt' 0
    RegSet $adv 'LaunchTo' 1
    RegSet $adv 'ShowTaskViewButton' 0
    RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' 'SearchboxTaskbarMode' 0
    RegSet "$adv\TaskbarDeveloperSettings" 'TaskbarEndTask' 1
    RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Start' 'VisiblePlaces' ([byte[]]@()) 'Binary'
    foreach ($k in 'ClassicStartMenu', 'NewStartPanel') {
        foreach ($g in '{5399e694-6ce5-4d6c-8fce-1d8870fdcba0}','{b4bfcc3a-db2c-424c-b029-7fe99a87c641}','{a8cdff1c-4878-43be-b5fd-f8091c1c60d0}',
                       '{374de290-123f-4565-9164-39c4925e467b}','{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}','{f874310e-b6b7-47dc-bc84-b9e6b38f5903}',
                       '{1cf1260c-4dd0-4ebb-811f-33c572699fde}','{f02c1a0d-be21-4350-88b0-7367fc96ef3c}','{3add1653-eb32-4cb0-bbd7-dfa0abb5acca}',
                       '{645ff040-5081-101b-9f08-00aa002f954e}','{20d04fe0-3aea-1069-a2d8-08002b30309d}','{59031a47-3f72-44a7-89c5-5595fe6b30ee}',
                       '{a0953c92-50dc-43bf-be83-3742fed03c9c}') {
            RegSet "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\$k" $g 1
        }
    }

    Step 'Мышь, залипание клавиш, тёмная тема'
    RegSet 'HKCU:\Control Panel\Mouse' 'MouseSpeed' '0' 'String'
    RegSet 'HKCU:\Control Panel\Mouse' 'MouseThreshold1' '0' 'String'
    RegSet 'HKCU:\Control Panel\Mouse' 'MouseThreshold2' '0' 'String'
    RegSet 'HKCU:\Control Panel\Accessibility\StickyKeys' 'Flags' '10' 'String'
    $pers = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize'
    RegSet $pers 'SystemUsesLightTheme' 0; RegSet $pers 'AppsUseLightTheme' 0
    RegSet $pers 'ColorPrevalence' 0; RegSet $pers 'EnableTransparency' 0
    RegSet 'HKCU:\Software\Microsoft\Windows\DWM' 'ColorPrevalence' 0

    Step 'Параметры быстродействия (оставлены 4 галочки)'
    Add-Type -Namespace Fln -Name Spi -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, IntPtr c, uint d);
[DllImport("user32.dll")] public static extern bool SystemParametersInfo(uint a, uint b, int[] c, uint d);
'@ -ErrorAction SilentlyContinue
    $upd = 3
    [Fln.Spi]::SystemParametersInfo(0x0004, 0, [int[]](0, 0, 0), $upd) | Out-Null          # ускорение мыши
    [Fln.Spi]::SystemParametersInfo(0x0025, 1, [IntPtr]::Zero, $upd) | Out-Null           # перетаскивать окна целиком
    [Fln.Spi]::SystemParametersInfo(0x004B, 1, [IntPtr]::Zero, $upd) | Out-Null           # сглаживание шрифтов
    foreach ($a in 0x1003, 0x1005, 0x1007, 0x1015, 0x1017, 0x101B, 0x1025, 0x1043) {        # анимации и тени — выкл
        [Fln.Spi]::SystemParametersInfo($a, 0, [IntPtr]::Zero, $upd) | Out-Null
    }
    [Fln.Spi]::SystemParametersInfo(0x0049, 8, [int[]](8, 0), $upd) | Out-Null            # анимация сворачивания окон
    RegSet 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects' 'VisualFXSetting' 3
    RegSet $adv 'TaskbarAnimations' 0; RegSet $adv 'ListviewShadow' 0; RegSet $adv 'ListviewAlphaSelect' 1; RegSet $adv 'IconsOnly' 0
    RegSet 'HKCU:\Software\Microsoft\Windows\DWM' 'EnableAeroPeek' 0
    RegSet 'HKCU:\Software\Microsoft\Windows\DWM' 'AlwaysHibernateThumbnails' 0

    $script:NeedExplorer = $true
    Write-Host ''
    Write-Host '   Готово. Перезагрузи компьютер, чтобы всё применилось (особенно UAC).' -ForegroundColor Green
    Read-Host '   Enter — назад в меню' | Out-Null
}

function Invoke-RemoveXbox {
    $cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name
    Write-Host ''
    Write-Host "   Процессор: $cpu" -ForegroundColor DarkGray
    if ($cpu -match '(7900|7950|9900|9950)X3D') {
        Write-Host '   ВНИМАНИЕ: у тебя Ryzen X3D с двумя блоками ядер.' -ForegroundColor Red
        Write-Host '   Драйвер AMD через Xbox Game Bar переводит игры на ядра с 3D-кэшем.' -ForegroundColor Red
        Write-Host '   Без Game Bar FPS в играх может упасть. Не рекомендуется удалять.' -ForegroundColor Red
    } else {
        Write-Host '   Будут удалены Xbox, Game Bar и связанные приложения.' -ForegroundColor Yellow
        Write-Host '   Не удаляй на Ryzen 7900X3D / 7950X3D / 9900X3D / 9950X3D — им Game Bar нужен для игр.' -ForegroundColor Yellow
    }
    if ((Read-Host '   Удалить? (y/n)').Trim() -notin 'y', 'Y', 'н', 'Н') { return }
    foreach ($p in 'Microsoft.Xbox.TCUI','Microsoft.XboxApp','Microsoft.XboxGameOverlay','Microsoft.XboxGamingOverlay',
                   'Microsoft.XboxIdentityProvider','Microsoft.XboxSpeechToTextOverlay','Microsoft.GamingApp') {
        Get-AppxPackage -AllUsers -Name $p -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
        Get-AppxProvisionedPackage -Online | Where-Object DisplayName -eq $p | Remove-AppxProvisionedPackage -Online -AllUsers -ErrorAction SilentlyContinue | Out-Null
    }
    Write-Host '   Готово. Вернуть Game Bar можно из Microsoft Store.' -ForegroundColor Green
    Read-Host '   Enter — назад в меню' | Out-Null
}

function Finish {
    if ($script:NeedExplorer) {
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
        $script:NeedExplorer = $false
    }
}

if ($env:FLN_TWEAKER_TEST) {
    foreach ($t in $Tweaks) { '{0,-35} {1}' -f $t.Name, [bool](& $t.Test) }
    return
}

$script:NeedExplorer = $false
while ($true) {
    Show-Menu
    $choice = (Read-Host '   Выбор').Trim().ToUpper()
    if ($choice -in 'Q', 'Й') { break }
    if ($choice -in 'F', 'А') {
        Write-Host ''
        Write-Host '   Будут удалены встроенные приложения (Teams, Outlook, Copilot, Ножницы и др.),' -ForegroundColor Yellow
        Write-Host '   выключены UAC и SmartScreen, скрыты значки рабочего стола. Обои и закрепы не трогаются.' -ForegroundColor Yellow
        if ((Read-Host '   Продолжить? (y/n)').Trim() -in 'y', 'Y', 'н', 'Н') { Invoke-AnswerFile }
    } elseif ($choice -in 'X', 'Ч') {
        Invoke-RemoveXbox
    } elseif ($choice -in 'A', 'Ф') {
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
