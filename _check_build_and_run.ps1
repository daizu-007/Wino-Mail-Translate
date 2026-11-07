#requires -Version 7.2
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$targetVersion = [version]'2.2.12.0'
$packageName = '58272BurakKSE.WinoMailPreview'
$packageFamilyName = '58272BurakKSE.WinoMailPreview_mhdqskaa8n2sj'
$repositoryRoot = $PSScriptRoot
$storeDirectory = Join-Path $repositoryRoot 'artifacts/local-release/2.2.12.0/Store'
$bundlePath = Join-Path $storeDirectory 'WinoMail_Store_2.2.12.0.msixbundle'
$certificatePath = Join-Path $storeDirectory 'WinoMail_Store_TestCertificate.cer'
$installScript = Join-Path $storeDirectory 'install-local.ps1'
$thumbprint = (Get-Content -LiteralPath (Join-Path $storeDirectory 'certificate-thumbprint.txt') -Raw).Trim()
$logPath = Join-Path $repositoryRoot '_check_build_and_run.log'

function Write-CheckLog([string]$Message) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') $Message"
    Add-Content -LiteralPath $logPath -Value $line
    Write-Host $line
}

if (-not (Test-Path -LiteralPath $bundlePath)) { throw "Built bundle not found: $bundlePath" }

while ($true) {
    $installed = Get-AppxPackage -Name $packageName
    if ($installed -and [version]$installed.Version -ge $targetVersion) {
        Write-CheckLog "Version $($installed.Version) is installed. Starting Wino Mail."
        Start-Process -FilePath explorer.exe -ArgumentList "shell:AppsFolder\$packageFamilyName!App"
        Write-CheckLog 'Wino Mail activation requested.'
        break
    }

    Write-CheckLog "Installed version is $($installed.Version); target is $targetVersion. Checking for a running Wino process."
    $winoProcesses = @(Get-Process -Name 'Wino.Mail.WinUI' -ErrorAction SilentlyContinue)
    foreach ($process in $winoProcesses) {
        if ($process.MainWindowHandle -ne 0) { [void]$process.CloseMainWindow() }
    }
    if ($winoProcesses.Count -gt 0) {
        Start-Sleep -Seconds 15
        foreach ($process in $winoProcesses) {
            $process.Refresh()
            if (-not $process.HasExited) {
                Write-CheckLog "Closing remaining Wino Mail process $($process.Id) so the package can update."
                Stop-Process -Id $process.Id -Force
            }
        }
    }

    try {
        & $installScript -BundlePath $bundlePath -CertificatePath $certificatePath -ExpectedThumbprint $thumbprint
    }
    catch {
        Write-CheckLog "Install attempt did not complete: $($_.Exception.Message)"
    }

    $installed = Get-AppxPackage -Name $packageName
    if ($installed -and [version]$installed.Version -ge $targetVersion) { continue }
    Write-CheckLog 'Not installed yet. Checking again in one minute.'
    Start-Sleep -Seconds 60
}
