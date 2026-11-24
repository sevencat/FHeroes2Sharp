<#
.SYNOPSIS
    Walks the first run end to end with scripted clicks.

.DESCRIPTION
    The onboarding of a first run is click-only: the language picker of fheroes2::selectLanguage(), the
    greeting of Game::MainMenu(), then the resolution chooser. Until --left-click-at there was no way to
    replay more than one click in a run, so the only smoke of this path needed a temporary patch that
    jumped straight past the first two windows (bd 归档（PROGRESS.md，FHeroes2Sharp-jwk） §6.9). This script is that smoke without the
    patch:

        1. writes a throwaway fheroes2.cfg that still has 'first time game run' on,
        2. runs the client with four clicks (language OKAY, greeting OKAY, a row of the chooser, OKAY),
        3. checks the log for the markers of every step.

    The click on the chooser's row is the interesting one: the chooser reads LocalEvent instead of the
    scene's SetLeftButton API, so the host has to push the click into the event state (ADR-209).

    Nothing is compared pixel by pixel: the point is the click path, not the frame. The log of the run
    stays in the work directory for inspection.

.EXAMPLE
    pwsh tools/scripts/SmokeFirstRun.ps1

.EXAMPLE
    pwsh tools/scripts/SmokeFirstRun.ps1 -DataDirectory 'D:\HoMM2' -KeepRunning
#>
param(
    # The folder that holds 'Data' and 'files'; default is $env:FHEROES2_DATA, then F:\game\fh2.
    [string] $DataDirectory = '',

    # Where the throwaway configuration and the log of the run go.
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/first-run'),

    # Skips 'dotnet build' when the client was just built.
    [switch] $SkipBuild,

    # Leaves the rewritten configuration in place instead of restoring the first-run one.
    [switch] $KeepRunning
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PixelBaseline.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$appProject = Join-Path $root 'src/FHeroes2.App'
$appExe = Join-Path $appProject 'bin/Debug/net10.0/FHeroes2Sharp.exe'

$data = Resolve-DataDirectory -Directory $DataDirectory
if (-not $data) {
    throw "No game data found. Pass -DataDirectory, set FHEROES2_DATA, or install the data in F:\game\fh2."
}

if (-not $SkipBuild) {
    Write-Host "Building $appProject"
    Invoke-Build -Project $appProject
}

if (-not (Test-Path -LiteralPath $appExe)) {
    throw "'$appExe' does not exist; build the solution first or drop -SkipBuild."
}

New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
$configPath = Join-Path $WorkDirectory 'fheroes2.cfg'
$logPath = Join-Path $WorkDirectory 'first-run.log'

# The flag is what makes the onboarding run; the language is pinned so the first selection is 'en' of
# the installed languages, which keeps the picker frame (and therefore the OKAY button) language
# independent of the machine's locale.
$config = @(
    'lang = en',
    'first time game run = on'
) -join [Environment]::NewLine
Set-Content -LiteralPath $configPath -Value $config -Encoding utf8

# One click per window, all in the same run. The points are the centres of the buttons and of the
# first row of the chooser; they come from the dialog layouts (the picker content is 345x306 centred
# on 640x480, the chooser 345x306 as well), so a layout change means new numbers here.
$clicks = @(
    '10:236,380', # language picker OKAY
    '20:320,322', # greeting OKAY
    '30:300,114', # a row of the resolution chooser
    '40:236,356'  # resolution chooser OKAY
) -join ';'

Write-Host "Running the first run with clicks $clicks"

$arguments = @(
    '--data', $data,
    '--config', $configPath,
    '--no-audio',
    # A touch screen adds a paragraph to the greeting, which moves the OKAY button the clicks below hit.
    '--no-touch',
    '--smoke-frames', '60',
    '--left-click-at', $clicks
)

$output = @(& $appExe @arguments 2>&1)
$exit = $LASTEXITCODE
$output | Set-Content -LiteralPath $logPath -Encoding utf8

$expected = [ordered]@{
    'First run: language picker opened' = 'the onboarding started'
    "First run: language 'en' -> 'en'." = 'the language OKAY was clicked'
    'First run: greeting shown.' = 'the greeting followed the picker'
    'First run: window resolution' = 'a row and the OKAY of the chooser were clicked'
    'First run completed' = 'the first run ended and the flag was cleared'
}

$missing = @()
foreach ($marker in $expected.Keys) {
    if (-not ($output -match [regex]::Escape($marker))) {
        $missing += "'$marker' ($($expected[$marker]))"
    }
}

if ($exit -ne 0) {
    $output | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
    throw "the client exited with code $exit; the log is in '$logPath'."
}

if ($missing.Count -gt 0) {
    $output | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
    throw "the first run did not reach every step; missing: $($missing -join ', '). The log is in '$logPath'."
}

if (-not $KeepRunning) {
    Set-Content -LiteralPath $configPath -Value $config -Encoding utf8
}

Write-Host 'the first run walked from the language picker to the cleared flag, match'
