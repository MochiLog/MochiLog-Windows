# Windows security maintenance — 2026-11-14

Migrated .NET projects and CI to .NET 11, updated Windows App SDK and compatible NuGet packages, and rebuilt the self-contained native app (11.0.0). Updated the official pymobiledevice3 dependency to 11.55.0 and Nuitka to 4.3.5 on Python 3.13.13. The standalone collector is compiled from those official versions; no dependency-source patches are applied.

Verification: native protocol checks passed; 15 Python tests passed; standalone collector smoke tests passed with Python and the source/build tree absent from its environment; the 1.0.1 installer compiled successfully. The standalone smoke check reads the expected dependency version from requirements-build.txt so it verifies the actual pin.

Before local installation, the previous installed application and private state were copied to a separate local backup. No raw logs, diagnostic snapshots, pairing records or keys are included in Git.
