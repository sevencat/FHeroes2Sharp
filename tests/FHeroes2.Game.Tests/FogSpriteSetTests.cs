using FHeroes2.Assets;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="FogSpriteSet"/> against the real archives: the four whole fog tiles of
/// <c>CLOF32.TIL</c>, the thirty-four pieces of <c>CLOP32.ICN</c> and the mirror <c>drawFog</c> asks
/// for with <c>BlitOnTile( ..., revert )</c>.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class FogSpriteSetTests
{
    [Fact]
    public void WithoutAnAssetBundle_ThereIsNoFogSpriteSet()
    {
        Assert.Null(FogSpriteSet.TryLoad(null));
    }

    [Fact]
    public void TheFourWholeTiles_AreTileSizedAndNotEmpty()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        FogSpriteSet? sprites = FogSpriteSet.TryLoad(assets);
        Assert.NotNull(sprites);

        for (int variant = 0; variant < FogFrames.FullTileCount; ++variant)
        {
            PixelBuffer? image = sprites.GetFullTile(variant);

            Assert.NotNull(image);
            Assert.Equal(32, image.Width);
            Assert.Equal(32, image.Height);
            Assert.True(HasVisiblePixel(image), $"CLOF32 tile {variant} is empty");
        }

        Assert.Null(sprites.GetFullTile(FogFrames.FullTileCount));
    }

    [Fact]
    public void TheBoundaryPieces_AreTileSizedFramesWithATransformLayer()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        FogSpriteSet? sprites = FogSpriteSet.TryLoad(assets);
        Assert.NotNull(sprites);

        for (int frame = 0; frame < FogFrames.BoundaryFrameCount; ++frame)
        {
            Sprite? sprite = sprites.GetBoundary(frame, false);

            Assert.NotNull(sprite);
            Assert.InRange(sprite.Width, 1, 32);
            Assert.InRange(sprite.Height, 1, 32);
            Assert.NotNull(sprite.Transform);
        }

        Assert.Null(sprites.GetBoundary(FogFrames.BoundaryFrameCount, false));
    }

    [Fact]
    public void TheMirroredPiece_IsTheOriginalFlippedWithItsTransformLayer()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        FogSpriteSet? sprites = FogSpriteSet.TryLoad(assets);
        Assert.NotNull(sprites);

        // A mirror that kept only the image layer would write the transparent pixels of the piece as
        // palette index zero, i.e. as a black rectangle over the fog, so both layers are compared.
        for (int frame = 0; frame < FogFrames.BoundaryFrameCount; ++frame)
        {
            Sprite original = Assert.IsType<Sprite>(sprites.GetBoundary(frame, false));
            Sprite mirrored = Assert.IsType<Sprite>(sprites.GetBoundary(frame, true));

            Assert.Equal(original.OffsetX, mirrored.OffsetX);
            Assert.Equal(original.OffsetY, mirrored.OffsetY);
            Assert.Equal(original.Width, mirrored.Width);
            Assert.Equal(original.Height, mirrored.Height);

            byte[] transform = Assert.IsType<byte[]>(original.Transform);
            byte[] flipped = Assert.IsType<byte[]>(mirrored.Transform);

            for (int y = 0; y < original.Height; ++y)
            {
                for (int x = 0; x < original.Width; ++x)
                {
                    int sourceX = original.Width - 1 - x;
                    int sourceIndex = (y * original.Width) + sourceX;

                    Assert.Equal(original.Image[sourceX, y], mirrored.Image[x, y]);
                    Assert.Equal(transform[sourceIndex], flipped[(y * original.Width) + x]);
                }
            }
        }
    }

    [Fact]
    public void ThePiecesAreNotAllFullyTransparent()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        FogSpriteSet? sprites = FogSpriteSet.TryLoad(assets);
        Assert.NotNull(sprites);

        // A transform value of 1 leaves the destination alone, so a piece whose whole transform layer
        // is 1 covers nothing at all.
        for (int frame = 0; frame < FogFrames.BoundaryFrameCount; ++frame)
        {
            Sprite sprite = Assert.IsType<Sprite>(sprites.GetBoundary(frame, false));
            byte[] transform = Assert.IsType<byte[]>(sprite.Transform);

            Assert.Contains(transform, value => value != 1);
        }
    }

    private static bool HasVisiblePixel(PixelBuffer image)
    {
        for (int y = 0; y < image.Height; ++y)
        {
            for (int x = 0; x < image.Width; ++x)
            {
                if (image[x, y] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
