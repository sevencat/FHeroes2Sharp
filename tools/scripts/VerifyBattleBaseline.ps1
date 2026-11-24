<#
.SYNOPSIS
    Guards the battle screen (P3) against pixel regressions.

.DESCRIPTION
    Opens Alteris.mp2 on the adventure map, starts a fight with --battle (the focus hero against the
    first wandering monster) and compares the 640x480 frame with a baseline in tests/baselines.
    Data lookup, build, baseline validation and the compare reporting are shared with the other
    baseline scripts through PixelBaseline.ps1; the same skip / -RequireData policy applies (ADR-010).

    The frame is made reproducible with
      --seed 1             the hero's starting army is rolled on load (bd 归档（PROGRESS.md，FHeroes2Sharp-jwk） §7.5)
      --freeze-animation   the scene does not step the fight, so the frame is the deployment
      --cursor 620,100     no pointer anywhere
    Without --freeze-animation the scene plays one round per frame and the frame would depend on how
    many frames the client rendered before the screenshot.

    -Siege fights the first town of another colour instead (--battle-siege) and uses battle-siege.png:
    the same ground plus the castle ground, the moat, the walls, the towers and the catapult.

.EXAMPLE
    pwsh tools/scripts/VerifyBattleBaseline.ps1

.EXAMPLE
    pwsh tools/scripts/VerifyBattleBaseline.ps1 -Siege

.EXAMPLE
    pwsh tools/scripts/VerifyBattleBaseline.ps1 -Update    # after an intentional visual change

.EXAMPLE
    pwsh tools/scripts/VerifyBattleBaseline.ps1 -RequireData -DataDirectory 'D:\HoMM2'
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $DataDirectory = '',

    # Empty means "the baseline that belongs to the mode": battle.png for a field fight, battle-siege.png
    # for a fight at a town.
    [Parameter(Mandatory = $false)]
    [string] $Baseline = '',

    [Parameter(Mandatory = $false)]
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/baseline'),

    # Map file the frame is built from, an absolute path or a file name below the map collection;
    # empty means the first installed map called Alteris.mp2.
    [Parameter(Mandatory = $false)]
    [string] $MapFile = '',

    [Parameter(Mandatory = $false)]
    [string] $Cursor = '620,100',

    [Parameter(Mandatory = $false)]
    [int] $Frames = 30,

    # Accepted difference in pixels; 0 means the render has to be byte for byte identical.
    [Parameter(Mandatory = $false)]
    [int] $Tolerance = 0,

    # Replaces the committed baseline with the frame that was just rendered.
    [switch] $Update,

    # Fights the first town of another colour (--battle-siege) instead of a wandering monster.
    [switch] $Siege,

    # Fails instead of skipping when the game data or the map is missing.
    [switch] $RequireData,

    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PixelBaseline.ps1')

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$appProject = Join-Path $root 'src/FHeroes2.App'
$probeProject = Join-Path $root 'tools/FHeroes2.AssetProbe'
$appExe = Join-Path $appProject 'bin/Debug/net10.0/FHeroes2Sharp.exe'
$probeExe = Join-Path $probeProject 'bin/Debug/net10.0/FHeroes2.AssetProbe.exe'

$Baseline = if ($Baseline) {
    [System.IO.Path]::GetFullPath($Baseline)
}
else {
    $name = if ($Siege) { 'battle-siege.png' } else { 'battle.png' }
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

    Write-Host 'skip   battle baseline (no game data found; pass -DataDirectory or set FHEROES2_DATA)'
    exit 0
}

if (-not $MapFile) {
    $MapFile = 'Alteris.mp2'
}

if (-not (Test-Path -LiteralPath $MapFile)) {
    $mapCandidates = @(
        (Join-Path $data "maps/$MapFile"),
        (Join-Path $data "Maps/$MapFile")
    )

    $MapFile = $mapCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $MapFile) {
        if ($RequireData) {
            Write-Host (("error  '{0}' was not found below '{1}'; pass -MapFile.") -f $MapFile, $data)
            exit 1
        }

        Write-Host (("skip   battle baseline ({0} is not installed; pass -MapFile)") -f $MapFile)
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

$label = if ($Siege) { 'battle-siege' } else { 'battle' }
$screenshot = Join-Path $WorkDirectory "$label.png"

Write-Host ("render {0,-13} {1}" -f $label, $MapFile)

$battleFlag = if ($Siege) { '--battle-siege' } else { '--battle' }

$appArguments = @(
    '--data', $data,
    '--lang', 'en',
    '--cursor', $Cursor,
    '--map', $MapFile,
    '--freeze-animation',
    '--no-audio',
    '--no-config',
    '--seed', '1',
    '--screenshot-frame', '3',
    '--smoke-frames', $Frames,
    '--screenshot', $screenshot,
    $battleFlag
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
    Ignore          = @()
}

$compare = Invoke-PixelCompare @compareArguments

Write-Host ("verify {0}" -f $Baseline)
foreach ($line in $compare.Lines) {
    Write-Host "       $line"
}

$verdictArguments = @{
    Compare       = $compare
    Subject       = 'battle'
    Baseline      = $Baseline
    DiffDirectory = $WorkDirectory
    Tolerance     = $Tolerance
}

exit (Complete-PixelCompare @verdictArguments)
