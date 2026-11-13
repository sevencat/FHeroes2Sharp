<#
.SYNOPSIS
    Shared plumbing of the pixel baseline scripts.

.DESCRIPTION
    VerifyMenuBaseline.ps1 and VerifySettingsBaseline.ps1 both render one 640x480 frame with the
    client, compare it against a committed PNG and do everything around it identically: look up the
    game data, build, validate the baseline file itself, and report the compare result with an
    optional tolerance. That part lives here; dot-source this file to use it:

        . (Join-Path $PSScriptRoot 'PixelBaseline.ps1')

    The specific part stays in the caller: which flags make its frame reproducible, where the
    baseline lives and which areas are excluded from the comparison.

    The functions print progress with Write-Host so a caller can keep its own narration. Failures
    that cannot be recovered from (a missing or corrupt baseline) exit with 1, exactly like the
    scripts did before the split.
#>

function Resolve-DataDirectory {
    param([string] $Directory)

    $candidates = @()
    if ($Directory) {
        $candidates += $Directory
    }
    elseif ($env:FHEROES2_DATA) {
        $candidates += $env:FHEROES2_DATA
    }

    # Same development fallback as FHeroes2.Assets.AssetLocator.
    $candidates += 'F:\game\fh2'

    foreach ($candidate in $candidates) {
        # The client wants the folder that contains 'Data' and 'files', which is what the
        # environment variable and the fallback point at.
        if (Test-Path -LiteralPath (Join-Path $candidate 'Data\Heroes2.agg')) {
            return (Get-Item -LiteralPath $candidate).FullName
        }

        # A caller may pass the 'Data' folder itself.
        if (Test-Path -LiteralPath (Join-Path $candidate 'Heroes2.agg')) {
            return (Split-Path -Parent (Get-Item -LiteralPath $candidate).FullName)
        }
    }

    return $null
}

function Invoke-Build {
    param([string] $Project)

    $output = & dotnet build $Project --nologo 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | Select-Object -Last 15 | ForEach-Object { Write-Host "        $_" }
        throw "dotnet build '$Project' failed with exit code $LASTEXITCODE."
    }
}

# PNG stores its integers big endian. -shl on a [byte] operand stays inside the byte width
# (0x02 -shl 8 evaluates to 0), so every operand is widened before the shift.
function Read-BigEndianInt32 {
    param([byte[]] $Bytes, [int] $Offset)

    $value = 0
    for ($index = 0; $index -lt 4; $index++) {
        $value = ($value -shl 8) -bor [int]$Bytes[$Offset + $index]
    }

    return $value
}

# The baseline is the artefact under test, so it is checked even when no game data exists: a
# deleted or wrongly sized baseline has to fail on a machine that can only run the cheap part
# (that is exactly what a hosted CI runner does).
function Assert-PngBaseline {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Host ("error  baseline '{0}' does not exist; run with -Update to create it." -f $Path)
        exit 1
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 24) {
        Write-Host ("error  baseline '{0}' is not a PNG file ({1} bytes)." -f $Path, $bytes.Length)
        exit 1
    }

    $signature = -join ($bytes[0..7] | ForEach-Object { $_.ToString('X2') })
    $chunk = [System.Text.Encoding]::ASCII.GetString($bytes, 12, 4)
    if ($signature -ne '89504E470D0A1A0A' -or $chunk -ne 'IHDR') {
        Write-Host ("error  baseline '{0}' is not a PNG file." -f $Path)
        exit 1
    }

    # The client renders on a fixed 640x480 logical surface; a baseline of another size can only
    # be a mistake (for example a 2x screenshot).
    $width = Read-BigEndianInt32 -Bytes $bytes -Offset 16
    $height = Read-BigEndianInt32 -Bytes $bytes -Offset 20
    if ($width -ne 640 -or $height -ne 480) {
        Write-Host ("error  baseline '{0}' is {1}x{2}; the logical surface is 640x480." -f $Path, $width, $height)
        exit 1
    }
}

# Runs the client in smoke mode and keeps the screenshot it writes. The client output is only
# echoed when something went wrong; a successful run stays silent so the caller's narration is
# the only thing on screen.
function Invoke-PixelRender {
    param(
        [string] $Executable,
        [string[]] $Arguments,
        [string] $ScreenshotPath
    )

    if (Test-Path -LiteralPath $ScreenshotPath) {
        Remove-Item -LiteralPath $ScreenshotPath
    }

    $output = @(& $Executable @Arguments 2>&1)
    $exit = $LASTEXITCODE

    if ($exit -ne 0) {
        $output | Select-Object -Last 15 | ForEach-Object { Write-Host "        $_" }
        throw "'$Executable' exited with code $exit."
    }

    if (-not (Test-Path -LiteralPath $ScreenshotPath)) {
        $output | Select-Object -Last 15 | ForEach-Object { Write-Host "        $_" }
        throw "'$Executable' did not write '$ScreenshotPath'."
    }
}

# Wraps `AssetProbe compare`: exit code 0 = identical, 3 = pixels differ, 1 = bad arguments. The
# ignored areas are the ones no flag can pin (the status bar prints the FPS counter and the
# version string, both of which move between builds).
function Invoke-PixelCompare {
    param(
        [string] $ProbeExecutable,
        [string] $Rendered,
        [string] $Baseline,
        [string] $DiffDirectory,
        [string[]] $Ignore
    )

    $arguments = @('compare', $Rendered, $Baseline, '--diff', $DiffDirectory)
    foreach ($area in $Ignore) {
        $arguments += @('--ignore', $area)
    }

    $output = @(& $ProbeExecutable @arguments 2>&1)

    return [pscustomobject] @{
        ExitCode = $LASTEXITCODE
        Lines    = $output
        Text     = ($output -join [Environment]::NewLine)
    }
}

# Prints the verdict and returns the exit code the caller should end with. A tolerance of 0 means
# byte for byte identity; a positive one accepts up to that many differing pixels, which is what
# the probe's "N of M pixels differ" line reports.
function Complete-PixelCompare {
    param(
        [object] $Compare,
        [string] $Subject,
        [string] $Baseline,
        [string] $DiffDirectory,
        [int] $Tolerance = 0
    )

    if ($Compare.ExitCode -eq 0) {
        Write-Host ("match  the rendered {0} is identical to the baseline" -f $Subject)
        return 0
    }

    if ($Compare.Text -match '(\d+) of (\d+) pixels differ') {
        $differing = [int]$Matches[1]
        $total = [int]$Matches[2]

        if ($differing -le $Tolerance) {
            Write-Host ("match  {0} of {1} pixels differ, tolerance is {2}" -f $differing, $total, $Tolerance)
            return 0
        }

        Write-Host ("DIFF   {0} of {1} pixels differ, tolerance is {2}" -f $differing, $total, $Tolerance)
    }
    else {
        Write-Host 'DIFF   the frames have different sizes (see the probe output above)'
    }

    # AssetProbe names the image after the file it compared against: menu.png -> diff_menu.png.
    $diffName = 'diff_{0}.png' -f [System.IO.Path]::GetFileNameWithoutExtension($Baseline)
    Write-Host ("       diff image: {0}" -f (Join-Path (Join-Path $DiffDirectory 'diff') $diffName))
    Write-Host '       If the change is intended, run with -Update and note it in tests/baselines/README.md.'
    return 1
}
