# ScreenCurtain 1.1.9

## Highlights

- New complete Windows 11 UI: first-run wizard, dashboard, settings, tray, exit/restore dialogs.
- Fixed update-guard handshake deadlock after UAC: helper speaks HELLO first, client replies OK, then policies are applied.
- Borderless full-screen privacy layer covers the taskbar and notification area; tray icon is not hidden.
- Laptops with a battery default to overlay-only blackout; a separate opt-in "laptop physical off" setting is available and defaults off.
- Laptop backlight-only mode: external panels use DDC/CI luminance (VCP 0x10) set to 0; internal panels use WMI WmiSetBrightness(0) when available. The monitors remain powered, so their USB hubs and keyboard/mouse stay alive. Original luminance is restored on wake. No VCP 0xD6 power-mode command is sent.
- Complete Windows 11 UI with Chinese/English localization. Auto follows the system language; a manual language selector is in Settings > General and takes effect after restart.
- Preview 3 seconds now matches the real blackout: same DDC/WMI backlight dimming, audio mute, borderless black layers, DWM square corners and hidden cursor; all are restored after the preview.
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

- installer/ScreenCurtain-Setup-1.1.9.exe
- portable/ScreenCurtain-1.1.9-portable.zip
