# Locked-state Analytics research (2026-10-07)

The requested scope is an iPhone **after first unlock, then screen lock**. Before-first-unlock after reboot is excluded.

The [Mac investigation](https://github.com/MochiLog/MochiLog-Mac/blob/main/docs/LOCKED_ANALYTICS_RESEARCH.md) tested a paired iPhone 17 on iOS 27.2. Native RSD exposed Analytics names and the size of a 21,327,604-byte file, but read-only FILE_OPEN returned AFC PERM_DENIED (10). Passing the saved RemotePairing unlock credential to RSDCheckin returned EscrowFailure. Classic paired Wi-Fi service connections also failed. Apple's CoreDevice file-copy route failed on remote openat with EPERM despite reporting Readable metadata.

These were Mac-hosted live probes, **not Windows live verification**. Windows uses a userspace RSD tunnel rather than the macOS native tunnel, so it needs its own locked-state verification if an accepted escrow route is found. The current product requirement to unlock during collection remains unchanged.

An additional [independent C/Python client](https://github.com/MochiLog/MochiLog-Mac/tree/main/scripts/research) implements native tunnel assertions, HTTP/2/RemoteXPC, plist check-in, paired TLS and read-only AFC without importing or executing pymobiledevice3. On the AFU-locked iPhone it again reached file metadata, then received PERM_DENIED at FILE_OPEN; its existing RemotePairing credential check-in returned EscrowFailure. Thus the tested denial is not specific to the upstream library's file-read implementation. The native assertion helper is macOS-only and has not been ported to Windows. The alternate osanalytics.logTransfer service was inventoried but no readable Analytics route was established. Nine offline protocol/output tests pass; there is no collector replacement or release from this experiment.

Further Mac-only probes found a longer-lock native tunnel rejection (RemotePairingError 1016), and classic diagnostics/file_relay StartService rejection with PasswordProtected. The OS's already-present RemotePairing host key differs from the saved tool key; difference alone is not proof of an invalid key. A user-assisted unlock/relock restored the comparison. Plain check-in read the complete 21,327,604-byte / 36,136-line file unlocked, but FILE_OPEN returned PERM_DENIED locked. OS-key check-in produced RSDCheckin/StartService responses, then the channel closed before a complete AFC reply in both states. Therefore OS-key check-in is not a working acquisition route. Backup and Mirroring were excluded. Thirteen offline tests pass; no Windows transport, shipped collector, OS trust, keys or protection settings were changed.

## Candidate: a locked live-battery snapshot, separate from Analytics

Mac's independent client succeeded on a freshly AFU-locked iPhone with **plain RSD check-in**, using `com.apple.mobile.diagnostics_relay.shim.remote` and length-prefixed plist requests:

```json
{"Request":"GasGauge"}
```

```json
{"Request":"IORegistry","EntryClass":"IOPMPowerSource"}
```

GasGauge returned numeric CycleCount / FullChargeCapacity fields. IORegistry returned numeric AppleRawMaxCapacity, CurrentCapacity, CycleCount, DesignCapacity, FullChargeCapacity, MaxCapacity and NominalChargeCapacity fields. The research output retained only whitelisted field names, not raw values or battery serials. Independent lock-state checks before and after these queries reported AFU-locked. This is a current snapshot, not a daily file, Watch log or historical record; units/accuracy and sustained long-lock availability remain unverified.

For exact reproducible commands, flow, evidence and constraints, see the [Mac snapshot recipe](https://github.com/MochiLog/MochiLog-Mac/blob/main/docs/LOCKED_BATTERY_SNAPSHOT_RECIPE.md). Windows would need to use its own existing userspace RSD transport to reach the same service, then verify these read-only requests on a real locked device. The Mac-only C assertion helper is not portable to Windows. Do not change Windows product promises or silently substitute snapshots for daily Analytics.

Shared requirements for any follow-up implementation:

- Verify the file body, not just a successful listing, size, process exit code or empty destination file.
- Preserve pending files after permission/incomplete-read failures. Do not permanently classify them as unrelated battery data.
- Classic escrow and RemotePairing unlock credentials are different. Use only the existing authorized credential for the correct transport, never store it in app-pairing JSON or diagnostic logs, and never silently re-pair users.
- `include_escrow_bag=True` is an upstream option, not evidence that Analytics acquisition works. Require a complete nonempty file while the phone stays locked, then Windows and Developer Mode-off verification before changing guides or promises.
- A successful Tailscale app transfer does not prove that new system Analytics collection works over Tailscale.

No runtime changes or release were made for this investigation. Follow-up work must explain the OS-key channel closure and separately validate snapshot units, accuracy, longer lock intervals, Developer Mode-off operation and Windows applicability.
