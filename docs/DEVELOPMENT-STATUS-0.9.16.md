# Vince Utility 0.9.16

## Changes

- Successful manual setup offers a native Windows Yes/No popup asking whether to launch the installed app immediately. Yes is selected by default, but setup still waits for the user's choice.
- Choosing Yes launches the installed executable and closes setup. Declining leaves the app closed. A launch failure is reported separately and does not roll back an otherwise completed installation.
- Fresh settings enable Start with Windows. First-time setup registers the app-owned HKCU Run entry after the isolated self-check, inside the shell-integration transaction. Failed startup registration restores the previous shell state.
- Upgrades preserve the actual startup registration; the installer does not re-enable it for an existing installed program. A reinstall with explicitly disabled startup in saved settings also preserves that choice. A retry of a failed first install can complete the startup registration from its fresh saved settings.
- The application reads the current registry startup state when opening the normal tray context. Users can turn startup off in Settings.
- User authorization for startup defaults supersedes the older off-by-default setup behavior. Existing legacy recorder/helper startup items remain untouched.

## Verification and publication

The ARM64 core suite passed 70/70 checks with zero compilation warnings/errors. Tests cover launch consent/decline/failure, fresh startup defaults, existing preferences, and rollback of a partially written startup registration, using synthetic stores/files only.

ARM64/x64 installers are prepared with the existing architecture/hash checks and refreshed into distribution. No installer, real popup, tray app, registry startup write or recording was run on the development PC for this change. Real first-install/upgrade validation remains pending, and x64 is not locally runtime-tested.

Publication is authorized for this completed and locally verified version. Pushing the new release notes to `main` triggers the existing GitHub Actions workflow; it publishes the prerelease only if its checks, both architecture builds, and all asset uploads succeed. Publishing does not install or run the app on the development PC.
