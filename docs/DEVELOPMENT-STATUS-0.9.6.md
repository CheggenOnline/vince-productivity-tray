# Development status - 0.9.6 candidate

Date: 2026-10-22

## Upgrade behavior

- Existing 0.9.5 program files can be upgraded in place; uninstalling first is not required.
- The tray process must be exited. Setup refuses to terminate the current or legacy helper itself.
- Program-directory replacement uses the existing transaction journal, validation, and rollback path.
- `%LOCALAPPDATA%\VinceProductivityTray\settings.json`, notes, recordings, time logs, legacy backups, and other user data remain outside the replaced program directory.

## VinceUtility structure

- The current-user business OneDrive resolver remains fail-closed on missing or ambiguous Vince AS roots.
- The chosen `VinceUtility` directory now receives three idempotent subdirectories: `Quick Notes`, `Meeting Recordings`, and `Time Capture`.
- Setup does not rename or delete folders made by 0.9.5. Read-only inspection found the three earlier Norwegian-named folders present and empty; they remain untouched.
- A synthetic test creates the structure twice and confirms that a pre-existing marker remains byte-identical.
- No storage setting or existing file is migrated automatically.

## Verification boundary

- 0.9.6 uses the full 0.9.5 implementation and safe test suite with the directory-name contract updated to exact English names.
- Neither installer nor normal tray runtime is executed. Audio, recording, live global hotkeys, clipboard, autostart, real time logs, and legacy AHK remain untouched.
- x64 is cross-built/PE-verified only on this ARM64 host. Installers remain unsigned.
