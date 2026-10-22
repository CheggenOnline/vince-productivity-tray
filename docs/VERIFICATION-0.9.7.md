# Vince Productivity Tray 0.9.7 verification

Date: 2026-10-22

- Release build: 0 warnings, 0 errors; official NuGet feed with vulnerability audit active.
- ARM64 safe core suite: 36 passed, 0 failed.
- ARM64 installer: 150,062,782 bytes; PE `0xAA64`; SHA-256 `FD37AA3066DA64F6152688145950701FE76756418472441E8F8927531A70C9BE`; unsigned.
- x64 installer: 146,290,354 bytes; PE `0x8664`; SHA-256 `6EF50AEF81A5CDEF05D5C836FAD755534B3D73DC2087E4DDDFC9A184272DC193`; unsigned.
- Both payloads contain recorder assets and no PDB, Whisper executable, or transcription model.
- Both exact installer files passed a Microsoft Defender custom file scan with antivirus and real-time protection enabled. Their hashes were unchanged, and no matching Defender detection/event was present after the scan. This does not establish SmartScreen or runtime-policy approval.
- Synthetic VinceUtility test creates all three subdirectories twice and preserves an existing file.
- Tests do not access a real OneDrive root, time log, note, recording, customer file, clipboard, hotkey, audio device, startup entry, or legacy process.
- Four first-install tests verify VinceUtility defaults, byte-preservation of existing custom settings, pre-existing empty directories, and fail-closed missing/ambiguous OneDrive discovery.
- Neither installer nor normal 0.9.7 tray app was executed. x64 is cross-built/PE-verified only on the ARM64 host.
