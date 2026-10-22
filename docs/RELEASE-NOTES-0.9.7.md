# Vince Productivity Tray 0.9.7 local candidate

0.9.7 contains the complete 0.9.6 feature set and adds explicit first-install storage defaults:

- On a first install with no existing app settings, setup resolves or asks for the current user's `OneDrive - Vince AS\VinceUtility`, idempotently creates `Quick Notes`, `Meeting Recordings`, and `Time Capture`, and configures all three as the app's storage paths.
- Setup warns that data in these directories, including meeting audio, may synchronize to OneDrive and be governed by company access and retention policies.
- Existing files and directories are preserved.
- Upgrades never rewrite existing notes, recording, or time-log settings, including custom paths whose directories are currently missing.
- Folders created by 0.9.5 are not renamed or deleted. Read-only inspection found those earlier folders empty on the development PC.
- Automatic recording, integrated time capture, and Start with Windows remain disabled by default. No existing data is migrated.

No update source was activated and no installer/runtime execution was performed during development.
