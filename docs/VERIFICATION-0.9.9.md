# Vince Productivity Tray 0.9.9 verification

Date: 2026-10-22

- Release build: 0 warnings, 0 errors; official NuGet feed with vulnerability audit active.
- ARM64 safe core suite: 42 passed, 0 failed.
- ARM64 native-only suite: 44 passed, 0 failed; real nb-NO exact-phrase correction passed, with no clipboard, selection, or global-hotkey test.
- ARM64 installer: 150,066,878 bytes; PE `0xAA64`; SHA-256 `3C670B6133205F6269A213D17E483D7C99FA43ED13362DD26DC4314EC4DA493E`; unsigned.
- x64 installer: 146,294,450 bytes; PE `0x8664`; SHA-256 `8165B99A2990C3B7AF8132249CF3A99867913585B6E8BAD464E266A846F45F45`; unsigned.
- Both payloads contain recorder assets and no PDB, Whisper executable, or transcription model.
- Both exact installer files passed a Microsoft Defender custom file scan with antivirus and real-time protection enabled. Their hashes were unchanged, and no matching Defender detection/event was present after the scan. This does not establish SmartScreen or runtime-policy approval.
- Synthetic VinceUtility test creates all three subdirectories twice and preserves an existing file.
- Tests do not access a real OneDrive root, time log, note, recording, customer file, clipboard, hotkey, audio device, startup entry, or legacy process.
- Four first-install tests verify VinceUtility defaults, byte-preservation of existing custom settings, pre-existing empty directories, and fail-closed missing/ambiguous OneDrive discovery.
- Three hotkey tests use a fake native-registration backend: immediate rebind including a swap, rollback after an occupied target, and availability-probe cleanup. They do not register or trigger real global hotkeys.
- The synthetic Time Capture window was rendered from a temporary folder and visually inspected. Its compact layout and DPI-safe controls passed; no real customer or time-log data was read or written.
- Neither installer nor normal 0.9.9 tray app was executed. x64 is cross-built/PE-verified only on the ARM64 host.
