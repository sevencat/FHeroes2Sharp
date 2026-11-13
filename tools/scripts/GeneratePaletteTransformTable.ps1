<#
.SYNOPSIS
    Regenerates src/FHeroes2.Core/Imaging/PaletteTransformTable.cs.

.DESCRIPTION
    The transform layer palettes are a 4096 byte literal table inside the reference engine
    (fheroes2, src/engine/image.cpp). Retyping them by hand is not an option, so this script
    extracts the array from the C++ source and emits it as a Base64 constant.

    Run it after updating the reference checkout, then run the tests: PaletteTransformTableTests
    pins the SHA-256 of the decoded data and fails when the table changes silently.

.EXAMPLE
    pwsh tools/scripts/GeneratePaletteTransformTable.ps1 -Fheroes2Source F:\code\fheroes2\src\engine\image.cpp
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $Fheroes2Source = 'F:\code\fheroes2\src\engine\image.cpp',

    [Parameter(Mandatory = $false)]
    [string] $Output = (Join-Path $PSScriptRoot '../../src/FHeroes2.Core/Imaging/PaletteTransformTable.cs')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Fheroes2Source)) {
    throw "The fheroes2 source file '$Fheroes2Source' does not exist."
}

$source = Get-Content -LiteralPath $Fheroes2Source -Raw
$start = $source.IndexOf('const uint8_t transformTable[')
if ($start -lt 0) {
    throw 'The transformTable literal was not found. The reference layout changed.'
}

$end = $source.IndexOf('};', $start)
if ($end -lt 0) {
    throw 'The end of the transformTable literal was not found.'
}

$block = $source.Substring($start, $end - $start)
$numbers = [regex]::Matches($block, '\b\d{1,3}\b') | ForEach-Object { [int] $_.Value }

# The first match belongs to the array size expression: paletteSize * 16.
if ($numbers.Count -ne 4097) {
    throw "Expected 4096 values but found $($numbers.Count - 1). The reference layout changed."
}

$bytes = [byte[]]::new(4096)
for ($i = 0; $i -lt 4096; ++$i) {
    $value = $numbers[$i + 1]
    if ($value -gt 255) {
        throw "Value $value at index $i does not fit into a byte."
    }

    $bytes[$i] = [byte] $value
}

$newLine = [Environment]::NewLine
$base64 = [Convert]::ToBase64String($bytes)
$builder = [System.Text.StringBuilder]::new()

[void] $builder.Append('using System;').Append($newLine).Append($newLine)
[void] $builder.Append('namespace FHeroes2.Core.Imaging;').Append($newLine).Append($newLine)
[void] $builder.Append('/// <summary>').Append($newLine)
[void] $builder.Append('/// The palette conversion tables used by the transform layer of a sprite.').Append($newLine)
[void] $builder.Append('/// </summary>').Append($newLine)
[void] $builder.Append('/// <remarks>').Append($newLine)
[void] $builder.Append('/// <para>').Append($newLine)
[void] $builder.Append('/// A sprite has an optional second byte layer, the <i>transform layer</i>. Each of its').Append($newLine)
[void] $builder.Append('/// entries selects how a pixel is combined with what is already on the destination:').Append($newLine)
[void] $builder.Append('/// <c>0</c> copies the image layer, <c>1</c> skips the pixel and every other value picks one').Append($newLine)
[void] $builder.Append('/// of the 16 tables below to convert the <i>destination</i> color, e.g. a shadow over a tile.').Append($newLine)
[void] $builder.Append('/// </para>').Append($newLine)
[void] $builder.Append('/// <para>').Append($newLine)
[void] $builder.Append('/// The data is a verbatim copy of the <c>transformTable</c> array from fheroes2').Append($newLine)
[void] $builder.Append('/// <c>src/engine/image.cpp</c>. Tables 0 and 1 are zero placeholders that no code references,').Append($newLine)
[void] $builder.Append('/// tables 2 to 5 are the shadow ramp (2 being the strongest, see <c>image.h</c>), tables 6 to 9').Append($newLine)
[void] $builder.Append('/// are the lightening ramp used by spell effects and table 15 maps the cycling color ranges').Append($newLine)
[void] $builder.Append('/// onto their static duplicates so that an image can be drawn without color cycling.').Append($newLine)
[void] $builder.Append('/// </para>').Append($newLine)
[void] $builder.Append('/// <para>').Append($newLine)
[void] $builder.Append('/// The array is stored as Base64 so that a diff of this file stays reviewable. Regenerate it').Append($newLine)
[void] $builder.Append('/// with <c>tools/scripts/GeneratePaletteTransformTable.ps1</c>;').Append($newLine)
[void] $builder.Append('/// <c>PaletteTransformTableTests</c> pins the SHA-256 of the decoded data.').Append($newLine)
[void] $builder.Append('/// </para>').Append($newLine)
[void] $builder.Append('/// </remarks>').Append($newLine)
[void] $builder.Append('public static class PaletteTransformTable').Append($newLine)
[void] $builder.Append('{').Append($newLine)
[void] $builder.Append('    /// <summary>Number of conversion tables.</summary>').Append($newLine)
[void] $builder.Append('    public const int TransformCount = 16;').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>Number of palette entries handled by a single table.</summary>').Append($newLine)
[void] $builder.Append('    public const int TransformSize = 256;').Append($newLine).Append($newLine)
[void] $builder.Append('    private const string EncodedData =').Append($newLine)

$quote = [char] 34

for ($i = 0; $i -lt $base64.Length; $i += 96) {
    $chunkLength = [Math]::Min(96, $base64.Length - $i)
    $chunk = $base64.Substring($i, $chunkLength)
    $suffix = if (($i + $chunkLength) -ge $base64.Length) { ';' } else { ' +' }
    [void] $builder.Append('        ' + $quote + $chunk + $quote).Append($suffix).Append($newLine)
}

[void] $builder.Append($newLine)
[void] $builder.Append('    private static readonly byte[] Data = Convert.FromBase64String(EncodedData);').Append($newLine).Append($newLine)

$quote = [char] 34

[void] $builder.Append('    /// <summary>All <see cref=' + $quote + 'TransformCount' + $quote + '/> tables, each one <see cref=' + $quote + 'TransformSize' + $quote + '/> bytes long.</summary>').Append($newLine)
[void] $builder.Append('    public static ReadOnlySpan<byte> All => Data;').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>Returns the table for <paramref name=' + $quote + 'transformId' + $quote + '/>.</summary>').Append($newLine)
[void] $builder.Append('    public static ReadOnlySpan<byte> Get(int transformId)').Append($newLine)
[void] $builder.Append('    {').Append($newLine)
[void] $builder.Append('        ArgumentOutOfRangeException.ThrowIfNegative(transformId);').Append($newLine)
[void] $builder.Append('        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(transformId, TransformCount);').Append($newLine).Append($newLine)
[void] $builder.Append('        return Data.AsSpan(transformId * TransformSize, TransformSize);').Append($newLine)
[void] $builder.Append('    }').Append($newLine).Append($newLine)
[void] $builder.Append('    /// <summary>Converts <paramref name=' + $quote + 'color' + $quote + '/> with the table selected by <paramref name=' + $quote + 'transformId' + $quote + '/>.</summary>').Append($newLine)
[void] $builder.Append('    public static byte Apply(int transformId, byte color) => Get(transformId)[color];').Append($newLine)
[void] $builder.Append('}').Append($newLine)

Set-Content -LiteralPath $Output -Value $builder.ToString() -Encoding utf8 -NoNewline

Write-Host "Wrote $((Get-Item -LiteralPath $Output).Length) bytes to $Output"
Write-Host "SHA-256 of the table: $([Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes)))"

