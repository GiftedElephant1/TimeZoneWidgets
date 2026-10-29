# Creates a self-signed code-signing certificate for building signed releases of Time Zone Widgets.
#
#   powershell -ExecutionPolicy Bypass -File tools\new-signing-cert.ps1 [-Name "Your Name"]
#
# The certificate goes into the current user's personal store (Cert:\CurrentUser\My) only. It is NOT
# added to any trusted-root store, so it changes nothing about what Windows trusts. build.ps1 -Sign
# picks it up automatically.
#
# A self-signed certificate proves the exe was not modified after you built it, but Windows will still
# show "Unknown publisher" and SmartScreen may still warn, because no certificate authority vouches for
# it. For warning-free installs you need a certificate from a CA (or Azure Trusted Signing / SignPath);
# pass that one to build.ps1 with -Thumbprint or -PfxPath instead.
param(
    [string]$Name = "Time Zone Widgets",
    [int]$Years = 5
)
$ErrorActionPreference = "Stop"
$subject = "CN=$Name"
$cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $subject `
    -FriendlyName "$Name code signing" -CertStoreLocation Cert:\CurrentUser\My `
    -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy Exportable `
    -NotAfter (Get-Date).AddYears($Years)
Write-Host "Created code-signing certificate"
Write-Host "  Subject:    $($cert.Subject)"
Write-Host "  Thumbprint: $($cert.Thumbprint)"
Write-Host "  Expires:    $($cert.NotAfter)"
Write-Host "Build a signed exe with:  build.cmd -Sign"
Write-Host "To back it up or use it in CI, export it from certmgr.msc (Personal > Certificates) as a password-protected PFX."
