# 0.9.0 demo candidate

- Combines the existing recorder, conservative local spellchecker, and quick notes in one tray/settings experience.
- Preserves manual recording, Teams transition-triggered automatic recording, original NAudio loopback/microphone mixing, graceful stop, and no restart during the same call after a manual stop.
- Keeps transcription and AI rewriting disabled; no model, API key, account, telemetry, recording, note, credential, or customer data is packaged.
- Adds configurable, conflict-safe defaults: spellcheck Ctrl+Shift+X, notes Ctrl+Alt+N, and recording Ctrl+Alt+R.
- Adds UTF-8 daily notes with serialized appends, multiline input, failure retention, and local-date rollover.
- Preserves English/Norwegian Bokmal language selection, local phrase repair, focus/selection safeguards, formatting normalization, and clipboard restoration.
- Adds first-install folder choices, one settings UI, per-user startup, recoverable uninstall, and explicit migration of both legacy startup mechanisms.
- Adds separate approved-release notification and strict signed in-app installation policies. Both remain disabled for this unpublished unsigned demo.
- Provides self-contained ARM64 and x64 per-user installer builds. x64 is cross-built and not runtime-tested on this ARM64 machine.

The installers are unsigned demo candidates. SmartScreen or corporate policy may warn or block them; no warning should be bypassed.
