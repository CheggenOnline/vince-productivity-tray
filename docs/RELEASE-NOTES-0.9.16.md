# Vince Productivity Tray 0.9.16 prerelease

- After a successful manual installation or upgrade, setup asks **Start Vince Productivity Tray now?** in a Windows popup.
- **Yes** launches the installed app and closes setup. **No** leaves the app closed; it can be opened from the Start menu later.
- The popup explains that the app icon appears by the clock, possibly under the hidden-icons arrow.
- If Windows blocks or fails to launch the app, the completed installation is retained and setup explains how to start it later.
- New installations enable **Start with Windows** by default. Users can turn it off in Settings. Upgrades preserve the existing startup choice, settings and data.
- Startup registration is part of the shell-integration transaction and is restored if that step fails. Legacy startup items are preserved.
- Downloads remain unsigned and manually launched.

This version has not been installed on the development PC. The launch choice is covered by synthetic tests; a real install and popup test remain pending.
