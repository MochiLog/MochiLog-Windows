# MochiLog Windows

WinUI 3 companion app for MochiLog's optional iOS/iPadOS 27 wireless analytics transfer. This repository is an alpha under active development.

The Windows app collects Apple analytics logs and sends them to the iPhone or iPad MochiLog app. Parsing and record storage remain on the mobile app. The Windows installer includes its collector and app runtimes, so users do not need Python, .NET, or the Windows App SDK installed separately. The installer registers an uninstaller in Windows Settings. Windows and Mac companions do not exchange data with each other.

For local development, build `src/MochiLog.Windows/MochiLog.Windows.csproj` on Windows with the .NET 10 SDK. The project uses the Windows App SDK and WinUI 3.

## Transfer compatibility

The existing Mac companion uses the authenticated, encrypted `v2` transfer protocol. Multi-computer pairing needs a versioned update so a second computer cannot replace the mobile device's existing physical ID. The mobile app must retain both host pairings and process each host's acknowledgements independently. This is being implemented before the Windows alpha is distributed.

Windows device discovery and log collection are being tested against iOS/iPadOS 27 with either Apple Devices or classic iTunes. The app must detect when Apple device support is unavailable and explain the supported choices. Users do not need to replace one Apple app with the other.

## Development safety

Do not commit pairing keys, collected analytics logs, signing certificates, or support diagnostics. The packaged collector is built from a pinned `pymobiledevice3` version; its licenses must be bundled with releases.
