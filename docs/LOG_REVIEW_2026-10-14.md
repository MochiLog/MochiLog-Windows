# Daily collection review — 2026-10-14

## Evidence

Reviewed locally copied native PC snapshots and daily activity logs. Raw logs, addresses and pairing identities remain in ignored Build directories. The Mac log contained 422 iPad AFC FILE_OPEN status-10 failures between 09:00 and 19:01 JST. Windows repeatedly tried a stale socket-peer address while its most recent sealed LAN hint had aged out of the one-hour preferred list.

At 19:06–19:07 JST the Mac saved current iPhone, Watch and iPad reports. At 19:11 JST it stopped automatic collection for both hosts until the next log day. Daily-completion logic was working and is unchanged. Resource snapshots showed stable retained app memory during this sample, rather than another runaway diagnostic reader. Mobile logs received by PCs did not contain a current-day archive at review time; this review does not claim full on-device coverage.

## Changes

- Coordinate file reads in Swift/C#, leaving the maintained Python transport unchanged. Direct collection probes one file per host/accessory before its remaining batch; legacy collection stops that source for the poll after an explicit AFC permission denial.
- Preserve failed and held files as pending. Only the actually failed file gets the existing persisted retry time. Prefer untried files within the same day on a subsequent poll; more recent days still come first.
- Keep Watch sources independent. A permission error is not treated as proof of device lock or a reason to discard a file.
- Allow sealed mobile LAN hints aged 1–24 hours as unverified fallbacks, after a recent OS-authenticated endpoint and before the socket peer. Reject stale hints beyond 24 hours. IP addresses never replace OS pairing identity verification.
- Trace requested files before helper execution, source gates and held counts. A held acquisition remains incomplete.

## Verification

Native regression fixtures cover host denial with a successful Watch read, retry rotation, pending recovery, split batch index mapping, precise error matching, route ordering/expiry and existing manual/address isolation. Full protocol suites also cover encryption, replay rejection, duplicate preflight, ACK, retention and daily host/Watch stopping. Release build and physical-PC installation results are recorded below after validation.

Windows validation: the complete Release protocol suite ran successfully on the physical Windows 11 PC, including direct/legacy source gates, Watch isolation, split batch mapping and pending recovery. Release 0.1.30 installed and launched; its fresh native snapshot retained the same paired iPad identity and now includes the recent authenticated hint before the older socket peer. The installed compiled collector passed eight offline entry/help commands with Python removed from PATH and nonexistent PYTHONHOME/PYTHONPATH; build metadata confirms Nuitka, all bridge modules compiled, and pymobiledevice3 11.19.1. No user Python setup is required. Reused distributions were copied as folder contents to avoid accidental nested collector bundles.

The installed native snapshot initially measured 191,016,960 private bytes and 10,047,536 retained managed bytes. These are bounded observations, not an indefinite-runtime guarantee. New live acquisition on Windows has not yet been verified after installation: PC discovery still reports the physical endpoint unavailable. Successful candidate ordering in fixtures and the installed snapshot is not proof of physical reachability.

Installer SHA-256: 484b05f28763e4d9a2af3d5381fe69a5395afc32b6588d5ec58b5cbf8c6734d0 (151,211,615 bytes).
