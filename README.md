# Vince Productivity Tray

A maintainable Windows tray app combining the inspected local meeting recorder, quick notes, time capture, and the existing Christian Spell Helper.

The recorder runtime is packaged from the supplied `MoteOpptak` source: PowerShell capture logic plus NAudio Core/WASAPI libraries. It preserves manual recording and offers opt-in Teams-triggered recording while forcing `Transkriber = nei`; cloud transcription and AI rewriting remain parked. Spellcheck, notes, and time capture remain local-only.

## Build

Requirements: Windows, .NET 9 SDK, and access to official NuGet runtime packs.

```powershell
.\scripts\build-release.ps1 -Version 0.9.11
```

The script creates version-scoped self-contained ARM64 and x64 application folders, payloads, installers, SHA-256 sidecars, and JSON manifests under `artifacts\release`. It fails closed if the PE architecture is wrong, recorder assets are missing, or debug symbols/parked transcription binaries enter a payload. ARM64 core tests run separately; x64 remains cross-built and must not be described as runtime-tested on this ARM64 host.

## Safety and distribution

Development did not start a recorder, access a microphone, restore quarantined files, or modify legacy startup entries. Setup refuses a running standalone helper, keeps Windows startup disabled, transactionally rolls back program files when validation fails, and records a persistent setup log. It runs only an isolated no-audio/no-hotkey self-check; the installed tray app must later be launched explicitly after security or IT approval.

Release payloads contain no personal profile, credential, note, recording, transcription model, customer data, or API key. The demo installers are unsigned, so SmartScreen and enterprise application control may warn or block them. The public release source is pinned to `CheggenOnline/vince-productivity-tray`, but update checking and installation remain disabled until an Authenticode publisher certificate is configured. Nothing installs automatically.

The 0.9.11 source is a development candidate. It has not been installed or launched normally and is not approved for distribution. See `docs/DEVELOPMENT-STATUS-0.9.11.md` for the latest bounded-test status.
