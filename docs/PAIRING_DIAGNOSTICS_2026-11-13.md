# Pairing diagnostics and support exports (2026-11-13)

The mobile app could display a QR pairing error without saving an event. The PC invitation and handshake audit also lacked sufficient detail. Meanwhile, log storage used physical feature files plus a complete daily stream, but support exports only copied the daily stream. A small attachment count was not by itself proof of lost categories; missing QR events were a separate defect.

## Changes

- `TransferServer.Audit` sends bounded per-invitation events into the runtime's persistent diagnostic archive. It is separate from user-visible status text. Invitation creation and known-session errors/results are recorded, including protocol/identity/key problems, expiry, rejected confirmation, failed credential storage and completion. Session IDs correlate PC and phone events. QR payloads, PINs, keys and proofs are excluded.
- `SupportLogExport` snapshots locally stored feature files and daily compatibility streams for the selected incident day and the preceding two days, with diagnostic JSON. Work runs off the UI thread.
- A schema-1 manifest lists included, missing, empty, unreadable and budget-omitted files and unavailable remote diagnostics. Empty placeholder files are not attached. Remote peers still exchange the full combined stream, so the original physical feature files may exist only on their source device; the manifest explains this.
- Snapshot payloads are limited to 12 MiB / 96 files, plus a small manifest. Copies use 64 KiB buffers and the file's initial size, without decoding all logs into strings. Reparse-point files/day directories are skipped. Export directories are unique and diagnostic JSON remains bounded by the existing report generator.
- Support remains usable without a selected phone. Before app pairing succeeds, a PC cannot export phone reports it never received. Export each side's own diagnostics separately in that case.

No wire-format, saved-pairing or authentication change is introduced. Existing encrypted transfer and append-only daily archive offsets remain compatible. The mobile receiver separately fixes a reproduced newline/EOF wait defect; the original reporter's timeout is not conclusively attributed to it.

## Verification

On the physical Windows host, using an isolated source snapshot and temporary synthetic state:

- `dotnet run --project tests/SupportExportTests/SupportExportTests.csproj -c Release`: feature and legacy daily files, missing sources, support before pairing and export limits.
- `dotnet run --project tests/ProtocolTests/ProtocolTests.csproj -c Release`: handshake error/result audit and secret exclusion, plus the existing encrypted transfer, replay, archive exchange, cloud-sharing and helper-lifetime regression suite.
- `dotnet build src/MochiLog.Windows/MochiLog.Windows.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64`: full native WinUI Release build.

CI runs the export test alongside the protocol suite. Temporary verification processes have explicit deadlines, file-output limits, resource checks and exit cleanup. No production pairing, USB driver, installer or running app is replaced by these tests. They do not send email or establish actual provider delivery behavior.
