<#
.SYNOPSIS
    Guards the settings dialog against pixel regressions.

.DESCRIPTION
    Renders the settings dialog of the main menu (the one the street door opens) and compares the
    640x480 frame with a baseline in tests/baselines, pixel by pixel. Data lookup, build, baseline
    validation and the compare reporting are shared with VerifyMenuBaseline.ps1 through
    PixelBaseline.ps1; the same skip / -RequireData policy applies (ADR-010).

    The dialog is modal, so a script cannot click the door - it uses --open-settings, the same
    hook that makes the window reachable in a smoke run. Two flags keep the frame reproducible:
      --freeze-animation  the lantern glow behind the dialog stays on its first frame
      --cursor 620,100    no hover state anywhere (the dialog sits at 138,181 348x133)
    The status bar is excluded for the same reason as in the menu baseline.

    The rendered text is interface text, so the language is part of the baseline: -Culture picks
    the language and, when -Baseline is left out, the file that belongs to it - the English frame
    is settings.png, every other language is settings.<culture>.png. Never overwrite one language
    with another; see tests/baselines/README.md.

.EXAMPLE
    pwsh tools/scripts/VerifySettingsBaseline.ps1

.EXAMPLE
    pwsh tools/scripts/VerifySettingsBaseline.ps1 -Culture zh_CN

.EXAMPLE
    pwsh tools/scripts/VerifySettingsBaseline.ps1 -Update    # after an intentional visual change

.EXAMPLE
    pwsh tools/scripts/VerifySettingsBaseline.ps1 -RequireData -DataDirectory 'D:\HoMM2'
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $DataDirectory = '',

    # Empty means "the baseline that belongs to -Culture": settings.png for 'en', otherwise
    # settings.<culture>.png next to it.
    [Parameter(Mandatory = $false)]
    [string] $Baseline = '',

    [Parameter(Mandatory = $false)]
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/baseline'),

    # Language of the interface; the dialog text is translated, so this is part of the baseline and,
    # unless -Baseline names a file, it also picks the baseline: 'en' is settings.png, anything else
    # is settings.<culture>.png.
    [Parameter(Mandatory = $false)]
    [string] $Culture = 'en',

    # Logical pixel for the synthetic cursor: empty background, no hover on the dialog.
    [Parameter(Mandatory = $false)]
    [string] $Cursor = '620,100',

    [Parameter(Mandatory = $false)]
    [int] $Frames = 90,

    # Areas the comparison skips, in logical pixels of the 640x480 frame.
    [Parameter(Mandatory = $false)]
    [string[]] $Ignore = @('0,469,640,11'),

    # Accepted difference in pixels; 0 means the render has to be byte for byte identical.
    [Parameter(Mandatory = $false)]
    [int] $Tolerance = 0,

    # Replaces the committed baseline with the frame that was just rendered.
    [switch] $Update,

    # Fails instead of skipping when the game data is missing.
    [switch] $RequireData,

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PixelBaseline.ps1')

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$appProject = Join-Path $root 'src/FHeroes2.App'
$probeProject = Join-Path $root 'tools/FHeroes2.AssetProbe'
# The client assembly is called FHeroes2Sharp (see AssemblyName in its csproj).
$appExe = Join-Path $appProject 'bin/Debug/net10.0/FHeroes2Sharp.exe'
$probeExe = Join-Path $probeProject 'bin/Debug/net10.0/FHeroes2.AssetProbe.exe'

$Baseline = if ($Baseline) {
    [System.IO.Path]::GetFullPath($Baseline)
}
else {
    $name = if ($Culture -eq 'en') { 'settings.png' } else { "settings.$Culture.png" }
    [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../tests/baselines/$name"))
}

$WorkDirectory = [System.IO.Path]::GetFullPath($WorkDirectory)

if (-not $Update) {
    Assert-PngBaseline $Baseline
}

$data = Resolve-DataDirectory $DataDirectory
if (-not $data) {
    if ($RequireData) {
        Write-Host 'error  Heroes2.agg was not found; pass -DataDirectory or set FHEROES2_DATA.'
        exit 1
    }

    Write-Host 'skip   settings baseline (no game data found; pass -DataDirectory or set FHEROES2_DATA)'
    exit 0
}

if (-not $SkipBuild) {
    Invoke-Build $appProject
    Invoke-Build $probeProject
}

foreach ($executable in @($appExe, $probeExe)) {
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "'$executable' does not exist. Run without -SkipBuild first."
    }
}

New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null

$screenshot = Join-Path $WorkDirectory "settings-$Culture.png"

Write-Host ("render settings    {0} ({1})" -f $data, $Culture)

$appArguments = @(
    '--data', $data,
    '--scene', 'menu',
    '--lang', $Culture,
    '--cursor', $Cursor,
    '--open-settings',
    '--freeze-animation',
    '--no-audio',
    '--screenshot-frame', '30',
    '--smoke-frames', $Frames,
    '--screenshot', $screenshot
)

Invoke-PixelRender -Executable $appExe -Arguments $appArguments -ScreenshotPath $screenshot

if ($Update) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Baseline) | Out-Null
    Copy-Item -LiteralPath $screenshot -Destination $Baseline -Force

    Write-Host ("update {0}" -f $Baseline)
    Write-Host ("       sha256 {0}" -f (Get-FileHash -LiteralPath $Baseline -Algorithm SHA256).Hash)
    exit 0
}

$compareArguments = @{
    ProbeExecutable = $probeExe
    Rendered        = $screenshot
    Baseline        = $Baseline
    DiffDirectory   = $WorkDirectory
    Ignore          = $Ignore
}

$compare = Invoke-PixelCompare @compareArguments

Write-Host ("verify {0}" -f $Baseline)
foreach ($line in $compare.Lines) {
    Write-Host "       $line"
}

$verdictArguments = @{
    Compare       = $compare
    Subject       = 'settings dialog'
    Baseline      = $Baseline
    DiffDirectory = $WorkDirectory
    Tolerance     = $Tolerance
}

exit (Complete-PixelCompare @verdictArguments)
