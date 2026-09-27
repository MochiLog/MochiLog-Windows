param(
    [string]$Version = "0.1.0",
    [string]$Python = "python",
    [string]$InnoCompiler = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))
New-Item -ItemType Directory -Force Build, Build\Collector, Build\Licenses | Out-Null

if (-not (Test-Path Build\PythonVenv\Scripts\python.exe)) {
    & $Python -m venv Build\PythonVenv
    if ($LASTEXITCODE -ne 0) { throw "Could not create the collector build environment." }
}
$venvPython = (Resolve-Path Build\PythonVenv\Scripts\python.exe).Path
& $venvPython -m pip install --disable-pip-version-check -r requirements-build.txt
if ($LASTEXITCODE -ne 0) { throw "Could not install pinned collector build dependencies." }
& $venvPython -m PyInstaller --noconfirm --clean --onefile `
    --collect-all pymobiledevice3 --collect-all pytun_pmd3 `
    --recursive-copy-metadata pymobiledevice3 --name pymobiledevice3 `
    --distpath Build\Collector --workpath Build\PyInstaller --specpath Build CollectorEntry.py
if ($LASTEXITCODE -ne 0) { throw "Collector bundling failed." }
& Build\Collector\pymobiledevice3.exe --help | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Bundled collector cannot start." }
& $venvPython scripts\collect-licenses.py Build\Licenses
if ($LASTEXITCODE -ne 0) { throw "License collection failed." }
& $venvPython scripts\make-assets.py
if ($LASTEXITCODE -ne 0) { throw "Windows icon conversion failed." }

dotnet publish src\MochiLog.Windows\MochiLog.Windows.csproj -c Release -r win-x64 `
    -p:Platform=x64 -p:Version=$Version --self-contained true -o Build\Publish
if ($LASTEXITCODE -ne 0) { throw "WinUI publish failed." }
if (-not (Test-Path $InnoCompiler)) { throw "Inno Setup 6 compiler not found: $InnoCompiler" }
& $InnoCompiler "/DAppVersion=$Version" installer.iss
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }
Write-Output (Resolve-Path Build\MochiLog-Windows-Alpha-Setup.exe).Path
