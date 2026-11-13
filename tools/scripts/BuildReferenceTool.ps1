<#
.SYNOPSIS
    Builds tools/reference/fh2_reference.cpp against a local fheroes2 checkout.

.DESCRIPTION
    The reference exporter links the engine's own decoding sources (AGG reader, ICN/TIL
    decoders, monochrome cursor decoder and SDL based BMP writer) so that the C# ports can be
    compared with the original implementation instead of with a second C# encoder.

    Only a C++ compiler, SDL2 and zlib are required; the game itself is not built. The produced
    executable is written to tools-output/reference, which is ignored by git.

.EXAMPLE
    pwsh tools/scripts/BuildReferenceTool.ps1

.EXAMPLE
    pwsh tools/scripts/BuildReferenceTool.ps1 -Fheroes2Source D:\src\fheroes2 -MsysRoot C:\msys64\ucrt64
#>
param(
    [Parameter(Mandatory = $false)]
    [string] $Fheroes2Source = 'F:\code\fheroes2',

    [Parameter(Mandatory = $false)]
    [string] $MsysRoot = 'D:\msys64\ucrt64',

    [Parameter(Mandatory = $false)]
    [string] $Output = (Join-Path $PSScriptRoot '../../tools-output/reference/fh2_reference.exe')
)

$ErrorActionPreference = 'Stop'

$compiler = Join-Path $MsysRoot 'bin/g++.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The C++ compiler '$compiler' does not exist. Pass -MsysRoot for your MSYS2 UCRT64 installation."
}

if (-not (Test-Path -LiteralPath (Join-Path $Fheroes2Source 'src/engine/image_tool.cpp'))) {
    throw "The fheroes2 source tree '$Fheroes2Source' does not contain src/engine/image_tool.cpp."
}

$source = Join-Path $PSScriptRoot '../reference/fh2_reference.cpp'
if (-not (Test-Path -LiteralPath $source)) {
    throw "The reference exporter source '$source' does not exist."
}

# Everything the reference exporter touches. Deliberately a short list: pulling in the game
# sources would drag the UI, the audio stack and the resource system into a decoding test.
$engineSources = @(
    'src/engine/agg_file.cpp',
    'src/engine/image.cpp',
    'src/engine/image_color_conversion.cpp',
    'src/engine/image_palette.cpp',
    'src/engine/image_tool.cpp',
    'src/engine/logging.cpp',
    'src/engine/serialize.cpp',
    'src/engine/system.cpp',
    'src/engine/tools.cpp',
    'src/engine/zzlib.cpp'
) | ForEach-Object { Join-Path $Fheroes2Source $_ }

$missing = $engineSources | Where-Object { -not (Test-Path -LiteralPath $_) }
if ($missing) {
    throw "These reference sources are missing: $($missing -join ', ')"
}

$outputDirectory = Split-Path -Parent $Output
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

# The compiler spawns cc1plus, as and ld from libexec, and those need the MSYS2 runtime DLLs
# that live next to the driver. Without this the compiler exits with status 1 and no diagnostics.
$env:PATH = (Join-Path $MsysRoot 'bin') + ';' + $env:PATH

$compilerArguments = @(
    '-std=c++17'
    '-O2'
    '-DNDEBUG'
    '-DSDL_MAIN_HANDLED'
    "-I$(Join-Path $Fheroes2Source 'src/engine')"
    "-I$(Join-Path $Fheroes2Source 'src')"
    "-I$(Join-Path $MsysRoot 'include/SDL2')"
    $source
) + $engineSources + @(
    '-lmingw32'
    '-lSDL2'
    '-lz'
    '-static-libgcc'
    '-static-libstdc++'
    '-o'
    $Output
)

Write-Host "Compiling $([System.IO.Path]::GetFileName($source)) with $compiler ..."
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "The C++ compiler failed with exit code $LASTEXITCODE."
}

Write-Host "Reference exporter written to '$([System.IO.Path]::GetFullPath($Output))'."

# sdl2-compat ships SDL2.dll (which itself loads SDL3.dll); copying them next to the exporter
# means the comparison script can run it without touching the PATH.
foreach ($runtime in @('SDL2.dll', 'SDL3.dll')) {
    $runtimePath = Join-Path $MsysRoot "bin/$runtime"
    if (Test-Path -LiteralPath $runtimePath) {
        Copy-Item -LiteralPath $runtimePath -Destination $outputDirectory -Force
    }
}
