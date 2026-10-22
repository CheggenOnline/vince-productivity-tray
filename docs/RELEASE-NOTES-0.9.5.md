# Vince Productivity Tray 0.9.5 local candidate

0.9.5 contains the complete 0.9.4 feature set and one installer-structure addition:

- Setup idempotently creates `Hurtignotater`, `Møteopptak`, and `Timeregistreringer` beneath the selected current-user `OneDrive - Vince AS\VinceUtility` directory.
- Existing files and directories are preserved.
- Setup does not migrate old data or rewrite existing notes, recording, or time-log settings.
- Fresh installs also retain explicit/local storage choices. In particular, recordings are not redirected to OneDrive without the user deliberately selecting `VinceUtility\Møteopptak` in Settings after considering organizational sync/retention requirements.

No update source was activated and no installer/runtime execution was performed during development.
