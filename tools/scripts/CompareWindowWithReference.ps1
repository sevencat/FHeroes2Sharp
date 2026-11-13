<#
.SYNOPSIS
    Compares the C# standard dialog window with the reference fheroes2 C++ implementation.

.DESCRIPTION
    A StandardWindow is assembled from three sprites and broken up by dithered seams and a gradient
    shadow, so its pixels can only be proven correct against the original engine. A unit test can
    check the geometry, not the artwork.

    'fh2_reference window' draws the window with the coordinate arithmetic of ui_window.cpp on
    synthetic canvases; 'FHeroes2.AssetProbe window' draws the same cases with FHeroes2.UI. Both
    write four 8 bit BMP files with identical names, and this script compares them with SHA-256.

    Build the pieces first:
        pwsh tools/scripts/BuildReferenceTool.ps1
        dotnet build tools/FHeroes2.AssetProbe

.EXAMPLE
    pwsh tools/scripts/CompareWindowWithReference.ps1

.EXAMPLE
    pwsh tools/scripts/CompareWindowWithReference.ps1 -DataDirectory 'D:\HoMM2' -MsysRoot 'C:\msys64\ucrt64'
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $DataDirectory = '',

    [Parameter(Mandatory = $false)]
    [string] $Reference = (Join-Path $PSScriptRoot '../../tools-output/reference/fh2_reference.exe'),

    [Parameter(Mandatory = $false)]
    [string] $Probe = (Join-Path $PSScriptRoot '../../tools/FHeroes2.AssetProbe/bin/Debug/net10.0/FHeroes2.AssetProbe.exe'),

    # Folder that holds the MSYS2 runtime the reference exporter links against.
    [Parameter(Mandatory = $false)]
    [string] $MsysRoot = 'D:\msys64\ucrt64',

    [Parameter(Mandatory = $false)]
    [string] $WorkDirectory = (Join-Path $PSScriptRoot '../../tools-output/window-compare')
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

$referenceDirectory = Join-Path $WorkDirectory 'reference'
$probeDirectory = Join-Path $WorkDirectory 'probe'

foreach ($directory in @($referenceDirectory, $probeDirectory)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

Write-Host "Archive: $archive"
Write-Host 'Drawing the windows with the reference engine ...'

# The exporter links the MSYS2 runtime, which is only found when its bin folder is on the PATH.
$msysBin = Join-Path $MsysRoot 'bin'
if (Test-Path -LiteralPath $msysBin) {
    $env:PATH = $msysBin + ';' + $env:PATH
}

$referenceOutput = & $Reference window $archive $referenceDirectory 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "The reference exporter failed: $(@($referenceOutput)[-1])"
}

Write-Host 'Drawing the windows with the C# port ...'
$probeOutput = & $Probe window $probeDirectory --agg $archive 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "The asset probe failed: $(@($probeOutput)[-1])"
}

$referenceFiles = @(Get-ChildItem -LiteralPath $referenceDirectory -Filter *.bmp -File | Sort-Object Name)
if ($referenceFiles.Count -eq 0) {
    throw "The reference exporter wrote no files to '$referenceDirectory'."
}

$differences = [System.Collections.Generic.List[string]]::new()
$matched = 0

foreach ($file in $referenceFiles) {
    $counterpart = Join-Path $probeDirectory $file.Name
    if (-not (Test-Path -LiteralPath $counterpart)) {
        $differences.Add("$($file.Name) is missing from the C# output")
        Write-Host ("DIFF  {0} (missing)" -f $file.Name)
        continue
    }

    if ((Get-FileDigest $file.FullName) -eq (Get-FileDigest $counterpart)) {
        $matched++
        Write-Host ("match {0}" -f $file.Name)
    }
    else {
        $differences.Add("$($file.Name) differs")
        Write-Host ("DIFF  {0}" -f $file.Name)
    }
}

foreach ($file in (Get-ChildItem -LiteralPath $probeDirectory -Filter *.bmp -File)) {
    if (-not (Test-Path -LiteralPath (Join-Path $referenceDirectory $file.Name))) {
        $differences.Add("$($file.Name) is only produced by the C# port")
    }
}

Write-Host ''
Write-Host ("Files: {0} of {1} are byte for byte identical" -f $matched, $referenceFiles.Count)

if ($differences.Count -gt 0) {
    Write-Host ''
    Write-Host 'Differences:'
    foreach ($difference in $differences) {
        Write-Host "  $difference"
    }

    exit 1
}

exit 0
