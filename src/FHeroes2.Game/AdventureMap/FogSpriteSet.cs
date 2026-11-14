using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The two sprite sets the fog of war is painted with: the four whole tiles of <c>CLOF32.TIL</c> and
/// the thirty-four boundary pieces of <c>CLOP32.ICN</c>.
/// </summary>
public interface IFogSpriteSource
{
    /// <summary>A whole fogged tile, one of four variants that differ in the noise of the pattern.</summary>
    PixelBuffer? GetFullTile(int variant);

    /// <summary>
    /// A boundary piece of the fog; <paramref name="flipped"/> asks for the mirrored copy, which the
    /// reference blits when it reverts the sprite instead of storing a second frame.
    /// </summary>
    Sprite? GetBoundary(int frame, bool flipped);
}

/// <summary>
/// <see cref="IFogSpriteSource"/> over the AGG assets. Both files are looked up once; a missing file
/// or a missing frame yields <c>null</c>, which makes the view draw the tile without fog
/// (<c>Assets::getImage</c> returns an empty sprite in the reference, which paints nothing).
/// </summary>
public sealed class FogSpriteSet : IFogSpriteSource
{
    /// <summary>Name of the file that holds the four whole fogged tiles.</summary>
    public const string TileFileName = "CLOF32.TIL";

    /// <summary>Name of the file that holds the fog boundary pieces.</summary>
    public const string BoundaryFileName = "CLOP32.ICN";

    private readonly PixelBuffer[] _fullTiles;
    private readonly Sprite?[] _boundary;
    private readonly Sprite?[] _flippedBoundary;

    private FogSpriteSet(PixelBuffer[] fullTiles, Sprite?[] boundary)
    {
        _fullTiles = fullTiles;
        _boundary = boundary;
        _flippedBoundary = new Sprite?[boundary.Length];
    }

    /// <summary>
    /// Loads both files, or returns <c>null</c> when there is no asset bundle — a scene without game
    /// data draws without fog instead of failing.
    /// </summary>
    public static FogSpriteSet? TryLoad(AssetBundle? assets)
    {
        if (assets is null)
        {
            return null;
        }

        PixelBuffer[] fullTiles;
        try
        {
            byte[]? data = assets.TryRead(TileFileName);
            if (data is null)
            {
                return null;
            }

            TilFile file = TilFile.Parse(data);

            fullTiles = new PixelBuffer[FogFrames.FullTileCount];
            for (int i = 0; i < fullTiles.Length && i < file.Count; ++i)
            {
                fullTiles[i] = file.Images[i];
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }

        Sprite?[] boundary = new Sprite?[FogFrames.BoundaryFrameCount];
        try
        {
            IcnSpriteFile? file = assets.TryReadIcn(BoundaryFileName);
            for (int i = 0; i < boundary.Length && file is not null && i < file.Count; ++i)
            {
                boundary[i] = file.Sprites[i];
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }

        return new FogSpriteSet(fullTiles, boundary);
    }

    /// <inheritdoc />
    public PixelBuffer? GetFullTile(int variant)
    {
        if ((uint)variant >= (uint)_fullTiles.Length)
        {
            return null;
        }

        return _fullTiles[variant];
    }

    /// <inheritdoc />
    public Sprite? GetBoundary(int frame, bool flipped)
    {
        if ((uint)frame >= (uint)_boundary.Length || _boundary[frame] is not { } sprite)
        {
            return null;
        }

        if (!flipped)
        {
            return sprite;
        }

        // The mirror keeps the offsets: the caller mirrors the position inside the tile, exactly like
        // Interface::GameArea::BlitOnTile( ..., revert ) does with the sprite it is given. The whole
        // sprite is mirrored, transform layer included, as fheroes2::Flip does.
        return _flippedBoundary[frame] ??= sprite.Flip(true, false);
    }
}
