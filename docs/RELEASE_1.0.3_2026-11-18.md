# Windows 1.0.3 release verification — 2026-11-18

## Source and package

Product source: `1814706b56826586fc45d6e0f306a51b665a9b42`, version 1.0.3. Includes the current-failure persistence correction in [the stability investigation](PC_LINK_STABILITY_2026-11-18.md) and [pymobiledevice3 11.59.0](DEVICE_LIBRARY_11.59_RELEASE_CHECK_2026-11-18.md).

[CI run 44288625891](https://github.com/MochiLog/MochiLog-Windows/actions/runs/44288625891) passed Python transport tests, native encrypted protocol/DPAPI persistence and support-export checks, self-contained Release build, installed/uninstalled/reinstalled app checks and isolated compiled-collector execution without Python. The real-library provider factory/cleanup and actual discovery/trust/crash CLI shapes passed. Both compiled checks report pymobiledevice3 11.59.0 and all seven checked modules compiled.

The public installer is that exact CI artifact: 151,966,977 bytes, SHA-256 `89d41d76377291bed26ab90bfac395055b3281ed1a12ba74ed016f100d6c5b63`. Users do not install Python, .NET or Windows App SDK separately. The installer is not Authenticode-signed.

## Physical update

Copied the existing native snapshot directly over SCP. Updated the existing per-user installation to 1.0.3.0 with a bounded installer invocation after backing up its encrypted state. The state file remained byte-identical during installation. Launched in the existing interactive session through an owned temporary scheduled task; the new native UI release/recreation test passed and restored the previous preference. A fresh native snapshot retained the same one paired physical-device identity. These checks do not claim a fresh real-device analytics acquisition or encrypted phone transfer.

Removed the temporary scheduled task and owned remote installer/update files after copying private evidence back. The updated app remains running. Never uninstalled the physical user's app; install/uninstall tests ran only on the disposable CI runner. No PowerShell log aggregation or deep JSON serialization was used.

## Publication

Published [v1.0.3](https://github.com/MochiLog/MochiLog-Windows/releases/tag/v1.0.3) as a stable release with Japanese/English notes and checksums. Both `MochiLog-Windows-Setup.exe` and the 0.x-updater compatibility name `MochiLog-Windows-Alpha-Setup.exe` have identical bytes and public GitHub digests matching the verified CI artifact. A fresh anonymous download matched its size and SHA-256. Existing automatic update checks can discover the new stable release.

Private snapshots, encrypted state backup, installer checks and CI logs remain in ignored `Build/release-1.0.3/`. The paired-device store format and Inno AppId were unchanged.
