# Locked-state Analytics research (2026-10-07)

The requested scope is an iPhone **after first unlock, then screen lock**. Before-first-unlock after reboot is excluded.

The [Mac investigation](https://github.com/MochiLog/MochiLog-Mac/blob/main/docs/LOCKED_ANALYTICS_RESEARCH.md) tested a paired iPhone 17 on iOS 27.2. Native RSD exposed Analytics names and the size of a 21,327,604-byte file, but read-only FILE_OPEN returned AFC PERM_DENIED (10). Passing the saved RemotePairing unlock credential to RSDCheckin returned EscrowFailure. Classic paired Wi-Fi service connections also failed. Apple's CoreDevice file-copy route failed on remote openat with EPERM despite reporting Readable metadata.

These were Mac-hosted live probes, **not Windows live verification**. Windows uses a userspace RSD tunnel rather than the macOS native tunnel, so it needs its own locked-state verification if an accepted escrow route is found. The current product requirement to unlock during collection remains unchanged.

An additional [independent C/Python client](https://github.com/MochiLog/MochiLog-Mac/tree/main/scripts/research) implements native tunnel assertions, HTTP/2/RemoteXPC, plist check-in, paired TLS and read-only AFC without importing or executing pymobiledevice3. On the AFU-locked iPhone it again reached file metadata, then received PERM_DENIED at FILE_OPEN; its existing RemotePairing credential check-in returned EscrowFailure. Thus the tested denial is not specific to the upstream library's file-read implementation. The native assertion helper is macOS-only and has not been ported to Windows. The alternate osanalytics.logTransfer service was inventoried but no readable Analytics route was established. Nine offline protocol/output tests pass; there is no collector replacement or release from this experiment.

Further Mac-only probes found a longer-lock native tunnel rejection (RemotePairingError 1016), and classic diagnostics/file_relay StartService rejection with PasswordProtected. The OS's already-present RemotePairing host key differs from the saved tool key, so the earlier EscrowFailure may reflect a pairing-context mismatch; difference is not proof of an invalid key. An identity-matched OS-key probe is implemented, but the live attempt stopped at tunnel creation before check-in. A new locked-state comparison is still unverified. Backup and Mirroring were excluded by the user. Thirteen offline tests now pass; no Windows transport, shipped collector, OS trust, keys or protection settings were changed. See the Mac memo for the precise evidence and remaining control.

Shared requirements for any follow-up implementation:

- Verify the file body, not just a successful listing, size, process exit code or empty destination file.
- Preserve pending files after permission/incomplete-read failures. Do not permanently classify them as unrelated battery data.
- Classic escrow and RemotePairing unlock credentials are different. Use only the existing authorized credential for the correct transport, never store it in app-pairing JSON or diagnostic logs, and never silently re-pair users.
- `include_escrow_bag=True` is an upstream option, not evidence that Analytics acquisition works. Require a complete nonempty file while the phone stays locked, then Windows and Developer Mode-off verification before changing guides or promises.
- A successful Tailscale app transfer does not prove that new system Analytics collection works over Tailscale.

No runtime changes or release were made for this investigation. The next experiment is validating the existing credential while unlocked, then retrying after screen lock; a fresh authorized unlock credential may need separate investigation if EscrowFailure persists.
