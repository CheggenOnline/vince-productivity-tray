# Vince Productivity Tray 0.9.10 verification

## Final packages

- ARM64 installer: 150,087,360 bytes; SHA-256 `CC3653219CFFC5ABBC5744A423C9DE7EAAB9BB52D9E5A69D6630BA3E2340DCA3`; PE machine `0xAA64`; Authenticode `NotSigned`.
- x64 installer: 146,314,932 bytes; SHA-256 `63834F741D133FCC7F5F0163F49EE9985D47501ABC6BDA1A62E9A91AB0F857E2`; PE machine `0x8664`; Authenticode `NotSigned`.
- Source ZIP: 325,705 bytes; SHA-256 `6EE2741AD1DA79087B1DCBE44D05EF25DFCD23628C9D78076994456007A90EC2`; 108 entries; no bin/obj/artifacts/NuGet cache/PDB/Whisper/model files.

## Bounded verification

- Release and test compilation: zero warnings and zero errors.
- ARM64 safe core suite: 51/51.
- ARM64 native dictionary suite: 54/54, including exact `hva med na da?` -> `hva med nå da?`. Clipboard/foreground-selection and live global-hotkey tests were deliberately skipped.
- Installed apps and Start-menu behavior is covered by isolated fake-registry/test-filesystem tests for idempotent upgrade, scoped removal, install rollback, and uninstall rollback. The real registry and Start menu were not changed.
- Settings layout tests at 150% scaling verify Browse/Save/Cancel height, complete labels, five tabs, no overlapping rows, feature-state compatibility, and tray action gates. All five tabs were also rendered with synthetic settings for visual inspection; previews are not distributed because the local default paths contain a Windows profile name.
- Both exact installer files passed Microsoft Defender custom file scans with antivirus and real-time protection enabled. Their hashes remained unchanged, with no matching new detection or Defender 1116/1117/1121/1122 event.
- Neither installer nor the normal 0.9.10 tray app was run. No audio, recording, global hotkey, clipboard, autostart, update, or uninstall flow was activated.
- x64 is cross-built and PE-verified only; it was not runtime-tested on this ARM64 machine.

Defender file scans do not establish SmartScreen, runtime behavior, or organizational-policy acceptance. The packages are unsigned.
