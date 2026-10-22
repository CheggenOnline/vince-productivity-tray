# Vince Productivity Tray 0.9.2 local candidate

## Improvements

- Rejects empty or shared notes/recording folders and invalid or duplicate global-hotkey definitions.
- Normalizes configured paths and validates the recorder output folder before any recorder process can start.
- Saves settings through a flushed temporary file and atomic replacement.
- Keeps invalid settings visible for correction and persists a declined autostart confirmation correctly.
- Keeps an unsaved quick-note draft visible across focus changes; successful save or Escape closes it.
- Produces version-scoped ARM64/x64 outputs with fail-closed PE, recorder-asset, excluded-file, hash, signature, and JSON-manifest checks.

## Verification boundary

The ARM64 safe core suite passed 22/22. Both installers were built using the official NuGet source with audit active and passed standard Defender file scans. The x64 package was cross-built on ARM64 and was not runtime-tested. Neither installer nor normal app was executed. This unsigned candidate is not approved for installation or distribution merely because its file scan was clean.
