$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $root 'build'
$srcDir = Join-Path $root 'src'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$outExe = Join-Path $outDir 'ScreenCurtain.exe'
$manifest = Join-Path $srcDir 'App.manifest'
$config = Join-Path $srcDir 'app.config'
$icon = Join-Path $srcDir 'App.ico'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "Cannot find in-box C# compiler: $csc"
}
if (-not (Test-Path -LiteralPath $manifest)) {
    throw "Cannot find application manifest: $manifest"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = Get-ChildItem -LiteralPath $srcDir -Recurse -Filter *.cs |
    Sort-Object FullName |
    ForEach-Object { $_.FullName }

if ($sources.Count -eq 0) {
    throw "No .cs source files found under src"
}

$refs = @(
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Management.dll',
    '/r:System.Xml.dll',
    '/r:System.Configuration.dll'
)

$compilerArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/warn:4',
    '/codepage:65001',
    "/out:$outExe",
    "/win32manifest:$manifest",
    "/resource:$(Join-Path $srcDir 'Resources\tray-active.ico'),ScreenCurtain.Resources.TrayActive.ico",
    "/resource:$(Join-Path $srcDir 'Resources\tray-degraded.ico'),ScreenCurtain.Resources.TrayDegraded.ico",
    "/resource:$(Join-Path $srcDir 'Resources\tray-off.ico'),ScreenCurtain.Resources.TrayOff.ico"
) + $refs + $sources

if (Test-Path -LiteralPath $icon) {
    $compilerArgs = @("/win32icon:$icon") + $compilerArgs
}

Write-Host "Compiling $($sources.Count) source files..."
& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath $config -Destination ($outExe + '.config') -Force

$readme = Join-Path $root 'README.md'
if (Test-Path -LiteralPath $readme) {
    Copy-Item -LiteralPath $readme -Destination (Join-Path $outDir 'README.md') -Force
}

Write-Host "Built: $outExe"
Get-Item -LiteralPath $outExe | Select-Object FullName, Length, LastWriteTime | Format-List
