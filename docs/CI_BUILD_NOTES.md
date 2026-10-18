# Compiled collector CI

## Investigation on 2026-10-17

The October 15 successful Windows run restored a 77,038,506-byte compiler cache created on October 10, yet rebuilt all 2,629 C files: zero clcache hits. C compilation lasted from 11:20:16 to 12:24:48 UTC. Its post-job action explicitly skipped saving because the cache key was an exact match. An Actions cache cannot be updated in place, so repeatedly restoring that key also repeatedly discarded newly built compiler outputs.

The [October 17 run](https://github.com/MochiLog/MochiLog-Windows/actions/runs/38991531838) completed successfully. C compilation lasted from 01:47:00 to 03:21:10 UTC, about 94 minutes, with zero hits and 2,628 misses. It saved a new cache because this source change produced a new input key. There is no evidence that this run hung. The former single build step covers compilation, .NET publication and installer compression; its step name alone cannot identify which substage is active. The old output also omitted individual C compiler commands. A long silent interval was not enough to distinguish a stalled process from compilation.

## Changes

- Each successful run now saves the refreshed compiler cache under a unique run/attempt key. Restore prefixes prefer matching build inputs and then the existing platform/Python family. The compiler script itself participates in the input hash.
- Nuitka now prints C backend commands with `--show-scons`. Compiler choice, optimization settings, native compilation, standalone verification and runtime behavior are unchanged.
- Installer artifacts are retained for seven days. Compilation reports retain their existing fourteen-day lifetime. GitHub Releases remain the distribution archive.

These changes fix the observed cache update problem and expose compiler progress; they do not prove a particular speedup. Check the next run's clcache hit/miss summary before attributing another long build to a compiler hang. A cold build still compiles the full maintained device library.

Reference: [GitHub cache matching and immutable entries](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching).

## Cold builds and formatted-source release, October 19

The 0.1.33 installer incorporates the formatted source from commit a12ff7b. It uses the
same resource-selection script as the Mac companion: count actual CPUs, reserve 2 GiB,
budget 1.5 GiB per compiler, and cap concurrency at eight. Standard 4-CPU/16-GiB Windows
runners select four jobs instead of the former fixed two. Unknown hardware keeps one
job, and runners below 6 GiB keep reduced-memory mode. Local defaults remain one job
and reduced-memory mode; no additional application runtime dependency is introduced.

The workflow's `cold_compile` input skips Actions compiler-cache restore/save and tells
Nuitka to disable all its caches. The initial 0.1.33 verification run uses this option to
measure the first build rather than a warm-cache build. `compiler-resources.json` and
`collector-build-metrics.json` accompany successful compilation reports. All maintained
device-library modules, optimization settings, offline checks and installer verification
remain enabled. Compare measured times only after the new run completes successfully.

The 0.1.33 cold workflow subsequently passed in 1h04m25s, compared with the previous
1h55m42s workflow. The exact installer was verified and installed on the physical PC,
then published as an alpha. See the [measurements, limits and distribution record](BETA_0.1.33_DISTRIBUTION.md).
