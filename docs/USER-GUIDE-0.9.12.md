# Vince Productivity Tray 0.9.12 - short guide

This is an unsigned local candidate. Use `win-arm64` on Windows on ARM and `win-x64` on ordinary Intel/AMD 64-bit Windows. Do not bypass SmartScreen, Defender, or organizational controls.

You do not need to uninstall an older Vince Productivity Tray first. Exit its tray process before setup; the installer refuses to stop it for you. Setup replaces only the program directory through its transaction/rollback path and preserves existing settings and user data.

For a first install with no existing app settings, setup creates `VinceUtility` inside a uniquely identified business `OneDrive - Vince AS`, plus `Quick Notes`, `Meeting Recordings`, and `Time Capture`. These become the actual default storage paths. If the Vince AS root is missing or ambiguous, setup asks rather than guessing.

Data saved to these directories, including meeting audio, may synchronize to OneDrive and be subject to company retention and access policies. Setup displays this warning before a first install. Automatic recording remains off.

On an upgrade, setup does not move old data or change any configured storage path—even if that path currently does not exist. Folders created by 0.9.5 are left untouched.

## Tray and recording

- The tray menu contains **Start Recording** (or **Stop Recording** while active), **New quick note**, **New time entry**, **Settings**, and **Exit** as the final item. Update controls, startup, and diagnostics are under **General**. Each feature's **Open folder** button is on its own tab.
- Settings has separate **General**, **Recording**, **Quick Notes**, **Time Capture**, and **Spell Check** tabs. The four feature tabs start with an enable/disable switch. Disabling a feature unregisters its global hotkey and disables its tray action; disabling Recording never removes the Stop command from an already active recording.
- Windows decides whether a new notification-area icon is shown directly on the taskbar or under the overflow arrow. The app cannot safely force this. To keep the Vince icon visible in Windows 11, open **Settings > Personalization > Taskbar > Other system tray icons** and enable **Vince Productivity Tray**. This is a per-user Windows choice.
- `Ctrl+Alt+R` starts a manual local recording. Stop it using the recorder window or **Stop recording** in the tray.
- **Autorecord meetings** is off by default. When enabled, it reacts to the modern Teams microphone-use transition, not merely an open Teams process. The bundled auto recorder stops after Teams releases the microphone for five seconds. A manual stop does not restart in the same call.
- Recording stores a local 16 kHz mono WAV mixed from system loopback and the default communications microphone. Transcription remains off.

## Notes and spellcheck

- `Ctrl+Alt+N` opens Quick note. Enter saves; Shift+Enter adds a line; Escape cancels. The X button hides the reusable window and preserves an unsent draft; reopening resumes it. A successful UTF-8 append clears the draft and hides the window.
- Select editable text and press `Ctrl+Shift+X` for local Windows-dictionary spellcheck. It supports configured English and Norwegian dictionaries but does not understand sentence meaning like an AI model.
- For text containing several misspellings, the engine compares the aggregate edit quality of Windows' English and Norwegian suggestions. After the language is sufficiently clear, it may use Windows' first-ranked edit-distance-one suggestion to break a tie. For example, `vorfor komer du ike?` becomes `hvorfor kommer du ikke?`. A single ambiguous word remains unchanged. This is local dictionary ranking, not semantic sentence understanding or AI.
- The 0.9.9 rules reject case-only suggestions such as lowercase `na` becoming `Na`. Because Windows nb-NO considers `na` valid, a bounded local rule repairs lowercase `na` to `nå` in multiword Bokmål text while leaving single-word, all-caps, and likely-name forms alone.

## Time capture

Integrated time capture is off by default so it cannot duplicate a still-running legacy `TimeCapture.ahk`. Enable it only after deciding how the legacy script should be handled.

The popup uses the original compact top-to-bottom flow: relative progress, current work, duration, customer, then Submit/Snooze/Hide. Escape or Hide keeps the unsent draft available for the next opening.

- Default hotkey: `Ctrl+Shift+A`.
- Default local folder: `C:\TimeLog`.
- Entries use compatible ISO-week files: `log_YYYY-Www.txt`.
- Format: `yyyy-MM-dd HH:mm - text | dur=Xh | cust="name"`.
- Multiline input is deliberately normalized to one line in the weekly log.
- Customers remain in a separate UTF-8 `customers.txt` beside the weekly log files in the selected Time Capture folder; customer quotes and backslashes are escaped compatibly. The customer manager is an owned modal window and returns focus to the Time Capture customer field when closed.
- Hourly popups default to 08:00 through 16:00. Snooze, workday start, daily target, hotkey, folder, and popup hours are configurable. Hiding or snoozing retains the unsent draft; duplicate timer popups are suppressed.

## Hotkeys and settings

Each shortcut has **Check hotkey**:

- **Available**: a temporary native `RegisterHotKey` registration succeeded and was cleaned up.
- **Occupied**: Windows or another program already owns it.
- **Already used by this app**: it is one of the tray app's active registrations.
- **Invalid**: the syntax is unsupported.

The check cannot detect every low-level keyboard hook or shortcut that only works inside another application. Changed shortcuts become active immediately after Save. A successful save closes Settings without an extra confirmation popup. If any requested registration fails, the actionable error remains visible, settings are not saved, and previous hotkeys are restored. Start with Windows is a separate manual opt-in and asks for confirmation.

## Updates and uninstall

General shows the current version and update state, plus **Check for updates**, **Install update**, and **Auto update**. The official release repository is pinned to `CheggenOnline/vince-productivity-tray`, but update checking remains disabled because no Authenticode publisher is configured. Install and Auto update therefore remain unavailable. Auto update defaults off and can only be enabled in a future signed build with the same repository, checksum, recording, and unsent-draft safeguards as manual installation.

From 0.9.10, uninstall through **Windows Settings > Apps > Installed apps > Vince Productivity Tray**, or run `Uninstall.exe --uninstall` from the installed program folder after exiting the tray app. The app also has a Start-menu shortcut. Settings, notes, recordings, time logs, diagnostics, and migration backups are preserved; removed program files are moved to the app's recoverable `UninstallPending` area.
