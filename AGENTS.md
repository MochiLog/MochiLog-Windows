# Runtime diagnostics

Use the app's native diagnostic snapshots or the existing support export when investigating Windows runtime behavior. Read [docs/NATIVE_DIAGNOSTICS.md](docs/NATIVE_DIAGNOSTICS.md) first. Fetch the generated JSON file directly over SFTP/SCP; do not recreate diagnostic reports in a remote shell.

Do not pipe Get-Content output into ConvertTo-Json or place those values in a deeply serialized PowerShell object. Windows PowerShell 5.1 adds provider metadata to strings, and this caused an investigation process to grow beyond 100 GiB.

Do not leave temporary remote processes running after a canceled or timed-out investigation. Long-lived app helpers use GuardedProcess with a deadline, output bounds and Windows Job Object ownership. If an investigation needs a new executable, it must have the same bounded lifetime and resource supervision.

Never commit raw diagnostic snapshots, logs, pairing data or private keys. Developer notes belong in docs/; keep README focused on user setup.
