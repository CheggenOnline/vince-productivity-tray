# Vince Productivity Tray 0.9.8 verification

Date: 2026-10-22

- Release build: 0 warnings, 0 errors; official NuGet feed with vulnerability audit active.
- ARM64 safe core suite: 39 passed, 0 failed.
- ARM64 installer: 150,066,878 bytes; PE `0xAA64`; SHA-256 `02FA82D9625A306039F7F060E408A57675C779754EC6FE71CB1A56EDDA4578BE`; unsigned.
- x64 installer: 146,294,450 bytes; PE `0x8664`; SHA-256 `838C044D212682830AD5E117783A4F1E036349040B37A8DC91F95166C3183A9E`; unsigned.
- Both payloads contain recorder assets and no PDB, Whisper executable, or transcription model.
- Both exact installer files passed a Microsoft Defender custom file scan with antivirus and real-time protection enabled. Their hashes were unchanged, and no matching Defender detection/event was present after the scan. This does not establish SmartScreen or runtime-policy approval.
- Synthetic VinceUtility test creates all three subdirectories twice and preserves an existing file.
- Tests do not access a real OneDrive root, time log, note, recording, customer file, clipboard, hotkey, audio device, startup entry, or legacy process.
- Four first-install tests verify VinceUtility defaults, byte-preservation of existing custom settings, pre-existing empty directories, and fail-closed missing/ambiguous OneDrive discovery.
- Three hotkey tests use a fake native-registration backend: immediate rebind including a swap, rollback after an occupied target, and availability-probe cleanup. They do not register or trigger real global hotkeys.
- Neither installer nor normal 0.9.8 tray app was executed. x64 is cross-built/PE-verified only on the ARM64 host.
