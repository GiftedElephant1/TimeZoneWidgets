# Builds TimeZoneWidgets.exe with the C# compiler that ships inside Windows (.NET Framework 4.x).
# Nothing needs to be installed: no Visual Studio, no .NET SDK.
#
#   powershell -ExecutionPolicy Bypass -File build.ps1            # build only
#   powershell -ExecutionPolicy Bypass -File build.ps1 -Sign      # build and Authenticode-sign the exe
#
# Signing uses the first code-signing certificate in the current user's personal store
# (Cert:\CurrentUser\My) whose subject contains "Time Zone Widgets", or the certificate given
# with -Thumbprint, or a PFX file given with -PfxPath/-PfxPassword (for CI).
# The signature is timestamped so it stays valid after the certificate expires.
#
param(
    [switch]$Sign,
    [string]$Thumbprint = $env:TZW_SIGN_THUMBPRINT,
    [string]$PfxPath = $env:TZW_SIGN_PFX,
    [string]$PfxPassword = $env:TZW_SIGN_PFX_PASSWORD,
    [string]$TimestampServer = "http://timestamp.digicert.com"
)
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

$cscArgs = @(
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

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed." }

Copy-Item (Join-Path $root "src\App.config") (Join-Path $out "TimeZoneWidgets.exe.config") -Force
Write-Host "Built $exe"

if (-not $Sign -and -not $Thumbprint -and -not $PfxPath) { return }

# ---- Authenticode signing -------------------------------------------------------------------
$cert = $null
if ($PfxPath) {
    $secure = if ($PfxPassword) { ConvertTo-SecureString $PfxPassword -AsPlainText -Force } else { $null }
    $cert = Get-PfxCertificate -FilePath $PfxPath -Password $secure
} elseif ($Thumbprint) {
    $cert = Get-ChildItem Cert:\CurrentUser\My\$Thumbprint -ErrorAction SilentlyContinue
    if (-not $cert) { $cert = Get-ChildItem Cert:\LocalMachine\My\$Thumbprint -ErrorAction SilentlyContinue }
    if (-not $cert) { throw "No certificate with thumbprint $Thumbprint in the personal stores." }
} else {
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
        Where-Object { $_.Subject -like "*Time Zone Widgets*" -and $_.NotAfter -gt (Get-Date) } |
        Sort-Object NotAfter -Descending | Select-Object -First 1
    if (-not $cert) {
        throw "No code-signing certificate found. Create one with tools\new-signing-cert.ps1 or pass -Thumbprint / -PfxPath."
    }
}

$sig = $null
try {
    $sig = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $TimestampServer
} catch {
    Write-Warning "Timestamping failed ($($_.Exception.Message)); signing without a timestamp."
    $sig = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert -HashAlgorithm SHA256
}
if ($sig.Status -eq "NotSigned" -or $sig.Status -eq "HashMismatch" -or $sig.Status -eq "Incompatible") {
    throw "Signing failed: $($sig.Status) $($sig.StatusMessage)"
}
Write-Host "Signed with '$($cert.Subject)' (thumbprint $($cert.Thumbprint)); signature status: $($sig.Status)"
if ($sig.Status -ne "Valid") {
    Write-Host "  ($($sig.StatusMessage.Trim()) - expected for a self-signed certificate that is not in a trusted root store.)"
}
