<#
.SYNOPSIS
    Rewrites the five generateCPxxxxGoodButtonFont functions of the reference ui_font.cpp as C# data.

.DESCRIPTION
    The button font has to carry the same letters as the normal font, and the reference draws them the
    same way: clone an ASCII letter, point a couple of pixels, add the diacritic with Copy / DrawLine /
    SetPixel. Those are ~1 400 lines of pure data, so they are transcribed by this script instead of
    by hand, exactly like GenerateFontAlphabets.ps1 does for the normal font.

    Every function becomes one method of the partial class ButtonFont:

        private static void GenerateCp1250Button( Sprite[] released )

    The array it receives is already 256 entries long: the hand written Create( language ) widens the
    ASCII base shape first, which is what the reference's
    "released.insert( released.end(), codePageExtraCharacterCount, firstSprite )" does.

    The reference reuses two-step "resize + reset" pairs and setPosition, neither of which exists on the
    port's immutable Sprite; they are mapped to the helpers ButtonFont.Create / ButtonFont.Moved.

    Run from anywhere:
        pwsh tools/GenerateButtonFontAlphabets.ps1

    The reference file defaults to F:\code\fheroes2 (the path in the project notes).
#>
param(
    [string]$Reference = 'F:\code\fheroes2\src\fheroes2\gui\ui_font.cpp',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../src/FHeroes2.Game/Gui')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$functions = @(
    @{ Name = 'generateCP1250GoodButtonFont'; Method = 'GenerateCp1250Button'; Page = 'Cp1250' },
    @{ Name = 'generateCP1251GoodButtonFont'; Method = 'GenerateCp1251Button'; Page = 'Cp1251' },
    @{ Name = 'generateCP1252GoodButtonFont'; Method = 'GenerateCp1252Button'; Page = 'Cp1252' },
    @{ Name = 'generateCP1253GoodButtonFont'; Method = 'GenerateCp1253Button'; Page = 'Cp1253' },
    @{ Name = 'generateCP1254GoodButtonFont'; Method = 'GenerateCp1254Button'; Page = 'Cp1254' }
)

function Get-FunctionBody {
    param([string[]]$Lines, [string]$Name)

    $start = -1
    for ($index = 0; $index -lt $Lines.Count; ++$index) {
        if ($Lines[$index] -match ('^\s*void\s+' + [regex]::Escape($Name) + '\s*\(')) {
            $start = $index
            break
        }
    }
    if ($start -lt 0) {
        throw "Function $Name not found in the reference."
    }

    $open = -1
    for ($index = $start; $index -lt $Lines.Count; ++$index) {
        if ($Lines[$index] -match '^\s*\{\s*$') {
            $open = $index
            break
        }
    }
    if ($open -lt 0) {
        throw "Opening brace of $Name not found."
    }

    $depth = 0
    for ($index = $open; $index -lt $Lines.Count; ++$index) {
        $text = $Lines[$index]
        $depth += ([regex]::Matches($text, '\{')).Count
        $depth -= ([regex]::Matches($text, '\}')).Count

        if ($depth -eq 0) {
            return $Lines[($open + 1)..($index - 1)]
        }
    }

    throw "Closing brace of $Name not found."
}

function Split-Arguments {
    param([string]$Text)

    $result = New-Object System.Collections.Generic.List[string]
    $depth = 0
    $current = ''

    foreach ($character in $Text.ToCharArray()) {
        if ($character -eq '(' -or $character -eq '{' -or $character -eq '[') { $depth++ }
        elseif ($character -eq ')' -or $character -eq '}' -or $character -eq ']') { $depth-- }

        if ($character -eq ',' -and $depth -eq 0) {
            $result.Add($current.Trim())
            $current = ''
        }
        else {
            $current += $character
        }
    }

    if ($current.Trim().Length -gt 0) {
        $result.Add($current.Trim())
    }

    return , $result
}

function Convert-Expression {
    param([string]$Text)

    $value = $Text.Trim()
    $value = $value -replace '\.width\(\)', '.Width'
    $value = $value -replace '\.height\(\)', '.Height'
    $value = $value -replace '\.x\(\)', '.OffsetX'
    $value = $value -replace '\.y\(\)', '.OffsetY'
    $value = $value -replace 'buttonFontOffset\.x', 'FontOffset.X'
    $value = $value -replace 'buttonFontOffset\.y', 'FontOffset.Y'
    $value = $value -replace 'buttonGoodReleasedColor', 'GoodReleasedColour'
    $value = $value -replace 'bigFontSpaceWidth', 'BigSpaceWidth'
    $value = $value -replace '\bcharCode\b', 'charCode'

    return $value
}

function Get-CallArguments {
    param([string]$Statement)

    $open = $Statement.IndexOf('(')
    $close = $Statement.LastIndexOf(')')
    $inner = $Statement.Substring($open + 1, $close - $open - 1)

    return , (Split-Arguments $inner)
}

function Convert-PointLiteral {
    param([string]$Text)

    $inner = $Text.Trim()
    if ($inner.StartsWith('{') -and $inner.EndsWith('}')) {
        $inner = $inner.Substring(1, $inner.Length - 2)
    }

    $parts = Split-Arguments $inner
    if ($parts.Count -ne 2) {
        throw "Not a point literal: $Text"
    }

    return 'new Point(' + (Convert-Expression $parts[0]) + ', ' + (Convert-Expression $parts[1]) + ')'
}

function Convert-Statement {
    param([string]$Statement)

    $text = $Statement.Trim()

    if ($text -eq '}') {
        return '}'
    }

    if ($text -match '^for \( const int charCode : \{(?<list>[^}]*)\} \)\s*\{$') {
        return 'foreach (int charCode in new[] { ' + $Matches.list.Trim() + ' })'
    }

    if ($text -match '^(?<target>released\[[^\]]+\])\.setPosition\((?<args>.*)\);$') {
        $arguments = Split-Arguments $Matches.args
        return $Matches.target + ' = Moved(' + $Matches.target + ', ' + (Convert-Expression $arguments[0]) + ', ' + (Convert-Expression $arguments[1]) + ');'
    }

    if ($text -match '^(?<target>released\[[^\]]+\])\.resize\((?<args>.*)\);$') {
        $arguments = Split-Arguments $Matches.args
        return $Matches.target + ' = Create(' + (Convert-Expression $arguments[0]) + ', ' + (Convert-Expression $arguments[1]) + ', ' + $Matches.target + ');'
    }

    if ($text -match '^(?<target>released\[[^\]]+\]) = (?<source>released\[[^\]]+\]);$') {
        return $Matches.target + ' = ' + $Matches.source + '.Clone();'
    }

    if ($text -match '^fheroes2::Copy\(') {
        $arguments = Get-CallArguments $text
        $source = Convert-Expression $arguments[0]
        $destination = Convert-Expression $arguments[3]
        $rect = 'new Rect(' + (Convert-Expression $arguments[1]) + ', ' + (Convert-Expression $arguments[2]) + ', ' + (Convert-Expression $arguments[6]) + ', ' + (Convert-Expression $arguments[7]) + ')'
        $point = 'new Point(' + (Convert-Expression $arguments[4]) + ', ' + (Convert-Expression $arguments[5]) + ')'

        return "ImageOps.Copy($source, $rect, $destination, $point);"
    }

    if ($text -match '^fheroes2::SetPixel\(') {
        $arguments = Get-CallArguments $text
        $target = Convert-Expression $arguments[0]

        return 'ImageOps.SetPixel(' + $target + ', ' + (Convert-Expression $arguments[1]) + ', ' + (Convert-Expression $arguments[2]) + ', ' + (Convert-Expression $arguments[3]) + ');'
    }

    if ($text -match '^fheroes2::DrawLine\(') {
        $arguments = Get-CallArguments $text
        $target = Convert-Expression $arguments[0]

        return 'ImageOps.DrawLine(' + $target + ', ' + (Convert-PointLiteral $arguments[1]) + ', ' + (Convert-PointLiteral $arguments[2]) + ', ' + (Convert-Expression $arguments[3]) + ');'
    }

    if ($text -match '^fheroes2::FillTransform\(') {
        $arguments = Get-CallArguments $text
        $target = Convert-Expression $arguments[0]
        $rect = 'new Rect(' + (Convert-Expression $arguments[1]) + ', ' + (Convert-Expression $arguments[2]) + ', ' + (Convert-Expression $arguments[3]) + ', ' + (Convert-Expression $arguments[4]) + ')'

        return "ImageOps.FillTransform($target, $rect, " + (Convert-Expression $arguments[5]) + ');'
    }

    if ($text -match '^fheroes2::SetTransformPixel\(') {
        $arguments = Get-CallArguments $text
        $target = Convert-Expression $arguments[0]

        return 'ImageOps.SetTransformPixel(' + $target + ', ' + (Convert-Expression $arguments[1]) + ', ' + (Convert-Expression $arguments[2]) + ', ' + (Convert-Expression $arguments[3]) + ');'
    }

    if ($text -match '^fheroes2::Flip\(') {
        $arguments = Get-CallArguments $text
        $source = Convert-Expression $arguments[0]
        $destination = Convert-Expression $arguments[3]
        $rect = 'new Rect(' + (Convert-Expression $arguments[1]) + ', ' + (Convert-Expression $arguments[2]) + ', ' + (Convert-Expression $arguments[6]) + ', ' + (Convert-Expression $arguments[7]) + ')'
        $point = 'new Point(' + (Convert-Expression $arguments[4]) + ', ' + (Convert-Expression $arguments[5]) + ')'

        return "ImageOps.FlipRegion($source, $rect, $destination, $point, " + (Convert-Expression $arguments[8]) + ', ' + (Convert-Expression $arguments[9]) + ');'
    }

    if ($text -match '^fheroes2::Blit\(') {
        $arguments = Get-CallArguments $text
        $source = Convert-Expression $arguments[0]
        $destination = Convert-Expression $arguments[1]

        return "ImageOps.FlipRegion($source, $source.Image.Bounds, $destination, Point.Zero, true, false);"
    }

    throw "No conversion for: $text"
}

function Convert-Function {
    param([string[]]$Body, [string]$Method, [string]$Page, [string]$Name, [int]$ReferenceLine)

    $output = New-Object System.Collections.Generic.List[string]
    $output.Add('    /// <summary>')
    $output.Add("    /// The $Page letters of the good released button face, the body of")
    $output.Add("    /// <c>${Name}()</c> (<c>ui_font.cpp:$ReferenceLine</c>).")
    $output.Add('    /// </summary>')
    $output.Add('    /// <param name="released">The good released face, 256 code point indexed frames.</param>')
    $output.Add("    private static void $Method(Sprite[] released)")
    $output.Add('    {')
    $output.Add('        const int offset = 2;')

    $indent = 8
    $buffer = ''
    $skipReset = $false

    foreach ($line in $Body) {
        $text = $line.Trim()

        if ($text.Length -eq 0) {
            $output.Add('')
            continue
        }

        if ($buffer.Length -eq 0 -and $text.StartsWith('//')) {
            $output.Add((' ' * $indent) + $text)
            continue
        }

        # The prologue widens the vector; the hand written Create( language ) does that already.
        if ($buffer.Length -eq 0 -and ($text -match '^(const fheroes2::Sprite firstSprite|released\.insert\()')) {
            continue
        }

        if ($buffer.Length -eq 0 -and ($text -match '^const int32_t offset = 2;$')) {
            continue
        }

        # "resize + reset" is one Create() call.
        if ($buffer.Length -eq 0 -and $skipReset) {
            if ($text -match '^released\[\d+\]\.reset\(\);$') {
                $skipReset = $false
                continue
            }

            $skipReset = $false
        }

        if ($buffer.Length -eq 0 -and $text -eq '}') {
            $indent -= 4
            $output.Add((' ' * $indent) + '}')
            continue
        }

        $buffer = if ($buffer.Length -eq 0) { $text } else { $buffer + ' ' + $text }

        if ($buffer.EndsWith('{')) {
            $converted = Convert-Statement $buffer
            $output.Add((' ' * $indent) + $converted)
            $output.Add((' ' * $indent) + '{')
            $indent += 4
            $buffer = ''
            continue
        }

        if (-not $buffer.EndsWith(';')) {
            continue
        }

        $converted = Convert-Statement $buffer

        if ($buffer -match '\.resize\(') {
            $skipReset = $true
        }

        $output.Add((' ' * $indent) + $converted)
        $buffer = ''
    }

    if ($buffer.Length -ne 0) {
        throw "Unfinished statement at the end of ${Method}: $buffer"
    }

    $output.Add('    }')

    return $output
}

$lines = Get-Content -Path $Reference -Encoding utf8
$check = [System.Text.UTF8Encoding]::new($false)

foreach ($function in $functions) {
    $body = Get-FunctionBody -Lines $lines -Name $function.Name
    $referenceLine = 0
    for ($index = 0; $index -lt $lines.Count; ++$index) {
        if ($lines[$index] -match ('^\s*void\s+' + [regex]::Escape($function.Name) + '\s*\(')) {
            $referenceLine = $index + 1
            break
        }
    }

    $method = Convert-Function -Body $body -Method $function.Method -Page $function.Page -Name $function.Name -ReferenceLine $referenceLine

    $text = New-Object System.Collections.Generic.List[string]
    $text.Add('// <auto-generated>')
    $text.Add('// Generated by tools/GenerateButtonFontAlphabets.ps1 from the reference ui_font.cpp.')
    $text.Add('// Do not edit; change the reference or the script and run the script again.')
    $text.Add('// </auto-generated>')
    $text.Add('')
    $text.Add('using FHeroes2.Core.Geometry;')
    $text.Add('using FHeroes2.Core.Imaging;')
    $text.Add('')
    $text.Add('namespace FHeroes2.Game.Gui;')
    $text.Add('')
    $text.Add('internal static partial class ButtonFont')
    $text.Add('{')
    foreach ($line in $method) { $text.Add($line) }
    $text.Add('}')

    $path = Join-Path $OutputDirectory ('ButtonFont.{0}.cs' -f $function.Page)
    [System.IO.File]::WriteAllText($path, ($text -join "`r`n") + "`r`n", $check)

    $statements = ($method | Where-Object { $_ -match ';\s*$|\}$' }).Count
    Write-Host ("{0}: {1} lines" -f $path, $method.Count)
}
