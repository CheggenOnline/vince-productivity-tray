# Vince Productivity Tray 0.9.12 verification

## Final packages

- ARM64 installer: 150,091,456 bytes; SHA-256 `A0EAB410D0E10A599B425239BEE197D9BAB128B6D4B6502B362E4216A991352A`; PE machine `0xAA64`; Authenticode `NotSigned`.
- x64 installer: 146,319,028 bytes; SHA-256 `8E759306A5029A9BD9EE7AA13501826D731FE048EE99E2AE451DF564B96E3C2A`; PE machine `0x8664`; Authenticode `NotSigned`.

## Bounded verification

- Release and test compilation: zero warnings and zero errors.
- ARM64 safe core suite: 56/56.
- ARM64 native dictionary suite: 60/60. The installed nb-NO engine corrects `vorfor komer du ike?` to `hvorfor kommer du ikke?`; the corresponding English multi-error regression also passes.
- The standalone ambiguous-word test still refuses tied suggestions. The existing `hva med na da?` regression still produces Norwegian `n&aring;` correctly.
- Quick Note synthetic lifecycle testing covers show, unsent draft, X/Close, reopen, changed settings callback, successful UTF-8 save, clear, and hide without disposing the reusable form.
- Synthetic Settings rendering verifies the revised General tab without the removed section/Exit button and with diagnostics under Application.
- Both exact installer files passed Microsoft Defender custom file scans. Their hashes remained unchanged, with no matching detection or new Defender 1116/1117/1121/1122 event.
- Neither installer nor the normal 0.9.12 tray app was run. No user text, clipboard, global hotkey, recording, autostart, update, or uninstall flow was activated.
- x64 is cross-built and PE-verified only; it was not runtime-tested on this ARM64 machine.

Defender file scans do not establish SmartScreen, runtime behavior, or organizational-policy acceptance. The packages are unsigned and automatic updates remain disabled.
