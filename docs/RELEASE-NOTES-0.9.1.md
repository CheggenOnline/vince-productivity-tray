# Vince Productivity Tray 0.9.1 repair candidate

This local-only repair candidate responds to Microsoft Defender quarantining the earlier unsigned installer and launcher after the installer created a per-user Run entry.

## Installer safety changes

- Setup never creates a Windows startup entry or changes a legacy startup item.
- The installed application is not left running. Manual launch is an explicit later action after security or IT approval.
- A persistent timestamped setup log is written under `%LOCALAPPDATA%\VinceProductivityTray\SetupLogs`.
- Program-directory replacement uses an atomic transaction journal. Failed validation restores the previous directory; the next setup run recovers an interrupted replacement.
- Setup runs an isolated startup self-check that does not initialize recording, Teams detection, global hotkeys, clipboard access, or updates.
- Start with Windows is off for a new installation. Enabling it later in Settings requires explicit confirmation.
- Uninstall preserves settings, logs, notes, recordings, and legacy migration backups.

## Preserved application behavior

Quick notes, local English/Norwegian Bokmål spellcheck, local WAV capture, manual recording, Teams-triggered automatic recording, status, and graceful stop remain as documented. Local Whisper transcription is still excluded and AI rewriting remains parked.

This build is unsigned. Static Defender scanning cannot guarantee that corporate application control or later behavior monitoring will permit it. Do not bypass warnings, restore the quarantined older build, or add exclusions; use an approved signing/allowlisting route for colleague distribution.
