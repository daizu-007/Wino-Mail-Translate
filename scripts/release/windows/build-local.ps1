#requires -Version 7.2
<#
.SYNOPSIS
Builds a self-signed Release x64 bundle for personal use.
.DESCRIPTION
Reuses the release packager and a certificate in CurrentUser\My. Only the
public certificate is exported. See README-JP.md for installation and backup.
#>
[CmdletBinding()]
param(
    [string]$OutputRoot,
    [string]$CertificateThumbprint,
    [ValidatePattern('^10\.0\.\d+$')][string]$RuntimeFrameworkVersion = '10.0.14',
    [switch]$Install
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This script requires Windows.' }
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (-not $OutputRoot) { $OutputRoot = Join-Path $repositoryRoot 'artifacts/local-release' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
[xml]$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot 'src/Wino.Mail.WinUI/Package.appxmanifest') -Raw
$publisher = [string]$manifest.Package.Identity.Publisher
$version = [string]$manifest.Package.Identity.Version
$destination = Join-Path $OutputRoot "$version/Store"
if (Test-Path -LiteralPath $destination) { throw "Output already exists; choose another -OutputRoot: $destination" }

$friendlyName = 'Wino Mail personal Release signing'
if ($CertificateThumbprint) {
    $CertificateThumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
    if ($CertificateThumbprint -notmatch '^[0-9A-F]{40}$') { throw 'Invalid certificate thumbprint.' }
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
}
else {
    $certificates = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object {
        $_.FriendlyName -eq $friendlyName -and $_.Subject -ceq $publisher
    })
    if ($certificates.Count -gt 1) { throw 'Multiple personal certificates exist. Select -CertificateThumbprint explicitly.' }
    $certificate = $certificates | Select-Object -First 1
    if ($null -eq $certificate) {
        $certificate = New-SelfSignedCertificate -Type Custom -Subject $publisher `
            -FriendlyName $friendlyName -KeyUsage DigitalSignature -KeyAlgorithm RSA `
            -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy Exportable `
            -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(3) `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    }
}
$now = Get-Date
if ($certificate.Subject -cne $publisher -or -not $certificate.HasPrivateKey -or
    $certificate.NotBefore -gt $now -or $certificate.NotAfter -le $now -or
    @($certificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq '1.3.6.1.5.5.7.3.3' }).Count -eq 0) {
    throw 'The certificate must match the manifest publisher, be valid for code signing, and have a private key.'
}
Write-Host "Signing certificate: $($certificate.Thumbprint) (expires $($certificate.NotAfter.ToString('yyyy-MM-dd')))"
$pwsh = Join-Path $PSHOME 'pwsh.exe'
Push-Location $repositoryRoot
try {
    & $pwsh -NoProfile -File (Join-Path $PSScriptRoot 'build-releases.ps1') `
        -NonInteractive -SkipTranslation -Store -Architectures x64 `
        -StoreTestCertificateThumbprint $certificate.Thumbprint -OutputRoot $OutputRoot `
        -RuntimeFrameworkVersion $RuntimeFrameworkVersion
    if ($LASTEXITCODE -ne 0) { throw "Release packaging failed (exit $LASTEXITCODE)." }
}
finally { Pop-Location }

$bundle = Join-Path $destination "WinoMail_Store_$version.msixbundle"
$publicCertificate = Join-Path $destination 'WinoMail_Store_TestCertificate.cer'
$signature = Get-AuthenticodeSignature -LiteralPath $bundle
if ($null -eq $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
    throw 'The bundle signer does not match the selected certificate.'
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install-local.ps1') -Destination $destination
@"
Wino Mail $version / Windows x64
証明書の拇印をビルドPCと別経路で照合してから、PowerShell 7.2以降で実行してください。
pwsh -NoProfile -File .\install-local.ps1 -ExpectedThumbprint '$($certificate.Thumbprint)'
証明書の信頼登録に管理者の承認（UAC）が必要です。完了後、スタートメニューから起動してください。
秘密鍵（PFX）は配布しないでください。詳しい手順はソースのREADME-JP.mdを参照してください。
"@ | Set-Content -LiteralPath (Join-Path $destination 'INSTALL.txt') -Encoding utf8
$certificate.Thumbprint | Set-Content -LiteralPath (Join-Path $destination 'certificate-thumbprint.txt')
Write-Host "Ready: $destination"
if ($Install) {
    & (Join-Path $destination 'install-local.ps1') -BundlePath $bundle -CertificatePath $publicCertificate `
        -ExpectedThumbprint $certificate.Thumbprint
}
