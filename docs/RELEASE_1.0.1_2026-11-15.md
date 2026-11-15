# Windows 1.0.1 release verification — 2026-11-15

The user requested updated PC distribution artifacts for the mobile 4.1.0 release and a customer-reply draft. The original connection failure is not claimed to be reproduced or conclusively resolved.

## Validated source and artifact

- Refreshed the previously stale local checkout before release. `682d902b4dcaa5cd9cec0eaadcca43e189d75e36` includes the November 13 pairing/support/settings fixes and the November 14 security-maintenance changes.
- Reused the exact installer from successful [CI run 43584452068](https://github.com/MochiLog/MochiLog-Windows/actions/runs/43584452068), rather than rebuilding identical source. CI passed Python transport tests, native encrypted-protocol and support-export tests, self-contained Release publish, install/uninstall/reinstall and compiled-collector checks without a runtime Python environment.
- Installer SHA-256: `6ceb46df4df240359bbe68c2de6459430671767781f8c649667616d21b638357`. The installer is not Authenticode-signed; release notes disclose this. Inno AppId, state directory and app path are unchanged.

## Physical Windows check

- Connected to the existing Windows host over its LAN address when the short SSH hostname did not resolve. No DNS, firewall or device trust settings were changed.
- Copied the existing native snapshot and encrypted state directly to a private backup. No PowerShell log aggregation or deep JSON conversion was used.
- Updated the installed app with the validated CI installer. Version is 1.0.1.0; the uninstall entry reports 1.0.1. The encrypted state file was unchanged byte-for-byte during installation.
- Launched in the existing interactive user session using a temporary scheduled launcher identified by the user's SID. The first attempt used an unresolved account name; the SID-based launcher succeeded. The launcher task was removed after completing; it is not a new startup setting.
- The fresh native snapshot retained the same paired device identity and confirmation. The physical app's UI-memory self-check passed all three release/reopen cycles with the background server preserved. No personal records or pairings were deleted. No new physical smartphone QR pairing was tested.

## Public release

- Published [v1.0.1](https://github.com/MochiLog/MochiLog-Windows/releases/tag/v1.0.1), targeting the exact successful CI source above, as a stable, non-draft, non-prerelease release.
- Both `MochiLog-Windows-Setup.exe` and the compatibility name `MochiLog-Windows-Alpha-Setup.exe` have identical bytes and matching GitHub SHA-256 digests. Both are below the existing updater's 500 MiB cap. `SHA256SUMS.txt` covers both names.
- A fresh anonymous download from the public installer URL matches the validated CI artifact. Japanese/English release notes are stored in `RELEASE_NOTES_1.0.1.md`.
- This documentation-only follow-up is separate from the tagged build commit. Build, download, installation and private runtime evidence remain ignored under `Build/Release-1.0.1/`; raw diagnostics and state are not committed.
