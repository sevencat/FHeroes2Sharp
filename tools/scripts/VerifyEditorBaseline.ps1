<#
.SYNOPSIS
    Guards the map editor (E1) against pixel regressions.

.DESCRIPTION
    Opens one map file directly in the editor and compares the 640x480 frame with a baseline in
    tests/baselines, pixel by pixel. Data lookup, build, baseline validation and the compare
    reporting are shared with the other baseline scripts through PixelBaseline.ps1; the same skip /
    -RequireData policy applies (ADR-010).

    The frame is made reproducible with
      --scene editor      the editor, without the menu behind it
      --freeze-animation  no animation clock, so the view cannot drift between frames
      --cursor 320,440    a tile in the middle of the frame, so the highlighted tile and the hover
                          text of the status bar are part of the baseline
      --editor-paint ...  one scripted brush stroke, so the baseline proves that a painted tile
                          reaches the frame (the view reads tiles per frame, it caches none)
    and the view starts centred on the middle of the map, which is what EditorScene.TryCreate does
    when there is no player to centre on.

    The status bar is excluded (0,469,640,11): it prints the viewport offset and the build version,
    both of which move between builds.

    The editor paints the ground and object layers through AdventureMapView, so this script starts
    checking those layers for the editor as well: it renders whatever the client draws. The toolbar,
    the tile grid and the highlighted tile are the editor's own drawing and are covered here for the
    first time.

    The editor has no translated text yet (its labels are the English literals of the toolbar), so
    -Culture only changes what the game data lookup resolves and the file name keeps a single
    baseline: editor.png for every culture.

.EXAMPLE
    pwsh tools/scripts/VerifyEditorBaseline.ps1

.EXAMPLE
    pwsh tools/scripts/VerifyEditorBaseline.ps1 -Update    # after an intentional visual change

.EXAMPLE
    pwsh tools/scripts/VerifyEditorBaseline.ps1 -RequireData -DataDirectory 'D:\HoMM2'
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $DataDirectory = '',

    [Parameter(Mandatory = $false)]
    [string] $Baseline = '',

    [Parameter(Mandatory = $false)]
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/baseline'),

    # Map file the frame is built from; empty means the first installed map called Alteris.mp2.
    [Parameter(Mandatory = $false)]
    [string] $MapFile = '',

    [Parameter(Mandatory = $false)]
    [string] $Culture = 'en',

    # Logical pixel for the synthetic cursor: a tile in the middle of the frame, below the toolbar.
    [Parameter(Mandatory = $false)]
    [string] $Cursor = '320,240',

    # Scripted brush stroke, '<x>,<y>,<width>,<height>,<ground>', applied before the frame is taken.
    # A screenshot cannot press a button, and without a painted tile the baseline would not notice if
    # the map view stopped reading the tiles (it draws them per frame, it does not cache them).
    [Parameter(Mandatory = $false)]
    [string] $Paint = '46,52,6,4,Snow',

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
    [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../tests/baselines/editor.png'))
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

    Write-Host 'skip   editor baseline (no game data found; pass -DataDirectory or set FHEROES2_DATA)'
    exit 0
}

if (-not $MapFile) {
    # The same map the adventure map baseline uses: 108x108 with water, coast, grass, dirt and sand
    # in one frame, so the grid and the highlighted tile are checked over several distinct grounds.
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

        Write-Host 'skip   editor baseline (Alteris.mp2 is not installed; pass -MapFile)'
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

$screenshot = Join-Path $WorkDirectory 'editor.png'

Write-Host ("render editor      {0}" -f $MapFile)

$appArguments = @(
    '--data', $data,
    '--lang', $Culture,
    '--scene', 'editor',
    '--cursor', $Cursor,
    '--map', $MapFile,
    '--freeze-animation',
    '--no-audio',
    '--no-config',
    '--screenshot-frame', '30',
    '--smoke-frames', $Frames,
    '--screenshot', $screenshot
)

if ($Paint) {
    $appArguments += @('--editor-paint', $Paint)
}

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
    Subject       = 'editor'
    Baseline      = $Baseline
    DiffDirectory = $WorkDirectory
    Tolerance     = $Tolerance
}

exit (Complete-PixelCompare @verdictArguments)
