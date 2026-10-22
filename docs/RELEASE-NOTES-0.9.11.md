# Vince Productivity Tray 0.9.11 local candidate

0.9.11 keeps the 0.9.10 settings, update safeguards, Windows Installed apps registration, Start-menu shortcut, and spelling/Time Capture fixes, with these focused UI corrections:

- The Time Capture customer manager is now explicitly owned by the Time Capture window. If the owner is topmost, ownership is temporarily transferred safely so the modal customer window stays reachable; closing it restores the owner's topmost state and focus.
- Recording, Quick Notes, and Time Capture each show their own **Open folder** action on the corresponding feature tab. General retains diagnostics and application-wide controls.
- **Exit** is restored as the final item in the tray context menu.
- Customer names continue to use a separate UTF-8 `customers.txt` in the configured Time Capture folder, next to `log_YYYY-Www.txt`. Existing files and escaping remain compatible; no customer data is migrated or deleted.

This candidate was built without launching the normal tray app, opening a real customer list, changing settings, activating hotkeys, or starting recording. The source and installers are intended for the user-approved public repository `CheggenOnline/vince-productivity-tray`. The installers remain unsigned, so in-app update checking and installation remain disabled.
