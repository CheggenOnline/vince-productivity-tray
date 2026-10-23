# Vince Productivity Tray 0.9.15 prerelease

- Tray-icon hover text shows only the installed version.
- A shared developer footer appears in Settings, Quick Note, Time Capture and Customers.
- The Quick Note hotkey saves selected text directly and confirms with a small tile. Without an accessible selection, the editor opens instead. Existing drafts are preserved.
- Time Capture has a **Use customer list** setting. Turning it off hides customer controls and saves entries without a customer. Existing customer lists are kept.
- Time Capture popup text uses regular weight.
- **Download update** downloads the correct ARM64/x64 unsigned installer and checks its SHA-256 checksum. It can open the download folder; you start setup yourself. Downloading never closes the app or runs setup automatically.
- The `distribution` README provides direct links for both architectures. Local release builds refresh the sharing folder with just its README and the two installers.

Save drafts, stop recording and choose **Exit** before running setup. The installers are unsigned by design. Windows or company policy may warn or block them. Existing settings and data are preserved.

Accessibility support for selected-text capture varies between applications. A real upgrade and the shortcuts in your everyday applications still need runtime validation.
