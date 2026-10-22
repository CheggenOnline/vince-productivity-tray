# Development status - 0.9.3 candidate

Date: 2026-10-22

## Evidence and diagnosis

- The local activity log confirms that the user's third attempt corrected two Norwegian words in 887 ms; no selected text was logged.
- The first reported message maps directly to the previous fixed 750 ms key-release timeout.
- The second message maps to the previous single 500 ms copy wait. The available 0.9.2 log did not record failed statuses, so its exact trigger cannot be reconstructed.
- The legacy `ChristianSpellHelper.exe` process was not running during inspection. Its HKCU Run entry still exists and could cause a future registration conflict if it starts before the unified app.

## Implemented

- Stable key-release debounce with a two-second timeout and 40 ms stable-up window.
- A fresh foreground/focus/selection identity snapshot after release, matched to the original target.
- A short post-release settle interval.
- One retry for transient copy/input timeouts; focus, password, and selection changes remain non-retryable.
- Content-free diagnostic logging for non-success results.

## Verification

- Release app build: 0 warnings, 0 errors.
- ARM64 safe core suite: 24 passed, 0 failed.
- New deterministic tests cover a brief key-up bounce, held-key timeout, transient first-attempt recovery, and terminal safety-stop behavior.
- The test project required `-m:1` because parallel MSBuild project-reference evaluation returned failure without diagnostics; the single-node build completed normally.
- Native dictionary, foreground selection, clipboard roundtrip, live global hotkey, recording, autostart, normal installation, and installed-app replacement were not run.

## Packaging

The first sandboxed restore stopped with `NU1900` because vulnerability audit could not reach the official `https://api.nuget.org/v3/index.json`. Audit was not disabled and no alternate source was used. The unchanged release build then ran through the approved official-feed network path; all ARM64/x64 application and setup restores completed with audit active.

- ARM64 installer: 150,009,534 bytes; SHA-256 `5B03FD94431C6942577D3AB57A6BC1BA370D4AFF657FB08AFB7F856E5452E4C9`; PE `0xAA64`; unsigned.
- x64 installer: 146,237,106 bytes; SHA-256 `0CCD730276B6D7D7775E39A974E93EB381F5F7E6A07915A328054CFDB761CC72`; PE `0x8664`; unsigned.
- Source ZIP verification requires no bin/obj/artifacts/cache/PDB or personal-path/secret-pattern hits; its final hash is recorded beside the archive after packaging.
- Both exact installer files remained present with unchanged hashes after Microsoft Defender custom scans. No new detection/remediation event 1116/1117 referenced 0.9.3.

The x64 build is cross-built/PE-verified only. Neither 0.9.3 installer nor application was executed. The installed 0.9.2 executable remained version `0.9.2.0`, hash `49642CE1D98946A502E91E9F00ED1890ED161AE1CC5244B63BB32C92D1825090`, with one running process and no unified Run entry. The legacy spellhelper Run entry was not changed.
