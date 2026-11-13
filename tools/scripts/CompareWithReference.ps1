<#
.SYNOPSIS
    Compares the C# exporters with the reference fheroes2 C++ implementation, byte for byte.

.DESCRIPTION
    Both FHeroes2.AssetProbe (--format bmp) and tools/reference/fh2_reference.exe decode the same
    archive entry with independent implementations and write 8 bit indexed BMP files with
    identical names. This script runs both and compares the files with SHA-256, so every pixel,
    both headers and the complete palette have to match.

    Build the pieces first:
        pwsh tools/scripts/BuildReferenceTool.ps1
        dotnet build tools/FHeroes2.AssetProbe

    Fixing the background index matters: the reference engine exports sprites and cursors over
    palette index 23, so the probe is called with --background 23 as well.

.EXAMPLE
    pwsh tools/scripts/CompareWithReference.ps1

.EXAMPLE
    pwsh tools/scripts/CompareWithReference.ps1 -All

.EXAMPLE
    pwsh tools/scripts/CompareWithReference.ps1 -Entries GROUND32.TIL,ADVMCO.ICN -DataDirectory 'D:\HoMM2'
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $DataDirectory = '',

    [Parameter(Mandatory = $false)]
    [string] $Reference = (Join-Path $PSScriptRoot '../../tools-output/reference/fh2_reference.exe'),

    [Parameter(Mandatory = $false)]
    [string] $Probe = (Join-Path $PSScriptRoot '../../tools/FHeroes2.AssetProbe/bin/Debug/net10.0/FHeroes2.AssetProbe.exe'),

    [Parameter(Mandatory = $false)]
    [string[]] $Entries = @(
        'GROUND32.TIL',
        'CLOF32.TIL',
        'STON.TIL',
        'HEROES.ICN',
        'ADVMCO.ICN',
        'SPELCO.ICN',
        'MINIMON.ICN',
        'ADVBORD.ICN',
        'HOURGLAS.ICN',
        'ESCROLL.ICN',
        'EDITOR.ICN',
        'ADVMBW01.BMP',
        'ADVMBW16.BMP',
        'SPELBW00.BMP'
    ),

    [Parameter(Mandatory = $false)]
    [int] $Background = 23,

    [Parameter(Mandatory = $false)]
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/reference-compare'),

    # Compare every ICN, TIL and BMP entry of the archive instead of the curated list.
    [switch] $All
)

$ErrorActionPreference = 'Stop'

function Resolve-ArchivePath {
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
        foreach ($file in @((Join-Path $candidate 'Heroes2.agg'), (Join-Path $candidate 'Data\Heroes2.agg'))) {
            if (Test-Path -LiteralPath $file) {
                return (Get-Item -LiteralPath $file).FullName
            }
        }
    }

    throw "Heroes2.agg was not found. Pass -DataDirectory or set the FHEROES2_DATA environment variable."
}

function Get-FileDigest {
    param([string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

$archive = Resolve-ArchivePath $DataDirectory
$Reference = [System.IO.Path]::GetFullPath($Reference)
$Probe = [System.IO.Path]::GetFullPath($Probe)
$WorkDirectory = [System.IO.Path]::GetFullPath($WorkDirectory)

if (-not (Test-Path -LiteralPath $Reference)) {
    throw "'$Reference' does not exist. Run tools/scripts/BuildReferenceTool.ps1 first."
}

if (-not (Test-Path -LiteralPath $Probe)) {
    throw "'$Probe' does not exist. Run 'dotnet build tools/FHeroes2.AssetProbe' first."
}

if ($All) {
    $listing = & $Probe list --agg $archive
    if ($LASTEXITCODE -ne 0) {
        throw "Listing '$archive' failed with exit code $LASTEXITCODE."
    }

    $Entries = @($listing |
        Where-Object { $_ -match '\.(ICN|TIL|BMP)\t' } |
        ForEach-Object { ($_ -split "`t")[0] })
}

Write-Host "Archive:   $archive"
Write-Host "Reference: $Reference"
Write-Host "Probe:     $Probe"
Write-Host "Entries:   $($Entries.Count), background index $Background"
Write-Host ''

$fileTotal = 0
$fileIdentical = 0
$entryMatched = 0
$entryDiffered = 0
$differences = [System.Collections.Generic.List[string]]::new()

foreach ($entry in $Entries) {
    $extension = [System.IO.Path]::GetExtension($entry).ToUpperInvariant()

    switch ($extension) {
        '.ICN' {
            $referenceCommand = 'icn'
            $probeCommand = 'sprites'
            $usesBackground = $true
        }
        '.TIL' {
            $referenceCommand = 'til'
            $probeCommand = 'tiles'
            $usesBackground = $false
        }
        '.BMP' {
            $referenceCommand = 'cursor'
            $probeCommand = 'cursor'
            $usesBackground = $true
        }
        default {
            Write-Host "skip  $entry (unsupported extension)"
            continue
        }
    }

    $folder = $entry -replace '[^A-Za-z0-9]', '_'
    $referenceDirectory = Join-Path $WorkDirectory "reference/$folder"
    $probeDirectory = Join-Path $WorkDirectory "probe/$folder"

    foreach ($directory in @($referenceDirectory, $probeDirectory)) {
        if (Test-Path -LiteralPath $directory) {
            Remove-Item -LiteralPath $directory -Recurse -Force
        }

        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    $referenceArguments = @($referenceCommand, $archive, $entry, $referenceDirectory)
    if ($usesBackground) {
        $referenceArguments += $Background
    }

    $referenceOutput = & $Reference @referenceArguments 2>&1
    $referenceExit = $LASTEXITCODE

    $probeArguments = @($probeCommand, $entry, $probeDirectory, '--agg', $archive, '--format', 'bmp')
    if ($usesBackground) {
        $probeArguments += @('--background', $Background)
    }

    $probeOutput = & $Probe @probeArguments 2>&1
    $probeExit = $LASTEXITCODE

    if ($referenceExit -ne 0 -and $probeExit -ne 0) {
        # Both implementations reject the entry (for example a BMP that is not a cursor).
        Write-Host "skip  $entry (rejected by both: $(@($referenceOutput)[-1]))"
        continue
    }

    if ($referenceExit -ne 0) {
        $differences.Add("$entry reference failed with exit code $referenceExit`: $(@($referenceOutput)[-1])")
        $entryDiffered++
        continue
    }

    if ($probeExit -ne 0) {
        $differences.Add("$entry probe failed with exit code $probeExit`: $(@($probeOutput)[-1])")
        $entryDiffered++
        continue
    }

    $referenceFiles = @(Get-ChildItem -LiteralPath $referenceDirectory -Filter *.bmp -File | Sort-Object Name)
    $probeFiles = @(Get-ChildItem -LiteralPath $probeDirectory -Filter *.bmp -File | Sort-Object Name)

    $entryDifferences = [System.Collections.Generic.List[string]]::new()
    if ($referenceFiles.Count -ne $probeFiles.Count) {
        $entryDifferences.Add("$entry file count $($probeFiles.Count) instead of $($referenceFiles.Count)")
    }

    $probeNames = @{}
    foreach ($file in $probeFiles) {
        $probeNames[$file.Name] = $true
    }

    $entryIdentical = 0
    foreach ($file in $referenceFiles) {
        $fileTotal++

        $counterpart = Join-Path $probeDirectory $file.Name
        if (-not (Test-Path -LiteralPath $counterpart)) {
            $entryDifferences.Add("$entry/$($file.Name) is missing from the probe output")
            continue
        }

        if ((Get-FileDigest $file.FullName) -eq (Get-FileDigest $counterpart)) {
            $fileIdentical++
            $entryIdentical++
        }
        else {
            $entryDifferences.Add("$entry/$($file.Name) differs")
        }
    }

    foreach ($name in $probeNames.Keys) {
        if (-not (Test-Path -LiteralPath (Join-Path $referenceDirectory $name))) {
            $entryDifferences.Add("$entry/$name is only produced by the probe")
        }
    }

    if ($entryDifferences.Count -eq 0) {
        $entryMatched++
        Write-Host ("match {0,-16} {1,5} files" -f $entry, $entryIdentical)
    }
    else {
        $entryDiffered++
        Write-Host ("DIFF  {0,-16} {1,5} of {2} files match" -f $entry, $entryIdentical, $referenceFiles.Count)
        foreach ($difference in $entryDifferences | Select-Object -First 5) {
            Write-Host "        $difference"
        }

        $differences.AddRange($entryDifferences)
    }
}

Write-Host ''
Write-Host ("Entries: $entryMatched matched, $entryDiffered differed")
Write-Host ("Files:   $fileIdentical of $fileTotal are byte for byte identical")

if ($entryDiffered -gt 0) {
    Write-Host ''
    Write-Host 'Differences:'
    foreach ($difference in $differences | Select-Object -First 20) {
        Write-Host "  $difference"
    }

    exit 1
}

exit 0
