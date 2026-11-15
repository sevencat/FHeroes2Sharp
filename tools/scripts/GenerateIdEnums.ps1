<#
.SYNOPSIS
    Regenerates src/FHeroes2.Game/Monsters/MonsterType.cs and
    src/FHeroes2.Game/Resources/ArtifactType.cs.

.DESCRIPTION
    Both enumerations are 70 to 100 members of plain sequential ids whose *values* are part of the map
    and save format: monster ids go into MP2 metadata, artifact ids into the artifact table. Retyping
    them by hand risks an invisible off-by-one, so this script reads them from the reference and emits
    C# with the same order and the same values.

    Every source line must be recognised; an unknown line stops the script instead of being skipped, so
    a new member or an explicit value in the reference cannot silently change the numbering.

    The generated files are checked in: building the port does not need the reference checkout. Run the
    script after updating that checkout, then run IdEnumTests, which pins the values both enumerations
    are used with.

.EXAMPLE
    pwsh tools/scripts/GenerateIdEnums.ps1
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $MonsterHeader = 'F:\code\fheroes2\src\fheroes2\monster\monster.h',

    [Parameter(Mandatory = $false)]
    [string] $ArtifactHeader = 'F:\code\fheroes2\src\fheroes2\resource\artifact.h',

    [Parameter(Mandatory = $false)]
    [string] $MonsterOutput = (Join-Path $PSScriptRoot '../../src/FHeroes2.Game/Monsters/MonsterType.cs'),

    [Parameter(Mandatory = $false)]
    [string] $ArtifactOutput = (Join-Path $PSScriptRoot '../../src/FHeroes2.Game/Resources/ArtifactType.cs')
)

$ErrorActionPreference = 'Stop'

function Get-EnumBody {
    param([string[]] $Lines, [string] $Pattern, [string] $Source)

    $start = -1
    for ($i = 0; $i -lt $Lines.Count; ++$i) {
        if ($start -lt 0 -and $Lines[$i] -match $Pattern) {
            $start = $i
            break
        }
    }

    if ($start -lt 0) {
        throw "Could not locate the enumeration '$Pattern' in '$Source'."
    }

    # The opening brace follows the declaration, the closing '};' ends the body.
    $open = -1
    for ($i = $start; $i -lt $Lines.Count; ++$i) {
        if ($Lines[$i] -match '\{') {
            $open = $i
            break
        }
    }

    if ($open -lt 0) {
        throw "The enumeration '$Pattern' of '$Source' has no opening brace."
    }

    $body = New-Object 'System.Collections.Generic.List[string]'
    for ($i = $open + 1; $i -lt $Lines.Count; ++$i) {
        if ($Lines[$i] -match '^\s*\}\s*;') {
            return $body
        }

        $body.Add($Lines[$i])
    }

    throw "The enumeration '$Pattern' of '$Source' is not closed."
}

function Convert-MemberName {
    param([string] $Name)

    # SCREAMING_CASE to PascalCase: 'RANDOM_MONSTER_LEVEL_1' is RandomMonsterLevel1, 'UNUSED_84' is
    # Unused84. Digits stay as they are, so the numbers the reference spells out remain readable.
    $parts = $Name -split '_'
    $converted = foreach ($part in $parts) {
        if ($part -match '^\d+$') { $part }
        else { $part.Substring(0, 1) + $part.Substring(1).ToLowerInvariant() }
    }

    return ($converted -join '')
}

function Convert-EnumMembers {
    param([string[]] $Body, [string] $Enumeration)

    $members = New-Object 'System.Collections.Generic.List[string]'
    $value = 0
    $sawUnknown = $false

    foreach ($line in $Body) {
        $trimmed = $line.Trim()

        if ($trimmed.Length -eq 0) {
            $members.Add('')
            continue
        }

        if ($trimmed.StartsWith('//')) {
            $members.Add($trimmed)
            continue
        }

        if ($trimmed -notmatch '^([A-Z][A-Z0-9_]*)(?:\s*=\s*(\d+))?\s*,?$') {
            throw "Unsupported member of $Enumeration`: '$trimmed'."
        }

        $name = Convert-MemberName -Name $Matches[1]

        if ($Matches[2].Length -gt 0) {
            $value = [int]$Matches[2]
        }

        if ($name -eq 'Unknown') {
            $sawUnknown = $true
            $members.Add("    Unknown = $value,")
        }
        else {
            $members.Add("    $name,")
        }

        ++$value
    }

    if (-not $sawUnknown) {
        throw "$Enumeration must start with Unknown."
    }

    return $members
}

function Save-Enum {
    param([string] $Namespace, [string[]] $Header, [string[]] $Members, [string] $Output)

    $file = New-Object 'System.Collections.Generic.List[string]'
    $file.Add("namespace $Namespace;")
    $file.Add('')
    foreach ($line in $Header) {
        $file.Add($line)
    }

    $file.Add('{')
    foreach ($line in $Members) {
        $file.Add($line)
    }

    $file.Add('}')

    $directory = Split-Path -Parent $Output
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    Set-Content -LiteralPath $Output -Value $file -Encoding utf8NoBOM

    Write-Host "Wrote $Output"
}

$monsterLines = Get-Content -LiteralPath $MonsterHeader
$monsterBody = Get-EnumBody -Lines $monsterLines -Pattern '^\s*enum MonsterType\s*:' -Source $MonsterHeader
$monsterMembers = Convert-EnumMembers -Body $monsterBody -Enumeration 'MonsterType'

Save-Enum -Namespace 'FHeroes2.Game.Monsters' -Members $monsterMembers -Output $MonsterOutput -Header @(
    '/// <summary>'
    '/// <c>Monster::MonsterType</c> of <c>src/fheroes2/monster/monster.h</c>, in the same order and with'
    '/// the same values: the id is what MP2 metadata and stacks store, so it is part of the map format.'
    '/// </summary>'
    '/// <remarks>'
    '/// The reference nests the enumeration in <c>class Monster</c>; the port has no class of that name'
    '/// yet and <c>Monster</c> is going to be the class with the behavior, so the enumeration keeps the'
    '/// name the reference gave it — <c>MonsterType</c>.'
    '/// </remarks>'
    'public enum MonsterType : int'
)

$artifactLines = Get-Content -LiteralPath $ArtifactHeader
$artifactBody = Get-EnumBody -Lines $artifactLines -Pattern '^\s*enum\s*:\s*int32_t' -Source $ArtifactHeader
$artifactMembers = Convert-EnumMembers -Body $artifactBody -Enumeration 'ArtifactType'

Save-Enum -Namespace 'FHeroes2.Game.Resources' -Members $artifactMembers -Output $ArtifactOutput -Header @(
    '/// <summary>'
    '/// The artifact ids: the anonymous <c>enum : int32_t</c> of <c>class Artifact</c> in'
    '/// <c>src/fheroes2/resource/artifact.h</c>, in the same order and with the same values.'
    '/// </summary>'
    '/// <remarks>'
    '/// <para>'
    '/// The reference notes that every id is one bigger than in the original game, which is what lets a'
    '/// patch add artifacts in the middle and keeps the numbering aligned with the object types.'
    '/// </para>'
    '/// <para>'
    '/// The reference gives the enumeration no name; the port calls it <c>ArtifactType</c> because'
    '/// <c>Artifact</c> is going to be the class with the behavior, mirroring <c>MonsterType</c>.'
    '/// </para>'
    '/// </remarks>'
    'public enum ArtifactType : int'
)
