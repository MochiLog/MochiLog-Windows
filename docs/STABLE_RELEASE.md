# Stable Windows release from CI

1. Set the marketing/installer version and write Japanese and English release notes. Commit/push source and use the successful `.github/workflows/build-installer.yml` run for that exact commit.
2. Require the native protocol and Python transport tests, self-contained Release publish, compiled collector checks with Python removed from the runtime environment, and installer/uninstaller verification to pass.
3. Download `MochiLog-Windows-Setup.exe` from that run and calculate SHA-256. Before a physical update, copy the existing native diagnostic snapshot directly over SFTP/SCP. Do not aggregate logs with PowerShell `Get-Content | ConvertTo-Json`.
4. Stop only the installed MochiLog process, back up its encrypted state, run the installer with a bounded deadline and verify the expected installed version. Launch in the interactive user session. Confirm that existing paired device identities and authenticated transfers remain valid.
5. Publish a non-prerelease GitHub release targeting the successful CI source commit. Include release notes, SHA-256 checksums and both installer filenames:

   - `MochiLog-Windows-Setup.exe`: current filename.
   - `MochiLog-Windows-Alpha-Setup.exe`: identical bytes for already-installed 0.x updaters, which recognize only this name.

6. Verify both published GitHub asset digests match the validated installer and that the release is not marked draft or prerelease. Keep the installer below the updater's existing 500 MB limit.

Keep the existing Inno Setup AppId, install directory and state directory. Never uninstall a user's app to test an update. Installer/uninstaller tests belong on the disposable CI runner.

Windows code-signing credentials are not configured. Publish this limitation and the SHA-256 in the release notes; do not claim Authenticode signing. Mobile App Store review and AltStore PAL notarized-package publication are separate processes.
