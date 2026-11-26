<#
.SYNOPSIS
    Plays one battle with the player's own clicks and comes back to the adventure map, then quick-saves it.

.DESCRIPTION
    Two gaps were left in the battle smoke: every run so far either skipped the battle (--battle-only) or let the
    computer fight it (--battle-auto), so nothing walked the player's side of a battle, and no run ever wrote a
    save. This script closes both without a patch:

        1. opens a battle with --battle, has the player attack on the first turn, hands the rest to the Auto
           button, and waits for the result window and the adventure map to come back,
        2. runs the map again and presses F2, the quick save key,
        3. starts the client without a map, clicks the LOAD GAME sign, and takes the quick save from the list,
        4. runs the map again and presses L instead: the confirmation box answered YES must open the save list
           directly, without a trip through the main menu.


        The first click is the point of step 1: it goes into Battle::Interface::HumanTurn through the ordinary hit
        test, so the run proves that a click on the battlefield turns into an arena command, that the command is
        animated, that the battle finishes and that the adventure map is restored with the outcome applied. The Auto
        button then takes over because a full hand-played battle needs one click per turn and the enemy moves between
        turns, which no scripted click can follow; --battle-auto already covers the all-computer case.

        Steps 2 and 3 are the two halves of a save/load round trip. The battle state is not part of a save - the
        reference has no save entry on the battle screen (battle_interface.cpp has no EventSaveGame,
        Game::QuickSave is bound to the adventure map only) - so what a save keeps is the world the battle happened
        in, and that is what F2 writes. Step 3 is the original's path back: the main menu sign (MainMenuScene), the
        file list of Dialog::SelectFileLoad (SelectFileDialog), and GameSave.Load. --load is the same code without
        the two windows, so this step is what proves the windows are wired; if it breaks, check --load first.

        Step 4 covers the map's own L key. Interface::AdventureMap::EventLoadGame is only the confirmation box
        (interface_events.cpp:352-359): answering YES returns GameMode::LOAD_GAME to StartGame(), and the main loop
        calls Game::LoadGame() right away (game_startgame.cpp:1166-1168, game_mainmenu.cpp:154-155) - no LOAD GAME
        sign in between. The port wires that through the scene's LoadGameEvent, so the run must leave the map and
        land in the file list on the same frame the box closes.

        Nothing is compared pixel by pixel; the log is the evidence. The battle coordinates are the ones of a
        640x480 battle: the centre cell of the second enemy column (545,155) and the Auto button of the status bar
        (25,455). The LOAD GAME sign is at (235,218), from the 'main menu ready' log line that lists the sign areas.
        They come from the layouts, so a different map or a layout change means new numbers here.


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
    'Fading the screen in over 6 frames' = 'the map faded back in after the battle, battle_interface.cpp:1534'
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

# F2 writes <user data>/files/save/QUICKSAVE.sav. The directory is not under the work directory, so the file is
# removed again at the end of this script - a smoke must not eat a quick save the player made.
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

# What the map looked like at the moment of the save: the focus hero and the tile it stands on, straight from the
# 'The game has started' line. Step 3 has to print the same tail word for word - that is the 'the hero did not
# move' half of the round trip, and it costs no new assertion code on the scene side.
$stateMatch = [regex]::Match(($saveOutput -join "`n"), 'in the kingdom(?<focus>[^.]*)\.')
if (-not $stateMatch.Success) {
    throw "the save run did not report the state of the map; the log is in '$saveLog'."
}

$saveState = $stateMatch.Groups['focus'].Value
if ($saveState -notmatch "focus hero") {
    throw "the save run reported no focus hero ('$saveState'), so there is no position to compare."
}

$mapName = [IO.Path]::GetFileNameWithoutExtension($MapPath)

# Step 3: the same save read back through the windows a player uses. No --map and no --load - the client starts on
# the main menu, the script clicks the LOAD GAME sign, and Enter confirms whatever row the list opens on. The list
# is sorted by time and QUICKSAVE was written a moment ago, so that row is the one on top; Enter pressed at a fixed
# frame can land before the window exists (the click is replayed first), so it is pressed twice.
Write-Host 'Load: clicking LOAD GAME and taking the quick save from the list'
$loadLog = Join-Path $WorkDirectory 'load.log'
$loadArguments = $common + @(
    '--left-click-at', '10:235,218',
    '--key-at', '40:Enter;70:Enter',
    '--smoke-frames', '150'
)

$loadOutput = @(& $appExe @loadArguments 2>&1)
$loadExit = $LASTEXITCODE
$loadOutput | Set-Content -LiteralPath $loadLog -Encoding utf8

$loadMarkers = [ordered]@{
    'opens the save file list' = 'the LOAD GAME sign reached GameHost.OpenLoadGame'
    "Adventure map opened: '$mapName'" = 'the save opened the same map'
    'reopened from a save' = 'the map behind the save was found and drawn again'
    "Loaded the game from '$savePath'" = 'the run read back the file it wrote'
    "in the kingdom$saveState." = 'the focus hero stands on the tile it had before the save'
    "Drawing the 'AdventureMapScene'." = 'the loaded world came up instead of the main menu'
}

$loadMissing = @()
foreach ($marker in $loadMarkers.Keys) {
    if (-not ($loadOutput -match [regex]::Escape($marker))) {
        $loadMissing += "'$marker' ($($loadMarkers[$marker]))"
    }
}

if ($loadExit -ne 0 -or $loadMissing.Count -gt 0) {
    $loadOutput | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
    if ($loadExit -ne 0) {
        throw "the load run exited with code $loadExit; the log is in '$loadLog'."
    }

    throw "the load run did not reach every step; missing: $($loadMissing -join ', '). The log is in '$loadLog'."
}

# Step 4: the map's own L key. L opens the confirmation box (EventLoadGame), Enter answers YES, and the file list
# has to open without the main menu's LOAD GAME sign: the host leaves the map and calls the same list the sign does.
# The second Enter takes the row the list opens on, which is the quick save written a moment ago.
Write-Host 'Load from the map: pressing L, confirming, and taking the quick save from the list'
$mapLoadLog = Join-Path $WorkDirectory 'map-load.log'
$mapLoadArguments = $common + @(
    '--map', $MapPath,
    '--key-at', '20:L;40:Enter;70:Enter',
    '--smoke-frames', '200'
)

$mapLoadOutput = @(& $appExe @mapLoadArguments 2>&1)
$mapLoadExit = $LASTEXITCODE
$mapLoadOutput | Set-Content -LiteralPath $mapLoadLog -Encoding utf8

$mapLoadMarkers = [ordered]@{
    "The adventure map asked for 'LoadGame', so the save file list is opened." = 'L answered YES and the host opened the list'
    'Leaving the adventure map' = 'the map was dropped first, as GameMode::LOAD_GAME does'
    'reopened from a save' = 'the list read the save back'
    "Loaded the game from '$savePath'" = 'the same quick save the F2 run wrote'
    'The saved game continues' = 'the loaded world resumed instead of starting a new day'
    "Drawing the 'AdventureMapScene'." = 'the map came back instead of the main menu'
}

$mapLoadMissing = @()
foreach ($marker in $mapLoadMarkers.Keys) {
    if (-not ($mapLoadOutput -match [regex]::Escape($marker))) {
        $mapLoadMissing += "'$marker' ($($mapLoadMarkers[$marker]))"
    }
}

if ($mapLoadExit -ne 0 -or $mapLoadMissing.Count -gt 0) {
    $mapLoadOutput | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
    if ($mapLoadExit -ne 0) {
        throw "the map load run exited with code $mapLoadExit; the log is in '$mapLoadLog'."
    }

    throw "the map load run did not reach every step; missing: $($mapLoadMissing -join ', '). The log is in '$mapLoadLog'."
}

# A quick save written by this run is removed again: the file name is fixed, so leaving it behind would overwrite
# the quick save of whoever runs the smoke next.
if ($savePath -like '*QUICKSAVE*') {
    Remove-Item -LiteralPath $savePath -Force
    Write-Host "Removed the quick save written by this run: '$savePath'"
}

Write-Host 'a battle was played by hand and the map came back, F2 wrote a save, the main menu and the map''s L both read it back, match'
