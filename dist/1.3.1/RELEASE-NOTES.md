# ScreenCurtain 1.3.1

## Highlights

- New complete Windows 11 UI: first-run wizard, dashboard, settings, tray, exit/restore dialogs.
- Fixed update-guard handshake deadlock after UAC: helper speaks HELLO first, client replies OK, then policies are applied.
- Borderless full-screen privacy layer covers the taskbar and notification area; tray icon is not hidden.
- Laptops with a battery default to overlay-only blackout; a separate opt-in "laptop physical off" setting is available and defaults off.
- Laptop backlight-only mode: external panels use DDC/CI luminance (VCP 0x10) set to 0; internal panels use WMI WmiSetBrightness(0) when available. The monitors remain powered, so their USB hubs and keyboard/mouse stay alive. Original luminance is restored on wake. No VCP 0xD6 power-mode command is sent.
- Complete Windows 11 UI with Chinese/English localization. Auto follows the system language; a manual language selector is in Settings > General and takes effect after restart.
- Setup and uninstaller are now bilingual too. They auto-select the system language; /LANG=EN forces English and /LANG=ZH forces Chinese.
- Start with Windows uses the current-user Run key (no administrator rights); the path is refreshed when a portable build moves.
- Update-guard start timing is selectable: at app startup (default) or blackout-only. Both request UAC only once at app startup; blackout-only starts the helper paused/restored, applies policies on blackout, pauses on wake, and never prompts again during the session.
- Dark theme supports Auto/Light/Dark and a dark title bar; theme changes apply after restart.
- English UI localization and layout fixes: all dark dropdowns select reliably, confirmation buttons auto-fit translated text, and translated help text no longer clips.
- Sleep protection now uses a system PowerRequest in addition to SetThreadExecutionState, blocking automatic sleep/hibernate while dark; resume restores the display and watchdog ignores suspend/resume gaps.
- Preview 3 seconds now matches the real blackout: same DDC/WMI backlight dimming, audio mute, borderless black layers, DWM square corners and hidden cursor; all are restored after the preview.
- Logging is asynchronous so a slow network/synced data folder cannot block the UI thread. Watchdog and unhandled failures write a detailed crash report to %LOCALAPPDATA%\ScreenCurtain\logs\crash-*.log (and best-effort to the portable data logs).
- Audio restore now runs after display/overlay restore, verifies mute state, retries, and retries after session unlock.
- Topmost Guard reduces third-party popups appearing above the black screen.
- System audio is muted while the screen is dark and restored on wake.
- Multi-size brand icon and tray status icons.
- Per-user installer and portable ZIP; settings remain in LocalAppData for the installed build.

## Known limits

- UAC, secure desktop, Ctrl+Alt+Del, lock screen and system-critical prompts cannot be covered.
- Physical DPMS support varies by display/driver; the full-screen layer remains black.
- DDC/CI support varies by monitor/dock. Unsupported external screens stay on the black overlay; when DisplayConfig connection type is unavailable, <=21 in screens are conservatively treated as internal. Some panels cannot fully extinguish their backlight at luminance 0. Internal WMI brightness may be unavailable on some drivers, OLED/HDR modes, or restricted accounts.
- No code-signing certificate is available; Windows SmartScreen may warn.
- The current build environment has no permission for HKCU/HKLM integration tests; full shortcut/ARP/UAC tests require a normal desktop.

## Files

- installer/ScreenCurtain-Setup-1.3.1.exe
- portable/ScreenCurtain-1.3.1-portable.zip
