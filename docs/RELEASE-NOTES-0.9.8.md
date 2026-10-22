# Vince Productivity Tray 0.9.8 local candidate

0.9.8 contains the complete 0.9.7 feature set and fixes settings-time global-hotkey rebinding:

- Changed shortcut combinations become active immediately after a successful Save; a tray restart is no longer required.
- Rebinding supports moving a combination from one app action to another in the same save operation.
- If Windows or another program owns a requested combination, Save reports the conflict and restores the complete previous registration set.
- Temporary **Check hotkey** registrations remain isolated and are always cleaned up.

The 0.9.7 first-install storage behavior remains unchanged:

- On a first install with no existing app settings, setup resolves or asks for the current user's `OneDrive - Vince AS\VinceUtility`, idempotently creates `Quick Notes`, `Meeting Recordings`, and `Time Capture`, and configures all three as the app's storage paths.
- Setup warns that data in these directories, including meeting audio, may synchronize to OneDrive and be governed by company access and retention policies.
- Existing files and directories are preserved.
- Upgrades never rewrite existing notes, recording, or time-log settings, including custom paths whose directories are currently missing.
- Folders created by 0.9.5 are not renamed or deleted. Read-only inspection found those earlier folders empty on the development PC.
- Automatic recording, integrated time capture, and Start with Windows remain disabled by default. No existing data is migrated.

No update source was activated and no installer/runtime execution was performed during development.
