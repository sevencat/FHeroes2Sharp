/***************************************************************************
 *   Reference exporter backed by the original fheroes2 C++ engine.
 *
 *   The C# decoders are ports of the code in <fheroes2>/src/engine, so the only way to prove
 *   that a port is faithful is to run the original code and compare the output. This tool is a
 *   thin driver around the engine's own AGG reader and image decoders: it links the decoding
 *   translation units (agg_file, serialize, zzlib, image, image_palette, image_tool plus the
 *   logging/system helpers they need) and only adds argument parsing and file naming.
 *
 *   It intentionally mirrors FHeroes2.AssetProbe --format bmp so that both tools produce files
 *   with the same names in the same order:
 *
 *     icn     <ARCHIVE> <NAME.ICN> <OUTDIR> [background]  -> <STEM>_000.bmp, <STEM>_001.bmp, ...
 *     til     <ARCHIVE> <NAME.TIL> <OUTDIR>               -> <STEM>_000.bmp, <STEM>_001.bmp, ...
 *     cursor  <ARCHIVE> <NAME.BMP> <OUTDIR> [background]  -> <STEM>.bmp
 *
 *   The output is 8 bit indexed BMP written by SDL_SaveBMP, exactly what the engine's
 *   fheroes2::Save() produces. See docs/MISC.md for the build and comparison workflow.
 ***************************************************************************/

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <iostream>
#include <string>
#include <vector>

#include "agg_file.h"
#include "image.h"
#include "image_palette.h"
#include "image_tool.h"
#include "serialize.h"

namespace
{
    // Size of the ICN and TIL entry headers: the sprite/tile data always starts after them.
    constexpr size_t headerSize = 6;

    // Header size of one ICN sprite descriptor.
    constexpr size_t icnHeaderSize = 13;

    void printUsage()
    {
        std::cout << "fh2_reference - reference image exporter built from the fheroes2 sources\n\n"
                  << "usage:\n"
                  << "  fh2_reference icn    <ARCHIVE> <NAME.ICN> <OUTDIR> [background]\n"
                  << "  fh2_reference til    <ARCHIVE> <NAME.TIL> <OUTDIR>\n"
                  << "  fh2_reference cursor <ARCHIVE> <NAME.BMP> <OUTDIR> [background]\n\n"
                  << "Writes 8 bit indexed BMP files (SDL_SaveBMP) named after the AssetProbe --format bmp\n"
                  << "output. 'background' is the palette index used for transparent pixels of sprites and\n"
                  << "cursors; the engine recommends 23 and uses 23 by default here.\n";
    }

    std::string replaceExtension( const std::string & entryName )
    {
        const size_t dot = entryName.find_last_of( '.' );
        return dot == std::string::npos ? entryName : entryName.substr( 0, dot );
    }

    std::string numberedPath( const std::string & directory, const std::string & stem, const size_t index )
    {
        char name[64] = { 0 };
        std::snprintf( name, sizeof( name ), "%s_%03zu.bmp", stem.c_str(), index );
        return directory + '/' + name;
    }

    int exportIcn( fheroes2::AGGFile & agg, const std::string & entryName, const std::string & outputDir, const uint8_t background )
    {
        const std::vector<uint8_t> body = agg.read( entryName );
        if ( body.size() < headerSize ) {
            std::cerr << "error: '" << entryName << "' is missing or truncated.\n";
            return 3;
        }

        ROStreamBuf stream( body );

        const uint32_t count = stream.getLE16();
        const uint32_t blockSize = stream.getLE32();
        if ( count == 0 || blockSize == 0 || body.size() < headerSize + count * icnHeaderSize ) {
            std::cerr << "error: '" << entryName << "' has an invalid ICN header.\n";
            return 3;
        }

        const std::string stem = replaceExtension( entryName );
        std::cout << "# " << entryName << ": " << count << " sprites\n";

        for ( uint32_t i = 0; i < count; ++i ) {
            stream.seek( headerSize + i * icnHeaderSize );

            fheroes2::ICNHeader header;
            stream >> header;

            const uint8_t * data = body.data() + headerSize + header.offsetData;
            const uint8_t * dataEnd = body.data() + body.size();

            const fheroes2::Sprite sprite = fheroes2::decodeICNSprite( data, dataEnd, header );
            const std::string path = numberedPath( outputDir, stem, i );

            if ( !fheroes2::Save( sprite, path, background ) ) {
                std::cerr << "error: unable to write '" << path << "'.\n";
                return 1;
            }

            std::cout << i << '\t' << header.width << 'x' << header.height << '\t' << header.offsetX << ',' << header.offsetY << '\t'
                      << ( ( header.animationFrames & 0x20 ) ? "mono" : "color" ) << '\t' << ( sprite.singleLayer() ? "single" : "transform" )
                      << '\t' << path << '\n';
        }

        std::cout << "Wrote " << count << " BMP files to '" << outputDir << "'.\n";
        return 0;
    }

    int exportTil( fheroes2::AGGFile & agg, const std::string & entryName, const std::string & outputDir )
    {
        const std::vector<uint8_t> body = agg.read( entryName );
        if ( body.size() < headerSize ) {
            std::cerr << "error: '" << entryName << "' is missing or truncated.\n";
            return 3;
        }

        ROStreamBuf stream( body );

        const size_t count = stream.getLE16();
        const int32_t width = stream.getLE16();
        const int32_t height = stream.getLE16();
        if ( count < 1 || width < 1 || height < 1 || headerSize + count * static_cast<size_t>( width ) * height != body.size() ) {
            std::cerr << "error: '" << entryName << "' has an invalid TIL header.\n";
            return 3;
        }

        std::vector<fheroes2::Image> images;
        fheroes2::decodeTILImages( body.data() + headerSize, count, width, height, images );

        const std::string stem = replaceExtension( entryName );
        std::cout << "# " << entryName << ": " << count << " tiles of " << width << 'x' << height << '\n';

        for ( size_t i = 0; i < images.size(); ++i ) {
            const std::string path = numberedPath( outputDir, stem, i );

            // Tiles carry no transform layer: the engine saves them as they are.
            if ( !fheroes2::Save( images[i], path ) ) {
                std::cerr << "error: unable to write '" << path << "'.\n";
                return 1;
            }

            std::cout << i << '\t' << path << '\n';
        }

        std::cout << "Wrote " << images.size() << " BMP files to '" << outputDir << "'.\n";
        return 0;
    }

    int exportCursor( fheroes2::AGGFile & agg, const std::string & entryName, const std::string & outputDir, const uint8_t background )
    {
        const std::vector<uint8_t> body = agg.read( entryName );
        if ( body.empty() ) {
            std::cerr << "error: '" << entryName << "' is missing.\n";
            return 3;
        }

        const fheroes2::Sprite cursor = fheroes2::decodeBMPFile( body );
        if ( cursor.empty() ) {
            std::cerr << "error: '" << entryName << "' is not a monochrome cursor bitmap.\n";
            return 3;
        }

        const std::string path = outputDir + '/' + replaceExtension( entryName ) + ".bmp";

        if ( !fheroes2::Save( cursor, path, background ) ) {
            std::cerr << "error: unable to write '" << path << "'.\n";
            return 1;
        }

        std::cout << "# " << entryName << ": " << cursor.width() << 'x' << cursor.height() << " monochrome cursor\n";
        std::cout << "Wrote 1 BMP file to '" << outputDir << "'.\n";
        return 0;
    }
}

int main( int argc, char ** argv )
{
    if ( argc < 5 ) {
        printUsage();
        return 2;
    }

    const std::string command = argv[1];
    const std::string archivePath = argv[2];
    const std::string entryName = argv[3];
    const std::string outputDir = argv[4];
    const uint8_t background = ( argc > 5 ) ? static_cast<uint8_t>( std::atoi( argv[5] ) ) : 23;

    fheroes2::AGGFile agg;
    if ( !agg.open( archivePath ) ) {
        std::cerr << "error: unable to open '" << archivePath << "'.\n";
        return 1;
    }

    const std::vector<uint8_t> palette = agg.read( "KB.PAL" );
    if ( palette.size() != fheroes2::paletteSizeBytes ) {
        std::cerr << "error: 'KB.PAL' is missing from '" << archivePath << "'.\n";
        return 1;
    }

    fheroes2::setGamePalette( palette );

    if ( command == "icn" ) {
        return exportIcn( agg, entryName, outputDir, background );
    }

    if ( command == "til" ) {
        return exportTil( agg, entryName, outputDir );
    }

    if ( command == "cursor" ) {
        return exportCursor( agg, entryName, outputDir, background );
    }

    std::cerr << "error: unknown command '" << command << "'.\n";
    printUsage();
    return 2;
}
