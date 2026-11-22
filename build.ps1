$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'wpf\CacheTrim.Wpf.csproj'
$dist = Join-Path $root 'dist'
$publish = Join-Path $dist 'publish'
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

& $dotnetPath publish $project -c Release -r win-x64 -p:Platform=x64 `
    -p:SelfContained=true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $publish 'CacheTrim.exe') -Destination $out -Force
$exe = Get-Item -LiteralPath $out
Write-Host ("OK  {0}  {1:N0} bytes" -f $exe.FullName, $exe.Length)
