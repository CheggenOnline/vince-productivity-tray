# Development status 0.9.13

## Verified

- Installed state was inspected read-only: 0.9.12 is installed and one tray process is running; its settings hash and timestamp were unchanged.
- The public release list contains non-draft prereleases 0.9.12 and 0.9.11, while GitHub's `/releases/latest` endpoint returns 404 because there is no stable release.
- The update parser now selects the highest valid non-draft version from a bounded public release list and ignores drafts and already-installed versions.
- Update discovery repeats every six hours after its initial startup check, while repeated checks suppress duplicate balloons for the same version.
- Update notification is enabled, while direct/automatic installation remains blocked by the empty approved Authenticode signer thumbprint.
- ARM64 core tests cover spelling, Quick Notes lifetime, Time Capture storage/modal behavior, settings feature gates, installer rollback, Installed apps/Start-menu integration, uninstall scope, and updater trust boundaries.
- ARM64 native dictionary tests use synthetic English/Norwegian text only. Clipboard, foreground selection, live hotkeys, recording, and user data are excluded.

## Not yet user-validated

- 0.9.13 has not been installed or launched normally.
- Background update indication and manual opening of the GitHub release page require a later user-controlled runtime test.
- x64 is cross-built and PE-verified; it is not runtime-tested on this ARM64 PC.
- Direct or automatic update installation remains intentionally unavailable until Authenticode signing is configured and verified end to end.
