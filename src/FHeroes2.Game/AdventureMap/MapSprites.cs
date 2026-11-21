using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Assets.Til;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Io;

namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The sprite cache of the adventure map renderer, the port of <c>Assets::getImage()</c>,
/// <c>Assets::getTileImage()</c> and the <c>GetPalette()</c> global of <c>game_assets.cpp</c> and
/// <c>agg.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference decodes every ICN and TIL file of the game into a process wide table and lets the renderer
/// ask for a frame by id. The port keeps the same shape because <c>Maps::redraw*</c> of
/// <c>maps_tiles_render.cpp</c> has no way to reach an asset bundle or a palette: those functions take
/// nothing but a tile and a frame buffer, so the sprite source has to be a session global, exactly like in
/// the reference. The composition root installs the bundle once with <see cref="Initialize"/>.
/// </para>
/// <para>
/// Decoding stays lazy per file so a session that never opens the adventure map pays nothing, which is the
/// same trade-off <see cref="IcnSpriteCache"/> and <see cref="TerrainTileSet"/> already make. The cache is
/// not thread safe on purpose: it is filled from the render thread only, as in the reference.
/// </para>
/// </remarks>
public static class MapSprites
{
    /// <summary>Number of pre flipped variants of every TIL image, see <see cref="TerrainTileSet"/>.</summary>
    public const int ShapeCount = 4;

    private static readonly Dictionary<TilType, PixelBuffer[][]?> TileSets = [];

    private static IcnSpriteCache? _sprites;
    private static AssetBundle? _assets;

    /// <summary>Whether an asset bundle has been installed, <c>Assets::isValid()</c>.</summary>
    public static bool IsInitialized => _assets is not null;

    /// <summary>
    /// The palette the alpha blits of the map are looked up in, <c>GetPalette()</c> of the reference.
    /// </summary>
    /// <remarks>
    /// <c>fheroes2::AlphaBlit</c> reads the same global; the port keeps it here so that
    /// <see cref="UI.Interface.GameArea.BlitOnTile(PixelBuffer, Sprite, int, int, Point, bool, byte)"/> does not
    /// have to thread a palette through every map renderer.
    /// </remarks>
    public static Palette? GamePalette { get; private set; }

    /// <summary>
    /// Installs the archives the map is drawn from, the port of the static tables of
    /// <c>Assets::initializeTileset()</c>.
    /// </summary>
    /// <param name="assets">Archives of the running installation; <see langword="null"/> disables drawing.</param>
    /// <param name="gamePalette">Palette of the display the map is drawn into.</param>
    public static void Initialize(AssetBundle? assets, Palette? gamePalette)
    {
        _assets = assets;
        _sprites = assets is null ? null : new IcnSpriteCache(assets);
        GamePalette = gamePalette;

        TileSets.Clear();
    }

    /// <summary>Drops every decoded file, the port of the cleanup the reference never does.</summary>
    public static void Reset() => Initialize(null, null);

    /// <summary>
    /// The sprite of <paramref name="index"/> inside the ICN file of <paramref name="icnId"/>, the port of
    /// <c>Assets::getImage( icnId, icnIndex )</c>.
    /// </summary>
    /// <param name="icnId">The ICN file, as listed in <c>agg/icn.h</c>.</param>
    /// <param name="index">Frame of the file.</param>
    /// <returns><see langword="null"/> when the file or the frame is missing.</returns>
    public static Sprite? GetSprite(IcnType icnId, int index)
    {
        if (_sprites is null || IcnCatalog.GetFileName(icnId) is not { } archiveName)
        {
            return null;
        }

        return _sprites.GetSprite(archiveName, index);
    }

    /// <summary>
    /// The sprite of <paramref name="index"/> inside the tile set of an object part, the port of
    /// <c>Assets::getImage( ICN::getIcnIdFromObjectIcnType( type ), index )</c>.
    /// </summary>
    /// <param name="type">Tile set of the part.</param>
    /// <param name="index">Frame of the tile set.</param>
    /// <returns><see langword="null"/> when the part draws nothing or the frame is missing.</returns>
    public static Sprite? GetSprite(Mp2ObjectIcnType type, int index)
    {
        if (_sprites is null
            || Mp2ObjectIcnTypes.GetArchiveName(type, _assets?.IsPriceOfLoyaltyPresent ?? false) is not { } archiveName)
        {
            return null;
        }

        return _sprites.GetSprite(archiveName, index);
    }

    /// <summary>
    /// The image of <paramref name="index"/> of a TIL file mirrored by <paramref name="shape"/>, the port of
    /// <c>Assets::getTileImage( tilId, tileIndex, shape )</c>.
    /// </summary>
    /// <param name="tilId">The TIL file: ground, roads, the stone of an empty tile or the fog.</param>
    /// <param name="index">Image of the file.</param>
    /// <param name="shape">
    /// Two bit mirroring: the low bit flips vertically, the second one horizontally, as in
    /// <see cref="TerrainTileSet"/>.
    /// </param>
    /// <returns><see langword="null"/> when the file or the image is missing.</returns>
    public static PixelBuffer? GetTileImage(TilType tilId, int index, int shape)
    {
        PixelBuffer[][]? shapes = GetTileSet(tilId);

        if (shapes is null || index < 0 || index >= shapes[0].Length)
        {
            return null;
        }

        return shapes[shape & 0x03][index];
    }

    private static PixelBuffer[][]? GetTileSet(TilType tilId)
    {
        if (TileSets.TryGetValue(tilId, out PixelBuffer[][]? cached))
        {
            return cached;
        }

        PixelBuffer[][]? shapes = _assets is null ? null : LoadTileSet(_assets, tilId);
        TileSets[tilId] = shapes;

        return shapes;
    }

    private static PixelBuffer[][]? LoadTileSet(AssetBundle assets, TilType tilId)
    {
        string fileName = $"{tilId.ToString().ToUpperInvariant()}.TIL";

        byte[]? data = assets.TryRead(fileName);
        if (data is null)
        {
            GameLog.Warn($"'{fileName}' is missing; the adventure map cannot draw with it.");
            return null;
        }

        try
        {
            TilFile til = TilFile.Parse(data);

            PixelBuffer[][] shapes = new PixelBuffer[ShapeCount][];
            shapes[0] = [.. til.Images];

            for (int shape = 1; shape < ShapeCount; ++shape)
            {
                bool horizontal = (shape & 2) != 0;
                bool vertical = (shape & 1) != 0;

                shapes[shape] = new PixelBuffer[shapes[0].Length];
                for (int index = 0; index < shapes[0].Length; ++index)
                {
                    shapes[shape][index] = ImageOps.Flip(shapes[0][index], horizontal, vertical);
                }
            }

            return shapes;
        }
        catch (BinaryFormatException exception)
        {
            GameLog.Error($"'{fileName}' could not be parsed", exception);
            return null;
        }
    }
}
