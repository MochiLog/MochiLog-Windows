# Locked-state Analytics research (2026-10-07)

The requested scope is an iPhone **after first unlock, then screen lock**. Before-first-unlock after reboot is excluded.

The [Mac investigation](https://github.com/MochiLog/MochiLog-Mac/blob/main/docs/LOCKED_ANALYTICS_RESEARCH.md) tested a paired iPhone 17 on iOS 27.2. Native RSD exposed Analytics names and the size of a 21,327,604-byte file, but read-only FILE_OPEN returned AFC PERM_DENIED (10). Passing the saved RemotePairing unlock credential to RSDCheckin returned EscrowFailure. Classic paired Wi-Fi service connections also failed. Apple's CoreDevice file-copy route failed on remote openat with EPERM despite reporting Readable metadata.

These were Mac-hosted live probes, **not Windows live verification**. Windows uses a userspace RSD tunnel rather than the macOS native tunnel, so it needs its own locked-state verification if an accepted escrow route is found. The current product requirement to unlock during collection remains unchanged.

Shared requirements for any follow-up implementation:

- Verify the file body, not just a successful listing, size, process exit code or empty destination file.
- Preserve pending files after permission/incomplete-read failures. Do not permanently classify them as unrelated battery data.
- Classic escrow and RemotePairing unlock credentials are different. Use only the existing authorized credential for the correct transport, never store it in app-pairing JSON or diagnostic logs, and never silently re-pair users.
- `include_escrow_bag=True` is an upstream option, not evidence that Analytics acquisition works. Require a complete nonempty file while the phone stays locked, then Windows and Developer Mode-off verification before changing guides or promises.
- A successful Tailscale app transfer does not prove that new system Analytics collection works over Tailscale.

No runtime changes or release were made for this investigation. The next experiment is validating the existing credential while unlocked, then retrying after screen lock; a fresh authorized unlock credential may need separate investigation if EscrowFailure persists.
