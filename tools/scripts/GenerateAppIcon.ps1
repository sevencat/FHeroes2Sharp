<#
.SYNOPSIS
    Regenerates assets/logo.ico, the application and window icon, from assets/logo.png.

.DESCRIPTION
    The desktop client embeds the icon file both as the executable icon (MSBuild
    ApplicationIcon) and as an EmbeddedResource that SdlPresenter hands to
    SDL_SetWindowIcon, so one file has to serve Windows Explorer and SDL at once.

    An .ico is an ICONDIR header, one 16 byte entry per image, then the payloads.
    Small images are written as uncompressed 32 bit bitmaps (BITMAPINFOHEADER with a
    doubled height, bottom-up BGRA rows and an all-zero AND mask) because every Windows
    version reads them; images above 64 pixels are written as PNG, which Vista and later
    support and which keeps the file small. Src/FHeroes2.Core/Imaging/IcoFile.cs is the
    reader; its tests build icons in memory, so this script is the only producer.

    Run it after changing assets/logo.png, then commit both files.

.EXAMPLE
    pwsh tools/scripts/GenerateAppIcon.ps1

.EXAMPLE
    pwsh tools/scripts/GenerateAppIcon.ps1 -Sizes 16,32,48,256 -Destination tools-output/logo.ico
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $Source = (Join-Path $PSScriptRoot '../../assets/logo.png'),

    [Parameter(Mandatory = $false)]
    [string] $Destination = (Join-Path $PSScriptRoot '../../assets/logo.ico'),

    [Parameter(Mandatory = $false)]
    [int[]] $Sizes = @(16, 24, 32, 48, 64, 128, 256)
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Source)) {
    throw "The source image '$Source' does not exist."
}

$Sizes = @($Sizes | Sort-Object -Unique)
if ($Sizes.Count -eq 0) {
    throw 'At least one icon size is required.'
}

foreach ($size in $Sizes) {
    if ($size -lt 1 -or $size -gt 256) {
        throw "The icon size $size is outside 1..256 (the entry header stores one byte per edge)."
    }
}

Add-Type -AssemblyName System.Drawing

# 64 px and below become bitmaps, everything above becomes PNG (see the description above).
$PngFrom = 65

function ConvertTo-IconBitmap {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Image] $Image,
        [Parameter(Mandatory = $true)] [int] $Size
    )

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.DrawImage($Image, 0, 0, $Size, $Size)
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

function Read-BgraPixels {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Bitmap] $Bitmap
    )

    $rect = New-Object System.Drawing.Rectangle(0, 0, $Bitmap.Width, $Bitmap.Height)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $buffer = New-Object byte[] ($stride * $Bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
        return @{ Pixels = $buffer; Stride = $stride }
    }
    finally {
        $Bitmap.UnlockBits($data)
    }
}

function Write-IconBitmapPayload {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Bitmap] $Bitmap,
        [Parameter(Mandatory = $true)] [System.IO.Stream] $Destination
    )

    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $pixels = Read-BgraPixels -Bitmap $Bitmap
    $colorStride = $width * 4
    $maskStride = [int]([math]::Floor(($width + 31) / 32)) * 4

    $writer = New-Object System.IO.BinaryWriter($Destination)
    # BITMAPINFOHEADER: the height covers the colour rows and the AND mask rows.
    $writer.Write([int]40)
    $writer.Write([int]$width)
    $writer.Write([int]($height * 2))
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]0)
    $writer.Write([int](($colorStride + $maskStride) * $height))
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)

    # Colour rows are stored bottom-up.
    for ($y = $height - 1; $y -ge 0; $y--) {
        $writer.Write($pixels.Pixels, $y * $pixels.Stride, $colorStride)
    }

    # The AND mask is zeroed: the alpha channel carries the transparency.
    $writer.Write([byte[]]::new($maskStride * $height))
    $writer.Flush()
}

function Write-IconPngPayload {
    param(
        [Parameter(Mandatory = $true)] [System.Drawing.Bitmap] $Bitmap,
        [Parameter(Mandatory = $true)] [System.IO.Stream] $Destination
    )

    $Bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
}

$image = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source).Path)
try {
    if ($image.Width -ne $image.Height) {
        throw "The source image is $($image.Width)x$($image.Height); an icon needs a square image."
    }

    $payloads = New-Object System.Collections.Generic.List[object]
    foreach ($size in $Sizes) {
        $bitmap = ConvertTo-IconBitmap -Image $image -Size $size
        try {
            $stream = New-Object System.IO.MemoryStream
            try {
                if ($size -lt $PngFrom) {
                    Write-IconBitmapPayload -Bitmap $bitmap -Destination $stream
                }
                else {
                    Write-IconPngPayload -Bitmap $bitmap -Destination $stream
                }

                $payload = $stream.ToArray()
            }
            finally {
                $stream.Dispose()
            }

            $format = if ($size -lt $PngFrom) { 'bitmap' } else { 'png' }
            $payloads.Add([pscustomobject]@{ Size = $size; Format = $format; Bytes = $payload })
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $image.Dispose()
}

$directory = Split-Path -Parent $Destination
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)
try {
    # ICONDIR: reserved, type 1 (icon), image count.
    $writer.Write([int16]0)
    $writer.Write([int16]1)
    $writer.Write([int16]$payloads.Count)

    # Payloads start after the header and the entry table.
    $offset = 6 + (16 * $payloads.Count)
    foreach ($entry in $payloads) {
        # 256 is stored as 0: each edge is one byte wide.
        $writer.Write([byte]($(if ($entry.Size -eq 256) { 0 } else { $entry.Size })))
        $writer.Write([byte]($(if ($entry.Size -eq 256) { 0 } else { $entry.Size })))
        $writer.Write([byte]0)                      # palette size, 0 for true colour
        $writer.Write([byte]0)                      # reserved
        $writer.Write([int16]1)                     # colour planes
        $writer.Write([int16]32)                    # bits per pixel
        $writer.Write([int]$entry.Bytes.Length)
        $writer.Write([int]$offset)
        $offset += $entry.Bytes.Length
    }

    foreach ($entry in $payloads) {
        $writer.Write($entry.Bytes)
    }

    [System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath($Destination), $stream.ToArray())
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

$written = Get-Item -LiteralPath $Destination
$summary = ($payloads | ForEach-Object { "$($_.Size) ($($_.Format), $($_.Bytes.Length) B)" }) -join ' + '
Write-Host "logo.ico: $($payloads.Count) images — $summary"
Write-Host "          $($written.Length) bytes  $($written.FullName)"
