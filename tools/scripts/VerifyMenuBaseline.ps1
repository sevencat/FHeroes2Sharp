<#
.SYNOPSIS
    Guards the main menu against pixel regressions.

.DESCRIPTION
    Renders the main menu with a frozen lantern glow and compares the 640x480 frame with
    tests/baselines/menu.png, pixel by pixel. The baseline lives in the repository; the game data
    does not, so the script skips with exit code 0 when no archive is found - the same policy as
    the data dependent xunit tests (ADR-010). Pass -RequireData on a machine that must have data
    (for example a self hosted runner) to turn that skip into a failure.

    Before the data lookup the committed baseline itself is validated (PNG signature and 640x480
    size), so a deleted, truncated or wrongly sized baseline fails even where the render has to
    be skipped; that is the part a hosted CI runner can always enforce.

    Two things make the frame reproducible:
      --freeze-animation  the lantern glow stays on its first frame instead of following the clock
      --cursor            a synthetic cursor is used, so the physical mouse cannot hover a sign
    The status bar is excluded (--ignore 0,469,640,11): it prints the live FPS counter and the
    version string, neither of which can be pinned.

    Data lookup, build, baseline validation and the compare reporting live in PixelBaseline.ps1,
    which VerifySettingsBaseline.ps1 shares.

.EXAMPLE
    pwsh tools/scripts/VerifyMenuBaseline.ps1

.EXAMPLE
    pwsh tools/scripts/VerifyMenuBaseline.ps1 -Update    # after an intentional visual change

.EXAMPLE
    pwsh tools/scripts/VerifyMenuBaseline.ps1 -RequireData -Tolerance 16 -DataDirectory 'D:\HoMM2'
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $DataDirectory = '',

    [Parameter(Mandatory = $false)]
    [string] $Baseline = (Join-Path $PSScriptRoot '../../tests/baselines/menu.png'),

    [Parameter(Mandatory = $false)]
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/baseline'),

    # Language of the interface; the status bar is ignored, so this only has to be stable.
    [Parameter(Mandatory = $false)]
    [string] $Culture = 'en',

    # Logical pixel for the synthetic cursor: empty background, no sign.
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

$Baseline = [System.IO.Path]::GetFullPath($Baseline)
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

    Write-Host 'skip   menu baseline (no game data found; pass -DataDirectory or set FHEROES2_DATA)'
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

$screenshot = Join-Path $WorkDirectory 'menu.png'

Write-Host "render menu        $data"

$appArguments = @(
    '--data', $data,
    '--scene', 'menu',
    '--lang', $Culture,
    '--cursor', $Cursor,
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
    Subject       = 'menu'
    Baseline      = $Baseline
    DiffDirectory = $WorkDirectory
    Tolerance     = $Tolerance
}

exit (Complete-PixelCompare @verdictArguments)
