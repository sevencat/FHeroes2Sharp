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
                  << "  fh2_reference icn     <ARCHIVE> <NAME.ICN> <OUTDIR> [background]\n"
                  << "  fh2_reference til     <ARCHIVE> <NAME.TIL> <OUTDIR>\n"
                  << "  fh2_reference cursor  <ARCHIVE> <NAME.BMP> <OUTDIR> [background]\n"
                  << "  fh2_reference effects <ARCHIVE> <OUTDIR>\n\n"
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

    /*******************************************************************************************
     *  Effects command: renders the image primitives the dialogs are made of on synthetic
     *  input. Every value is generated by a formula so the C# port can produce the very same
     *  pixels and the two outputs can be compared with SHA-256.
     ******************************************************************************************/

    constexpr int32_t effectCanvasWidth = 96;
    constexpr int32_t effectCanvasHeight = 64;
    constexpr int32_t effectSourceWidth = 37;
    constexpr int32_t effectSourceHeight = 21;

    // A display surface has no transform layer, which is what the C# port models as PixelBuffer.
    fheroes2::Image makeEffectCanvas()
    {
        fheroes2::Image canvas( effectCanvasWidth, effectCanvasHeight );
        canvas._disableTransformLayer();

        uint8_t * pixels = canvas.image();
        for ( int32_t y = 0; y < effectCanvasHeight; ++y ) {
            for ( int32_t x = 0; x < effectCanvasWidth; ++x ) {
                pixels[y * effectCanvasWidth + x] = static_cast<uint8_t>( x * 11 + y * 17 + 40 );
            }
        }

        return canvas;
    }

    fheroes2::Sprite makeEffectSource()
    {
        fheroes2::Sprite source( effectSourceWidth, effectSourceHeight );

        uint8_t * pixels = source.image();
        uint8_t * transform = source.transform();
        for ( int32_t y = 0; y < effectSourceHeight; ++y ) {
            for ( int32_t x = 0; x < effectSourceWidth; ++x ) {
                const int32_t offset = y * effectSourceWidth + x;
                pixels[offset] = static_cast<uint8_t>( x * 29 + y * 7 + 3 );
                transform[offset] = ( ( x + y ) % 5 == 0 ) ? 1 : 0;
            }
        }

        return source;
    }

    int saveEffect( const fheroes2::Image & image, const std::string & outputDir, const std::string & name )
    {
        const std::string path = outputDir + '/' + name;

        if ( !fheroes2::Save( image, path ) ) {
            std::cerr << "error: unable to write '" << path << "'.\n";
            return 1;
        }

        std::cout << path << '\n';
        return 0;
    }

    int exportEffects( const std::string & outputDir )
    {
        const fheroes2::Sprite source = makeEffectSource();

        {
            fheroes2::Image canvas = makeEffectCanvas();
            fheroes2::ApplyTransform( canvas, 8, 6, 40, 30, 3 );

            const int result = saveEffect( canvas, outputDir, "01-transform.bmp" );
            if ( result != 0 ) {
                return result;
            }
        }

        {
            fheroes2::Image canvas = makeEffectCanvas();
            fheroes2::addGradientShadow( source, canvas, { 20, 18 }, { -5, 5 } );

            const int result = saveEffect( canvas, outputDir, "02-shadow-sprite.bmp" );
            if ( result != 0 ) {
                return result;
            }
        }

        {
            fheroes2::Image canvas = makeEffectCanvas();
            fheroes2::addGradientShadowForArea( canvas, { 30, 10 }, 50, 36, 16 );

            const int result = saveEffect( canvas, outputDir, "03-shadow-area.bmp" );
            if ( result != 0 ) {
                return result;
            }
        }

        {
            fheroes2::Image canvas = makeEffectCanvas();
            fheroes2::CreateDitheringTransition( source, 0, 0, canvas, 20, 20, 20, 16, true, false );

            const int result = saveEffect( canvas, outputDir, "04-transition-vertical.bmp" );
            if ( result != 0 ) {
                return result;
            }
        }

        {
            fheroes2::Image canvas = makeEffectCanvas();
            fheroes2::CreateDitheringTransition( source, 2, 3, canvas, 24, 22, 21, 17, true, true );

            const int result = saveEffect( canvas, outputDir, "05-transition-vertical-odd-reverse.bmp" );
            if ( result != 0 ) {
                return result;
            }
        }

        {
            fheroes2::Image canvas = makeEffectCanvas();
            fheroes2::CreateDitheringTransition( source, 4, 5, canvas, 18, 14, 24, 18, false, false );

            const int result = saveEffect( canvas, outputDir, "06-transition-horizontal.bmp" );
            if ( result != 0 ) {
                return result;
            }
        }

        {
            fheroes2::Image canvas = makeEffectCanvas();
            fheroes2::CreateDitheringTransition( source, 6, 7, canvas, 22, 16, 25, 19, false, true );

            const int result = saveEffect( canvas, outputDir, "07-transition-horizontal-odd-reverse.bmp" );
            if ( result != 0 ) {
                return result;
            }
        }

        {
            // The band leaves the canvas on both sides, which exercises the clipping of the reference.
            fheroes2::Image canvas = makeEffectCanvas();
            fheroes2::CreateDitheringTransition( source, 0, 0, canvas, 80, 50, 40, 20, true, false );

            const int result = saveEffect( canvas, outputDir, "08-transition-clipped.bmp" );
            if ( result != 0 ) {
                return result;
            }
        }

        std::cout << "Wrote 8 BMP files to '" << outputDir << "'.\n";
        return 0;
    }
}

int main( int argc, char ** argv )
{
    if ( argc < 4 ) {
        printUsage();
        return 2;
    }

    const std::string command = argv[1];
    const std::string archivePath = argv[2];

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

    // The effects command only needs the palette of the archive, so it has no entry name.
    if ( command == "effects" ) {
        return exportEffects( argv[3] );
    }

    if ( argc < 5 ) {
        printUsage();
        return 2;
    }

    const std::string entryName = argv[3];
    const std::string outputDir = argv[4];
    const uint8_t background = ( argc > 5 ) ? static_cast<uint8_t>( std::atoi( argv[5] ) ) : 23;

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
