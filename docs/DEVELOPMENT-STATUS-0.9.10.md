# Development status 0.9.10

- Installed apps/Start-menu integration is implemented for the current Windows user.
- Five-tab Settings and the reduced four-action tray menu are implemented. Synthetic rendering covers every tab; automated tests cover DPI clipping, compatibility defaults, toggles, active/inactive hotkey collisions, tray feature gates, and the recording Stop exception.
- Isolated fake registry/filesystem tests cover repeat upgrade, scoped removal, install rollback, and uninstall rollback without touching live Windows state.
- Existing 0.9.9 spelling and Time Capture behavior remains covered by the full core/native suites.
- No installer was executed and no normal tray app, recording, global hotkey, clipboard flow, autostart change, or real uninstall was started.
- ARM64 is the native test architecture. x64 is cross-built and PE-verified only.
- Packages remain unsigned. Defender file scans are not equivalent to SmartScreen, runtime behavior, or organizational approval.
- GitHub publishing/direct updates remain gated by confirmation of the exact repository/visibility and a real Authenticode signing identity. No Git repository, GitHub CLI, authenticated publishing connector, or signing credential was present in the inspected workspace.
