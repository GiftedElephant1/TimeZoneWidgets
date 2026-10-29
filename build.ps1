# Builds TimeZoneWidgets.exe with the C# compiler that ships inside Windows (.NET Framework 4.x).
# Nothing needs to be installed: no Visual Studio, no .NET SDK.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1
#
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$candidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$csc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) { throw "Could not find csc.exe. .NET Framework 4.x is required (it is part of Windows 8.1 and later)." }

$out = Join-Path $root "bin"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$exe = Join-Path $out "TimeZoneWidgets.exe"

$icon = Join-Path $root "assets\app.ico"
if (-not (Test-Path $icon)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tools\make-icon.ps1")
}

$sources = Get-ChildItem (Join-Path $root "src") -Filter *.cs | ForEach-Object { $_.FullName }

$args = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/warn:3",
    "/out:$exe",
    "/win32icon:$icon",
    "/win32manifest:$(Join-Path $root 'src\app.manifest')",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Runtime.Serialization.dll"
) + $sources

& $csc @args
if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }

Copy-Item (Join-Path $root "src\App.config") (Join-Path $out "TimeZoneWidgets.exe.config") -Force
Write-Host "Built $exe"
