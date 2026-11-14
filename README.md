# MochiLog Windows

WinUI 3 companion app for MochiLog's optional iOS/iPadOS 27 wireless analytics transfer. This repository is an alpha under active development.

The Windows app collects Apple analytics logs and sends them to the iPhone or iPad MochiLog app. Parsing and record storage remain on the mobile app. The Windows installer includes its collector and app runtimes, so users do not need Python, .NET, or the Windows App SDK installed separately. The installer registers an uninstaller in Windows Settings. Windows and Mac companions do not exchange data with each other.

For local development, build `src/MochiLog.Windows/MochiLog.Windows.csproj` on Windows with the .NET 10 SDK. The project uses the Windows App SDK and WinUI 3. The user-facing installer is built with `scripts/build-installer.ps1`; it bundles the Python collector, .NET runtime, Windows App SDK files, and license notices into `Build/MochiLog-Windows-Alpha-Setup.exe`. It installs for the current user and registers its uninstaller in Windows Settings. The encrypted pairing state under `%LOCALAPPDATA%\MochiLog Windows` is kept when uninstalling so a reinstall can reconnect. The tested build host is Windows 11 build 26200, x64.

The build script requires Python 3.10, the .NET 10 SDK, and Inno Setup 6 on the **developer machine**. GitHub Actions builds the same installer and uploads it as an artifact. Test the transfer protocol with `dotnet run --project tests/ProtocolTests/ProtocolTests.csproj -c Release`.

## Transfer compatibility

The Mac companion and Windows app use authenticated, encrypted `v2` transfers. The mobile app keeps separate pairings and acknowledgements for multiple computers, and a second computer cannot replace the device's physical ID. The transfer protocol has automated pairing, encryption, acknowledgement, and replay tests.

Windows device discovery and log collection are being tested against iOS/iPadOS 27 with Apple Devices and classic iTunes. Initial OS trust setup uses a data cable and an unlocked device. A real iPad diagnostic file was collected over USB with Apple Devices, but **wireless log collection has not yet passed** on that host. The bundled collector can see the iPad's Wi-Fi advertisement while USB is attached; after unplugging, the advertisement disappears. This must be resolved and tested before a public alpha release. The optional Tailscale route delivers files already queued on the PC to MochiLog over another network; it does not provide direct access to Apple's diagnostic service.

## Development safety

Do not commit pairing keys, collected analytics logs, signing certificates, or support diagnostics. The packaged collector is built from a pinned `pymobiledevice3` version; its licenses must be bundled with releases.
