# Vince Productivity Tray 0.9.14 prerelease

- Setup detects a running installed tray app by process name and executable path, and checks again immediately before the program-directory swap.
- When the app is unsigned, Settings now labels the available-update action **Open release page**. It opens only the verified official GitHub release page; it does not imply or attempt unsigned automatic installation.
- The General → Application section displays `developed by Christian Holberg Heggen`.
- Signed automatic installation remains disabled until an approved Authenticode publisher identity is configured and tested end to end.

Close the running tray app with **Exit** before installing this version. Existing settings and data are preserved.
