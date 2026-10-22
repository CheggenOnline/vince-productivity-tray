# Vince Productivity Tray 0.9.11 verification

## Final packages

- ARM64 installer: 150,087,360 bytes; SHA-256 `06A1A11215A1C55E7295915927350315039DF3285A634B6ABDE10221E57BF496`; PE machine `0xAA64`; Authenticode `NotSigned`.
- x64 installer: 146,314,932 bytes; SHA-256 `FB9EAAD631C26AAA81E0792AC9A3DCE77ECA041A09F7AF1134046BE90BB46C20`; PE machine `0x8664`; Authenticode `NotSigned`.

## Bounded verification

- Release and test compilation: zero warnings and zero errors.
- ARM64 safe core suite: 53/53.
- ARM64 native dictionary suite: 56/56, including the exact Norwegian phrase regression. Clipboard/foreground-selection and live global-hotkey tests were deliberately skipped.
- New tests verify that the customer manager stays above its modal owner and restores the owner's topmost state, and that `customers.txt` remains a separate file beside weekly Time Capture logs.
- Synthetic rendering covers all five Settings tabs and the customer-manager dialog. The Recording, Quick Notes, and Time Capture folder buttons are on their respective tabs without clipping; the customer dialog has reachable Add/Remove/Save/Close controls.
- Both exact installer files passed Microsoft Defender custom file scans with antivirus and real-time protection enabled. Their hashes remained unchanged, with no matching detection or new Defender 1116/1117/1121/1122 event.
- Neither installer nor the normal 0.9.11 tray app was run. No customer content, audio, recording, global hotkey, clipboard, autostart, update, or uninstall flow was activated.
- x64 is cross-built and PE-verified only; it was not runtime-tested on this ARM64 machine.

Defender file scans do not establish SmartScreen, runtime behavior, or organizational-policy acceptance. The packages are unsigned.
