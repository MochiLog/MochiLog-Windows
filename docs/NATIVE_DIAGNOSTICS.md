# Native Windows diagnostics

Windows 0.1.28 produces its diagnostic report in C#. It does not start PowerShell to read caches or aggregate logs.

## Mechanism

- A background task produces NativeDiagnostics/current.json and one latest snapshot per local calendar day, under the existing app data directory. Updates happen once per minute. Resource measurements also enter the ordinary activity log every five minutes.
- Schema 1 explicitly describes the app version, timestamp, own process memory and GC counters, status, collection results, per-device LAN hints and verified candidates, and the latest activity messages. It does not serialize application state or arbitrary runtime objects.
- Each snapshot is limited to 32 devices, 50 messages and 256 KiB. Text fields, hint lists and candidate lists are bounded before JSON serialization. Invalid unbounded DTOs are rejected before allocating the JSON result.
- Files are replaced atomically. Daily snapshots follow the same retention setting as activity logs, initially 30 days. The delete-logs action clears the saved snapshots too; later activity creates new reports.
- The support export includes a fresh native snapshot, produced off the UI thread. Existing daily PC/mobile log exports continue to work.
- Pairing secrets, OS UDIDs and live battery values are excluded. Device physical IDs, names and network addresses remain private diagnostics and must not be posted in public issues without redaction.

For automated investigation, retrieve the existing NativeDiagnostics/current.json through SFTP/SCP and parse it locally. Do not use Get-Content plus a second PowerShell JSON conversion. The app creates the report, so inspection does not need to start a Windows reader process.

## Helper lifetime

GuardedProcess owns redirected stdout/stderr and the process lifetime together. Existing collector deadlines apply to both execution and inherited output pipes. Caller cancellation is preserved. Output is limited to 8 Mi characters on stdout and 1 Mi on stderr; USB detection uses smaller 64 Ki limits and a 30-second deadline.

On Windows, an anonymous Job Object owns the helper and descendants, terminates them when the owner closes, and caps committed memory at 1 GiB per process / 2 GiB per job. Failure to establish supervision aborts the helper. USB repair has a 90-second deadline after the elevation prompt. This is separate from the intentionally persistent MochiLog desktop app.

Reference: [Microsoft Job Object limits](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_extended_limit_information).

## Incident on 2026-10-14

An investigation command launched on October 13 at 22:08 JST read small endpoint caches and tailed log files, then called ConvertTo-Json -Depth 10. The command was not part of the shipped MochiLog application. It remained alive after the remote reader stopped waiting.

At inspection its private committed memory reached 122,527,506,432 bytes (about 114.1 GiB), while the app used approximately 232 MiB. The command and process metadata were saved privately, and only that runaway process was stopped. Physical free memory recovered to about 35 GiB. Actual input was reported to be around 550 KiB.

Windows PowerShell 5.1 retains provider properties on Get-Content strings. A deep JSON conversion can recursively expand these properties instead of only the text. [Microsoft documents that extended String and DateTime properties stopped being serialized in PowerShell 7.2](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.utility/convertto-json). No attempt was made to rerun the runaway full command.

The native report and repository instructions replace this ad hoc collection path.

## Validation on 2026-10-14

The Windows tests generated a 26,760-byte report from 596,990 bytes of synthetic events, retained the latest 50 events, capped 1,000 devices at 32 and rejected unbounded DTOs before serialization. After warmup, 1,000 exports changed managed retained memory by -408 bytes. Atomic current/daily snapshots, retention and deletion passed.

The actual Windows machine passed the complete protocol suite and Release publish. Helper tests verified execution deadline, caller cancellation, output overflow and Job Object cleanup of a child after its parent exited. Encrypted transfer, duplicate preflight, daily host/Watch routing and archive exchange checks passed too. The elevated USB driver-repair action was not invoked during this incident; no driver restart was needed for the diagnostic fix.
