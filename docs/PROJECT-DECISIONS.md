# Project decisions

## Distribution and updates — 2026-10-23

The user has decided that Vince Utility will be distributed **without code signing**. Treat unsigned installers as the intended distribution model for future work. Do not make signing a prerequisite for downloading updates or repeatedly suggest acquiring a signing certificate.

The chosen update flow is: discover the official release, download the correct architecture's installer, check SHA-256, offer to open its folder, and let the user launch setup manually. Downloading does not execute setup or stop the app. Normal Windows and organization policy still apply.

The user authorizes publishing completed and verified changes to GitHub. This does not authorize installing or upgrading the app on the development PC.

## Installation completion and startup — 2026-10-23

After successful installation, ask whether to start the app immediately. Starting with Windows defaults to on for new users; preserve existing users' startup choices on upgrades. Both preferences remain under user control in the installer/Settings. This supersedes older documentation that kept startup off during first-time setup.
