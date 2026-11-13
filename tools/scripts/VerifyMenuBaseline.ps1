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

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$clientProject = Join-Path $root 'src/FHeroes2.Client.Desktop'
$probeProject = Join-Path $root 'tools/FHeroes2.AssetProbe'
# The client assembly is called FHeroes2Sharp (see AssemblyName in its csproj).
$clientExe = Join-Path $clientProject 'bin/Debug/net10.0/FHeroes2Sharp.exe'
$probeExe = Join-Path $probeProject 'bin/Debug/net10.0/FHeroes2.AssetProbe.exe'

function Resolve-DataDirectory {
    param([string] $Directory)

    $candidates = @()
    if ($Directory) {
        $candidates += $Directory
    }
    elseif ($env:FHEROES2_DATA) {
        $candidates += $env:FHEROES2_DATA
    }

    # Same development fallback as FHeroes2.Assets.AssetLocator.
    $candidates += 'F:\game\fh2'

    foreach ($candidate in $candidates) {
        # The client wants the folder that contains 'Data' and 'files', which is what the
        # environment variable and the fallback point at.
        if (Test-Path -LiteralPath (Join-Path $candidate 'Data\Heroes2.agg')) {
            return (Get-Item -LiteralPath $candidate).FullName
        }

        # A caller may pass the 'Data' folder itself.
        if (Test-Path -LiteralPath (Join-Path $candidate 'Heroes2.agg')) {
            return (Split-Path -Parent (Get-Item -LiteralPath $candidate).FullName)
        }
    }

    return $null
}

function Invoke-Build {
    param([string] $Project)

    $output = & dotnet build $Project --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | Select-Object -Last 15 | ForEach-Object { Write-Host "        $_" }
        throw "dotnet build '$Project' failed with exit code $LASTEXITCODE."
    }
}

# PNG stores its integers big endian. -shl on a [byte] operand stays inside the byte width
# (0x02 -shl 8 evaluates to 0), so every operand is widened before the shift.
function Read-BigEndianInt32 {
    param([byte[]] $Bytes, [int] $Offset)

    $value = 0
    for ($index = 0; $index -lt 4; $index++) {
        $value = ($value -shl 8) -bor [int]$Bytes[$Offset + $index]
    }

    return $value
}

# The baseline is the artefact under test, so it is checked even when no game data exists: a
# deleted or wrongly sized baseline has to fail on a machine that can only run the cheap part
# (that is exactly what a hosted CI runner does).
function Assert-PngBaseline {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Host ("error  baseline '{0}' does not exist; run with -Update to create it." -f $Path)
        exit 1
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24) {
        Write-Host ("error  baseline '{0}' is not a PNG file ({1} bytes)." -f $Path, $bytes.Length)
        exit 1
    }

    $signature = -join ($bytes[0..7] | ForEach-Object { $_.ToString('X2') })
    $chunk = [System.Text.Encoding]::ASCII.GetString($bytes, 12, 4)
    if ($signature -ne '89504E470D0A1A0A' -or $chunk -ne 'IHDR') {
        Write-Host ("error  baseline '{0}' is not a PNG file." -f $Path)
        exit 1
    }

    # The client renders on a fixed 640x480 logical surface; a baseline of another size can only
    # be a mistake (for example a 2x screenshot).
    $width = Read-BigEndianInt32 -Bytes $bytes -Offset 16
    $height = Read-BigEndianInt32 -Bytes $bytes -Offset 20
    if ($width -ne 640 -or $height -ne 480) {
        Write-Host ("error  baseline '{0}' is {1}x{2}; the logical surface is 640x480." -f $Path, $width, $height)
        exit 1
    }
}

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
    Invoke-Build $clientProject
    Invoke-Build $probeProject
}

foreach ($executable in @($clientExe, $probeExe)) {
    if (-not (Test-Path -LiteralPath $executable)) {
        throw "'$executable' does not exist. Run without -SkipBuild first."
    }
}

New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null

$screenshot = Join-Path $WorkDirectory 'menu.png'
if (Test-Path -LiteralPath $screenshot) {
    Remove-Item -LiteralPath $screenshot
}

Write-Host "render menu        $data"

$clientArguments = @(
    '--data', $data,
    '--scene', 'menu',
    '--lang', $Culture,
    '--cursor', $Cursor,
    '--freeze-animation',
    '--screenshot-frame', '30',
    '--smoke-frames', $Frames,
    '--screenshot', $screenshot
)

$clientOutput = @(& $clientExe @clientArguments 2>&1)
$clientExit = $LASTEXITCODE

if ($Update) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Baseline) | Out-Null
    Copy-Item -LiteralPath $screenshot -Destination $Baseline -Force

    Write-Host ("update {0}" -f $Baseline)
    Write-Host ("       sha256 {0}" -f (Get-FileHash -LiteralPath $Baseline -Algorithm SHA256).Hash)
    exit 0
}

$compareArguments = @('compare', $screenshot, $Baseline, '--diff', (Join-Path $WorkDirectory 'diff'))
foreach ($area in $Ignore) {
    $compareArguments += @('--ignore', $area)
}

$compareOutput = @(& $probeExe @compareArguments 2>&1)
$compareExit = $LASTEXITCODE
$compareText = $compareOutput -join [Environment]::NewLine

Write-Host ("verify {0}" -f $Baseline)
foreach ($line in $compareOutput) {
    Write-Host "       $line"
}

if ($compareExit -eq 0) {
    Write-Host 'match  the rendered menu is identical to the baseline'
    exit 0
}

if ($compareText -match '(\d+) of (\d+) pixels differ') {
    $differing = [int]$Matches[1]
    $total = [int]$Matches[2]

    if ($differing -le $Tolerance) {
        Write-Host ("match  {0} of {1} pixels differ, tolerance is {2}" -f $differing, $total, $Tolerance)
        exit 0
    }

    Write-Host ("DIFF   {0} of {1} pixels differ, tolerance is {2}" -f $differing, $total, $Tolerance)
}
else {
    Write-Host 'DIFF   the frames have different sizes (see the probe output above)'
}

Write-Host ("       diff image: {0}" -f (Join-Path $WorkDirectory 'diff/diff_menu.png'))
Write-Host '       If the change is intended, run with -Update and note it in tests/baselines/README.md.'
exit 1
