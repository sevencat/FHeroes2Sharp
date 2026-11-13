<#
.SYNOPSIS
    Compiles every translations/<culture>.po into a gettext .mo catalog and installs it into
    the game data directory used by the port (<data>/files/lang/<culture>.mo).

.DESCRIPTION
    The .po sources live in the repository, the binary .mo files do not: they are generated
    artifacts and are installed next to the other fheroes2 catalogs (<data>/files/lang). This
    mirrors fheroes2, which is what reads them at runtime.

    GNU gettext (msgfmt) is required. When it is not on PATH the MSYS2 installation is probed,
    because `BuildReferenceTool.ps1` already requires MSYS2 anyway.

.EXAMPLE
    pwsh tools/scripts/BuildTranslations.ps1
    Compiles all .po files, reports the statistics and installs the .mo files.

.EXAMPLE
    pwsh tools/scripts/BuildTranslations.ps1 -Cultures zh_CN -NoInstall
    Only validates zh_CN.po, the .mo file stays in the temporary directory.
#>
[CmdletBinding()]
param(
    # Game data directory. Must contain (or receive) the "files/lang" folder.
    [string] $DataDirectory = '',

    # Culture names to build, for example zh_CN. Defaults to every translations/*.po file.
    [string[]] $Cultures = @(),

    # Explicit path to msgfmt.exe. Defaults to PATH and then to the MSYS2 locations.
    [string] $MsgfmtPath = '',

    # Validate and compile, but do not copy the .mo file into the data directory.
    [switch] $NoInstall
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$translationsDirectory = Join-Path $repoRoot 'translations'

if ( -not ( Test-Path -LiteralPath $translationsDirectory ) ) {
    throw "The translations directory '$translationsDirectory' does not exist."
}

function Resolve-Msgfmt {
    if ( $MsgfmtPath ) {
        if ( -not ( Test-Path -LiteralPath $MsgfmtPath ) ) {
            throw "msgfmt was not found at '$MsgfmtPath'."
        }

        return ( Resolve-Path -LiteralPath $MsgfmtPath ).Path
    }

    $command = Get-Command msgfmt -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ( $command ) {
        return $command.Source
    }

    foreach ( $candidate in @(
            'D:\msys64\usr\bin\msgfmt.exe',
            'C:\msys64\usr\bin\msgfmt.exe',
            "$env:USERPROFILE\msys64\usr\bin\msgfmt.exe" ) ) {
        if ( Test-Path -LiteralPath $candidate ) {
            return $candidate
        }
    }

    throw 'msgfmt (GNU gettext) was not found. Install MSYS2 (pacman -S gettext) or pass -MsgfmtPath.'
}

function Resolve-DataDirectory {
    if ( $DataDirectory ) {
        return ( Resolve-Path -LiteralPath $DataDirectory ).Path
    }

    if ( $env:FHEROES2_DATA -and ( Test-Path -LiteralPath $env:FHEROES2_DATA ) ) {
        return ( Resolve-Path -LiteralPath $env:FHEROES2_DATA ).Path
    }

    foreach ( $candidate in @( 'F:\game\fh2' ) ) {
        if ( Test-Path -LiteralPath $candidate ) {
            return ( Resolve-Path -LiteralPath $candidate ).Path
        }
    }

    throw 'The game data directory is unknown. Pass -DataDirectory <path> or set FHEROES2_DATA.'
}

$msgfmt = Resolve-Msgfmt
$cultureFiles = @( Get-ChildItem -LiteralPath $translationsDirectory -Filter '*.po' | Sort-Object Name )

if ( $Cultures.Count -gt 0 ) {
    $wanted = @( $Cultures | ForEach-Object { if ( $_ -like '*.po' ) { $_ } else { "$_.po" } } )
    $cultureFiles = @( $cultureFiles | Where-Object { $wanted -contains $_.Name } )

    $missing = @( $wanted | Where-Object { $name = $_; -not ( $cultureFiles | Where-Object { $_.Name -eq $name } ) } )
    if ( $missing.Count -gt 0 ) {
        throw "No .po file for: $($missing -join ', ')."
    }
}

if ( $cultureFiles.Count -eq 0 ) {
    throw "There are no .po files in '$translationsDirectory'."
}

$installDirectory = $null
if ( -not $NoInstall ) {
    $installDirectory = Join-Path (Resolve-DataDirectory) 'files\lang'
    New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null
}

$stagingDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "fheroes2sharp-translations-$PID"
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

$results = @()

try {
    foreach ( $poFile in $cultureFiles ) {
        $culture = [System.IO.Path]::GetFileNameWithoutExtension( $poFile.Name )
        $moFile = Join-Path $stagingDirectory "$culture.mo"

        # MoCatalog only decodes UTF-8 catalogs today, so warn early instead of throwing later.
        $charset = ( Select-String -LiteralPath $poFile.FullName -Pattern 'charset=([^\\\s]+)' -Encoding utf8 |
                Select-Object -First 1 )
        if ( $charset -and $charset.Matches[0].Groups[1].Value -notmatch '^(UTF-8|utf-8|ASCII|ascii)$' ) {
            Write-Warning "$culture declares charset '$($charset.Matches[0].Groups[1].Value)'; MoCatalog only decodes UTF-8."
        }

        # msgfmt localises its own messages; force the C locale so the statistics can be parsed.
        $previousLocale = $env:LC_ALL
        $env:LC_ALL = 'C'

        try {
            $statistics = & $msgfmt --check --statistics --output-file="$moFile" "$($poFile.FullName)" 2>&1
            $exitCode = $LASTEXITCODE
        }
        finally {
            $env:LC_ALL = $previousLocale
        }

        if ( $exitCode -ne 0 ) {
            throw "msgfmt failed for '$($poFile.Name)':`n$($statistics -join [Environment]::NewLine)"
        }

        $summary = @( $statistics | Where-Object { $_ -match 'translated message' } ) | Select-Object -First 1
        $installedPath = $null
        if ( $installDirectory ) {
            $installedPath = Join-Path $installDirectory "$culture.mo"
            Copy-Item -LiteralPath $moFile -Destination $installedPath -Force
        }

        $results += [pscustomobject]@{
            Culture     = $culture
            Statistics  = if ( $summary ) { ( [string] $summary ).Trim() } else { 'compiled' }
            Size        = ( Get-Item -LiteralPath $moFile ).Length
            InstalledTo = if ( $installedPath ) { $installedPath } else { '(not installed)' }
        }
    }
}
finally {
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

$results | Format-Table -AutoSize

