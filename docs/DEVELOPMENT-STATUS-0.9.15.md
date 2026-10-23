# Vince Utility 0.9.15

## Behavior

- Tray hover text contains only the running version (three components), and no longer changes with spelling or update status.
- Settings, Quick Note, Time Capture and Customers use the same small, left-aligned developer footer. Time Capture and Customers are wider to fit the credit beside the actions. Transient notifications and Windows-owned dialogs are not application editor windows.
- The Quick Note hotkey reads the foreground accessibility text selection after the configured shortcut has been released. A stable nonempty selection is appended to the daily note without opening the editor, changing the clipboard, editing the source, or clearing an existing unsent Quick Note draft. A non-activating tile confirms the successful save in the lower-right working area of the screen containing the pointer.
- No selection opens the editor. Unsupported accessibility providers, password fields, changed focus/selection, and selections exceeding 20,000 characters fall back to the editor with an explanatory tile. There is no blind Ctrl+C fallback that could capture an entire line or document when no selection exists. Cross-application accessibility support still needs a user-controlled runtime test.
- Time Capture settings include **Use customer list**, defaulting to on for compatibility with existing settings. Off hides the customer label, dropdown and manager button, collapses those layout rows, and writes time entries without a customer. The customer file and existing entries are preserved. Settings apply to an already visible popup as well as the next opening.
- Time Capture popup text uses regular weight, including the relative progress value.

## Updates and sharing

The intended distribution is unsigned (see PROJECT-DECISIONS.md). Download update selects the PC architecture, downloads from the pinned GitHub repository, validates SHA-256 and offers to open the file location. The user starts setup manually. No signing certificate is required. A checksum confirms file integrity, not publisher identity. Downloading never closes the app or starts setup.

The distribution folder holds its README and the ARM64/x64 installers for 0.9.15 after a local release build. GitHub Releases hosts the binaries. The release workflow verifies and builds both architectures before publishing a complete prerelease.

## Scope and remaining verification

No install, normal tray launch, startup change, recording or real user selection was performed for these changes. Existing installed 0.9.14 is untouched. Release installers are built for ARM64 and x64; installation and a real upgrade remain untested. Compilation succeeded with zero warnings and zero errors; 67/67 ARM64 core tests passed (`artifacts/test-reports/core-0.9.15.json`). Synthetic tests cover selection policy, saves/fallback/failure, draft preservation, customer settings/persistence/omission and footer layout at normal and scaled sizes. Generated UI previews were inspected. Live shortcut behavior in Outlook, Teams and browsers, notification placement on multiple displays, and a real upgrade remain pending. x64 runtime verification is not claimed.

## Publication status

Publication was attempted through the GitHub connector but rejected with: MCP tool call requires approval, but approval policy is never. No repository write, commit, push or release publication was performed. Direct Git networking is also unavailable in this execution environment. Both final installers remain available locally in distribution. The GitHub Actions workflow is prepared but has not run.
