# Vince Productivity Tray 0.9.10 local candidate

0.9.10 preserves the 0.9.9 spelling correction and compact Time Capture UI, and adds Windows shell integration:

- Setup registers the current-user installation under Windows **Installed apps** with display version, publisher, install location, icon, size, and an uninstall command.
- Setup creates a current-user **Vince Productivity Tray** Start-menu shortcut.
- Upgrade is idempotent. If shell integration or installed-app validation fails, program files and the previous registration/shortcut are restored.
- Uninstall removes only the app's named Run value, exact Installed apps registration, Start-menu shortcut, and program directory. Program files are moved to the recoverable `UninstallPending` area. Settings, notes, recordings, time logs, diagnostics, and migration backups remain untouched.
- The Settings label column is wider and regression-tested at 150% scaling so `Autorecord meetings` and `Start with Windows` remain complete.
- Settings now uses five focused tabs: **General**, **Recording**, **Quick Notes**, **Time Capture**, and **Spell Check**. Each feature tab has a top-level enable switch; older settings files keep recording, notes, and spellcheck enabled for compatibility.
- The tray menu is limited to **Start/Stop Recording**, **New quick note**, **New time entry**, and **Settings**. Disabled features have disabled actions; an active recording always retains its Stop action.
- General contains version/update state, Check/Install update controls, Auto update (off by default and unavailable until signing is configured), folder shortcuts, diagnostics, startup, and Exit.

The update UI now has a prepared, explicit **Install update** action and a persistent tray tooltip when a newer release is found. It refuses to install during recording or while a Time Capture draft is pending, downloads only the architecture-specific asset from the pinned GitHub release path, checks SHA-256, requires a valid Authenticode signature from the pinned publisher, and relaunches only after setup validation succeeds.

Update checking remains disabled in this candidate. The source directory has no Git metadata/origin, the proposed repository `CheggenOnline/vince-productivity-tray` could not be verified from this environment, and no publisher certificate/thumbprint is configured. The Install/Auto update controls remain unavailable, the signing gate was not weakened, and nothing was published.
