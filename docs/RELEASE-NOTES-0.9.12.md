# Vince Productivity Tray 0.9.12 prerelease

- Corrects realistic multi-error Norwegian text such as `vorfor komer du ike?` to `hvorfor kommer du ikke?` using only installed Windows dictionaries.
- Language selection now considers aggregate suggestion edit quality. Ranked tie-breaking is limited to edit-distance-one candidates in already-confident multi-error text; a single ambiguous word remains unchanged.
- Prevents the phrase repairer from splitting a misspelled word when Windows has a close whole-word suggestion, avoiding output such as `vor for kom er`.
- Fixes the reusable Quick Note window being disposed by its X button. X now hides and preserves an unsent draft, Escape cancels and clears it, and a successful save clears it after writing.
- Removes the successful Settings/hotkeys confirmation popup. Registration conflicts and save failures still show actionable errors.
- Removes the General `Folders and application` section and redundant Settings Exit button. `Open diagnostics folder` now sits in Application; tray Exit remains last.

The release remains unsigned. In-app update checking and installation remain disabled until an Authenticode publisher is configured and the end-to-end update flow is reverified.
