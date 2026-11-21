using System.Diagnostics;

using FHeroes2.Core.Random;

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
    /// A random terrain image of one ground, <c>Maps::Ground::getRandomTerrainImageIndex()</c>
    /// (<c>ground.cpp:244-291</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 四段：水只有四个变体；其余地形在 <paramref name="allowEmbeddedObjects"/> 打开时有一成概率
    /// （<c>Rand( 6 ) == 0</c>）挑「图上自带小物件」的那一段，起点与数量按地形不同；落到普通变体时取
    /// <c>Rand( 7 )</c> 加 <see cref="PlainImageOffset"/>。所有 <c>Rand</c> 都是闭区间，和参考一致。
    /// </para>
    /// <para>
    /// <b>编辑器不用它</b>：地形笔刷要「同一次点击产出同样的字节」，走
    /// <see cref="TerrainTransitions.PlainImageIndex"/> 的确定性变体。这个函数给地图随机生成器
    /// （<c>map_random_generator_helper.cpp:750</c>）和「战斗背景挑一块地」（<c>battle_only.cpp:160</c>）用。
    /// </para>
    /// </remarks>
    /// <param name="ground">Ground to pick an image of.</param>
    /// <param name="allowEmbeddedObjects">Whether the image may be one that carries a scenery object.</param>
    /// <returns>The terrain image index, <c>0</c> for a ground without images.</returns>
    public static ushort GetRandomTerrainImageIndex(MapGround ground, bool allowEmbeddedObjects = true)
    {
        if (ground == MapGround.Unknown)
        {
            return (ushort)UnsupportedGround(ground);
        }

        ushort start = StartImageIndex(ground);

        if (ground == MapGround.Water)
        {
            return (ushort)(Rand.Get(3) + 16 + start);
        }

        if (allowEmbeddedObjects && Rand.Get(6) == 0)
        {
            return (ushort)(ground switch
            {
                MapGround.Grass or MapGround.Swamp => Rand.Get(15) + 46 + start,
                MapGround.Snow or MapGround.Wasteland or MapGround.Lava => Rand.Get(7) + 46 + start,
                MapGround.Desert => Rand.Get(12) + 46 + start,
                MapGround.Dirt => Rand.Get(15) + 24 + start,
                MapGround.Beach => Rand.Get(8) + 8 + start,
                _ => UnsupportedGround(ground),
            });
        }

        return (ushort)(Rand.Get(7) + ground switch
        {
            MapGround.Dirt => 16,
            MapGround.Beach => 0,
            _ => 38,
        } + start);
    }

    /// <summary>Logs the reference's <c>assert( 0 )</c> for a ground without images.</summary>
    /// <param name="ground">The offending ground.</param>
    /// <returns>The value the reference returns after the assertion, <c>0</c>.</returns>
    private static int UnsupportedGround(MapGround ground)
    {
        Debug.Assert(false, $"The ground has no terrain images: {ground}.");

        return 0;
    }

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
