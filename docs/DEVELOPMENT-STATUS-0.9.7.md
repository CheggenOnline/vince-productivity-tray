# Development status - 0.9.7 candidate

Date: 2026-10-22

## Upgrade behavior

- Existing 0.9.5/0.9.6 program files can be upgraded in place; uninstalling first is not required.
- The tray process must be exited. Setup refuses to terminate the current or legacy helper itself.
- Program-directory replacement uses the existing transaction journal, validation, and rollback path.
- `%LOCALAPPDATA%\VinceProductivityTray\settings.json`, notes, recordings, time logs, legacy backups, and other user data remain outside the replaced program directory.

## VinceUtility structure

- The current-user business OneDrive resolver remains fail-closed on missing or ambiguous Vince AS roots.
- The chosen `VinceUtility` directory now receives three idempotent subdirectories: `Quick Notes`, `Meeting Recordings`, and `Time Capture`.
- Setup does not rename or delete folders made by 0.9.5. Read-only inspection found the three earlier Norwegian-named folders present and empty; they remain untouched.
- A synthetic test creates the structure twice and confirms that a pre-existing marker remains byte-identical.
- No storage setting or existing file is migrated automatically.

## First-install defaults

- Only the absence of `%LOCALAPPDATA%\VinceProductivityTray\settings.json` enables first-install initialization. Existing settings win even when their configured directories are missing.
- Setup resolves or explicitly asks for the current user's Vince AS business OneDrive root, creates the three VinceUtility directories idempotently, and writes all three paths to new settings.
- Setup explicitly warns that notes, time logs, and meeting audio stored under this root may synchronize to OneDrive and be subject to company access and retention policies.
- Automatic recording, integrated time capture, and Start with Windows remain off.

## Verification boundary

- 0.9.7 uses the full 0.9.6 implementation. Four focused tests cover fresh settings, custom-setting preservation, pre-existing empty directories, and missing/ambiguous OneDrive discovery.
- Neither installer nor normal tray runtime is executed. Audio, recording, live global hotkeys, clipboard, autostart, real time logs, and legacy AHK remain untouched.
- x64 is cross-built/PE-verified only on this ARM64 host. Installers remain unsigned.
