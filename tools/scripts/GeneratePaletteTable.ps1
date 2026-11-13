<#
.SYNOPSIS
    Regenerates src/FHeroes2.Core/Imaging/PaletteTable.cs.

.DESCRIPTION
    The recolouring palettes of the reference engine (fheroes2, src/engine/pal.cpp) are thirteen
    256 byte literal tables plus the identity table that PAL::GetPalette() builds at run time.
    Retyping 3584 numbers by hand is not an option, so this script extracts the arrays from the
    C++ source and emits them as one Base64 constant.

    Run it after updating the reference checkout, then run the tests: PaletteTableTests pins the
    SHA-256 of the decoded data and fails when a table changes silently.

.EXAMPLE
    pwsh tools/scripts/GeneratePaletteTable.ps1 -Fheroes2Source F:\code\fheroes2\src\engine\pal.cpp
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $Fheroes2Source = 'F:\code\fheroes2\src\engine\pal.cpp',

    [Parameter(Mandatory = $false)]
    [string] $Output = (Join-Path $PSScriptRoot '../../src/FHeroes2.Core/Imaging/PaletteTable.cs')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Fheroes2Source)) {
    throw "The fheroes2 source file '$Fheroes2Source' does not exist."
}

$source = Get-Content -LiteralPath $Fheroes2Source -Raw
$paletteSize = 256

function Get-PaletteValues {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    $marker = "const std::vector<uint8_t> $Name"
    $start = $source.IndexOf($marker)
    if ($start -lt 0) {
        throw "The '$Name' palette literal was not found. The reference layout changed."
    }

    $open = $source.IndexOf('{', $start)
    $close = $source.IndexOf('};', $open)
    if ($open -lt 0 -or $close -lt 0) {
        throw "The braces of the '$Name' palette literal were not found."
    }

    $block = $source.Substring($open, $close - $open)
    $numbers = [regex]::Matches($block, '\b\d{1,3}\b') | ForEach-Object { [int] $_.Value }
    if ($numbers.Count -ne $paletteSize) {
        throw "Expected $paletteSize values in '$Name' but found $($numbers.Count). The reference layout changed."
    }

    $bytes = [byte[]]::new($paletteSize)
    for ($i = 0; $i -lt $paletteSize; ++$i) {
        $value = $numbers[$i]
        if ($value -gt 255) {
            throw "Value $value at index $i of '$Name' does not fit into a byte."
        }

        $bytes[$i] = [byte] $value
    }

    return , $bytes
}

# The order is the one of the PaletteId enum, which mirrors PAL::PaletteType. Index 0 is the
# identity table PAL::GetPalette( STANDARD ) synthesizes at run time.
$tableNames = @(
    $null,
    'yellowTextTable',
    'whiteTextTable',
    'grayTextTable',
    'redTable',
    'grayTable',
    'brownTable',
    'tanTable',
    'noCycleTable',
    'mirrorImageTable',
    'darkeningTable',
    'goodToEvilInterfaceTable',
    'goodToEvilButtonTable',
    'purpleTable'
)

$identity = [byte[]]::new($paletteSize)
for ($i = 0; $i -lt $paletteSize; ++$i) {
    $identity[$i] = [byte] $i
}

$all = [System.Collections.Generic.List[byte]]::new()
[void] $all.AddRange($identity)

foreach ($name in $tableNames) {
    if ($null -eq $name) {
        continue
    }

    [void] $all.AddRange((Get-PaletteValues -Name $name))
}

$count = $tableNames.Count
if ($all.Count -ne $count * $paletteSize) {
    throw "Expected $($count * $paletteSize) bytes but built $($all.Count)."
}

$newLine = [Environment]::NewLine
$base64 = [Convert]::ToBase64String($all.ToArray())
$builder = [System.Text.StringBuilder]::new()

[void] $builder.Append('using System;').Append($newLine).Append($newLine)
[void] $builder.Append('namespace FHeroes2.Core.Imaging;').Append($newLine).Append($newLine)
[void] $builder.Append('/// <summary>').Append($newLine)
[void] $builder.Append('/// The recolouring palettes of the reference engine: every table maps a palette index').Append($newLine)
[void] $builder.Append('/// onto another one, so applying it to an indexed image swaps its colours without').Append($newLine)
[void] $builder.Append('/// touching the geometry.').Append($newLine)
[void] $builder.Append('/// </summary>').Append($newLine)
[void] $builder.Append('/// <remarks>').Append($newLine)
[void] $builder.Append('/// <para>').Append($newLine)
[void] $builder.Append('/// The tables are the ones <c>fheroes2</c> keeps in <c>src/engine/pal.cpp</c>').Append($newLine)
[void] $builder.Append('/// (<c>PAL::GetPalette</c>). They are what makes text yellow or gray - the bitmap font is').Append($newLine)
[void] $builder.Append('/// always the same <c>FONT.ICN</c> sprite table, only the palette changes - and they').Append($newLine)
[void] $builder.Append('/// produce disabled buttons, the petrified unit colour, the red Blood Lust flash, the').Append($newLine)
[void] $builder.Append('/// purple random object variants and the Evil interface out of the Good one.').Append($newLine)
[void] $builder.Append('/// </para>').Append($newLine)
[void] $builder.Append('/// <para>').Append($newLine)
[void] $builder.Append('/// <see cref="PaletteId.Standard"/> is the identity table <c>PAL::GetPalette</c> builds at').Append($newLine)
[void] $builder.Append('/// run time; the other entries are verbatim copies of the literals. The data is stored as').Append($newLine)
[void] $builder.Append('/// Base64 so that a diff of this file stays reviewable. Regenerate it with').Append($newLine)
[void] $builder.Append('/// <c>tools/scripts/GeneratePaletteTable.ps1</c>; <c>PaletteTableTests</c> pins the SHA-256').Append($newLine)
[void] $builder.Append('/// of the decoded data.').Append($newLine)
[void] $builder.Append('/// </para>').Append($newLine)
[void] $builder.Append('/// </remarks>').Append($newLine)
[void] $builder.Append('public static class PaletteTable').Append($newLine)
[void] $builder.Append('{').Append($newLine)
[void] $builder.Append('    /// <summary>Number of palette entries a table converts.</summary>').Append($newLine)
[void] $builder.Append('    public const int Size = 256;').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>Number of tables, one per <see cref="PaletteId"/> value.</summary>').Append($newLine)
[void] $builder.Append("    public const int Count = $count;").Append($newLine).Append($newLine)
[void] $builder.Append('    private const string EncodedData =').Append($newLine)

$chunkSize = 100
for ($offset = 0; $offset -lt $base64.Length; $offset += $chunkSize) {
    $length = [Math]::Min($chunkSize, $base64.Length - $offset)
    $chunk = $base64.Substring($offset, $length)
    $suffix = if ($offset + $length -lt $base64.Length) { ' +' } else { ';' }
    [void] $builder.Append('        "').Append($chunk).Append('"').Append($suffix).Append($newLine)
}

[void] $builder.Append($newLine)
[void] $builder.Append('    private static readonly byte[] Data = Convert.FromBase64String(EncodedData);').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>').Append($newLine)
[void] $builder.Append('    /// All <see cref="Count"/> tables, each one <see cref="Size"/> bytes long, in').Append($newLine)
[void] $builder.Append('    /// <see cref="PaletteId"/> order.').Append($newLine)
[void] $builder.Append('    /// </summary>').Append($newLine)
[void] $builder.Append('    public static ReadOnlySpan<byte> All => Data;').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>Returns the table of <paramref name="id"/>.</summary>').Append($newLine)
[void] $builder.Append('    public static ReadOnlySpan<byte> Get(PaletteId id)').Append($newLine)
[void] $builder.Append('    {').Append($newLine)
[void] $builder.Append('        if ((uint) id >= Count)').Append($newLine)
[void] $builder.Append('        {').Append($newLine)
[void] $builder.Append('            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown palette table.");').Append($newLine)
[void] $builder.Append('        }').Append($newLine).Append($newLine)
[void] $builder.Append('        return Data.AsSpan((int) id * Size, Size);').Append($newLine)
[void] $builder.Append('    }').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>Converts <paramref name="color"/> with the table of <paramref name="id"/>.</summary>').Append($newLine)
[void] $builder.Append('    public static byte Apply(PaletteId id, byte color) => Get(id)[color];').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>').Append($newLine)
[void] $builder.Append('    /// Returns <c>first</c> followed by <c>second</c> (<c>PAL::CombinePalettes</c>): the result').Append($newLine)
[void] $builder.Append('    /// maps an index through <paramref name="first"/> and then through').Append($newLine)
[void] $builder.Append('    /// <paramref name="second"/>, which is how a disabled icon becomes both gray and dark.').Append($newLine)
[void] $builder.Append('    /// </summary>').Append($newLine)
[void] $builder.Append('    public static byte[] Combine(PaletteId first, PaletteId second) =>').Append($newLine)
[void] $builder.Append('        Combine(Get(first), Get(second));').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>Combines two raw tables, see <see cref="Combine(PaletteId, PaletteId)"/>.</summary>').Append($newLine)
[void] $builder.Append('    /// <exception cref="ArgumentException">A table is not <see cref="Size"/> bytes long.</exception>').Append($newLine)
[void] $builder.Append('    public static byte[] Combine(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)').Append($newLine)
[void] $builder.Append('    {').Append($newLine)
[void] $builder.Append('        if (first.Length != Size)').Append($newLine)
[void] $builder.Append('        {').Append($newLine)
[void] $builder.Append('            throw new ArgumentException($"A table must hold {Size} entries.", nameof(first));').Append($newLine)
[void] $builder.Append('        }').Append($newLine).Append($newLine)
[void] $builder.Append('        if (second.Length != Size)').Append($newLine)
[void] $builder.Append('        {').Append($newLine)
[void] $builder.Append('            throw new ArgumentException($"A table must hold {Size} entries.", nameof(second));').Append($newLine)
[void] $builder.Append('        }').Append($newLine).Append($newLine)
[void] $builder.Append('        byte[] combined = new byte[Size];').Append($newLine)
[void] $builder.Append('        for (int index = 0; index < Size; ++index)').Append($newLine)
[void] $builder.Append('        {').Append($newLine)
[void] $builder.Append('            combined[index] = second[first[index]];').Append($newLine)
[void] $builder.Append('        }').Append($newLine).Append($newLine)
[void] $builder.Append('        return combined;').Append($newLine)
[void] $builder.Append('    }').Append($newLine)
[void] $builder.Append('}').Append($newLine)

$directory = Split-Path -Parent $Output
if (-not (Test-Path -LiteralPath $directory)) {
    throw "The output directory '$directory' does not exist."
}

Set-Content -LiteralPath $Output -Value $builder.ToString() -NoNewline

$hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($all.ToArray()))
Write-Host "Wrote $($all.Count) bytes ($count tables) to '$Output'."
Write-Host "SHA-256: $hash"
