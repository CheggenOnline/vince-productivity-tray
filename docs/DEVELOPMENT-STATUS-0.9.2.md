# Development status — 0.9.2 candidate

Date: 2026-10-21

## Implemented

- Settings now reject empty folders, a shared notes/recordings folder, invalid hotkeys, and equivalent duplicate hotkeys.
- Folder paths and surrounding quotes/whitespace are normalized before use.
- Invalid settings keep the dialog open so the entered values can be corrected instead of being discarded.
- Settings are written through a uniquely named temporary file, flushed to disk, and moved into place; temporary files are cleaned up on failure.
- Declining the separate Start with Windows confirmation is now reflected in the saved settings file.
- The recorder resolves and validates one output-folder value before preparing or starting its PowerShell process.
- A quick-note draft is no longer hidden merely because focus changes. It closes only after a successful save or explicit Escape.
- Release outputs are version-scoped. The build script verifies ARM64/x64 PE machine values, required recorder assets, exclusion of PDB/Whisper/model files, SHA-256, signature status, and a machine-readable manifest.

## Tests completed

- Release configuration compiled with 0 warnings and 0 errors.
- ARM64 safe core suite: 22 passed, 0 failed.
- New tests cover folder/hotkey normalization, unsafe collisions, atomic UTF-8 settings storage, and temporary-file cleanup.
- Native dictionary integration, foreground selection, clipboard roundtrip, live global hotkeys, normal tray startup, installation, and recording were intentionally not executed.

## NuGet diagnosis and packaging result

The first sandboxed ARM64 restore failed with `NU1900` because NuGet could not refresh vulnerability data from `https://api.nuget.org/v3/index.json`; warnings-as-errors correctly stopped the job. Required official 9.0.8 runtime packs and an older official vulnerability cache were already present locally, but the failed restore had persisted the audit error in `project.assets.json`. Reusing that state with `--no-restore` was therefore not a valid clean build. Audit was not disabled, failed sources were not ignored, and no alternate source was introduced.

A narrowly approved HEAD request to the official endpoint returned HTTP 200. This established that the earlier failure was the execution sandbox's network restriction, not a verified IT denial, a vulnerable-package finding, or a permanent NuGet outage. The unchanged build script was then run through the approved network path with audit active and only `NuGet.Online.Config`; all four restores completed normally.

- ARM64 installer: 150,005,438 bytes; SHA-256 `5643D2D00E148C27B1A9ADCFEB498F1C9EF50025D05FC5D469D60424B6F2D399`; PE `0xAA64`; unsigned.
- x64 installer: 146,233,010 bytes; SHA-256 `F48B5F1B5ABAE4D7583A81CD78AD37A0B536E58C48AF59BA58D0B5D7626238DE`; PE `0x8664`; unsigned.
- Both exact files remained present with unchanged hashes after Microsoft Defender custom scans. No new Defender detection/remediation event 1116/1117 appeared.

Neither installer nor either normal application was executed. No self-check, normal tray launch, installation, recording, global hotkey, clipboard integration, autostart change, upload, or publication was performed. A clean file scan does not establish that SmartScreen, runtime behavior monitoring, or a corporate policy will permit an unsigned application.

There is no verified IT policy or approval in this work record. Normal installation/execution and colleague distribution still require an explicit user decision plus any signing, allowlisting, or deployment approval their organization actually requires. The earlier quarantined files must not be restored or excluded from security scanning.
