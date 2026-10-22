# Vince Productivity Tray 0.9.6 local candidate

0.9.6 contains the complete 0.9.5 feature set and corrects the installer-created directory names:

- Setup idempotently creates `Quick Notes`, `Meeting Recordings`, and `Time Capture` beneath the selected current-user `OneDrive - Vince AS\VinceUtility` directory.
- Existing files and directories are preserved.
- Setup does not migrate old data or rewrite existing notes, recording, or time-log settings.
- Folders created by 0.9.5 are not renamed or deleted. Read-only inspection found those earlier folders empty on the development PC.
- Fresh installs also retain explicit/local storage choices. In particular, recordings are not redirected to OneDrive without the user deliberately selecting `VinceUtility\Meeting Recordings` in Settings after considering organizational sync/retention requirements.

No update source was activated and no installer/runtime execution was performed during development.
