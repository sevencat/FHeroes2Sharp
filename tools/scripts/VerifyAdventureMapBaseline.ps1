<#
.SYNOPSIS
    Guards the adventure map (M4) against pixel regressions.

.DESCRIPTION
    Opens one map file directly on the adventure map and compares the 640x480 frame with a baseline
    in tests/baselines, pixel by pixel. Data lookup, build, baseline validation and the compare
    reporting are shared with the other baseline scripts through PixelBaseline.ps1; the same skip /
    -RequireData policy applies (ADR-010).

    The map is opened with --map instead of going through NEW GAME: the baseline then depends on one
    committed map file only and not on the order of the installed map collection. The frame is made
    reproducible with
      --freeze-animation  no animation clock, so the view cannot drift between frames
      --cursor 620,100    no hover state anywhere
    and the view is centred on the castle closest to the middle of the map, which is what
    AdventureMapScene.TryCreate does when there is no hero to centre on yet.

    The status bar is excluded (0,469,640,11): it prints the viewport offset and the build version,
    both of which move between builds.

    M4 draws the ground layer only. When the object layer lands, the same script starts checking it
    for free: it renders whatever the client draws.

    The interface text is translated but the frame contains none of it (the map name comes from the
    map file), so a translation can only change the baseline through the status bar, which is
    ignored. -Culture is kept for symmetry with the other scripts and does pick the file name:
    'en' is adventure-map.png, anything else adventure-map.<culture>.png.

.EXAMPLE
    pwsh tools/scripts/VerifyAdventureMapBaseline.ps1

.EXAMPLE
    pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -Update    # after an intentional visual change

.EXAMPLE
    pwsh tools/scripts/VerifyAdventureMapBaseline.ps1 -RequireData -DataDirectory 'D:\HoMM2'
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $DataDirectory = '',

    # Empty means "the baseline that belongs to -Culture": adventure-map.png for 'en', otherwise
    # adventure-map.<culture>.png next to it.
    [Parameter(Mandatory = $false)]
    [string] $Baseline = '',

    [Parameter(Mandatory = $false)]
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/baseline'),

    # Map file the frame is built from; empty means the first installed map called Alteris.mp2.
    [Parameter(Mandatory = $false)]
    [string] $MapFile = '',

    [Parameter(Mandatory = $false)]
    [string] $Culture = 'en',

    # Logical pixel for the synthetic cursor: outside every interactive area, no hover anywhere.
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

    # Fails instead of skipping when the game data or the map is missing.
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
    $name = if ($Culture -eq 'en') { 'adventure-map.png' } else { "adventure-map.$Culture.png" }
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

    Write-Host 'skip   adventure map baseline (no game data found; pass -DataDirectory or set FHEROES2_DATA)'
    exit 0
}

if (-not $MapFile) {
    # The map collection lives in a folder whose name depends on the installation; the maps
    # themselves do not. Alteris ships with the original game, is 108x108 and has water, coast,
    # grass, dirt and sand in one frame, which makes it a good regression subject.
    $mapCandidates = @(
        (Join-Path $data 'maps/Alteris.mp2'),
        (Join-Path $data 'Maps/Alteris.mp2')
    )

    $MapFile = $mapCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $MapFile) {
        if ($RequireData) {
            Write-Host ("error  'Alteris.mp2' was not found below '{0}'; pass -MapFile." -f $data)
            exit 1
        }

        Write-Host 'skip   adventure map baseline (Alteris.mp2 is not installed; pass -MapFile)'
        exit 0
    }
}

$MapFile = (Get-Item -LiteralPath $MapFile).FullName

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

$screenshot = Join-Path $WorkDirectory "adventure-map-$Culture.png"

Write-Host ("render adventure   {0}" -f $MapFile)

$appArguments = @(
    '--data', $data,
    '--lang', $Culture,
    '--cursor', $Cursor,
    '--map', $MapFile,
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
    Subject       = 'adventure map'
    Baseline      = $Baseline
    DiffDirectory = $WorkDirectory
    Tolerance     = $Tolerance
}

exit (Complete-PixelCompare @verdictArguments)
