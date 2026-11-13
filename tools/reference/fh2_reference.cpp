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
 *     effects <ARCHIVE> <OUTDIR>                          -> 01-... 08-...bmp
 *     window  <ARCHIVE> <OUTDIR>                          -> 01-window.bmp, 01-window-odd.bmp, ...
 *
 *   The output is 8 bit indexed BMP written by SDL_SaveBMP, exactly what the engine's
 *   fheroes2::Save() produces. See docs/MISC.md for the build and comparison workflow.
 ***************************************************************************/

#include <algorithm>
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
                  << "  fh2_reference effects <ARCHIVE> <OUTDIR>\n"
                  << "  fh2_reference window  <ARCHIVE> <OUTDIR>\n\n"
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

    /***********************************************************************************************
     *  Window command: draws fheroes2::StandardWindow with the artwork of the archive. The body of
     *  renderStandardWindow() is the coordinate arithmetic of src/fheroes2/gui/ui_window.cpp, copied
     *  line by line; only the members (_windowArea, _hasBackground) became parameters and the asset
     *  lookups became decoded sprites.
     **********************************************************************************************/

    constexpr int32_t windowCanvasWidth = 640;
    constexpr int32_t windowCanvasHeight = 480;

    // Layout constants of fheroes2::StandardWindow (ui_window.cpp).
    constexpr int32_t windowBorderSize = 16;
    constexpr int32_t windowBorderEdgeOffset = 43;
    constexpr int32_t windowTransitionSize = 10;
    constexpr int32_t windowBackgroundOffset = 22;

    fheroes2::Sprite readIcnSprite( fheroes2::AGGFile & agg, const std::string & entryName, const size_t index )
    {
        const std::vector<uint8_t> body = agg.read( entryName );
        if ( body.size() < headerSize + icnHeaderSize ) {
            std::cerr << "error: '" << entryName << "' is missing or truncated.\n";
            return {};
        }

        ROStreamBuf stream( body );

        const uint32_t count = stream.getLE16();
        const uint32_t blockSize = stream.getLE32();
        if ( count == 0 || blockSize == 0 || body.size() < headerSize + count * icnHeaderSize || index >= count ) {
            std::cerr << "error: '" << entryName << "' has an invalid ICN header.\n";
            return {};
        }

        stream.seek( headerSize + index * icnHeaderSize );

        fheroes2::ICNHeader header;
        stream >> header;

        const uint8_t * data = body.data() + headerSize + header.offsetData;
        return fheroes2::decodeICNSprite( data, body.data() + body.size(), header );
    }

    // Same synthetic fill as the effects command: the canvas has no transform layer, like a display.
    fheroes2::Image makeWindowCanvas()
    {
        fheroes2::Image canvas( windowCanvasWidth, windowCanvasHeight );
        canvas._disableTransformLayer();

        uint8_t * pixels = canvas.image();
        for ( int32_t y = 0; y < windowCanvasHeight; ++y ) {
            for ( int32_t x = 0; x < windowCanvasWidth; ++x ) {
                pixels[y * windowCanvasWidth + x] = static_cast<uint8_t>( x * 11 + y * 17 + 40 );
            }
        }

        return canvas;
    }

    void renderWindowBackgroundImage( fheroes2::Image & output, const fheroes2::Sprite & backgroundSprite, const fheroes2::Rect & roi,
                                     const int32_t borderOffset )
    {
        const int32_t backgroundSpriteWidth{ backgroundSprite.width() };
        const int32_t backgroundSpriteHeight{ backgroundSprite.height() };

        const int32_t backgroundWidth = roi.width - borderOffset * 2;
        const int32_t backgroundHeight = roi.height - borderOffset * 2;
        const int32_t backgroundHorizontalCopies = ( backgroundWidth - 1 - windowTransitionSize ) / ( backgroundSpriteWidth - windowTransitionSize );
        const int32_t backgroundVerticalCopies = ( backgroundHeight - 1 - windowTransitionSize ) / ( backgroundSpriteHeight - windowTransitionSize );

        const int32_t backgroundCopyWidth = std::min( backgroundSpriteWidth, backgroundWidth );
        const int32_t backgroundCopyHeight = std::min( backgroundSpriteHeight, backgroundHeight );
        const int32_t backgroundOffsetX = roi.x + borderOffset;
        const int32_t backgroundOffsetY = roi.y + borderOffset;

        // We do a copy as the background image does not have transparent pixels.
        Copy( backgroundSprite, 0, 0, output, backgroundOffsetX, backgroundOffsetY, backgroundCopyWidth, backgroundCopyHeight );

        // If we need more copies to fill background horizontally we make a transition and copy existing image.
        if ( backgroundHorizontalCopies > 0 ) {
            int32_t toOffsetX = borderOffset + backgroundSpriteWidth;
            CreateDitheringTransition( backgroundSprite, 0, 0, output, roi.x + toOffsetX - windowTransitionSize, backgroundOffsetY, windowTransitionSize,
                                       backgroundCopyHeight, true, false );

            const int32_t stepX = backgroundSpriteWidth - windowTransitionSize;
            const int32_t fromOffsetX = borderOffset + windowTransitionSize;

            for ( int32_t i = 0; i < backgroundHorizontalCopies; ++i ) {
                Copy( output, roi.x + fromOffsetX, backgroundOffsetY, output, roi.x + toOffsetX, backgroundOffsetY,
                      std::min( backgroundSpriteWidth, roi.width - borderOffset - toOffsetX ), backgroundCopyHeight );
                toOffsetX += stepX;
            }
        }

        // If we need more copies to fill background vertically we make a transition and copy existing image in full background width.
        if ( backgroundVerticalCopies > 0 ) {
            int32_t toOffsetY = borderOffset + backgroundSpriteHeight;
            CreateDitheringTransition( output, backgroundOffsetX, backgroundOffsetY, output, backgroundOffsetX, roi.y + toOffsetY - windowTransitionSize,
                                       backgroundWidth, windowTransitionSize, false, false );

            const int32_t stepY = backgroundSpriteHeight - windowTransitionSize;
            const int32_t fromOffsetY = borderOffset + windowTransitionSize;

            for ( int32_t i = 0; i < backgroundVerticalCopies; ++i ) {
                Copy( output, backgroundOffsetX, roi.y + fromOffsetY, output, backgroundOffsetX, roi.y + toOffsetY, backgroundWidth,
                      std::min( backgroundSpriteHeight, roi.height - borderOffset - toOffsetY ) );
                toOffsetY += stepY;
            }
        }
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
    void renderStandardWindow( fheroes2::Image & output, const fheroes2::Sprite & horizontalSprite, const fheroes2::Sprite & verticalSprite,
                              const fheroes2::Sprite & backgroundSprite, const fheroes2::Rect & windowArea, const bool hasBackground )
    {
        // Offset from window edges to background copy area and also the size of corners to render.
        const int32_t cornerSize = hasBackground ? windowBackgroundOffset : windowBorderSize;

        const int32_t horizontalSpriteWidth = horizontalSprite.width() - windowBorderSize;
        const int32_t horizontalSpriteHeight = horizontalSprite.height() - windowBorderSize;
        const int32_t verticalSpriteHeight = verticalSprite.height();
        const int32_t verticalSpriteWidth = verticalSprite.width();

        // Render window corners. The corners are the same in used original images, so we use only 'verticalSprite'.
        const int32_t rightCornerOffsetX = windowArea.x + windowArea.width - cornerSize;
        const int32_t bottomCornerOffsetY = windowArea.y + windowArea.height - cornerSize;
        const int32_t rightCornerSpriteOffsetX = verticalSpriteWidth - cornerSize;
        const int32_t bottomCornerSpriteOffsetY = verticalSpriteHeight - cornerSize;
        Blit( verticalSprite, 0, 0, output, windowArea.x, windowArea.y, cornerSize, cornerSize );
        Blit( verticalSprite, rightCornerSpriteOffsetX, 0, output, rightCornerOffsetX, windowArea.y, cornerSize, cornerSize );
        Blit( verticalSprite, 0, bottomCornerSpriteOffsetY, output, windowArea.x, bottomCornerOffsetY, cornerSize, cornerSize );
        Blit( verticalSprite, rightCornerSpriteOffsetX, bottomCornerSpriteOffsetY, output, rightCornerOffsetX, bottomCornerOffsetY, cornerSize, cornerSize );

        // Render additional part of border corners. This part will not be repeated to fill the border length.
        const int32_t extraCornerSize = windowBorderEdgeOffset - cornerSize;
        const fheroes2::Point cornerOffset( windowArea.x + cornerSize, windowArea.y + cornerSize );
        const int32_t rightBorderEdgeOffset = verticalSpriteWidth - windowBorderEdgeOffset;
        const int32_t bottomBorderEdgeOffset = verticalSpriteHeight - windowBorderEdgeOffset;

        Blit( verticalSprite, cornerSize, 0, output, cornerOffset.x, windowArea.y, extraCornerSize, cornerSize );
        Blit( verticalSprite, 0, cornerSize, output, windowArea.x, cornerOffset.y, cornerSize, extraCornerSize );

        Blit( verticalSprite, rightBorderEdgeOffset, 0, output, rightCornerOffsetX - extraCornerSize, windowArea.y, extraCornerSize, cornerSize );
        Blit( verticalSprite, rightCornerSpriteOffsetX, cornerSize, output, rightCornerOffsetX, cornerOffset.y, cornerSize, extraCornerSize );

        Blit( verticalSprite, cornerSize, bottomCornerSpriteOffsetY, output, cornerOffset.x, bottomCornerOffsetY, extraCornerSize, cornerSize );
        Blit( verticalSprite, 0, bottomBorderEdgeOffset, output, windowArea.x, bottomCornerOffsetY - extraCornerSize, cornerSize, extraCornerSize );

        Blit( verticalSprite, rightBorderEdgeOffset, bottomCornerSpriteOffsetY, output, rightCornerOffsetX - extraCornerSize, bottomCornerOffsetY, extraCornerSize,
              cornerSize );
        Blit( verticalSprite, rightCornerSpriteOffsetX, bottomBorderEdgeOffset, output, rightCornerOffsetX, bottomCornerOffsetY - extraCornerSize, cornerSize,
              extraCornerSize );

        if ( hasBackground ) {
            // Render the background image.
            renderWindowBackgroundImage( output, backgroundSprite, windowArea, windowBackgroundOffset );

            // Make a transition from borders to the background in the corners.
            CreateDitheringTransition( verticalSprite, cornerSize, cornerSize, output, cornerOffset.x, cornerOffset.y, extraCornerSize, windowTransitionSize, false,
                                       true );
            CreateDitheringTransition( verticalSprite, cornerSize, cornerSize, output, cornerOffset.x, cornerOffset.y, windowTransitionSize, extraCornerSize, true,
                                       true );

            CreateDitheringTransition( verticalSprite, rightBorderEdgeOffset, cornerSize, output, rightCornerOffsetX - extraCornerSize, cornerOffset.y,
                                       extraCornerSize, windowTransitionSize, false, true );
            CreateDitheringTransition( verticalSprite, rightCornerSpriteOffsetX - windowTransitionSize, cornerSize, output,
                                       rightCornerOffsetX - windowTransitionSize, cornerOffset.y, windowTransitionSize, extraCornerSize, true, false );

            CreateDitheringTransition( verticalSprite, cornerSize, bottomCornerSpriteOffsetY - windowTransitionSize, output, cornerOffset.x,
                                       bottomCornerOffsetY - windowTransitionSize, extraCornerSize, windowTransitionSize, false, false );
            CreateDitheringTransition( verticalSprite, cornerSize, bottomBorderEdgeOffset, output, cornerOffset.x, bottomCornerOffsetY - extraCornerSize,
                                       windowTransitionSize, extraCornerSize, true, true );

            CreateDitheringTransition( verticalSprite, rightBorderEdgeOffset, bottomCornerSpriteOffsetY - windowTransitionSize, output,
                                       rightCornerOffsetX - extraCornerSize, bottomCornerOffsetY - windowTransitionSize, extraCornerSize, windowTransitionSize,
                                       false, false );
            CreateDitheringTransition( verticalSprite, rightCornerSpriteOffsetX - windowTransitionSize, bottomBorderEdgeOffset, output,
                                       rightCornerOffsetX - windowTransitionSize, bottomCornerOffsetY - extraCornerSize, windowTransitionSize, extraCornerSize,
                                       true, false );
        }

        // Render vertical borders.
        const int32_t doubleBorderEdgeOffset = windowBorderEdgeOffset * 2;
        const int32_t verticalSpriteCopyHeight = std::min( windowArea.height, verticalSpriteHeight ) - doubleBorderEdgeOffset;
        const int32_t verticalSpriteCopies
            = ( windowArea.height - doubleBorderEdgeOffset - 1 - windowTransitionSize ) / ( bottomBorderEdgeOffset - windowBorderEdgeOffset - windowTransitionSize );
        const int32_t topBorderEdgeOffset = windowArea.y + windowBorderEdgeOffset;

        Blit( verticalSprite, 0, windowBorderEdgeOffset, output, windowArea.x, topBorderEdgeOffset, cornerSize, verticalSpriteCopyHeight );
        Blit( verticalSprite, rightCornerSpriteOffsetX, windowBorderEdgeOffset, output, rightCornerOffsetX, topBorderEdgeOffset, cornerSize,
              verticalSpriteCopyHeight );

        // Render a transition to the background.
        if ( hasBackground ) {
            CreateDitheringTransition( verticalSprite, cornerSize, windowBorderEdgeOffset, output, cornerOffset.x, topBorderEdgeOffset,
                                       windowTransitionSize, verticalSpriteCopyHeight, true, true );
            CreateDitheringTransition( verticalSprite, rightCornerSpriteOffsetX - windowTransitionSize, windowBorderEdgeOffset, output,
                                       rightCornerOffsetX - windowTransitionSize, topBorderEdgeOffset, windowTransitionSize, verticalSpriteCopyHeight, true,
                                       false );
        }

        // If we need more copies to fill vertical borders we make a transition and copy the central part of the border.
        if ( verticalSpriteCopies > 0 ) {
            int32_t toOffsetY = windowBorderEdgeOffset + verticalSpriteCopyHeight;
            const int32_t outputY = windowArea.y + toOffsetY - windowTransitionSize;
            CreateDitheringTransition( verticalSprite, 0, windowBorderEdgeOffset, output, windowArea.x, outputY, cornerSize, windowTransitionSize, false,
                                       false );
            CreateDitheringTransition( verticalSprite, rightCornerSpriteOffsetX, windowBorderEdgeOffset, output, rightCornerOffsetX, outputY, cornerSize,
                                       windowTransitionSize, false, false );

            const int32_t stepY = verticalSpriteCopyHeight - windowTransitionSize;
            const int32_t fromOffsetY = windowBorderEdgeOffset + windowTransitionSize;

            for ( int32_t i = 0; i < verticalSpriteCopies; ++i ) {
                const int32_t copyHeight = std::min( verticalSpriteCopyHeight, windowArea.height - windowBorderEdgeOffset - toOffsetY );
                const int32_t toY = windowArea.y + toOffsetY;

                Blit( verticalSprite, 0, fromOffsetY, output, windowArea.x, toY, cornerSize, copyHeight );
                Blit( verticalSprite, rightCornerSpriteOffsetX, fromOffsetY, output, rightCornerOffsetX, toY, cornerSize, copyHeight );

                // Render a transition to the background.
                if ( hasBackground ) {
                    CreateDitheringTransition( verticalSprite, cornerSize, fromOffsetY, output, cornerOffset.x, toY, windowTransitionSize, copyHeight, true,
                                               true );
                    CreateDitheringTransition( verticalSprite, rightCornerSpriteOffsetX - windowTransitionSize, fromOffsetY, output,
                                               rightCornerOffsetX - windowTransitionSize, toY, windowTransitionSize, copyHeight, true, false );
                }

                toOffsetY += stepY;
            }
        }

        // Make a transition to the bottom corners.
        const int32_t verticalSpriteBottomCornerEdgeY = bottomBorderEdgeOffset - windowTransitionSize;
        const int32_t optputBottomCornerEdgeY = windowArea.y + windowArea.height - windowBorderEdgeOffset - windowTransitionSize;
        CreateDitheringTransition( verticalSprite, 0, verticalSpriteBottomCornerEdgeY, output, windowArea.x, optputBottomCornerEdgeY, cornerSize,
                                   windowTransitionSize, false, false );
        CreateDitheringTransition( verticalSprite, rightCornerSpriteOffsetX, verticalSpriteBottomCornerEdgeY, output, rightCornerOffsetX,
                                   optputBottomCornerEdgeY, cornerSize, windowTransitionSize, false, false );

        // Render horizontal borders. We have to remember that 'verticalSprite' has 16 (equals to borderWidthPx) pixels of shadow at the left and bottom sides.
        const int32_t horizontalSpriteCopyWidth = std::min( windowArea.width, horizontalSpriteWidth ) - doubleBorderEdgeOffset;
        const int32_t horizontalSpriteCopies
            = ( windowArea.width - doubleBorderEdgeOffset - 1 - windowTransitionSize ) / ( horizontalSpriteWidth - doubleBorderEdgeOffset - windowTransitionSize );
        const int32_t bottomBorderSpriteOffsetY = horizontalSpriteHeight - cornerSize;
        const int32_t horizontalSpriteCopyStartX = windowBorderEdgeOffset + windowBorderSize;
        const int32_t leftBorderEdgeOffset = windowArea.x + windowBorderEdgeOffset;

        Blit( horizontalSprite, horizontalSpriteCopyStartX, 0, output, leftBorderEdgeOffset, windowArea.y, horizontalSpriteCopyWidth, cornerSize );
        Blit( horizontalSprite, horizontalSpriteCopyStartX, bottomBorderSpriteOffsetY, output, leftBorderEdgeOffset, bottomCornerOffsetY,
              horizontalSpriteCopyWidth, cornerSize );

        // Render a transition to the background.
        if ( hasBackground ) {
            CreateDitheringTransition( horizontalSprite, horizontalSpriteCopyStartX, cornerSize, output, leftBorderEdgeOffset, cornerOffset.y,
                                       horizontalSpriteCopyWidth, windowTransitionSize, false, true );
            CreateDitheringTransition( horizontalSprite, horizontalSpriteCopyStartX, bottomBorderSpriteOffsetY - windowTransitionSize, output,
                                       leftBorderEdgeOffset, bottomCornerOffsetY - windowTransitionSize, horizontalSpriteCopyWidth, windowTransitionSize,
                                       false, false );
        }

        // If we need more copies to fill horizontal borders we make a transition and copy the central part of the border.
        if ( horizontalSpriteCopies > 0 ) {
            int32_t toOffsetX = windowBorderEdgeOffset + horizontalSpriteCopyWidth;
            const int32_t outputX = windowArea.x + toOffsetX - windowTransitionSize;
            CreateDitheringTransition( horizontalSprite, horizontalSpriteCopyStartX, 0, output, outputX, windowArea.y, windowTransitionSize, cornerSize,
                                       true, false );
            CreateDitheringTransition( horizontalSprite, horizontalSpriteCopyStartX, bottomBorderSpriteOffsetY, output, outputX, bottomCornerOffsetY,
                                       windowTransitionSize, cornerSize, true, false );

            const int32_t stepX = horizontalSpriteCopyWidth - windowTransitionSize;
            const int32_t fromOffsetX = horizontalSpriteCopyStartX + windowTransitionSize;

            for ( int32_t i = 0; i < horizontalSpriteCopies; ++i ) {
                const int32_t copyWidth = std::min( horizontalSpriteCopyWidth, windowArea.width - windowBorderEdgeOffset - toOffsetX );
                const int32_t toX = windowArea.x + toOffsetX;

                Blit( horizontalSprite, fromOffsetX, 0, output, toX, windowArea.y, copyWidth, cornerSize );
                Blit( horizontalSprite, fromOffsetX, bottomBorderSpriteOffsetY, output, toX, bottomCornerOffsetY, copyWidth, cornerSize );

                // Render a transition to the background.
                if ( hasBackground ) {
                    CreateDitheringTransition( horizontalSprite, fromOffsetX, cornerSize, output, toX, cornerOffset.y, copyWidth, windowTransitionSize,
                                               false, true );
                    CreateDitheringTransition( horizontalSprite, fromOffsetX, bottomBorderSpriteOffsetY - windowTransitionSize, output, toX,
                                               bottomCornerOffsetY - windowTransitionSize, copyWidth, windowTransitionSize, false, false );
                }

                toOffsetX += stepX;
            }
        }

        // Make a transition to the right corners.
        const int32_t horizontalSpriteRightCornerEdgeX = horizontalSprite.width() - windowBorderEdgeOffset - windowTransitionSize;
        const int32_t optputRightCornerEdgeX = windowArea.x + windowArea.width - windowBorderEdgeOffset - windowTransitionSize;
        CreateDitheringTransition( horizontalSprite, horizontalSpriteRightCornerEdgeX, 0, output, optputRightCornerEdgeX, windowArea.y, windowTransitionSize,
                                   cornerSize, true, false );
        CreateDitheringTransition( horizontalSprite, horizontalSpriteRightCornerEdgeX, bottomBorderSpriteOffsetY, output, optputRightCornerEdgeX,
                                   bottomCornerOffsetY, windowTransitionSize, cornerSize, true, false );

        fheroes2::addGradientShadowForArea( output, { windowArea.x, windowArea.y }, windowArea.width, windowArea.height, windowBorderSize );
    }

    // One dialog case: the active area the caller asks for, and whether the stone interior is tiled.
    struct WindowCase {
        const char * name;
        int32_t x;
        int32_t y;
        int32_t width;
        int32_t height;
        bool hasBackground;
    };

    // Must stay in sync with FHeroes2.AssetProbe WindowPreview.
    const WindowCase windowCases[] = {
        { "01-window.bmp", 170, 140, 300, 200, true },
        { "02-window-odd.bmp", 195, 161, 251, 157, true },
        { "03-window-plain.bmp", 170, 140, 300, 200, false },
        { "04-window-wide.bmp", 64, 40, 520, 400, true },
    };

    int exportWindow( fheroes2::AGGFile & agg, const std::string & outputDir )
    {
        const fheroes2::Sprite horizontalSprite = readIcnSprite( agg, "SURDRBKG.ICN", 0 );
        const fheroes2::Sprite verticalSprite = readIcnSprite( agg, "WINLOSE.ICN", 0 );
        const fheroes2::Sprite backgroundSprite = readIcnSprite( agg, "STONEBAK.ICN", 0 );
        if ( horizontalSprite.empty() || verticalSprite.empty() || backgroundSprite.empty() ) {
            std::cerr << "error: the window artwork is missing from the archive.\n";
            return 3;
        }

        size_t written = 0;

        for ( const WindowCase & windowCase : windowCases ) {
            fheroes2::Image canvas = makeWindowCanvas();

            const fheroes2::Rect windowArea( windowCase.x - windowBorderSize, windowCase.y - windowBorderSize,
                                             windowCase.width + windowBorderSize * 2, windowCase.height + windowBorderSize * 2 );

            renderStandardWindow( canvas, horizontalSprite, verticalSprite, backgroundSprite, windowArea, windowCase.hasBackground );

            const std::string path = outputDir + '/' + windowCase.name;
            if ( !fheroes2::Save( canvas, path ) ) {
                std::cerr << "error: unable to write '" << path << "'.\n";
                return 1;
            }

            std::cout << windowCase.name << '\t' << windowArea.x << ',' << windowArea.y << '\t' << windowArea.width << 'x' << windowArea.height << '\t'
                      << ( windowCase.hasBackground ? "tiled" : "plain" ) << '\n';

            ++written;
        }

        std::cout << "Wrote " << written << " BMP files to '" << outputDir << "'.\n";
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

    // The window command renders dialogs, so it needs the archive but no entry name either.
    if ( command == "window" ) {
        return exportWindow( agg, argv[3] );
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
