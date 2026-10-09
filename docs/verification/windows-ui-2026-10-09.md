# Windows UI verification — 2026-10-09

Native Release build 0.1.19 on Windows 11, at 150% display scaling. UI Automation measured heading coordinates relative to the app window. Measurements are in [the JSON report](windows-ui-2026-10-09.json).

- At physical widths 1920, 1440 and 1000 px, all six pages were checked: Home, Live Battery, Battery Logs, About, Settings and Licenses.
- At 760 px, the four main pages were checked.
- All measured pages at a given width have identical heading origins. The wide pane is expanded; smaller widths retain compact navigation icons.
- Settings was tested with the actual daily diagnostic file exceeding 4 MB. The original single TextBox stalled the automation; the row-virtualized view allowed navigation and geometry checks to finish. Copy and support export continue to use the original complete text.

The check ran against the existing profile and normal runtime. Temporary test tasks were removed and the installed companion was restarted afterwards. The test did not change pairing or retention preferences.

Installer and protocol verification: [GitHub Actions](https://github.com/MochiLog/MochiLog-Windows/actions/runs/37941592240).
