<#
.SYNOPSIS
    Regenerates the id enumerations and the id to file name tables of src/fheroes2/agg.

.DESCRIPTION
    Five things in the reference's agg directory are nothing but numbered names: ICN:: (914 real files
    plus a few engine made ones), M82::SoundType (332), XMI:: (21) and TIL:: (4), and the two string
    tables that map the first two to archive entry names (icn.cpp's icnMap, m82.cpp's m82map). Retyping
    them by hand risks an invisible off-by-one in a table nothing would catch, so this script reads them
    from the reference and emits C# with the same order and the same values.

    Every source line must be recognised; an unknown line stops the script instead of being skipped, so
    a new member or an explicit value in the reference cannot silently change the numbering. The name
    tables are cross checked against the enumerations: the enumerations are what the port refers to and
    the tables are what gets opened, so a drift between them is the bug this script exists to prevent.

    The generated files are checked in: building the port does not need the reference checkout. Run the
    script after updating that checkout, then run AggTableTests, which pins the sizes and a few names.

.EXAMPLE
    pwsh tools/scripts/GenerateAggEnums.ps1
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $IcnHeader = 'F:\code\fheroes2\src\fheroes2\agg\icn.h',

    [Parameter(Mandatory = $false)]
    [string] $IcnSource = 'F:\code\fheroes2\src\fheroes2\agg\icn.cpp',

    [Parameter(Mandatory = $false)]
    [string] $M82Header = 'F:\code\fheroes2\src\fheroes2\agg\m82.h',

    [Parameter(Mandatory = $false)]
    [string] $M82Source = 'F:\code\fheroes2\src\fheroes2\agg\m82.cpp',

    [Parameter(Mandatory = $false)]
    [string] $XmiHeader = 'F:\code\fheroes2\src\fheroes2\agg\xmi.h',

    [Parameter(Mandatory = $false)]
    [string] $XmiSource = 'F:\code\fheroes2\src\fheroes2\agg\xmi.cpp',

    [Parameter(Mandatory = $false)]
    [string] $TilHeader = 'F:\code\fheroes2\src\fheroes2\agg\til.h',

    [Parameter(Mandatory = $false)]
    [string] $AssetsDirectory = (Join-Path $PSScriptRoot '../../src/FHeroes2.Assets')
)

$ErrorActionPreference = 'Stop'

function Get-EnumBody {
    param([string] $Path, [string] $Pattern, [string] $Name)

    $lines = Get-Content -LiteralPath $Path
    $start = -1
    for ($i = 0; $i -lt $lines.Count; ++$i) {
        if ($lines[$i] -match $Pattern) {
            $start = $i
            break
        }
    }

    if ($start -lt 0) {
        throw "Could not locate the enumeration '$Name' in '$Path'."
    }

    # The opening brace follows the declaration, the closing '};' ends the body.
    $open = -1
    for ($i = $start; $i -lt $lines.Count; ++$i) {
        if ($lines[$i] -match '\{') {
            $open = $i
            break
        }
    }

    if ($open -lt 0) {
        throw "The enumeration '$Name' of '$Path' has no opening brace."
    }

    $body = New-Object 'System.Collections.Generic.List[string]'
    for ($i = $open + 1; $i -lt $lines.Count; ++$i) {
        if ($lines[$i] -match '^\s*\}\s*;') {
            return $body
        }

        $body.Add($lines[$i])
    }

    throw "The enumeration '$Name' of '$Path' is not closed."
}

function Convert-MemberName {
    param([string] $Name)

    # SCREAMING_CASE to PascalCase: 'ADVBORDE' is Advborde, 'B_BFLG32' is BBflg32, 'WSND00' is Wsnd00.
    # Digits stay as they are, so the numbers the reference spells out remain readable.
    $parts = $Name -split '_' | Where-Object { $_.Length -gt 0 }
    $converted = foreach ($part in $parts) {
        if ($part -match '^\d') { $part }
        else { $part.Substring(0, 1) + $part.Substring(1).ToLowerInvariant() }
    }

    return ($converted -join '')
}

function Convert-EnumMembers {
    param([System.Collections.Generic.List[string]] $Body, [string] $Enumeration, [hashtable] $Overrides)

    $members = New-Object 'System.Collections.Generic.List[string]'
    $names = New-Object 'System.Collections.Generic.List[string]'
    $value = 0

    foreach ($line in $Body) {
        $trimmed = $line.Trim()

        if ($trimmed.Length -eq 0) {
            $members.Add('')
            continue
        }

        if ($trimmed.StartsWith('//')) {
            $members.Add("    $trimmed")
            continue
        }

        if ($trimmed -notmatch '^([A-Z][A-Z0-9_]*)\s*(?:=\s*(\d+))?\s*,?\s*(?://\s*(.*))?$') {
            throw "Unsupported member of $Enumeration`: '$trimmed'."
        }

        $reference = $Matches[1]
        if ($Matches[2].Length -gt 0) {
            $value = [int]$Matches[2]
        }

        $name = if ($Overrides.ContainsKey($reference)) { $Overrides[$reference] } else { Convert-MemberName -Name $reference }

        if ($value -eq 0) {
            $members.Add("    $name = 0, // $reference")
        }
        else {
            $members.Add("    $name, // $reference")
        }

        $names.Add($reference)
        ++$value
    }

    return @{ Members = $members; Names = $names }
}

function Get-NameTable {
    param([string] $Path, [string] $Pattern, [string] $Name)

    $lines = Get-Content -LiteralPath $Path
    $start = -1
    for ($i = 0; $i -lt $lines.Count; ++$i) {
        if ($lines[$i] -match $Pattern) {
            $start = $i
            break
        }
    }

    if ($start -lt 0) {
        throw "Could not locate '$Name' in '$Path'."
    }

    $pairs = New-Object 'System.Collections.Generic.List[object]'
    for ($i = $start; $i -lt $lines.Count; ++$i) {
        # The braces of the initialiser come in many shapes ('} };' for the array, '};' for the map), so
        # the pairs of a line are taken first and a line that starts with a closing brace then ends it.
        $matches = [regex]::Matches($lines[$i], '\{\s*([A-Z][A-Z0-9_]*)\s*,\s*"([^"]+)"\s*\}')
        foreach ($match in $matches) {
            # A comment on the entry line is a remark about that entry (six XMI aliases explain a bug of the
            # original release), so it is carried into the generated file.
            $note = ''
            if ($matches.Count -eq 1) {
                $comment = [regex]::Match($lines[$i], '//\s*(.+)$')
                if ($comment.Success) {
                    $note = $comment.Groups[1].Value.Trim()
                }
            }

            $pairs.Add([pscustomobject]@{ Name = $match.Groups[1].Value; File = $match.Groups[2].Value; Note = $note })
        }

        # The declaration line itself can start with a closing brace (m82.cpp's '} m82map[] ='), so only
        # the lines after it can end the table.
        if ($i -gt $start -and $lines[$i].TrimStart().StartsWith('}')) {
            return $pairs
        }
    }

    throw "'$Name' of '$Path' is not closed."
}

function Get-EntryLine {
    param([psobject] $Pair, [string] $Member)

    # The member name comes from the enumeration, the note from the reference line. Keeping both means a
    # reader can find the entry in the reference without a lookup.
    if ($Pair.Note.Length -gt 0) {
        return "        `"$($Pair.File)`", // $Member — $($Pair.Note)"
    }

    return "        `"$($Pair.File)`", // $Member"
}

function Save-File {
    param([string] $Output, [System.Collections.Generic.List[string]] $Content)

    $directory = Split-Path -Parent $Output
    if (-not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    Set-Content -LiteralPath $Output -Value $Content -Encoding utf8NoBOM
    Write-Host "Wrote $Output"
}

$icn = Convert-EnumMembers -Body (Get-EnumBody -Path $IcnHeader -Pattern '^\s*enum\s*:\s*int32_t' -Name 'ICN') `
    -Enumeration 'IcnType' -Overrides @{ LASTICN = 'LastIcn' }

$m82 = Convert-EnumMembers -Body (Get-EnumBody -Path $M82Header -Pattern '^\s*enum SoundType\s*:\s*int' -Name 'M82') `
    -Enumeration 'M82Sound' -Overrides @{}

$xmi = Convert-EnumMembers -Body (Get-EnumBody -Path $XmiHeader -Pattern '^\s*enum\s*$' -Name 'XMI') `
    -Enumeration 'XmiTrack' -Overrides @{}

$til = Convert-EnumMembers -Body (Get-EnumBody -Path $TilHeader -Pattern '^\s*enum\s*$' -Name 'TIL') `
    -Enumeration 'TilType' -Overrides @{}

# The two tables: the enumerations are what the port refers to, these are what gets opened.
$icnFiles = Get-NameTable -Path $IcnSource -Pattern '^\s*const std::array<ICNMapItem, LAST_VALID_FILE_ICN> icnMap' -Name 'icnMap'
$m82Files = Get-NameTable -Path $M82Source -Pattern 'm82map\[\] =' -Name 'm82map'
$xmiFiles = Get-NameTable -Path $XmiSource -Pattern 'xmimap\[\] =' -Name 'xmimap'

$icnValidCount = [array]::IndexOf($icn.Names.ToArray(), 'LAST_VALID_FILE_ICN')

if ($icnFiles.Count -ne $icnValidCount) {
    throw "icnMap has $($icnFiles.Count) entries but IcnType has $icnValidCount real files."
}

if ($m82Files.Count -ne $m82.Names.Count) {
    throw "m82map has $($m82Files.Count) entries but M82Sound has $($m82.Names.Count)."
}

for ($i = 0; $i -lt $icnFiles.Count; ++$i) {
    if ($icnFiles[$i].Name -ne $icn.Names[$i]) {
        throw "icnMap entry $i is $($icnFiles[$i].Name) but IcnType has $($icn.Names[$i])."
    }
}

for ($i = 0; $i -lt $m82Files.Count; ++$i) {
    if ($m82Files[$i].Name -ne $m82.Names[$i]) {
        throw "m82map entry $i is $($m82Files[$i].Name) but M82Sound has $($m82.Names[$i])."
    }
}

if ($xmiFiles.Count -ne $xmi.Names.Count) {
    throw "xmimap has $($xmiFiles.Count) entries but XmiTrack has $($xmi.Names.Count)."
}

for ($i = 0; $i -lt $xmiFiles.Count; ++$i) {
    if ($xmiFiles[$i].Name -ne $xmi.Names[$i]) {
        throw "xmimap entry $i is $($xmiFiles[$i].Name) but XmiTrack has $($xmi.Names[$i])."
    }
}

$file = New-Object 'System.Collections.Generic.List[string]'
$file.Add('namespace FHeroes2.Assets.Icn;')
$file.Add('')
$file.Add('/// <summary>')
$file.Add('/// The ICN ids: the anonymous <c>enum : int32_t</c> of namespace <c>ICN</c> in')
$file.Add('/// <c>src/fheroes2/agg/icn.h</c>, in the same order and with the same values.')
$file.Add('/// </summary>')
$file.Add('/// <remarks>')
$file.Add('/// <para>')
$file.Add('/// The reference gives the enumeration no name; the port calls it <c>IcnType</c> the way it calls the')
$file.Add('/// artifact one <c>ArtifactType</c>. Every member carries its reference name in a trailing comment,')
$file.Add('/// because the name is the identity used by the archive lookups and the tables.')
$file.Add('/// </para>')
$file.Add('/// <para>')
$file.Add('/// <see cref="LastValidFileIcn"/> is the boundary the reference draws between the files that exist in')
$file.Add('/// the archive and the ones the engine draws itself (<c>ROUTERED</c> onwards):')
$file.Add('/// <see cref="IcnCatalog.GetFileName"/> has a name only for the former.')
$file.Add('/// </para>')
$file.Add('/// <para>Generated by <c>tools/scripts/GenerateAggEnums.ps1</c>; do not edit by hand.</para>')
$file.Add('/// </remarks>')
$file.Add('public enum IcnType : int')
$file.Add('{')
foreach ($line in $icn.Members) { $file.Add($line) }
$file.Add('}')
Save-File -Output (Join-Path $AssetsDirectory 'Icn/IcnType.cs') -Content $file

$file = New-Object 'System.Collections.Generic.List[string]'
$file.Add('namespace FHeroes2.Assets.Icn;')
$file.Add('')
$file.Add('/// <summary>')
$file.Add('/// The archive entry name of every real ICN file, the <c>icnMap</c> of')
$file.Add('/// <c>src/fheroes2/agg/icn.cpp:26</c>, indexed by <see cref="IcnType"/>.')
$file.Add('/// </summary>')
$file.Add('/// <remarks>')
$file.Add('/// <para>')
$file.Add('/// Almost every name is the enumeration member plus <c>.ICN</c>; the exceptions are the two flags and the')
$file.Add('/// three fonts whose archive names carry a dash or a plain spelling, which is exactly why the table exists')
$file.Add('/// instead of a string concatenation.')
$file.Add('/// </para>')
$file.Add('/// <para>Generated by <c>tools/scripts/GenerateAggEnums.ps1</c>; do not edit by hand.</para>')
$file.Add('/// </remarks>')
$file.Add('internal static class IcnFileNames')
$file.Add('{')
$file.Add('    /// <summary>Names of the ids below <see cref="IcnType.LastValidFileIcn"/>.</summary>')
$file.Add('    internal static readonly string[] ById =')
$file.Add('    [')
foreach ($pair in $icnFiles) {
    $member = Convert-MemberName -Name $pair.Name
    $file.Add((Get-EntryLine -Pair $pair -Member $member))
}
$file.Add('    ];')
$file.Add('}')
Save-File -Output (Join-Path $AssetsDirectory 'Icn/IcnFileNames.cs') -Content $file

$file = New-Object 'System.Collections.Generic.List[string]'
$file.Add('namespace FHeroes2.Assets.Audio;')
$file.Add('')
$file.Add('/// <summary>')
$file.Add('/// The sound ids: <c>M82::SoundType</c> of <c>src/fheroes2/agg/m82.h</c>, in the same order and with')
$file.Add('/// the same values.')
$file.Add('/// </summary>')
$file.Add('/// <remarks>')
$file.Add('/// <para>')
$file.Add('/// Every member carries its reference name in a trailing comment, because the name is the identity:')
$file.Add('/// <see cref="M82Catalog.GetFileName"/> resolves it through the table generated from <c>m82map</c>.')
$file.Add('/// </para>')
$file.Add('/// <para>Generated by <c>tools/scripts/GenerateAggEnums.ps1</c>; do not edit by hand.</para>')
$file.Add('/// </remarks>')
$file.Add('public enum M82Sound : int')
$file.Add('{')
foreach ($line in $m82.Members) { $file.Add($line) }
$file.Add('}')
Save-File -Output (Join-Path $AssetsDirectory 'Audio/M82Sound.cs') -Content $file

$file = New-Object 'System.Collections.Generic.List[string]'
$file.Add('namespace FHeroes2.Assets.Audio;')
$file.Add('')
$file.Add('/// <summary>')
$file.Add('/// The archive entry name of every sound, the <c>m82map</c> of <c>src/fheroes2/agg/m82.cpp:26</c>,')
$file.Add('/// indexed by <see cref="M82Sound"/>.')
$file.Add('/// </summary>')
$file.Add('/// <remarks>')
$file.Add('/// <para>')
$file.Add('/// The names are not the enumeration members plus <c>.82M</c>: they are eight characters at most, so the')
$file.Add('/// reference spells them out one by one, and one of them (<c>METEOR~1.82M</c>) even carries a patch')
$file.Add('/// suffix. Generated by <c>tools/scripts/GenerateAggEnums.ps1</c>; do not edit by hand.')
$file.Add('/// </para>')
$file.Add('/// </remarks>')
$file.Add('internal static class M82FileNames')
$file.Add('{')
$file.Add('    /// <summary>Names of every id, in <see cref="M82Sound"/> order.</summary>')
$file.Add('    internal static readonly string[] ById =')
$file.Add('    [')
foreach ($pair in $m82Files) {
    $member = Convert-MemberName -Name $pair.Name
    $file.Add((Get-EntryLine -Pair $pair -Member $member))
}
$file.Add('    ];')
$file.Add('}')
Save-File -Output (Join-Path $AssetsDirectory 'Audio/M82FileNames.cs') -Content $file

$file = New-Object 'System.Collections.Generic.List[string]'
$file.Add('namespace FHeroes2.Assets.Audio;')
$file.Add('')
$file.Add('/// <summary>')
$file.Add('/// The MIDI track ids: the anonymous <c>enum</c> of namespace <c>XMI</c> in <c>src/fheroes2/agg/xmi.h</c>,')
$file.Add('/// in the same order and with the same values.')
$file.Add('/// </summary>')
$file.Add('/// <para>')
$file.Add('/// The ids are the last four digits of the archive entry name (<c>MIDI0002.XMI</c> is')
$file.Add('/// <c>Midi0002</c>), which is why the reference can build the name with a formatter; the port keeps the')
$file.Add('/// spelling in one place, <see cref="XmiCatalog.GetFileName"/>.')
$file.Add('/// </para>')
$file.Add('/// <para>Generated by <c>tools/scripts/GenerateAggEnums.ps1</c>; do not edit by hand.</para>')
$file.Add('public enum XmiTrack : int')
$file.Add('{')
foreach ($line in $xmi.Members) { $file.Add($line) }
$file.Add('}')
Save-File -Output (Join-Path $AssetsDirectory 'Audio/XmiTrack.cs') -Content $file

$file = New-Object 'System.Collections.Generic.List[string]'
$file.Add('namespace FHeroes2.Assets.Audio;')
$file.Add('')
$file.Add('/// <summary>')
$file.Add('/// The archive entry name of every MIDI track, the <c>xmimap</c> of')
$file.Add('/// <c>src/fheroes2/agg/xmi.cpp:29</c>, indexed by <see cref="XmiTrack"/>.')
$file.Add('/// </summary>')
$file.Add('/// <remarks>')
$file.Add('/// <para>')
$file.Add('/// Six entries are aliases, not namesakes: the <c>MidiOriginal*</c> ids point at the file the original')
$file.Add('/// release actually used for that castle (the Knight theme was shared with the Barbarian and Wizard, the')
$file.Add('/// Sorceress and Warlock swapped). The comments of those six are carried over from the reference, because')
$file.Add('/// the mapping is a bug of the original game and not an obvious rule.')
$file.Add('/// </para>')
$file.Add('/// <para>Generated by <c>tools/scripts/GenerateAggEnums.ps1</c>; do not edit by hand.</para>')
$file.Add('/// </remarks>')
$file.Add('internal static class XmiFileNames')
$file.Add('{')
$file.Add('    /// <summary>Names of every id, in <see cref="XmiTrack"/> order.</summary>')
$file.Add('    internal static readonly string[] ById =')
$file.Add('    [')
foreach ($pair in $xmiFiles) {
    $member = Convert-MemberName -Name $pair.Name
    $file.Add((Get-EntryLine -Pair $pair -Member $member))
}
$file.Add('    ];')
$file.Add('}')
Save-File -Output (Join-Path $AssetsDirectory 'Audio/XmiFileNames.cs') -Content $file

$file = New-Object 'System.Collections.Generic.List[string]'
$file.Add('namespace FHeroes2.Assets.Til;')
$file.Add('')
$file.Add('/// <summary>')
$file.Add('/// The TIL ids: the anonymous <c>enum</c> of namespace <c>TIL</c> in <c>src/fheroes2/agg/til.h</c>, in the')
$file.Add('/// same order and with the same values.')
$file.Add('/// </summary>')
$file.Add('/// <para>Generated by <c>tools/scripts/GenerateAggEnums.ps1</c>; do not edit by hand.</para>')
$file.Add('public enum TilType : int')
$file.Add('{')
foreach ($line in $til.Members) { $file.Add($line) }
$file.Add('}')
Save-File -Output (Join-Path $AssetsDirectory 'Til/TilType.cs') -Content $file
