namespace FHeroes2.Game.AdventureMap;

/// <summary>
/// The ground of a tile, the port of <c>Maps::Ground</c> of the reference. The MP2 file does not
/// store the ground itself, only the index of the <c>GROUND32.TIL</c> image, so the type is read back
/// from that index.
/// </summary>
public enum MapGround
{
    /// <summary>An index outside the nine ground ranges, which a well formed map never has.</summary>
    Unknown = 0,

    Water,

    Grass,

    Snow,

    Swamp,

    Lava,

    Desert,

    Dirt,

    Wasteland,

    Beach,
}

/// <summary>Helpers of <see cref="MapGround"/>.</summary>
public static partial class MapGrounds
{
    /// <summary>The first terrain image of water, <c>WATER_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort WaterStartImageIndex = 0;

    /// <summary>The first terrain image of grass, <c>GRASS_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort GrassStartImageIndex = 30;

    /// <summary>The first terrain image of snow, <c>SNOW_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort SnowStartImageIndex = 92;

    /// <summary>The first terrain image of swamp, <c>SWAMP_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort SwampStartImageIndex = 146;

    /// <summary>The first terrain image of lava, <c>LAVA_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort LavaStartImageIndex = 208;

    /// <summary>The first terrain image of desert, <c>DESERT_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort DesertStartImageIndex = 262;

    /// <summary>The first terrain image of dirt, <c>DIRT_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort DirtStartImageIndex = 321;

    /// <summary>The first terrain image of wasteland, <c>WASTELAND_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort WastelandStartImageIndex = 361;

    /// <summary>The first terrain image of beach, <c>BEACH_START_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort BeachStartImageIndex = 415;

    /// <summary>One past the last terrain image, <c>MAX_IMAGE_INDEX</c> of the reference.</summary>
    public const ushort MaxImageIndex = 432;

    /// <summary>
    /// Reads the ground back from the terrain image index, the port of
    /// <c>Maps::Ground::getGroundByImageIndex</c>. The nine ranges are contiguous and ascending, so a
    /// chain of upper bounds is enough.
    /// </summary>
    /// <param name="terrainImageIndex">Image index stored in the tile of the MP2 file.</param>
    public static MapGround FromTerrainImageIndex(ushort terrainImageIndex) => terrainImageIndex switch
    {
        < GrassStartImageIndex => MapGround.Water,
        < SnowStartImageIndex => MapGround.Grass,
        < SwampStartImageIndex => MapGround.Snow,
        < LavaStartImageIndex => MapGround.Swamp,
        < DesertStartImageIndex => MapGround.Lava,
        < DirtStartImageIndex => MapGround.Desert,
        < WastelandStartImageIndex => MapGround.Dirt,
        < BeachStartImageIndex => MapGround.Wasteland,
        < MaxImageIndex => MapGround.Beach,
        _ => MapGround.Unknown,
    };

    /// <summary>
    /// The terrain image index the range of <paramref name="ground"/> starts at. Not the image to
    /// paint: the first images of a range are transitions to another ground, so an editor paints
    /// <see cref="PlainImageOffset"/> past this and lets <c>TerrainTransitions</c> blend the edges.
    /// </summary>
    /// <param name="ground">Ground to paint; <see cref="MapGround.Unknown"/> has no image.</param>
    /// <exception cref="ArgumentOutOfRangeException">The ground has no terrain images.</exception>
    public static ushort StartImageIndex(MapGround ground) => ground switch
    {
        MapGround.Water => WaterStartImageIndex,
        MapGround.Grass => GrassStartImageIndex,
        MapGround.Snow => SnowStartImageIndex,
        MapGround.Swamp => SwampStartImageIndex,
        MapGround.Lava => LavaStartImageIndex,
        MapGround.Desert => DesertStartImageIndex,
        MapGround.Dirt => DirtStartImageIndex,
        MapGround.Wasteland => WastelandStartImageIndex,
        MapGround.Beach => BeachStartImageIndex,
        _ => throw new ArgumentOutOfRangeException(nameof(ground), ground, "The ground has no terrain images."),
    };

    /// <summary>
    /// The offset inside the block of <paramref name="ground"/> where its plain images start: 38 for
    /// grass, snow, swamp, lava, desert and wasteland, 16 for water and dirt, 0 for beach. The images
    /// before that offset draw a transition to another ground; the eight images from it are the plain
    /// tile in its variants.
    /// </summary>
    /// <param name="ground">Ground to look up.</param>
    /// <exception cref="ArgumentOutOfRangeException">The ground has no terrain images.</exception>
    public static int PlainImageOffset(MapGround ground) => ground switch
    {
        MapGround.Water or MapGround.Dirt => 16,
        MapGround.Beach => 0,
        MapGround.Unknown => throw new ArgumentOutOfRangeException(nameof(ground), ground, "The ground has no terrain images."),
        _ => 38,
    };

    /// <summary>
    /// A deterministic variant in <c>0 .. count - 1</c> for a tile, the stand-in for the
    /// <c>Rand::Get</c> the reference uses when it picks among the eight images of a ground. A painted
    /// tile has to be reproducible: the same click must produce the same bytes, and a screenshot
    /// baseline cannot depend on a random generator.
    /// </summary>
    /// <param name="x">Tile column.</param>
    /// <param name="y">Tile row.</param>
    /// <param name="count">Number of variants, must be positive.</param>
    /// <returns>Variant index.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public static int VariantOffset(int x, int y, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        // The product term breaks the eight-tile period the two linear terms would leave in the low
        // bits, and shifting down mixes the high bits into the answer: a plain xor of two multiples
        // makes the variants line up in diagonal stripes.
        uint hash = (uint)((x * 73856093) ^ (y * 19349663) ^ (x * y * 6037));

        return (int)((hash >> 7) % (uint)count);
    }
}
