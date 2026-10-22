# Development status - 0.9.4 candidate

Date: 2026-10-22

## Implemented

- Supplied SVG materialized from Library and converted deterministically into an eight-frame transparent ICO; original SVG is retained in source.
- Settings layout rebuilt for DPI scaling and all new options; hotkey availability checks use temporary native registrations and never unregister another program's binding.
- Teams autorecord is opt-in and driven by a tested rising-edge state machine. Teams merely being open is insufficient: the existing modern Teams microphone-use registry signal must enter its active state.
- TimeCapture AHK behavior integrated into the .NET app with compatible UTF-8 customer and ISO-week log files. Integrated activation defaults off, preserving the old AHK process/startup state.
- Installer organization-folder resolution considers business accounts only, requires a unique Vince AS match, persists the selected per-user target, and creates only `VinceUtility` without moving user data.
- Spell correction casing and short diacritic handling tightened with a regression for `na` to `nå`.

## Safe verification

- Release solution compilation: 0 warnings, 0 errors.
- ARM64 core suite: 31 passed, 0 failed. It covers spell correction, notes, settings, hotkey timing, time-entry format/escaping/progress, duplicate popup suppression, meeting rising-edge behavior, DPI control sizing, OneDrive ambiguity, recorder settings, updater boundaries, and installer transactions.
- Tests use temporary synthetic folders only. Native dictionary, foreground selection, clipboard, live hotkeys, audio, recorder, autostart, installer, and normal tray runtime remain untested for this version.

## Packaging boundary

- ARM64 is the host architecture. x64 is cross-built and PE-verified only; do not describe x64 as runtime-tested.
- Both installers are unsigned and require explicit approval before installation or normal execution.
