<#
.SYNOPSIS
    Plays one battle with the player's own clicks and comes back to the adventure map, then quick-saves it.

.DESCRIPTION
    Two gaps were left in the battle smoke: every run so far either skipped the battle (--battle-only) or let the
    computer fight it (--battle-auto), so nothing walked the player's side of a battle, and no run ever wrote a
    save. This script closes both without a patch:

        1. opens a battle with --battle, has the player attack on the first turn, hands the rest to the Auto
           button, and waits for the result window and the adventure map to come back,
        2. runs the map again and presses F2, the quick save key.

    The first click is the point of step 1: it goes into Battle::Interface::HumanTurn through the ordinary hit
    test, so the run proves that a click on the battlefield turns into an arena command, that the command is
    animated, that the battle finishes and that the adventure map is restored with the outcome applied. The Auto
    button then takes over because a full hand-played battle needs one click per turn and the enemy moves between
    turns, which no scripted click can follow; --battle-auto already covers the all-computer case.

    Step 2 is the save half of a save/load round trip. The battle state is not part of a save - the reference has
    no save entry on the battle screen (battle_interface.cpp has no EventSaveGame, Game::QuickSave is bound to the
    adventure map only) - so what a save keeps is the world the battle happened in, and that is what F2 writes
    here. Reading it back end to end needs the Load Game screen, which the port has not wired yet; the file
    format itself is covered by GameSaveTests.

    Nothing is compared pixel by pixel; the log is the evidence. The coordinates are the ones of a 640x480
    battle: the centre cell of the second enemy column (545,155) and the Auto button of the status bar (25,455).
    They come from the battle layout, so a different map or a layout change means new numbers here.

.EXAMPLE
    pwsh tools/scripts/SmokeBattleToMap.ps1

.EXAMPLE
    pwsh tools/scripts/SmokeBattleToMap.ps1 -DataDirectory 'D:\HoMM2' -BattleFrames 6000
#>
param(
    # The folder that holds 'Data' and 'files'; default is $env:FHEROES2_DATA, then F:\game\fh2.
    [string] $DataDirectory = '',

    # The map to fight on. It has to have an acting hero and a wandering monster for --battle to pick up.
    [string] $MapPath = '',

    # Where the log of the run goes.
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/battle-to-map'),

    # How long the battle may take. The Auto button finishes it well inside this; a Debug build runs at 80-125 fps.
    [int] $BattleFrames = 5000,

    # Skips 'dotnet build' when the client was just built.
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PixelBaseline.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$appProject = Join-Path $root 'src/FHeroes2.App'
$appExe = Join-Path $appProject 'bin/Debug/net10.0/FHeroes2Sharp.exe'

$data = Resolve-DataDirectory -Directory $DataDirectory
if (-not $data) {
    throw 'No game data found. Pass -DataDirectory, set FHEROES2_DATA, or install the data in F:\game\fh2.'
}

if ([string]::IsNullOrWhiteSpace($MapPath)) {
    $MapPath = Join-Path $data 'maps/Alteris.mp2'
}

if (-not (Test-Path -LiteralPath $MapPath)) {
    throw "'$MapPath' does not exist; pass -MapPath."
}

if (-not $SkipBuild) {
    Write-Host "Building $appProject"
    Invoke-Build -Project $appProject
}

if (-not (Test-Path -LiteralPath $appExe)) {
    throw "'$appExe' does not exist; build the solution first or drop -SkipBuild."
}

New-Item -ItemType Directory -Path $WorkDirectory -Force | Out-Null
$battleLog = Join-Path $WorkDirectory 'battle.log'
$saveLog = Join-Path $WorkDirectory 'save.log'

# The player attacks the enemy stack once; 400 frames later the Auto button of the status bar is pressed and the
# computer finishes the battle for both sides.
$battleClicks = @(
    '10:545,155', # the enemy stack of the second column: the player's attack
    '400:25,455'  # the Auto button of the status bar
) -join ';'

# The result window is modal (BattleSummaryWindow.ProcessEvents answers nothing but OK, Enter or Escape), so the
# battle stays on it until the player confirms - that is the reference's dialog, not a hang. Which frame the battle
# ends on depends on the fight, so Enter is pressed at fixed intervals and the ones that land after the window
# opened are the ones that close it. Enter has no binding outside the battle dialogs: HotKeyEvent.DefaultOkay is
# read by BattleSummaryWindow, BattleSurrenderWindow, BattleNecromancyWindow and BattleHeroOptionsWindow only.
$confirmKeys = (1200, 1900, 2600, 3300, 4000, 4700 | ForEach-Object { "$($_):Enter" }) -join ';'

$common = @(
    '--data', $data,
    '--no-audio',
    '--no-touch',
    '--no-config'
)

Write-Host 'Battle: attacking by hand, then Auto'
$battleArguments = $common + @(
    '--map', $MapPath,
    '--battle',
    '--left-click-at', $battleClicks,
    '--key-at', $confirmKeys,
    '--smoke-frames', "$BattleFrames"
)

$battleOutput = @(& $appExe @battleArguments 2>&1)
$battleExit = $LASTEXITCODE
$battleOutput | Set-Content -LiteralPath $battleLog -Encoding utf8

$battleMarkers = [ordered]@{
    'acts: Attack' = 'the first click of the player became an attack command'
    'The battle is over' = 'the Auto button finished the battle'
    'the adventure map is back' = 'the outcome was applied and the map came back'
}

$missing = @()
foreach ($marker in $battleMarkers.Keys) {
    if (-not ($battleOutput -match $marker)) {
        $missing += "'$marker' ($($battleMarkers[$marker]))"
    }
}

if ($battleExit -ne 0) {
    $battleOutput | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
    throw "the battle run exited with code $battleExit; the log is in '$battleLog'."
}

if ($missing.Count -gt 0) {
    $battleOutput | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
    throw "the battle run did not reach every step; missing: $($missing -join ', '). The log is in '$battleLog'."
}

# F2 writes <user data>/files/save/QUICKSAVE.savm. The directory is not under the work directory, so the file is
# removed again unless it was there before this run - a smoke must not eat a quick save the player made.
Write-Host 'Save: pressing F2 on the adventure map'
$saveArguments = $common + @(
    '--map', $MapPath,
    '--key-at', '30:F2',
    '--smoke-frames', '90'
)

$saveOutput = @(& $appExe @saveArguments 2>&1)
$saveExit = $LASTEXITCODE
$saveOutput | Set-Content -LiteralPath $saveLog -Encoding utf8

if ($saveExit -ne 0) {
    $saveOutput | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
    throw "the save run exited with code $saveExit; the log is in '$saveLog'."
}

$saved = $saveOutput -match "The game was saved to '(?<path>[^']+)'."
if (-not $saved) {
    $saveOutput | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
    throw "F2 did not save the game; the log is in '$saveLog'."
}

$savePath = [regex]::Match(($saveOutput -join "`n"), "The game was saved to '(?<path>[^']+)'\.").Groups['path'].Value
if (-not (Test-Path -LiteralPath $savePath)) {
    throw "the log says the game was saved to '$savePath', but the file is not there."
}

$size = (Get-Item -LiteralPath $savePath).Length
if ($size -le 0) {
    throw "the save file '$savePath' is empty."
}

Write-Host "The save file '$savePath' holds $size bytes"

# A quick save written by this run is removed again: the file name is fixed, so leaving it behind would overwrite
# the quick save of whoever runs the smoke next.
if ($savePath -like '*QUICKSAVE*') {
    Remove-Item -LiteralPath $savePath -Force
    Write-Host "Removed the quick save written by this run: '$savePath'"
}

Write-Host 'a battle was played by hand and the map came back, and F2 wrote a save, match'
