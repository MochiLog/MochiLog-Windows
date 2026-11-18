# Windows 1.0.2 release verification — 2026-11-18

## Source and checks

- Product and installer-test source: `2d7224f6ca57244104fd607cdd1eae097c30db0a`, version 1.0.2. Includes the transfer diagnostics in [the audit](TRANSFER_AUDIT_2026-11-18.md) and pymobiledevice3 11.58.0 API/CLI compatibility changes in [the library update](DEVICE_LIBRARY_UPDATE_2026-11-18.md).
- Python bridge and native encrypted transfer/support-export checks passed locally and in CI. Native protocol tests were also run on the existing physical Windows computer in an isolated temporary checkout.
- The first final-source CI run built the app and installer but its install test stopped because a fresh user profile did not yet have a per-user Uninstall registry key. The test now treats a missing key as no installation. A read-only physical Windows check verified both the missing-path case and detection of the existing 1.0.1 installation; it did not install, uninstall or mutate the registry.
- [CI run 44240293071](https://github.com/MochiLog/MochiLog-Windows/actions/runs/44240293071) passed: self-contained Release build, install/uninstall/reinstall and isolated checks of the installed compiled collector without external Python. Build information reports pymobiledevice3 11.58.0, compiled adapters and the direct-provider contract. The failed candidate was not published.
- Used that exact CI installer: 151,989,151 bytes, SHA-256 `f4a8705fc75bd3883cf0a9999483ee13162afb720cbab2695500fff099181197`. Compilation took 940.818 seconds and collector staging/verification 32.841 seconds on the 4-core CI runner; this is not the entire workflow duration.

## Scope

Windows already had rolling idle write deadlines; it did not receive a redundant replacement for the Mac-only absolute-deadline bug. It received detailed phase/type/HResult diagnostics and the same dependency/API/CLI corrections.

Raw native snapshots, encrypted state, remote test output and installer evidence remain private in ignored build directories. Existing snapshots are copied directly over SCP and parsed locally; no PowerShell log aggregation or deep JSON conversion is used.

## Physical installation

- Verified the installer's hash again on Windows, then updated the existing per-user installation to 1.0.2.0. The encrypted state file stayed byte-identical during installation; no records or pairings were reset.
- Launched in the existing interactive user session using a temporary scheduled task identified by the user's SID. Removed that task after the check; the app remains running. This did not introduce a new startup setting.
- The native UI self-check passed three release/recreation cycles with the same background server, plus the disabled-setting retention case; the previous preference was restored. Fresh native snapshots retained the same paired device identity and confirmation.
- A native protocol test and physical app startup are not proof of a fresh physical phone transfer or an external mobile/VPN collection path. The iPhone was away. The new compiled Mac helper separately read the physical iPad's registry and current-day log; that result is not presented as a new Windows hardware acquisition test.

## Public release

- Published [v1.0.2](https://github.com/MochiLog/MochiLog-Windows/releases/tag/v1.0.2) targeting the exact CI source, with Japanese/English notes. Both `MochiLog-Windows-Setup.exe` and the compatibility filename `MochiLog-Windows-Alpha-Setup.exe` have identical bytes and matching public GitHub digests; `SHA256SUMS.txt` covers both. Each is below the existing updater's 500 MiB limit.
- The installer is not Authenticode-signed; the release notes disclose this. Inno AppId and state paths are unchanged. End users do not install Python, .NET or Windows App SDK separately.
- A fresh anonymous download matched the validated CI artifact's size and SHA-256. Developer documentation and work-rule updates are committed separately from the tagged binary source.
- Removed the owned temporary protocol checkout, installer, update script and verification task after successful checks. One updated installed app remains running; private rollback/state evidence was retained.
