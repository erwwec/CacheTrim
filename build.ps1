# Builds CacheTrim.exe with the .NET Framework compiler that ships with Windows.
# No SDK, no NuGet, no restore step.
# ASCII-only on purpose: Windows PowerShell 5.1 misparses UTF-8 without a BOM.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src'
$dist = Join-Path $root 'dist'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'csc.exe not found - .NET Framework 4.x is required (ships with Windows 10/11).'
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

$sources = @(Get-ChildItem -Path $src -Filter *.cs | ForEach-Object { $_.FullName })
if ($sources.Count -eq 0) { throw "no .cs files in $src" }

$out = Join-Path $dist 'CacheTrim.exe'
if (Test-Path $out) { Remove-Item $out -Force }

$cscArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/codepage:65001'
    '/warn:4'
    "/win32manifest:$root\app.manifest"
    "/out:$out"
    '/r:System.dll'
    '/r:System.Core.dll'
    '/r:System.Drawing.dll'
    '/r:System.ServiceProcess.dll'
    '/r:System.Windows.Forms.dll'
    '/r:System.Web.Extensions.dll'
) + $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "compile failed with exit code $LASTEXITCODE" }

$exe = Get-Item $out
Write-Host ("OK  {0}  {1} bytes" -f $exe.FullName, $exe.Length)

