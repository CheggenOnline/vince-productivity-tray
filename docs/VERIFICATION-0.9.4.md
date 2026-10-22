# Vince Productivity Tray 0.9.4 verification

Date: 2026-10-22

## Builds

- Release solution build: 0 warnings, 0 errors.
- Official NuGet feed used with vulnerability audit active. The first sandboxed x64 setup restore stopped with NU1900 because the feed was unreachable; the unchanged command completed through the approved official-feed network path. Audit was not disabled and no alternative feed was used.
- ARM64 installer: 150,058,686 bytes; PE `0xAA64`; SHA-256 `A5DA923EBDF2FEEA4DD2F85074732D86C190A71693B14118FAD08C7B617C9B26`; `NotSigned`.
- x64 installer: 146,286,258 bytes; PE `0x8664`; SHA-256 `7F59512AF08449C806E01DA472B7A82A4648A65EFA272704C34609587B5BD42B`; `NotSigned`.
- Both payloads contain the recorder assets and no PDB, Whisper executable, or transcription model.

## Safe tests

- ARM64 core suite: 31 passed, 0 failed.
- Tests used temporary synthetic directories and data. They did not use `C:\TimeLog`, notes, recordings, customer data, clipboard contents, audio devices, global hotkeys, or startup entries.
- Settings controls were constructed and scaled to 150% without showing the form; all Browse/Save/Cancel controls met their preferred vertical size. This is bounded layout testing, not live visual verification on each monitor/DPI.
- The icon contains transparent PNG frames at 16, 20, 24, 32, 40, 48, 64, and 256 px and was loaded for visual inspection from the build asset.

## Defender and runtime boundary

- Microsoft Defender antivirus and real-time protection were enabled. Standard CustomScan completed for both exact installers; each remained present with the same SHA-256.
- No Defender event 1116, 1117, 1121, or 1122 and no threat detection referenced 0.9.4 after scanning.
- A clean file scan does not prove SmartScreen, behavior-monitoring, or organizational acceptance at runtime.
- Neither installer nor the normal 0.9.4 tray app was executed. No recording, live shortcut, clipboard, autostart, updater, or legacy-AHK change was made. x64 is cross-built/PE-verified only and was not runtime-tested on this ARM64 host.
