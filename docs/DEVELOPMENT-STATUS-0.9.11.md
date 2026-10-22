# Development status 0.9.11

- The customer-manager z-order/focus fix, feature-specific folder buttons, and final tray Exit action are implemented.
- Safe core tests cover owned-dialog topmost restoration, customer-file placement, the five-tab layout, DPI clipping, feature gates, hotkey rollback, notes, spelling, Time Capture format/scheduling, updater restrictions, installer rollback, Installed apps registration, Start-menu integration, and scoped uninstall.
- Synthetic settings rendering covers every tab. No normal tray app or customer manager was launched against the user's running installation.
- Customer content was not read. The configured path was inspected as metadata only.
- No installer was executed and no recording, global hotkey, clipboard flow, autostart change, update, or uninstall was activated.
- ARM64 is the native test architecture. x64 is cross-built and PE-verified only.
- Packages remain unsigned. Defender file scans are not equivalent to SmartScreen, runtime behavior, or organizational approval.
- GitHub CLI 2.103.0 is installed per-user from the official winget package and authenticated as `CheggenOnline` through the Windows keyring with an expiring, refreshable token. Git credential-helper configuration was declined, so no persistent Git helper setting was changed.
