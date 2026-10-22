# Vince Productivity Tray 0.9.6 verification

Date: 2026-10-22

- Release build: 0 warnings, 0 errors; official NuGet feed with vulnerability audit active.
- ARM64 safe core suite: 32 passed, 0 failed.
- ARM64 installer: 150,062,782 bytes; PE `0xAA64`; SHA-256 `E238488E1FECC7AAE5F60EAEEF074595561CAA41E3C0228B2037CEB682F8A93E`; unsigned.
- x64 installer: 146,290,354 bytes; PE `0x8664`; SHA-256 `5054682A4ADA4BAC4B6AF2ECAD016234DC6A5DF3F9E27D3E72537EC02649DB81`; unsigned.
- Both payloads contain recorder assets and no PDB, Whisper executable, or transcription model.
- Both exact installer files passed a Microsoft Defender custom file scan with antivirus and real-time protection enabled. Their hashes were unchanged, and no matching Defender detection/event was present after the scan. This does not establish SmartScreen or runtime-policy approval.
- Synthetic VinceUtility test creates all three subdirectories twice and preserves an existing file.
- Tests do not access a real OneDrive root, time log, note, recording, customer file, clipboard, hotkey, audio device, startup entry, or legacy process.
- Neither installer nor normal 0.9.6 tray app was executed. x64 is cross-built/PE-verified only on the ARM64 host.
