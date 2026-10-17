# Daily log review — 2026-10-17

## Evidence

Reviewed existing native snapshots and daily logs for October 16 and 17. Raw archives, addresses, device identifiers and keys stay outside Git in ignored Build folders.

Mac 0.2.32 stored the October 17 iPhone report at 09:04:40 JST and its Watch report at 09:19:39; automatic collection stopped at 09:24:19. The 09:09 collection recorded inaccessible accessory directories and took 39 seconds. The code grouped current and archived Watch directory reads in one catch block, so an archive failure could prevent checking current reports. This is a reproduced code path; the logs do not prove a Watch report already existed during that earlier attempt.

Both native implementations could report acquisitionComplete=true after ignoring an inaccessible accessory directory. The direct-IP bridge also swallowed accessory-listing failures. A successful listing of another directory is not proof that all report sources were checked.

At the initial 10:23 sample, Mac resident memory was 66,289,664 bytes with a peak of 160,710,656. Windows private memory was 227,856,384 bytes and managed memory was 6,933,640. Windows cumulative allocations are not retained memory. These samples do not show another runaway diagnostic reader.

The iPad still exposed zero current-day candidates in successful listings. Mac later lost its OS diagnostic route while Windows continued reading through an explicitly identified USB route. Windows' discovery status came from an earlier search, not a fresh determination that the device was locked; its eight-language empty-search message now makes that scope explicit. A restore-mode service response was recorded once on October 16; the exact reason for that temporary device state is not established.

## Changes and checks

Current and archived directories are independent. Only advertised archive directories are requested after a successful current-directory read; if that read fails, the archive remains an independent fallback. Available host/Watch reports continue to be saved. Duplicate current/archive paths still prefer the archived copy.

The compiled transport returns bounded listingFailures metadata. Swift and C# preserve a separate listing-failure count in collection results, activity logs and native snapshots, display a translated partial-result explanation, and do not report complete acquisition while a listing remains unknown. The next ordinary collection retries it. No extra cooldown or retry-frequency reduction was added.

The new helper field is additive; native readers accept older helper output without that field. Existing pairing identities, request encryption, replay rejection, preflight deduplication, ACK, daily completion and mobile transfer formats are unchanged. Python remains only the maintained transport adapter; report selection and application policy remain native.

Regression checks reproduce an inaccessible archive with readable current Watch data, an unavailable current directory with an independent archived listing, omitted optional archive folders, a partial result with no pending downloads, and recovery on the next collection. The complete native Mac suite and complete Release suite on the physical Windows PC passed, along with all ten Python tests in each repository. Physical installed-build verification and release artifact results are recorded below after packaging.

## Packaging and physical verification

The [Windows installer run](https://github.com/MochiLog/MochiLog-Windows/actions/runs/38991531838) completed successfully for source commit aa8b8d85de32e1c15aa707419b22c3716d56f021. Python transport and native encrypted-protocol tests passed, followed by installer installation/uninstallation and standalone runtime verification without user Python. The same installer was hash-verified on the physical Windows PC before an in-place update. Uninstallation tests ran only on the disposable CI runner.

The installed app reported 0.1.32.0 in a fresh 12:33 JST native snapshot. Its confirmed pairing identity was preserved, and older collection summaries decoded with listingFailures=0. Private memory was 150,876,160 bytes and managed memory was 4,157,400 bytes at startup. The new helper completed USB and Bonjour discovery within their deadlines; the subsequent OS diagnostic connection was unavailable. The corrected last-search message appeared. Earlier successful iPad listings had zero current-day candidates; no successful acquisition of a new physical iPad report is claimed. At 12:34 the app remained running with 202,240,000 private bytes and 11,307,464 managed bytes.

Installer: 151655897 bytes; SHA-256 17ccb0e6f6c047b22b5bdfeb97a39caffc5acc963ff393ab768dd2d6a00df8f6.

The long build investigation and compiler-cache refresh fix are recorded in [CI build notes](CI_BUILD_NOTES.md). These are build-system changes; their effect on the next run's duration is not yet measured.
