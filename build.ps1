$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'wpf\CacheTrim.Wpf.csproj'
$petProject = Join-Path $root 'companion\DesktopPet\DesktopPet.csproj'
$dist = Join-Path $root 'dist'
$publish = Join-Path $dist 'publish'
$petPublish = Join-Path $dist 'pet-publish'
$out = Join-Path $dist 'CacheTrim.exe'

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetPath = $null
if ($dotnetCommand) { $dotnetPath = $dotnetCommand.Source }
if (-not $dotnetPath) {
    $candidate = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $candidate) { $dotnetPath = $candidate }
}
if (-not $dotnetPath) {
    throw '.NET 10 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0'
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
if (Test-Path -LiteralPath $petPublish) { Remove-Item -LiteralPath $petPublish -Recurse -Force }

& $dotnetPath publish $petProject -c Release -r win-x64 `
    -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false -o $petPublish
if ($LASTEXITCODE -ne 0) { throw "Desktop Pet publish failed: $LASTEXITCODE" }
$petExe = Join-Path $petPublish 'DesktopPet.exe'
$petHash = (Get-FileHash -LiteralPath $petExe -Algorithm SHA256).Hash.ToLowerInvariant()
& $dotnetPath publish $project -c Release -r win-x64 -p:Platform=x64 `
    -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false "-p:TrustedDesktopPetSha256=$petHash" -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

$assembly = Join-Path $root 'wpf\obj\x64\Release\net10.0-windows10.0.19041.0\win-x64\CacheTrim.dll'
$pinTest = Join-Path $root 'tests\BuildPinTests\BuildPinTests.csproj'
& $dotnetPath run --project $pinTest -c Release -- $assembly $petHash
if ($LASTEXITCODE -ne 0) { throw 'CacheTrim does not pin the matching Desktop Pet' }
Copy-Item -LiteralPath (Join-Path $publish 'CacheTrim.exe') -Destination $out -Force
Copy-Item -LiteralPath $petExe -Destination (Join-Path $dist 'DesktopPet.exe') -Force
# Vosk native dependencies and model are separate files, not covered by the EXE hash.
$modelSource = Join-Path $petPublish 'VoiceModel'
if (-not (Test-Path -LiteralPath $modelSource)) { throw 'Bundled Russian voice model is missing' }
$modelTarget = Join-Path $dist 'VoiceModel'
if (Test-Path -LiteralPath $modelTarget) { Remove-Item -LiteralPath $modelTarget -Recurse -Force }
Copy-Item -LiteralPath $modelSource -Destination $modelTarget -Recurse -Force
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$voskNative = Join-Path $nugetRoot 'vosk\0.3.38\build\lib\win-x64'
foreach ($name in @('libvosk.dll', 'libgcc_s_seh-1.dll', 'libstdc++-6.dll', 'libwinpthread-1.dll')) {
    $source = Join-Path $voskNative $name
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing Vosk native dependency: $name" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $dist $name) -Force
}
$exe = Get-Item -LiteralPath $out
Write-Host ("OK  {0}  {1:N0} bytes" -f $exe.FullName, $exe.Length)
