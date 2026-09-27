$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))
$setup = (Resolve-Path Build\MochiLog-Windows-Alpha-Setup.exe).Path
$registry = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall"

function Find-MochiLogInstallation {
    Get-ChildItem $registry | ForEach-Object { Get-ItemProperty $_.PSPath } |
        Where-Object { $_.DisplayName -eq "MochiLog Windows" }
}
function Invoke-Setup {
    $process = Start-Process -FilePath $setup -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" `
        -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Setup failed with exit code $($process.ExitCode)." }
}

if (Find-MochiLogInstallation) {
    throw "MochiLog Windows is already installed; refusing to replace or remove an existing installation."
}
Invoke-Setup
$entry = Find-MochiLogInstallation
if (-not $entry -or -not $entry.UninstallString) { throw "The uninstaller was not registered." }
$uninstaller = $entry.UninstallString.Trim('"')
if (-not (Test-Path $uninstaller)) { throw "The registered uninstaller is missing: $uninstaller" }
$app = Join-Path $env:LOCALAPPDATA "Programs\MochiLog Windows\MochiLog.Windows.exe"
$collector = Join-Path $env:LOCALAPPDATA "Programs\MochiLog Windows\Collector\pymobiledevice3.exe"
if (-not (Test-Path $app) -or -not (Test-Path $collector)) {
    throw "The app or bundled collector is missing."
}
Write-Output "Installed app and collector; uninstaller registered: $uninstaller"
$removed = Start-Process -FilePath $uninstaller -ArgumentList "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" `
    -Wait -PassThru
if ($removed.ExitCode -ne 0 -or (Find-MochiLogInstallation) -or (Test-Path $app)) {
    throw "The uninstaller did not remove the application."
}
Write-Output "Uninstall removed the app and registration."
Invoke-Setup
if (-not (Find-MochiLogInstallation)) { throw "Reinstallation failed." }
Write-Output "Reinstalled for further alpha testing."
