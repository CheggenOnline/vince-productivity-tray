# Development status - 0.9.5 candidate

Date: 2026-10-22

## Upgrade behavior

- Existing 0.9.2/0.9.4 program files can be upgraded in place; uninstalling first is not required.
- The tray process must be exited. Setup refuses to terminate the current or legacy helper itself.
- Program-directory replacement uses the existing transaction journal, validation, and rollback path.
- `%LOCALAPPDATA%\VinceProductivityTray\settings.json`, notes, recordings, time logs, legacy backups, and other user data remain outside the replaced program directory.

## VinceUtility structure

- The current-user business OneDrive resolver remains fail-closed on missing or ambiguous Vince AS roots.
- The chosen `VinceUtility` directory now receives three idempotent subdirectories: `Hurtignotater`, `Møteopptak`, and `Timeregistreringer`.
- A synthetic test creates the structure twice and confirms that a pre-existing marker remains byte-identical.
- No storage setting or existing file is migrated automatically.

## Verification boundary

- 0.9.5 uses the full 0.9.4 implementation and safe test suite plus the new directory-structure test.
- Neither installer nor normal tray runtime is executed. Audio, recording, live global hotkeys, clipboard, autostart, real time logs, and legacy AHK remain untouched.
- x64 is cross-built/PE-verified only on this ARM64 host. Installers remain unsigned.
