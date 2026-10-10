# Shared by local builds and the installer CI. No runtime Python setup for users.
param([string]$Python = "python", [ValidateRange(1, 32)][int]$Jobs = 1)
$ErrorActionPreference = "Stop"
Set-Location (Resolve-Path (Join-Path $PSScriptRoot ".."))
New-Item -ItemType Directory -Force Build | Out-Null
if ((& $Python -c 'import sys; print(sys.version_info[:2] == (3, 13))') -ne 'True') {
    throw "Use Python 3.13 to build the pinned collector."
}
if (-not (Test-Path Build\NuitkaVenv\Scripts\python.exe)) {
    & $Python -m venv Build\NuitkaVenv
    if ($LASTEXITCODE -ne 0) { throw "Could not create the collector build environment." }
}
$venvPython = (Resolve-Path Build\NuitkaVenv\Scripts\python.exe).Path
if ((& $venvPython -c 'import sys; print(sys.version_info[:2] == (3, 13))') -ne 'True') {
    throw "Remove Build/NuitkaVenv and recreate it with Python 3.13."
}
& $venvPython -m pip install --disable-pip-version-check -r requirements-build.txt
if ($LASTEXITCODE -ne 0) { throw "Could not install pinned collector build dependencies." }
& $venvPython scripts\compile-collector.py --jobs $Jobs
if ($LASTEXITCODE -ne 0) { throw "Collector compilation or standalone verification failed." }
