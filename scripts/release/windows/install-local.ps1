#requires -Version 7.2
<#
.SYNOPSIS
Trusts a verified public signing certificate and installs a personal Wino bundle.
#>
[CmdletBinding()]
param(
    [string]$BundlePath,
    [string]$CertificatePath = (Join-Path $PSScriptRoot 'WinoMail_Store_TestCertificate.cer'),
    [Parameter(Mandatory)][string]$ExpectedThumbprint
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This script requires Windows.' }
if (-not $BundlePath) {
    $bundles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.msixbundle' -File)
    if ($bundles.Count -ne 1) { throw 'Specify -BundlePath when the directory does not contain exactly one bundle.' }
    $BundlePath = $bundles[0].FullName
}
$BundlePath = (Resolve-Path -LiteralPath $BundlePath).Path
$CertificatePath = (Resolve-Path -LiteralPath $CertificatePath).Path
$ExpectedThumbprint = $ExpectedThumbprint.Replace(' ', '').ToUpperInvariant()
if ($ExpectedThumbprint -notmatch '^[0-9A-F]{40}$') { throw 'Invalid expected thumbprint.' }
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
if ($certificate.Thumbprint -ne $ExpectedThumbprint -or $certificate.HasPrivateKey) {
    throw 'Supply the public .cer certificate with the independently verified thumbprint.'
}
$signature = Get-AuthenticodeSignature -LiteralPath $BundlePath
# Windows can report an untrusted self-signed MSIX as UnknownError; require Valid after importing the pinned certificate.
if ($null -eq $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $ExpectedThumbprint -or
    $signature.Status -notin @('Valid', 'NotTrusted', 'UnknownError')) {
    throw "The bundle signature is invalid or uses another certificate: $($signature.Status)"
}
Add-Type -AssemblyName System.IO.Compression.ZipFile
$archive = [IO.Compression.ZipFile]::OpenRead($BundlePath)
try {
    $entry = $archive.GetEntry('AppxMetadata/AppxBundleManifest.xml')
    if ($null -eq $entry) { throw 'The bundle manifest is missing.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
}
finally { $archive.Dispose() }
$identity = $manifest.Bundle.Identity
if ([string]$identity.Name -cne '58272BurakKSE.WinoMailPreview' -or [string]$identity.Publisher -cne $certificate.Subject) {
    throw 'The bundle identity or publisher does not match the personal Wino package.'
}
$installed = @(Get-AppxPackage -Name ([string]$identity.Name))
foreach ($package in $installed) {
    if ($package.Publisher -cne [string]$identity.Publisher -or $package.IsDevelopmentMode) {
        throw 'An incompatible or development installation exists. Preserve it and use another Windows account or PC.'
    }
    $signatureBytes = [IO.File]::ReadAllBytes((Join-Path $package.InstallLocation 'AppxSignature.p7x'))
    $cms = [Security.Cryptography.Pkcs.SignedCms]::new()
    # AppxSignature.p7x prefixes its CMS payload with the four-byte PKCX header.
    $cms.Decode($signatureBytes[4..($signatureBytes.Length - 1)])
    if ($cms.SignerInfos.Count -ne 1 -or $cms.SignerInfos[0].Certificate.Thumbprint -ne $ExpectedThumbprint) {
        throw 'The installed package uses another signer. It will not be replaced.'
    }
    if ([version]$package.Version -ge [version]$identity.Version) {
        throw 'The installed version is the same or newer. Build a higher manifest version to update.'
    }
}

if (-not (Test-Path -LiteralPath "Cert:\LocalMachine\TrustedPeople\$ExpectedThumbprint")) {
    $escapedPath = $CertificatePath.Replace("'", "''")
    $command = @"
`$ErrorActionPreference = 'Stop'
`$cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new('$escapedPath')
if (`$cert.Thumbprint -ne '$ExpectedThumbprint') { throw 'Certificate changed before import.' }
Import-Certificate -FilePath '$escapedPath' -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
"@
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $process = Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -Verb RunAs -Wait -PassThru `
        -WindowStyle Hidden -ArgumentList @('-NoProfile', '-EncodedCommand', $encoded)
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath "Cert:\LocalMachine\TrustedPeople\$ExpectedThumbprint")) {
        throw 'Certificate trust was not installed. Accept the Windows administrator prompt and retry.'
    }
}
$signature = Get-AuthenticodeSignature -LiteralPath $BundlePath
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $ExpectedThumbprint) {
    throw "Signature validation failed after trust import: $($signature.Status)"
}
Add-AppxPackage -Path $BundlePath
Write-Host 'Installed. Open Wino Mail from the Start menu.'
