param(
    [string]$Version = "0.1.22",
    [string]$Python = "python",
    [ValidateRange(1, 32)][int]$CompilerJobs = 1,
    [string]$InnoCompiler = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))
New-Item -ItemType Directory -Force Build, Build\Collector, Build\Licenses | Out-Null

& "$PSScriptRoot\build-collector.ps1" -Python $Python -Jobs $CompilerJobs
$venvPython = (Resolve-Path Build\NuitkaVenv\Scripts\python.exe).Path
& Build\Collector\pymobiledevice3.exe --help | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Bundled collector cannot start." }
& $venvPython scripts\collect-licenses.py Build\Licenses
if ($LASTEXITCODE -ne 0) { throw "License collection failed." }
& $venvPython scripts\make-assets.py
if ($LASTEXITCODE -ne 0) { throw "Windows icon conversion failed." }

dotnet publish src\MochiLog.Windows\MochiLog.Windows.csproj -c Release -r win-x64 `
    -p:Platform=x64 -p:Version=$Version --self-contained true -o Build\Publish
if ($LASTEXITCODE -ne 0) { throw "WinUI publish failed." }
& $venvPython scripts\collect-nuget-licenses.py Build\Licenses
if ($LASTEXITCODE -ne 0) { throw "NuGet license collection failed." }
if (-not (Test-Path $InnoCompiler)) { throw "Inno Setup 6 compiler not found: $InnoCompiler" }
& $InnoCompiler "/DAppVersion=$Version" installer.iss
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }
Write-Output (Resolve-Path Build\MochiLog-Windows-Alpha-Setup.exe).Path
