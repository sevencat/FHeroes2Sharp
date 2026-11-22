# Generates src/FHeroes2.Game/Gui/FontAlphabet.<CodePage>.cs from ui_font.cpp.
#
# The eight generateCPxxxxAlphabet functions are data, not algorithms: a flat list of
# resize / reset / Copy / FillTransform / setPosition calls that build the non-ASCII letters of a
# code page out of the letters the archive already has. Transcribing five thousand lines by hand
# would invite typos, so the statements are translated one to one by this script and the result is
# committed next to the hand written shell (FontAlphabet.cs).
#
# Usage: pwsh tools/GenerateFontAlphabets.ps1 [-Reference <ui_font.cpp>] [-OutputDirectory <dir>]

param(
    [string]$Reference = 'F:/code/fheroes2/src/fheroes2/gui/ui_font.cpp',
    [string]$OutputDirectory = 'F:/code/FHeroes2Sharp/src/FHeroes2.Game/Gui'
)

$ErrorActionPreference = 'Stop'

$functions = @(
    @{ CodePage = 'Cp1250'; Reference = 'generateCP1250Alphabet'; Line = 186 }
    @{ CodePage = 'Cp1251'; Reference = 'generateCP1251Alphabet'; Line = 1371 }
    @{ CodePage = 'Cp1252'; Reference = 'generateCP1252Alphabet'; Line = 2398 }
    @{ CodePage = 'Cp1253'; Reference = 'generateCP1253Alphabet'; Line = 3463 }
    @{ CodePage = 'Cp1254'; Reference = 'generateCP1254Alphabet'; Line = 3792 }
    @{ CodePage = 'Cp1258'; Reference = 'generateCP1258Alphabet'; Line = 4033 }
    @{ CodePage = 'Iso8859_16'; Reference = 'generateISO8859_16Alphabet'; Line = 4519 }
    @{ CodePage = 'Iso8859_3'; Reference = 'generateISO8859_3Alphabet'; Line = 4719 }
)

$lines = Get-Content -LiteralPath $Reference

function Get-Body([string]$Name) {
    $start = -1
    for ($i = 0; $i -lt $lines.Count; ++$i) {
        if ($lines[$i] -match "^    void $Name\(") {
            $start = $i
            break
        }
    }

    if ($start -lt 0) {
        throw "Cannot find $Name in $Reference."
    }

    $end = $start + 1
    while ($end -lt $lines.Count -and $lines[$end] -ne '    }') {
        ++$end
    }

    # The first line after the signature is the function's own brace, which the generated method writes itself.
    return $lines[($start + 2)..($end - 1)]
}

# Splits an argument list at the commas that are not inside brackets, parentheses or braces.
function Split-Arguments([string]$Text) {
    $arguments = [System.Collections.Generic.List[string]]::new()
    $depth = 0
    $current = [System.Text.StringBuilder]::new()

    foreach ($character in $Text.ToCharArray()) {
        switch ($character) {
            { $_ -eq '(' -or $_ -eq '[' -or $_ -eq '{' } { ++$depth }
            { $_ -eq ')' -or $_ -eq ']' -or $_ -eq '}' } { --$depth }
        }

        if ($character -eq ',' -and $depth -eq 0) {
            $arguments.Add($current.ToString().Trim())
            [void]$current.Clear()
            continue
        }

        [void]$current.Append($character)
    }

    $arguments.Add($current.ToString().Trim())

    return $arguments
}

function Convert-Expression([string]$Text, [string]$Table) {
    $result = $Text
    $result = $result -replace 'font\[', "$Table["
    $result = $result -replace '\.width\(\)', '.Width'
    $result = $result -replace '\.height\(\)', '.Height'
    $result = $result -replace '\.x\(\)', '.OffsetX'
    $result = $result -replace '\.y\(\)', '.OffsetY'
    $result = $result -replace 'normalFontSpaceWidth', 'NormalFontSpaceWidth'
    $result = $result -replace 'smallFontSpaceWidth', 'SmallFontSpaceWidth'

    return $result.Trim()
}

function Convert-Statement([string]$Statement, [string]$Table, [string]$Temp) {
    $text = $Statement.Trim()

    if ($text.StartsWith('//')) {
        return $text
    }

    # size_t offset = 0; ... offset = 32;
    if ($text -match '^size_t offset = (?<value>\d+);$') {
        return "int offset = $($Matches.value);"
    }

    if ($text -match '^offset = (?<value>\d+);$') {
        return "offset = $($Matches.value);"
    }

    # font[198].image()[ 1 + font[198].width() ] reads one pixel of the sprite's own buffer.
    $text = $text -replace 'font\[(?<index>[^\]]+)\]\.image\(\)\[\s*(?<x>[^\]]+?)\s*\+\s*font\[\k<index>\]\.width\(\)\s*\]', 'font[${index}].Image[${x}, 0]'

    # The reference's blocks are dropped, so the local Image of a block gets a name of its own.
    if ($Temp -ne '') {
        $text = $text -replace '\btemp\b', $Temp
    }

    # const fheroes2::Image temp = fheroes2::Flip( font[254], true, false );
    if ($text -match '^const fheroes2::Image (?<name>\w+) = fheroes2::Flip\((?<args>.*)\);$') {
        $arguments = Split-Arguments $Matches.args
        $source = Convert-Expression $arguments[0] $Table
        $horizontal = if ($arguments[1] -eq 'true') { 'horizontal: true' } else { 'horizontal: false' }
        $vertical = if ($arguments[2] -eq 'true') { 'vertical: true' } else { 'vertical: false' }

        return "Sprite $($Matches.name) = $source.Flip($horizontal, $vertical);"
    }

    # font[138] = font[83];
    if ($text -match '^(?<target>font\[[^\]]+\]) = (?<source>font\[[^\]]+\]);$') {
        $target = Convert-Expression $Matches.target $Table
        $source = Convert-Expression $Matches.source $Table

        return "$target = $source.Clone();"
    }

    # font[138].resize( font[83].width(), font[83].height() + 3 );
    if ($text -match '^(?<target>font\[[^\]]+\])\.(?<member>\w+)\((?<args>.*)\);$') {
        $target = Convert-Expression $Matches.target $Table
        $member = $Matches.member
        $arguments = @(Split-Arguments $Matches.args | ForEach-Object { Convert-Expression $_ $Table })

        switch ($member) {
            'resize' {
                $width = Convert-Expression $arguments[0] $Table
                $height = Convert-Expression $arguments[1] $Table

                # The reference keeps the anchor of the resized sprite, and every generated letter is
                # placed again by setPosition below, so carrying the offsets over is enough.
                return "$target = new Sprite($width, $height, $target.OffsetX, $target.OffsetY);"
            }
            'reset' {
                return "$target.Reset();"
            }
            'setPosition' {
                $x = Convert-Expression $arguments[0] $Table
                $y = Convert-Expression $arguments[1] $Table

                return "$target = At($target, $x, $y);"
            }
            default {
                throw "Unsupported member call: $_"
            }
        }
    }

    if ($text -match '^(?<name>[\w:]+)\((?<args>.*)\);$') {
        $name = $Matches.name
        $arguments = @(Split-Arguments $Matches.args | ForEach-Object { Convert-Expression $_ $Table })

        switch ($name) {
            'fheroes2::Copy' {
                $source = Convert-Expression $arguments[0] $Table
                $destination = Convert-Expression $arguments[3] $Table
                $width = Convert-Expression $arguments[6] $Table
                $height = Convert-Expression $arguments[7] $Table

                return "ImageOps.Copy($source, new Rect($($arguments[1]), $($arguments[2]), $width, $height), $destination, new Point($($arguments[4]), $($arguments[5])));"
            }
            'fheroes2::Flip' {
                $source = Convert-Expression $arguments[0] $Table
                $destination = Convert-Expression $arguments[3] $Table
                $width = Convert-Expression $arguments[6] $Table
                $height = Convert-Expression $arguments[7] $Table
                $horizontal = if ($arguments[8] -eq 'true') { 'true' } else { 'false' }
                $vertical = if ($arguments[9] -eq 'true') { 'true' } else { 'false' }

                return "ImageOps.FlipRegion($source, new Rect($($arguments[1]), $($arguments[2]), $width, $height), $destination, new Point($($arguments[4]), $($arguments[5])), horizontally: $horizontal, vertically: $vertical);"
            }
            'fheroes2::FillTransform' {
                $target = Convert-Expression $arguments[0] $Table

                return "ImageOps.FillTransform($target, new Rect($($arguments[1]), $($arguments[2]), $($arguments[3]), $($arguments[4])), $($arguments[5]));"
            }
            'fheroes2::Fill' {
                $target = Convert-Expression $arguments[0] $Table

                return "ImageOps.FillSprite($target, new Rect($($arguments[1]), $($arguments[2]), $($arguments[3]), $($arguments[4])), $($arguments[5]));"
            }
            'fheroes2::SetTransformPixel' {
                $target = Convert-Expression $arguments[0] $Table

                return "ImageOps.FillTransform($target, new Rect($($arguments[1]), $($arguments[2]), 1, 1), $($arguments[3]));"
            }
            'updateNormalFontLetterShadow' {
                return "UpdateNormalFontLetterShadow($(Convert-Expression $arguments[0] $Table));"
            }
            'updateSmallFontLetterShadow' {
                return "UpdateSmallFontLetterShadow($(Convert-Expression $arguments[0] $Table));"
            }
            'removeShadows' {
                return "RemoveShadows($(Convert-Expression $arguments[0] $Table));"
            }
            default {
                throw "Unsupported call: $_"
            }
        }
    }

    throw "Unsupported statement: $_"
}

function Convert-Function([hashtable]$Function) {
    $body = Get-Body $Function.Reference
    $normal = [System.Collections.Generic.List[string]]::new()
    $small = [System.Collections.Generic.List[string]]::new()
    $output = $normal
    $table = 'font'
    $pending = ''
    $index = 0

    # The reference opens a block for every local Image; C# would need braces of its own, and a block may
    # start in the normal half and end in the small one, so the blocks are dropped and the Image gets a
    # unique name instead.
    $tempNames = [System.Collections.Generic.List[string]]::new()
    $tempIndex = 0

    while ($index -lt $body.Count) {
        $line = $body[$index]
        $trimmed = $line.Trim()

        # Each function fills the normal table and then the small one, in that order; the two halves become
        # two C# methods because the port builds the two tables separately.
        if ($trimmed -match 'std::vector<fheroes2::Sprite> & font = icnVsSprite\[ICN::(?<icn>\w+)\];') {
            $table = if ($Matches.icn -eq 'FONT') { 'font' } else { 'small' }

            if ($Matches.icn -eq 'FONT') {
                $output = $normal
            }
            else {
                $output = $small
            }

            ++$index
            continue
        }

        if ($trimmed -eq 'resizeCodePage( icnVsSprite );' -or $trimmed -eq '') {
            ++$index
            continue
        }

        if ($trimmed -eq '{') {
            $tempNames.Add('')
            ++$index
            continue
        }

        if ($trimmed -eq '}') {
            $tempNames.RemoveAt($tempNames.Count - 1)
            ++$index
            continue
        }

        # Statements may span lines; they end with a semicolon or are a whole comment.
        $pending = if ($pending -eq '') { $trimmed } else { "$pending $trimmed" }

        if ($pending.StartsWith('//')) {
            $output.Add($pending)
            $pending = ''
            ++$index
            continue
        }

        if ($pending.EndsWith(';')) {
            if ($pending -match '^const fheroes2::Image temp = fheroes2::Flip\(') {
                ++$tempIndex
                $tempNames[$tempNames.Count - 1] = "temp$tempIndex"
            }

            $temp = ''
            for ($depth = $tempNames.Count - 1; $depth -ge 0; --$depth) {
                if ($tempNames[$depth] -ne '') {
                    $temp = $tempNames[$depth]
                    break
                }
            }

            $output.Add((Convert-Statement $pending $table $temp))
            $pending = ''
        }

        ++$index
    }

    return @{ Normal = $normal; Small = $small }
}

foreach ($function in $functions) {
    $halves = Convert-Function $function
    $builder = [System.Text.StringBuilder]::new()

    [void]$builder.AppendLine('using FHeroes2.Core.Geometry;')
    [void]$builder.AppendLine('using FHeroes2.Core.Imaging;')
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('namespace FHeroes2.Game.Gui;')
    [void]$builder.AppendLine()

    foreach ($half in @(@{ Suffix = 'Normal'; Table = 'font'; Statements = $halves.Normal },
                        @{ Suffix = 'Small'; Table = 'small'; Statements = $halves.Small })) {
        $icn = if ($half.Suffix -eq 'Normal') { 'FONT' } else { 'SMALFONT' }

        [void]$builder.AppendLine('/// <summary>')
        [void]$builder.AppendLine("/// The $($function.CodePage) letters of the $icn table, half of $($function.Reference)")
        [void]$builder.AppendLine("/// (ui_font.cpp:$($function.Line)). Generated by tools/GenerateFontAlphabets.ps1;")
        [void]$builder.AppendLine('/// do not edit, change the reference and regenerate.')
        [void]$builder.AppendLine('/// </summary>')
        [void]$builder.AppendLine('internal static partial class FontAlphabet')
        [void]$builder.AppendLine('{')
        [void]$builder.AppendLine("    private static void Generate$($function.CodePage)$($half.Suffix)(Sprite[] $($half.Table))")
        [void]$builder.AppendLine('    {')

        foreach ($statement in $half.Statements) {
            [void]$builder.AppendLine("        $statement")
        }

        [void]$builder.AppendLine('    }')
        [void]$builder.AppendLine('}')
        [void]$builder.AppendLine()
    }

    $path = Join-Path $OutputDirectory "FontAlphabet.$($function.CodePage).cs"
    Set-Content -LiteralPath $path -Value $builder.ToString() -NoNewline
    Write-Host "Wrote $path ($($halves.Normal.Count + $halves.Small.Count) statements)"
}
