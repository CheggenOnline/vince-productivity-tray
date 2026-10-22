# Vince Productivity Tray 0.9.4 - short guide

This is an unsigned local candidate. Use `win-arm64` on Windows on ARM and `win-x64` on ordinary Intel/AMD 64-bit Windows. Do not bypass SmartScreen, Defender, or organizational controls.

Setup keeps existing settings and user data. It does not enable Start with Windows, stop a legacy helper, or change legacy startup entries. For the current user it creates `VinceUtility` inside a uniquely identified business `OneDrive - Vince AS`. If more than one matching location exists, setup asks rather than guessing. This folder creation does not move notes, recordings, or time logs into OneDrive.

## Tray and recording

- `Ctrl+Alt+R` starts a manual local recording. Stop it using the recorder window or **Stop recording** in the tray.
- **Autorecord meetings** is off by default. When enabled, it reacts to the modern Teams microphone-use transition, not merely an open Teams process. The bundled auto recorder stops after Teams releases the microphone for five seconds. A manual stop does not restart in the same call.
- Recording stores a local 16 kHz mono WAV mixed from system loopback and the default communications microphone. Transcription remains off.

## Notes and spellcheck

- `Ctrl+Alt+N` opens Quick note. Enter saves; Shift+Enter adds a line; Escape cancels. The window closes only after a successful UTF-8 append.
- Select editable text and press `Ctrl+Shift+X` for local Windows-dictionary spellcheck. It supports configured English and Norwegian dictionaries but does not understand sentence meaning like an AI model.
- The 0.9.4 rules reject case-only suggestions such as lowercase `na` becoming `Na`, and allow a unique diacritic correction such as `na` to `nå`.

## Time capture

Integrated time capture is off by default so it cannot duplicate a still-running legacy `TimeCapture.ahk`. Enable it only after deciding how the legacy script should be handled.

- Default hotkey: `Ctrl+Shift+A`.
- Default local folder: `C:\TimeLog`.
- Entries use compatible ISO-week files: `log_YYYY-Www.txt`.
- Format: `yyyy-MM-dd HH:mm - text | dur=Xh | cust="name"`.
- Multiline input is deliberately normalized to one line in the weekly log.
- Customers remain in UTF-8 `customers.txt`; customer quotes and backslashes are escaped compatibly.
- Hourly popups default to 08:00 through 16:00. Snooze, workday start, daily target, hotkey, folder, and popup hours are configurable. Hiding or snoozing retains the unsent draft; duplicate timer popups are suppressed.

## Hotkeys and settings

Each shortcut has **Check hotkey**:

- **Available**: a temporary native `RegisterHotKey` registration succeeded and was cleaned up.
- **Occupied**: Windows or another program already owns it.
- **Already used by this app**: it is one of the tray app's active registrations.
- **Invalid**: the syntax is unsupported.

The check cannot detect every low-level keyboard hook or shortcut that only works inside another application. Saving changed shortcuts requires restarting the tray app. Start with Windows is a separate manual opt-in and asks for confirmation.

## Updates and uninstall

Update checking remains disabled in this candidate. The menu item displays that GitHub repository visibility/name must first be confirmed; it does not download or install anything.

Run `Uninstall.exe --uninstall` from the installed program folder after exiting the tray app. Settings, notes, recordings, time logs, and migration backups are preserved.
