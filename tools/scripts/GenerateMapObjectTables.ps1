<#
.SYNOPSIS
    Generates src/FHeroes2.Game/Maps/MapObjectCatalog.Generated.cs from the fheroes2 reference.

.DESCRIPTION
    The object tables of the reference (src/fheroes2/maps/map_object_info.cpp) are between 20 and 1345
    lines of data per group: every line adds one part or patches one field of a local Maps::ObjectInfo.
    Retyping them by hand is not an option and hand-checking them is worse.

    This script extracts the body of every requested populate*() function and translates it statement
    by statement, keeping the order and the constants:

      Maps::ObjectInfo object{ MP2::OBJ_X };                             -> ObjectInfo object = new(Mp2ObjectType.X);
      object.groundLevelParts.emplace_back( ICN, i, Point{ x, y }, .. ); -> object.AddGroundPart(..);
      object.topLevelParts.emplace_back( .. );                           -> object.AddTopPart(..);
      object.groundLevelParts.back().animationFrames = n;                -> object.GroundLevelParts[^1].AnimationFrames = n;
      object.metadata[i] = n;                                           -> object.Metadata[i] = n;
      objects.emplace_back( std::move( object ) );                       -> objects.Add(object);
      objects.emplace_back( object );                                    -> objects.Add(object.Copy());
      objects[i] = objects[j];                                           -> objects[i] = objects[j].Copy();
      for ( const MP2::ObjectIcnType type : { A, B } ) {                 -> foreach (Mp2ObjectIcnType type in new[] { .. })
      for ( const auto & [type, offset] : { make_pair( A, 1U ) } ) {     -> foreach (var (type, offset) in new[] { (A, 1U) })
      for ( uint32_t c = 0; c < n; ++c ) {                               -> for (uint c = 0; c < n; ++c)

    Enum names are mapped by numeric value, not by spelling: the C++ and the C# enumerations are
    parsed, paired on their value and the C++ name is looked up in that pair table. A value that only
    exists on one side stops the script, so a table can never name a type the port does not have. The
    same holds for Resource::, paired with Mp2ResourceType.

    Integer types are not copied: the reference's uint8_t locals and counters become uint, because the
    sprite index they end up in is a word, and a part argument computed from a signed local gets the
    cast C++ does implicitly. What the C# enumerations cannot express -- a lambda, an enumeration the
    port does not have -- is hand-written next to the generated file and named in its header.

    A statement the script does not recognise also stops it. The generated file is checked in;
    regeneration is only needed when the reference checkout moves.

.EXAMPLE
    pwsh tools/scripts/GenerateMapObjectTables.ps1
#>
param(
    [string] $Fheroes2Source = 'F:\code\fheroes2\src\fheroes2\maps\map_object_info.cpp',
    [string] $Mp2Header = 'F:\code\fheroes2\src\fheroes2\maps\mp2.h',
    [string] $IcnEnumFile = (Join-Path $PSScriptRoot '../../src/FHeroes2.Assets/Maps/Mp2ObjectIcnType.cs'),
    [string] $TypeEnumFile = (Join-Path $PSScriptRoot '../../src/FHeroes2.Assets/Maps/Mp2ObjectType.cs'),
    [string] $ResourceHeader = 'F:\code\fheroes2\src\fheroes2\resource\resource.h',
    [string] $ResourceEnumFile = (Join-Path $PSScriptRoot '../../src/FHeroes2.Assets/Maps/Mp2ObjectData.cs'),
    [string] $Output = (Join-Path $PSScriptRoot '../../src/FHeroes2.Game/Maps/MapObjectCatalog.Generated.cs'),

    [string[]] $Functions = @(
        'populateLandscapeMountains',
        'populateLandscapeRocks',
        'populateLandscapeWater',
        'populateLandscapeMiscellaneous',
        'populateLandscapeTownBasements',
        'populateLandscapeFlags',
        'populateAdventureDwellings',
        'populateAdventureMines',
        'populateAdventurePowerUps',
        'populateAdventureTreasures',
        'populateAdventureWater',
        'populateAdventureMiscellaneous',
        'populateKingdomHeroes',
        'populateExtraBoatDirections'
    )
)

$ErrorActionPreference = 'Stop'

function Get-EnumValues {
    param([string] $Path, [string] $StartPattern, [switch] $Cpp)

    # Without a start pattern a C# file is assumed to hold one enumeration from its first line, which
    # is how the two generated enum files look; a C++ file always needs the pattern to find the enum.
    $inside = (-not $StartPattern)
    $next = 0
    $values = @{}

    foreach ($raw in (Get-Content -LiteralPath $Path)) {
        $line = ($raw -replace '//.*$', '').Trim()

        if (-not $inside) {
            if ($line -match $StartPattern) { $inside = $true }
            continue
        }

        if ($line -match '^\}') { break }

        if ($line -notmatch '^([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(.+?))?,?$') { continue }

        $name = $Matches[1]
        $expression = $Matches[2]

        if ($Cpp -and -not $expression) {
            $value = $next
        }
        elseif ($expression) {
            $value = 0
            foreach ($term in ($expression -split '\s*[+|]\s*')) {
                $term = $term.Trim()
                $value += if ($term -match '^0x([0-9A-Fa-f]+)$') { [Convert]::ToInt32($Matches[1], 16) }
                elseif ($term -match '^\d+$') { [int] $term }
                else { [int] $values[$term] }
            }
        }
        else {
            continue
        }

        $values[$name] = $value
        $next = $value + 1
    }

    return $values
}

function New-PairTable {
    param([hashtable] $CppValues, [hashtable] $CsValues)

    $candidates = @{}
    foreach ($name in $CsValues.Keys) {
        $value = $CsValues[$name]
        if (-not $candidates.ContainsKey($value)) { $candidates[$value] = @() }
        $candidates[$value] += $name
    }

    $byValue = @{}
    foreach ($value in $candidates.Keys) {
        # '_' is the throw-away alias the C# enumerations keep for the "no object" slot; prefer a real name.
        $sorted = @($candidates[$value] | Sort-Object { if ($_ -eq '_') { 1 } else { 0 } })
        $byValue[$value] = $sorted[0]
    }

    $pairs = @{}
    foreach ($name in $CppValues.Keys) {
        $value = $CppValues[$name]
        if (-not $byValue.ContainsKey($value)) {
            throw "Value $value of '$name' has no counterpart in the C# enumeration."
        }
        $pairs[$name] = $byValue[$value]
    }

    return $pairs
}

$script:IcnPairs = New-PairTable `
    -CppValues (Get-EnumValues -Path $Mp2Header -StartPattern 'enum (class )?ObjectIcnType' -Cpp) `
    -CsValues (Get-EnumValues -Path $IcnEnumFile)

$script:ObjPairs = New-PairTable `
    -CppValues (Get-EnumValues -Path $Mp2Header -StartPattern 'enum (class )?MapObjectType' -Cpp) `
    -CsValues (Get-EnumValues -Path $TypeEnumFile)

$script:ResourcePairs = New-PairTable `
    -CppValues (Get-EnumValues -Path $ResourceHeader -StartPattern '^namespace Resource$' -Cpp) `
    -CsValues (Get-EnumValues -Path $ResourceEnumFile -StartPattern 'enum Mp2ResourceType')

function Convert-Tokens {
    param([string] $Text)

    $result = $Text
    $result = $result -replace 'Maps::ObjectInfo', 'ObjectInfo'
    $result = $result -replace 'std::move\(\s*([A-Za-z_]\w*)\s*\)', '$1'

    $result = [regex]::Replace($result, 'MP2::OBJ_ICN_TYPE_([A-Z0-9_]+)', {
            param($m)
            $key = 'OBJ_ICN_TYPE_' + $m.Groups[1].Value
            if (-not $script:IcnPairs.ContainsKey($key)) { throw "Unknown ICN type $key." }
            'Mp2ObjectIcnType.' + $script:IcnPairs[$key]
        })

    $result = [regex]::Replace($result, 'MP2::OBJ_([A-Z0-9_]+)', {
            param($m)
            $key = 'OBJ_' + $m.Groups[1].Value
            if (-not $script:ObjPairs.ContainsKey($key)) { throw "Unknown object type $key." }
            'Mp2ObjectType.' + $script:ObjPairs[$key]
        })

    $result = [regex]::Replace($result, 'Resource::([A-Z0-9_]+)', {
            param($m)
            $key = $m.Groups[1].Value
            if (-not $script:ResourcePairs.ContainsKey($key)) { throw "Unknown resource $key." }
            'Mp2ResourceType.' + $script:ResourcePairs[$key]
        })

    $result = [regex]::Replace($result, 'std::make_pair\(\s*(.+?)\s*,\s*(.+?)\s*\)', '($1, $2)')

    $result = $result -replace 'Maps::OBJECT_LAYER', 'ObjectLayerType.Object'
    $result = $result -replace 'Maps::BACKGROUND_LAYER', 'ObjectLayerType.Background'
    $result = $result -replace 'Maps::SHADOW_LAYER', 'ObjectLayerType.Shadow'
    $result = $result -replace 'Maps::TERRAIN_LAYER', 'ObjectLayerType.Terrain'
    $result = $result -replace 'const uint32_t', 'uint'
    $result = $result -replace 'const uint8_t', 'byte'
    $result = $result -replace 'const int32_t', 'int'
    $result = $result -replace 'const bool', 'bool'
    $result = $result -replace '\bsize_t\b', 'int'
    $result = $result -replace '\buint32_t\b', 'uint'
    $result = $result -replace '\buint8_t\b', 'byte'
    $result = $result -replace '\bint32_t\b', 'int'

    $result = [regex]::Replace($result, 'fheroes2::Point\{\s*(-?\d+)\s*,\s*(-?\d+)\s*\}', 'new Point($1, $2)')

    # 'object' is a C# keyword: the local Maps::ObjectInfo the reference fills is called objectInfo here.
    # Case sensitive: ObjectLayerType.Object must keep its member name.
    $result = $result -creplace '\bobject\b', 'objectInfo'

    return $result
}

# The declared type of every local of the function being translated. C++ converts any integer to the
# uint32_t the part constructors take; C# does not, so an argument that names a signed local has to be
# cast explicitly and the only way to know that is to remember the declarations.
$script:LocalTypes = @{}

function Register-Local {
    param([string] $Name, [string] $Type)
    if ($Name) { $script:LocalTypes[$Name] = $Type }
}

function Join-Items {
    param([string] $Text, [string] $Separator)

    $items = $Text -split $Separator
    return (($items | ForEach-Object { $_.Trim() }) -join ', ')
}

function Split-Arguments {
    param([string] $Text)

    $parts = [System.Collections.Generic.List[string]]::new()
    $depth = 0
    $current = ''

    foreach ($character in $Text.ToCharArray()) {
        if ($character -eq '(' -or $character -eq '{') { $depth++; $current += $character }
        elseif ($character -eq ')' -or $character -eq '}') { $depth--; $current += $character }
        elseif ($character -eq ',' -and $depth -eq 0) { $parts.Add($current.Trim()); $current = '' }
        else { $current += $character }
    }

    if ($current.Trim() -ne '') { $parts.Add($current.Trim()) }

    return $parts
}

# Wraps the sprite index of a part — the second argument — in a cast when it is computed from a signed
# local, because the port keeps the index as a word.
function Convert-IndexArguments {
    param([string] $Arguments)

    $parts = Split-Arguments -Text $Arguments
    if ($parts.Count -lt 2) { return $Arguments }

    foreach ($identifier in [regex]::Matches($parts[1], '[A-Za-z_]\w*')) {
        if ($script:LocalTypes[$identifier.Value] -eq 'int') {
            $parts[1] = '(uint)(' + $parts[1] + ')'
            return ($parts -join ', ')
        }
    }

    return $Arguments
}

function Convert-Statement {
    param([string] $Statement)

    # A push back of the local object without a move is a copy in the reference: the mines push the
    # object, then pop the last part and push it again with another resource. Converting this before
    # the token pass is what keeps the difference between the two lines visible.
    if ($Statement -match '^objects\.(?:emplace_back|push_back)\( (\w+) \);$') {
        $copied = Convert-Tokens -Text $Matches[1]
        return @(('objects.Add({0}.Copy());' -f $copied))
    }

    $t = (Convert-Tokens -Text $Statement).Trim()

    if ($t -match '^assert\(') { return @() }

    if ($t -match '^ObjectInfo (\w+)\{ (Mp2ObjectType\.\w+) \};$') {
        return @(('ObjectInfo {0} = new({1});' -f $Matches[1], $Matches[2]))
    }

    if ($t -match '^objects\.(?:emplace_back|push_back)\( (\w+) \);$') {
        return @(('objects.Add({0});' -f $Matches[1]))
    }

    if ($t -match '^objects\.(?:emplace_back|push_back)\( (Mp2ObjectType\.\w+) \);$') {
        return @(('objects.Add(new ObjectInfo({0}));' -f $Matches[1]))
    }

    if ($t -match '^objects\.resize\( (\d+), ObjectInfo\{ (Mp2ObjectType\.\w+) \} \);$') {
        return @(('for (int i = 0; i < {0}; ++i) {{ objects.Add(new ObjectInfo({1})); }}' -f $Matches[1], $Matches[2]))
    }

    if ($t -match '^(\w+)\.groundLevelParts\.emplace_back\( (.*) \);$') {
        return @(('{0}.AddGroundPart({1});' -f $Matches[1], (Convert-IndexArguments -Arguments $Matches[2])))
    }

    if ($t -match '^(\w+)\.topLevelParts\.emplace_back\( (.*) \);$') {
        return @(('{0}.AddTopPart({1});' -f $Matches[1], (Convert-IndexArguments -Arguments $Matches[2])))
    }

    if ($t -match '^(\w+)\.groundLevelParts\.back\(\)\.animationFrames = (\d+);$') {
        return @(('{0}.GroundLevelParts[^1].AnimationFrames = {1};' -f $Matches[1], $Matches[2]))
    }

    if ($t -match '^(\w+)\.topLevelParts\.back\(\)\.animationFrames = (\d+);$') {
        return @(('{0}.TopLevelParts[^1].AnimationFrames = {1};' -f $Matches[1], $Matches[2]))
    }

    if ($t -match '^(\w+)\.metadata\[(\d+)\] = (.*);$') {
        $owner = $Matches[1]
        $slot = $Matches[2]
        $value = $Matches[3]
        # The reference stores the resource itself; the port keeps metadata as a word.
        if ($value -match '^Mp2ResourceType\.' -or $script:LocalTypes[$value] -eq 'Mp2ResourceType') { $value = '(uint)' + $value }
        elseif ($script:LocalTypes[$value] -eq 'int') { $value = '(uint)' + $value }
        return @(('{0}.Metadata[{1}] = {2};' -f $owner, $slot, $value))
    }

    if ($t -match '^objects\[(.+?)\] = objects\[(.+?)\];$') {
        return @(('objects[{0}] = objects[{1}].Copy();' -f $Matches[1], $Matches[2]))
    }

    if ($t -match '^(\w+)\.groundLevelParts\.pop_back\(\);$') {
        return @(('{0}.GroundLevelParts.RemoveAt({0}.GroundLevelParts.Count - 1);' -f $Matches[1]))
    }

    if ($t -match '^for \( const MP2::ObjectIcnType (\w+) : \{(.*)\} \) \{$') {
        $name = $Matches[1]
        $items = Join-Items -Text $Matches[2] -Separator '\s*,\s*'
        Register-Local -Name $name -Type 'Mp2ObjectIcnType'
        return @(('foreach (Mp2ObjectIcnType {0} in new[] {{ {1} }})' -f $name, $items), '{')
    }

    # Ranges over an initialiser list, 'for ( <type> name : { .. } )': the element type is the one
    # every item has, which is the only thing the reference's 'auto' and 'uint32_t' do not tell us.
    if ($t -match '^for \( (?:const )?(?:auto|uint|int|byte) (\w+) : \{(.*)\} \) \{$') {
        $name = $Matches[1]
        $list = $Matches[2]
        $items = Join-Items -Text $list -Separator '\s*,\s*'
        $type = if ($list -match 'Mp2ObjectIcnType\.') { 'Mp2ObjectIcnType' }
        elseif ($list -match 'Mp2ResourceType\.') { 'Mp2ResourceType' }
        else { throw "Cannot infer the element type of: $t" }
        Register-Local -Name $name -Type $type
        return @(('foreach ({0} {1} in new[] {{ {2} }})' -f $type, $name, $items), '{')
    }

    # 'const auto & [type, offset] : { std::make_pair( .. ), .. }' — a list of pairs.
    if ($t -match '^for \( const auto & \[(\w+), (\w+)\] : \{(.*)\} \) \{$') {
        $first = $Matches[1]
        $second = $Matches[2]
        $items = Join-Items -Text $Matches[3] -Separator '(?<=\))\s*,\s*(?=\()'
        Register-Local -Name $first -Type 'Mp2ObjectIcnType'
        Register-Local -Name $second -Type 'uint'
        return @(('foreach (var ({0}, {1}) in new[] {{ {2} }})' -f $first, $second, $items), '{')
    }

    if ($t -match '^for \( (uint|int|byte) (\w+) = (.*?); \2 < (.*?); \+\+\2 \) \{$') {
        $type = $Matches[1]
        $name = $Matches[2]
        # The reference's uint8_t counters feed an ICN index, which is a word here: widening them
        # keeps every 'index + n' expression an expression of that word instead of an int.
        if ($type -eq 'byte') { $type = 'uint' }
        Register-Local -Name $name -Type $type
        return @(('for ({0} {1} = {2}; {1} < {3}; ++{1})' -f $type, $name, $Matches[3], $Matches[4]), '{')
    }

    if ($t -match '^for \( (.*) \{$') {
        throw "Unsupported for loop: $t"
    }

    if ($t -match '^(if|switch) \( (.*) \) \{$') {
        return @(('{0} ({1})' -f $Matches[1], $Matches[2]), '{')
    }

    if ($t -match '^else \{$') { return @('else', '{') }
    if ($t -eq '{') { return @('{') }
    if ($t -eq '}') { return @('}') }
    if ($t -match '^(case .+:|default:)$') { return @($t) }
    if ($t -match '^(break|continue);$') { return @($t) }
    if ($t -match '^\+\+\w+;$') { return @($t) }

    if ($t -match '^(uint|int|bool) (\w+) = (.*);$') {
        $type = $Matches[1]
        $name = $Matches[2]
        $value = $Matches[3]
        Register-Local -Name $name -Type $type
        # C# widens a mix of int and uint to long where C++ keeps the operands an unsigned int, so an
        # initialiser that picks between int literals has to be brought back to the declared type.
        if ($type -eq 'uint' -and $value -match '\?') {
            return @(('uint {0} = (uint)({1});' -f $name, $value))
        }
        return @($t)
    }

    if ($t -match '^byte (\w+) = (.*);$') {
        $name = $Matches[1]
        $value = $Matches[2]
        Register-Local -Name $name -Type 'uint'
        if ($value -match '^\d+$') { return @(('uint {0} = {1};' -f $name, $value)) }
        return @(('uint {0} = (uint)({1});' -f $name, $value))
    }

    if ($t -match '^([A-Za-z_]\w*)( = | \+= | -= | \*= )(.*);$') {
        return @($t)
    }

    throw "Unsupported statement: $t"
}

function Get-FunctionBody {
    param([string[]] $Lines, [string] $Name)

    $start = -1
    for ($i = 0; $i -lt $Lines.Count; ++$i) {
        if ($Lines[$i] -match ('^\s*void ' + [regex]::Escape($Name) + '\(')) { $start = $i; break }
    }

    if ($start -lt 0) { throw "Function $Name not found." }

    $depth = 0
    $started = $false
    $body = [System.Collections.Generic.List[string]]::new()

    for ($i = $start; $i -lt $Lines.Count; ++$i) {
        $clean = $Lines[$i] -replace '//.*$', ''
        $depth += ($clean.Length - $clean.Replace('{', '').Length) - ($clean.Length - $clean.Replace('}', '').Length)

        if (-not $started) {
            if ($depth -gt 0) { $started = $true }
            continue
        }

        if ($depth -le 0) { break }

        $body.Add($Lines[$i])
    }

    return $body
}

$sourceLines = Get-Content -LiteralPath $Fheroes2Source
$result = [System.Collections.Generic.List[string]]::new()

foreach ($function in $Functions) {
    $methodName = 'Populate' + $function.Substring('populate'.Length)
    $result.Add('')
    $result.Add('    /// <summary><c>' + $function + '</c> of the reference, one line per reference line.</summary>')
    $result.Add('    /// <param name="objects">Table of the group, must be empty.</param>')
    $result.Add('    private static void ' + $methodName + '(List<ObjectInfo> objects)')
    $result.Add('    {')

    $level = 0
    $statement = ''
    $paren = 0
    $script:LocalTypes = @{}
    $caseLevel = [System.Collections.Generic.List[hashtable]]::new()

    foreach ($raw in (Get-FunctionBody -Lines $sourceLines -Name $function)) {
        $line = $raw.Trim()

        if ($line -eq '') { continue }

        if ($line.StartsWith('//')) {
            if ($statement -eq '') { $result.Add((' ' * (8 + 4 * $level)) + $line) }
            continue
        }

        $statement = if ($statement -eq '') { $line } else { $statement + ' ' + $line }
        $paren = ($statement.Length - $statement.Replace('(', '').Length) - ($statement.Length - $statement.Replace(')', '').Length)

        $tail = $statement.Substring($statement.Length - 1)
        if ($paren -gt 0 -or ($tail -ne ';' -and $tail -ne '{' -and $tail -ne '}' -and $tail -ne ':')) { continue }

        # Kept an array even for a single line: a bare string would answer [0] with its first letter.
        $translated = @(Convert-Statement -Statement $statement)
        $statement = ''
        $paren = 0

        if ($translated.Count -eq 0) { continue }

        if ($translated[0] -eq '}') {
            # The label of a switch case is a level of its own here, so leaving the case has to undo it
            # before the brace of the switch itself is written.
            if ($caseLevel.Count -gt 0 -and $caseLevel[-1].Opened -and $level - 1 -eq $caseLevel[-1].Level) {
                $level--
                $caseLevel[-1].Opened = $false
                $caseLevel.RemoveAt($caseLevel.Count - 1)
            }

            $level = [Math]::Max(0, $level - 1)
            $result.Add((' ' * (8 + 4 * $level)) + '}')
        }
        elseif ($translated[0] -eq '{') {
            $result.Add((' ' * (8 + 4 * $level)) + '{')
            $level++
        }
        elseif ($translated.Count -eq 2 -and $translated[1] -eq '{') {
            $result.Add((' ' * (8 + 4 * $level)) + $translated[0])
            $result.Add((' ' * (8 + 4 * $level)) + '{')
            $level++

            if ($translated[0] -match '^switch \(') {
                $caseLevel.Add(@{ Level = $level; Opened = $false })
            }
        }
        elseif ($translated[0] -match '^(case .+:|default:)$') {
            if ($caseLevel.Count -gt 0 -and $caseLevel[-1].Opened) {
                $level--
                $caseLevel[-1].Opened = $false
            }

            $result.Add((' ' * (8 + 4 * $level)) + $translated[0])
            $level++

            if ($caseLevel.Count -gt 0) { $caseLevel[-1].Opened = $true }
        }
        else {
            foreach ($translatedLine in $translated) {
                $result.Add((' ' * (8 + 4 * $level)) + $translatedLine)
            }
        }
    }

    if ($statement -ne '') { throw "Trailing statement in $function`: $statement" }

    $result.Add('    }')
}

$file = [System.Collections.Generic.List[string]]::new()
$file.Add('using FHeroes2.Assets.Maps;')
$file.Add('using FHeroes2.Core.Geometry;')
$file.Add('')
$file.Add('namespace FHeroes2.Game.Maps;')
$file.Add('')
$file.Add('/// <summary>')
$file.Add('/// The object tables of the reference, apart from the ones that are hand-written: roads, streams')
$file.Add('/// and trees (<c>MapObjectCatalog.Roads.cs</c>, <c>MapObjectCatalog.Landscape.cs</c>), kingdom')
$file.Add('/// towns (<c>MapObjectCatalog.Kingdom.cs</c>), monsters (<c>MapObjectCatalog.Monsters.cs</c>) and')
$file.Add('/// artifacts (<c>MapObjectCatalog.Artifacts.cs</c>).')
$file.Add('///')
$file.Add('/// <para>')
$file.Add('/// Generated by <c>tools/scripts/GenerateMapObjectTables.ps1</c> and must not be edited by hand:')
$file.Add('/// one C# line per line of <c>src/fheroes2/maps/map_object_info.cpp</c>, in the same order, and')
$file.Add('/// the order is part of the map format. Re-run the script only when the reference checkout moves,')
$file.Add('/// then run <c>MapObjectCatalogTests</c>, which pins the size of every table.')
$file.Add('/// </para>')
$file.Add('/// </summary>')
$file.Add('public static partial class MapObjectCatalog')
$file.Add('{')

foreach ($line in $result) { $file.Add($line) }

$file.Add('}')

Set-Content -LiteralPath $Output -Value $file -Encoding utf8

Write-Output "Wrote $($file.Count) lines to '$Output'."
