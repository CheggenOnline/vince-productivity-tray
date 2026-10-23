# Vince Productivity Tray

A maintainable Windows tray app combining the inspected local meeting recorder, quick notes, time capture, and the existing Christian Spell Helper.

The recorder runtime is packaged from the supplied `MoteOpptak` source: PowerShell capture logic plus NAudio Core/WASAPI libraries. It preserves manual recording and offers opt-in Teams-triggered recording while forcing `Transkriber = nei`; cloud transcription and AI rewriting remain parked. Spellcheck, notes, and time capture remain local-only.

## Build

Requirements: Windows, .NET 9 SDK, and access to official NuGet runtime packs.

```powershell
.\scripts\build-release.ps1 -Version 0.9.16
```

The script creates version-scoped self-contained ARM64 and x64 application folders, payloads, installers, SHA-256 sidecars, and JSON manifests under `artifacts\release`. It fails closed if the PE architecture is wrong, recorder assets are missing, or debug symbols/parked transcription binaries enter a payload. ARM64 core tests run separately; x64 remains cross-built and must not be described as runtime-tested on this ARM64 host.

## Safety and distribution

Development did not start a recorder, access a microphone, restore quarantined files, or modify legacy startup entries. Setup refuses a running standalone helper, transactionally rolls back program files when validation fails, and records a persistent setup log. It runs an isolated no-audio/no-hotkey self-check. After successful setup, a popup offers to start the app immediately. New installations enable per-user Start with Windows by default; upgrades preserve the existing startup choice. The user can disable startup in Settings. Legacy startup items remain unchanged.

Release payloads contain no personal profile, credential, note, recording, transcription model, customer data, or API key. Installers are unsigned by design; see `docs/PROJECT-DECISIONS.md`. The public release source is pinned to `CheggenOnline/vince-productivity-tray`. Update discovery and **Download update** are enabled. Downloads select the PC architecture and check SHA-256; the user launches setup manually. Downloading never closes the app or runs an installer. Windows and enterprise application control may warn or block unsigned installers.

Version 0.9.16 adds an optional launch at the end of setup and defaults Start with Windows to on for new installations. No installer was run during development of this change. See `docs/DEVELOPMENT-STATUS-0.9.16.md` for validation limits.

## Sharing with colleagues

Send the [GitHub Releases link](https://github.com/CheggenOnline/vince-productivity-tray/releases), or share the local `distribution` folder. It contains only its README and the ARM64/x64 installers for one version. Refresh it from existing local release artifacts with:

```powershell
.\scripts\prepare-distribution.ps1 -Version 0.9.16
```

The sharing folder is refreshed after each local release build. The preparation script validates both source checksums and both copies before removing obsolete generated installers. It does not build, install, or upload anything. Installer binaries are excluded from Git; GitHub Releases hosts them, while the repository keeps the sharing instructions and preparation script. The GitHub Actions release workflow builds and publishes a prerelease when new release notes are pushed to `main`; it publishes only after synthetic checks, both architecture builds and all asset uploads succeed. Use `-Offline` for a local build with previously cached runtime packs.
