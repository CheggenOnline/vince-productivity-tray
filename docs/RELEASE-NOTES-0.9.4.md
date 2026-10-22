# Vince Productivity Tray 0.9.4 local candidate

## Added

- Multi-resolution transparent Vince tray/application icon generated from the supplied SVG (16, 20, 24, 32, 40, 48, 64, and 256 px).
- DPI-aware, resizable settings window with taller rows, scrolling content, and fixed Save/Cancel area.
- Per-shortcut **Check hotkey** action: available, occupied, already used by this app, or invalid. It uses native `RegisterHotKey`; keyboard hooks and application-local shortcuts are outside that check.
- `Autorecord meetings`, default off. It reacts to the existing modern Teams microphone-use rising edge, preserves same-call no-restart after manual stop, retains the recorder's five-second end grace, and exposes status/Stop in the tray.
- Integrated local time capture, default off to avoid colliding with a still-running legacy `TimeCapture.ahk`: `Ctrl+Shift+A`, hourly popup, snooze, customer list, half-hour durations, weekly files, and relative progress. Folder, hotkey, popup, hours, snooze, workday start, and target are configurable.
- Setup creates `VinceUtility` under a uniquely identified current-user business `OneDrive - Vince AS`. It prompts when the organization root is missing or ambiguous and never changes notes/recording/time-log locations.

## Correctness

- Case-only Windows spell replacements are rejected and all-lowercase input stays lowercase.
- Diacritic-only short-word corrections are allowed when uniquely suggested; `hva med na da?` is covered as `hva med nå da?` without sentence-specific hardcoding.
- Existing spellchecking remains local dictionary/rule based and does not gain semantic sentence understanding or AI/API use.

## Boundaries

- No meeting/audio capture, live global hotkey, clipboard, autostart, legacy AHK, real `C:\TimeLog`, installer, or normal 0.9.4 tray runtime was exercised during development.
- The candidate remains unsigned. File scanning is not SmartScreen, behavior-monitoring, or organizational approval.
