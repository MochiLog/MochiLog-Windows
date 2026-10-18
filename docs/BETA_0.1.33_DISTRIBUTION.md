# Windows 0.1.33 Alpha verification — 2026-10-19

Published release: [MochiLog Windows 0.1.33 Alpha](https://github.com/MochiLog/MochiLog-Windows/releases/tag/v0.1.33).

## Source and cold build

- Source: `771f44172e8b59b8d2476b9580eaca0cfb9c46cf`, incorporating the repository-wide
  source formatting from `a12ff7b`.
- Successful [installer workflow 39145623103](https://github.com/MochiLog/MochiLog-Windows/actions/runs/39145623103).
- The runner reported four CPUs and 16,378 MiB of physical memory. Compilation used
  four jobs and normal memory mode. Local defaults remain one job and reduced-memory
  mode.
- Actions compiler-cache restore/save was skipped and Nuitka received
  `--disable-cache=all`. The result does not rely on a previously compiled collector.

| Measurement | Previous run | New cold run |
| --- | ---: | ---: |
| Workflow creation to completion | 1h 55m 42s | 1h 04m 25s |

The observed reduction is 51m17s, about 44%. The previous reference is
[run 38991531838](https://github.com/MochiLog/MochiLog-Windows/actions/runs/38991531838).
These are separate hosted VMs, and the new run disables all Nuitka caches while the old
run restored a compiler cache with no C compiler hits. They are practical run comparisons,
not a controlled same-machine benchmark or a guaranteed future build time.

The compiler subprocess timer, including its startup/report work, measured 3,282.150
seconds. Staging and standalone verification took another 25.918 seconds.

## Verification and physical update

- All 15 Python tests and the full native encrypted-protocol regression suite passed.
- The self-contained Release installer passed fresh install, registered-uninstaller,
  uninstall and reinstallation checks on the disposable CI runner.
- The installed collector passed standalone checks with user Python removed from its
  environment: all seven required modules compiled, typed plist, crypto, TLS roots,
  dynamic schema and platform-provider imports.
- The exact CI installer was SHA-256 verified on the physical Windows PC before an
  in-place update. Its encrypted pairing state was unchanged across installation,
  and the same paired-device identities remained in the native diagnostic snapshot.
- The updated application reported `0.1.33.0`, with product version
  `0.1.33+771f44172e8b59b8d2476b9580eaca0cfb9c46cf`. It was running and responding in
  interactive session 1, with its connection listener present.
- Physical verification was an in-place update and startup check. Uninstall tests ran
  only on the disposable CI machine. This maintenance verification does not claim a
  new physical battery-log transfer.

The public installer is the same file installed on the physical PC. Its GitHub asset
digest was independently checked against the local artifact:

- File: `MochiLog-Windows-Alpha-Setup.exe`
- Size: `151491631` bytes
- SHA-256: `78605aa6f9228a04bc721a44145eff8e97d69d2b7620b572ceec601eddf775be`

The release includes Japanese/English notes and a checksum file. Windows 11 remains
required; installer code signing is still not configured. Detailed build logs and private
physical snapshots remain under the ignored local `Build/beta-release-0.1.33/` directory.
No raw device diagnostics, pairing data or keys are included in this document.
