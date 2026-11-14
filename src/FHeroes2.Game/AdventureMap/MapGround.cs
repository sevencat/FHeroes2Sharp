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
public static class MapGrounds
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
    /// The terrain image index a brush of <paramref name="ground"/> paints: the first image of its
    /// range, which is the plainest image of that ground. The reference's terrain tools start from the
    /// same plain tile and then blend the edges with the neighbours; the blending is not ported yet.
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
}
