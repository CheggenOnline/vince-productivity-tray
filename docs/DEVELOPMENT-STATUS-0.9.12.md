# Development status 0.9.12

- Installed 0.9.11 was confirmed running before development; no installed files, processes, settings, drafts, clipboard, hotkeys, recordings, or startup entries were changed.
- The concrete Norwegian failure was diagnosed against nb-NO and en-US Windows dictionaries with synthetic text only.
- Safe core tests cover multi-error Norwegian/English correction, retained single-word ambiguity protection, Quick Note close/reopen/draft/settings behavior, Settings layout, and all existing recorder/notes/time/hotkey/updater/installer safeguards.
- Native ARM64 tests use the installed Windows dictionaries without foreground selection or clipboard access.
- Synthetic Settings rendering verifies the revised General tab. No normal 0.9.12 tray app or installer was launched.
- ARM64 is the native test architecture. x64 is cross-built and PE-verified only.
- Packages remain unsigned. Defender file scans are not equivalent to SmartScreen, runtime behavior, or organizational approval.
