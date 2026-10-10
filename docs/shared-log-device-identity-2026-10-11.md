# Shared source identity validation (2026-10-11)

The mobile October 10 defect used the receiver model for an Analytics log with no hardwareModel. Origin UUIDs were already correct. TransferServer now includes sourceModel inside authenticated v3 encrypted preflight controls, derived from the origin paired PhoneState. Foreign sharing and subsequent file-decision resolution require logSourceIdentityVersion=1; older recipients continue receiving their own files. No pairing or encryption keys change.

The protocol suite passed on Windows 11 with real DPAPI StateStore persistence, including four-peer sharing, authenticated source model, old client capability gating, scope revocation, encrypted transfer/ACK, replay rejection, idle timeout and shutdown cancellation. The macOS-hosted net10.0-windows test cannot exercise DPAPI and was not used as a Windows pass.

Only native C# transfer metadata changed. The unchanged collector and dependency license bundle are reused from the verified 0.1.22 Release installation for the incremental local installer. The standard local/CI build script still compiles the maintained Python library with Nuitka and includes its runtime.
