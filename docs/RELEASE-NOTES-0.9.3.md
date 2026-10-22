# Vince Productivity Tray 0.9.3 local candidate

## Spellcheck reliability

- Allows up to two seconds for `Ctrl+Shift+X` to be released and requires a stable 40 ms key-up interval before copying.
- Re-captures and verifies the foreground/focused selection after key release, so a focus change still fails closed.
- Gives the editor 35 ms to settle after key release.
- Retries one transient copy/input timeout after 40 ms, while focus changes, password fields, and changed selections remain terminal safety failures.
- Records failure status and elapsed time without logging selected text.

## Verification boundary

The ARM64 safe core suite passed 24/24, including deterministic key-release bounce/timeout and transient-retry/terminal-stop tests. Native ARM64 and x64 installers were built with NuGet audit active and passed standard Defender file scans with unchanged hashes. x64 was cross-built on ARM64 and was not runtime-tested. The installed 0.9.2 application was not replaced or stopped, and no live hotkey, clipboard, recording, autostart, or installer execution was performed. Runtime verification of this fix still requires explicit approval to install and test the new unsigned candidate.
