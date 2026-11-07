# Cleanup applied (1.2.0)

The cleanup sequence from the review was applied. This file records what was
removed, what was deliberately kept, and what remains optional.

## Removed

### Abandoned display-output switching

- `src/Core/DisplayTopology.cs`
- `DisplayConfigReader.TryDisableExternalOutputs`
- `DisplayConfigReader.TryRestoreExternalOutputs`
- `DisplayConfigReader.GetPathOutputTechnology`
- `DisplayConfigReader` `SetDisplayConfig` P/Invoke, `SDC_*` constants and
  saved path/mode arrays
- `DisplayTargetInfo.DevicePath`
- `AppPaths.TopologyStateFile`
- `Program` `--test-topology` branch and `TopologyTest`

`DisplayConfigReader.GetActiveTargets()` and `outputTechnology` classification
are retained; they are used to identify internal/external panels.

### Old DDC power path

- `src/Core/DdcMonitorPower.cs` (off/standby/on/probe/test code)
- `Program` `--test-ddc`, `--test-ddc-toggle`, `DdcTest`
- `Settings.DdcOffExternal`
- Settings reset of `DdcOffExternal`

A small compatibility helper remains:

- `src/Core/DdcLegacyRecovery.cs`
- `AppPaths.DdcStateFile`

It only reads an old `ddc-restore` marker left by pre-1.1.6 builds and sends
`VCP 0xD6=0x01` once. The current blackout never sends `0xD6`.

### Dead fields/helpers

- `DisplayInfo.PhysicalWidthMm`
- `DisplayInfo.PhysicalHeightMm`
- `DisplayInfo.DetectionLabel`
- `BacklightState.Maximum`
- `Native.FindWindowEx`
- `Native.IsIconic`
- `InlineBanner` and `InlineBanner.Configure`
- `UiLayout.SectionLabel`
- `UiLayout.BodyLabel`
- `AudioMuteService.RefreshIfDue`
- `PolicyRegistry.UseCurrentUserTestScope`
- `Log.OpenFolder`
- `TrayController.SetDarkForNotifications`
- `Settings.LogLevel`
- `SCREENCURTAIN_TEST_NO_MUTEX`
- private `_notifier` field in `TrayController`

## Kept intentionally

- `--test-backlight` / `DdcBacklight.Probe`: current backlight diagnostic.
- `--self-test`, `--dry-run`, `--test-blackout`, `--test-policy`,
  `--test-audio`, `--test-shutdown-guard`: regression tools.
- `DisplayConfigReader.GetActiveTargets`: internal/external detection.
- `DdcBacklight`, `InternalBacklight`: current backlight-only path.
- `MonitorPower`: desktop/global `SC_MONITORPOWER` path.
- `DisplayInfo.Primary`: chooses the overlay that receives focus.
- `SetupLang`, `SetupDpi`, `Localization`: bilingual app/installer.
- `PreviewOverlay`: preview parity.

## Remaining optional cleanup

- `SetupInfo.ProductName` is Chinese-flavored for ARP even in the English
  installer; changing it affects installed-app naming and should be a product
  decision.
- `--test-backlight` changes the real external backlight for a few seconds; it
  is intentionally a manual diagnostic, not part of normal startup.

## Verification after cleanup

- `build.ps1`: PASS, 44 source files.
- No remaining references to `DisplayTopology`, `DdcMonitorPower`,
  `DdcOffExternal`, `LogLevel`, `TopologyStateFile`, `DetectionLabel`,
  `PhysicalWidthMm`, `PhysicalHeightMm`, `DevicePath`, `FindWindowEx`,
  `IsIconic`, `RefreshIfDue`, `SetDarkForNotifications`,
  `UseCurrentUserTestScope`, `OpenFolder`, `SectionLabel`, `BodyLabel`,
  `InlineBanner`, `DdcTest`, `TopologyTest`, `test-ddc`, `test-topology` or
  `SCREENCURTAIN_TEST_NO_MUTEX`.
