$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$version = '1.2.0'
$buildDir = Join-Path $root 'build'
$distRoot = Join-Path $root ('dist\' + $version)
$installerDir = Join-Path $distRoot 'installer'
$portableDir = Join-Path $distRoot 'portable'
$portableStage = Join-Path $root ('artifacts\ScreenCurtain-' + $version + '-portable')
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$manifest = Join-Path $root 'installer\Setup.manifest'
$icon = Join-Path $root 'src\App.ico'
$refs = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.dll')

Write-Host "Building ScreenCurtain $version"

if (-not (Test-Path -LiteralPath $csc)) { throw "Missing csc: $csc" }
if (-not (Test-Path -LiteralPath $manifest)) { throw "Missing setup manifest: $manifest" }
if (-not (Test-Path -LiteralPath $icon)) { throw "Missing icon: $icon" }

if (Test-Path -LiteralPath $distRoot) { Remove-Item -LiteralPath $distRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null
New-Item -ItemType Directory -Force -Path $portableDir | Out-Null

# Icons are committed; regenerate when Pillow is available.
$python = Get-Command python -ErrorAction SilentlyContinue
if ($python) {
    Write-Host 'Generating icons'
    & python (Join-Path $root 'tools\generate_icons.py')
} else {
    Write-Host 'Python not found; using committed icons'
}

Write-Host 'Compiling application'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }

$common = @((Join-Path $root 'installer\SetupAssemblyInfo.cs'), (Join-Path $root 'installer\SetupCommon.cs'))
$uninstaller = Join-Path $buildDir 'Uninstall.exe'
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 "/win32icon:$icon" "/win32manifest:$manifest" "/out:$uninstaller" @common (Join-Path $root 'installer\Uninstaller.cs') @refs
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed' }

$setup = Join-Path $installerDir ('ScreenCurtain-Setup-' + $version + '.exe')
$setupArgs = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001',
    "/win32icon:$icon", "/win32manifest:$manifest", "/out:$setup",
    "/resource:$(Join-Path $buildDir 'ScreenCurtain.exe'),Payload.ScreenCurtain.exe",
    "/resource:$(Join-Path $buildDir 'ScreenCurtain.exe.config'),Payload.ScreenCurtain.exe.config",
    "/resource:$uninstaller,Payload.Uninstall.exe",
    "/resource:$(Join-Path $root 'README.md'),Payload.README.md"
)
& $csc @setupArgs @common (Join-Path $root 'installer\ScreenCurtainSetup.cs') @refs
if ($LASTEXITCODE -ne 0) { throw 'Setup build failed' }

function Invoke-Exe($file, $arguments, $timeoutMs) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $file
    $psi.Arguments = $arguments
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    if (-not $p.WaitForExit($timeoutMs)) {
        try { $p.Kill() } catch { }
        throw "Timed out: $file $arguments"
    }
    if ($p.ExitCode -ne 0) { throw "Failed ($($p.ExitCode)): $file $arguments" }
}

Write-Host 'Running non-destructive tests'
Invoke-Exe (Join-Path $buildDir 'ScreenCurtain.exe') '--self-test' 15000
Invoke-Exe (Join-Path $buildDir 'ScreenCurtain.exe') '--test-policy' 15000
Invoke-Exe (Join-Path $buildDir 'ScreenCurtain.exe') '--test-shutdown-guard' 15000

Write-Host 'Building portable package'
if (Test-Path -LiteralPath $portableStage) { Remove-Item -LiteralPath $portableStage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $portableStage | Out-Null
Copy-Item -LiteralPath (Join-Path $buildDir 'ScreenCurtain.exe') -Destination $portableStage
Copy-Item -LiteralPath (Join-Path $buildDir 'ScreenCurtain.exe.config') -Destination $portableStage
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $portableStage
Set-Content -LiteralPath (Join-Path $portableStage 'portable.mode') -Value 'ScreenCurtain portable mode' -Encoding ASCII
New-Item -ItemType Directory -Force -Path (Join-Path $portableStage 'data') | Out-Null

$portableZip = Join-Path $portableDir ('ScreenCurtain-' + $version + '-portable.zip')
if (Test-Path -LiteralPath $portableZip) { Remove-Item -LiteralPath $portableZip -Force }
Compress-Archive -LiteralPath $portableStage -DestinationPath $portableZip -CompressionLevel Optimal

Write-Host 'Testing portable ZIP in an isolated folder'
$portableTest = Join-Path $root 'artifacts\portable-zip-test'
if (Test-Path -LiteralPath $portableTest) { Remove-Item -LiteralPath $portableTest -Recurse -Force }
Expand-Archive -LiteralPath $portableZip -DestinationPath $portableTest -Force
$portableExe = Join-Path $portableTest ('ScreenCurtain-' + $version + '-portable\ScreenCurtain.exe')
Invoke-Exe $portableExe '--self-test' 15000
if (-not (Test-Path -LiteralPath (Join-Path $portableTest ('ScreenCurtain-' + $version + '-portable\data')))) { throw 'Portable data directory was not created' }
if (Test-Path -LiteralPath $portableStage) { Remove-Item -LiteralPath $portableStage -Recurse -Force }

Write-Host 'Testing installer payload extraction'
$testDir = Join-Path $root 'artifacts\release-install-check'
if (Test-Path -LiteralPath $testDir) { Remove-Item -LiteralPath $testDir -Recurse -Force }
Invoke-Exe $setup ("/SILENT /DIR=`"$testDir`" /NOARP /NOSHORTCUTS /NORUN") 20000
$builtHash = (Get-FileHash -LiteralPath (Join-Path $buildDir 'ScreenCurtain.exe') -Algorithm SHA256).Hash
$installedHash = (Get-FileHash -LiteralPath (Join-Path $testDir 'ScreenCurtain.exe') -Algorithm SHA256).Hash
if ($builtHash -ne $installedHash) { throw 'Installed payload hash mismatch' }
Invoke-Exe (Join-Path $testDir 'Uninstall.exe') '/SILENT' 15000
Start-Sleep -Seconds 3
if (Test-Path -LiteralPath $testDir) { throw 'Test uninstall did not remove the install directory' }

Write-Host 'Writing release notes and checksums'
$notes = @"
# ScreenCurtain $version

## Highlights

- New complete Windows 11 UI: first-run wizard, dashboard, settings, tray, exit/restore dialogs.
- Fixed update-guard handshake deadlock after UAC: helper speaks HELLO first, client replies OK, then policies are applied.
- Borderless full-screen privacy layer covers the taskbar and notification area; tray icon is not hidden.
- Laptops with a battery default to overlay-only blackout; a separate opt-in "laptop physical off" setting is available and defaults off.
- Laptop backlight-only mode: external panels use DDC/CI luminance (VCP 0x10) set to 0; internal panels use WMI WmiSetBrightness(0) when available. The monitors remain powered, so their USB hubs and keyboard/mouse stay alive. Original luminance is restored on wake. No VCP 0xD6 power-mode command is sent.
- Complete Windows 11 UI with Chinese/English localization. Auto follows the system language; a manual language selector is in Settings > General and takes effect after restart.
- Setup and uninstaller are now bilingual too. They auto-select the system language; `/LANG=EN` forces English and `/LANG=ZH` forces Chinese.
- `Preview 3 seconds` now matches the real blackout: same DDC/WMI backlight dimming, audio mute, borderless black layers, DWM square corners and hidden cursor; all are restored after the preview.
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

- installer/ScreenCurtain-Setup-$version.exe
- portable/ScreenCurtain-$version-portable.zip
"@
Set-Content -LiteralPath (Join-Path $distRoot 'RELEASE-NOTES.md') -Value $notes -Encoding UTF8

$files = Get-ChildItem -LiteralPath $distRoot -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName
$lines = @()
foreach ($file in $files) {
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $relative = $file.FullName.Substring($distRoot.Length + 1).Replace('\', '/')
    $lines += ($hash + '  ' + $relative)
}
Set-Content -LiteralPath (Join-Path $distRoot 'SHA256SUMS.txt') -Value $lines -Encoding ASCII

Write-Host ''
Write-Host 'Release artifacts:'
Get-ChildItem -LiteralPath $distRoot -Recurse -File | Select-Object FullName, Length | Format-Table -AutoSize
Write-Host 'Done.'

