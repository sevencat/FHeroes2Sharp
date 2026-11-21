using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Castle;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The small castle picture of the construction window, <c>Castle::DrawImageCastle()</c>
/// (<c>castle.cpp:1326-1426</c>).
/// </summary>
/// <remarks>
/// <para>
/// 这是冒险地图上那座城的五乘五块图，不是城镇屏的大图：底下两行是 <c>OBJNTWBA</c> 的地面，上面四行是
/// <c>OBJNTOWN</c> 的城墙与塔，种的族决定从哪一块开始，没建城堡时用栅栏那十六块。
/// </para>
/// <para>
/// 地面取城所在瓦片的地面，按参考的十块一档换基址（草地 0、雪地 10、沼泽 20……），城不在当前世界里时当草地。
/// </para>
/// </remarks>
public static class CastleImagePainter
{
    /// <summary>The size of one map tile, <c>TILEWIDTH</c>.</summary>
    public const int TileSize = 32;

    /// <summary>
    /// Paints the castle picture with its top-left corner on <paramref name="offset"/>.
    /// </summary>
    /// <param name="target">The frame to draw into.</param>
    /// <param name="castle">The town to draw.</param>
    /// <param name="offset">Where the five by five block starts.</param>
    /// <param name="world">The world whose tile gives the ground, or <see langword="null"/> for grass.</param>
    public static void Draw(PixelBuffer target, Castle castle, Point offset, World? world)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(castle);

        int groundIndex = GroundIndex(GroundOf(castle, world));

        for (int i = 0; i < 5; ++i)
        {
            DrawSprite(target, IcnType.Objntwba, groundIndex + i, offset.X + (i * TileSize), offset.Y + (3 * TileSize));
        }

        for (int i = 0; i < 5; ++i)
        {
            DrawSprite(target, IcnType.Objntwba, groundIndex + 5 + i, offset.X + (i * TileSize), offset.Y + (4 * TileSize));
        }

        int castleIndex = RaceIndex(castle.Race);

        if (!castle.IsBuild(BuildingType.Castle))
        {
            castleIndex += 16;
        }

        DrawSprite(target, IcnType.Objntown, castleIndex, offset.X + (2 * TileSize), offset.Y);

        for (int i = 0; i < 5; ++i)
        {
            DrawSprite(target, IcnType.Objntown, castleIndex + 1 + i, offset.X + (i * TileSize), offset.Y + TileSize);
        }

        for (int i = 0; i < 5; ++i)
        {
            DrawSprite(target, IcnType.Objntown, castleIndex + 6 + i, offset.X + (i * TileSize), offset.Y + (2 * TileSize));
        }

        for (int i = 0; i < 5; ++i)
        {
            DrawSprite(target, IcnType.Objntown, castleIndex + 11 + i, offset.X + (i * TileSize), offset.Y + (3 * TileSize));
        }
    }

    /// <summary>The ground of the tile the town stands on, <c>world.getTile( GetIndex() ).GetGround()</c>.</summary>
    /// <param name="castle">The town.</param>
    /// <param name="world">The world, or <see langword="null"/>.</param>
    /// <returns>The ground, grass when the town is not on the map.</returns>
    public static MapGround GroundOf(Castle castle, World? world)
    {
        ArgumentNullException.ThrowIfNull(castle);

        if (world is null || castle.TileIndex < 0 || castle.TileIndex >= world.Tiles.Count)
        {
            return MapGround.Grass;
        }

        return world.Tiles[castle.TileIndex].Ground;
    }

    /// <summary>
    /// The first <c>OBJNTWBA</c> frame of a ground, the <c>switch</c> at <c>castle.cpp:1335-1363</c>.
    /// </summary>
    /// <param name="ground">The ground.</param>
    /// <returns>Ten per ground, in the order grass, snow, swamp, lava, desert, dirt, wasteland, beach.</returns>
    public static int GroundIndex(MapGround ground) => ground switch
    {
        MapGround.Snow => 10,
        MapGround.Swamp => 20,
        MapGround.Lava => 30,
        MapGround.Desert => 40,
        MapGround.Dirt => 50,
        MapGround.Wasteland => 60,
        MapGround.Beach => 70,
        _ => 0,
    };

    /// <summary>
    /// The first <c>OBJNTOWN</c> frame of a race, the <c>switch</c> at <c>castle.cpp:1380-1401</c>.
    /// </summary>
    /// <param name="race">The town's race.</param>
    /// <returns>Thirty-two per race, in the reference's order.</returns>
    public static int RaceIndex(MapRace race) => race switch
    {
        MapRace.Barbarian => 32,
        MapRace.Sorceress => 64,
        MapRace.Warlock => 96,
        MapRace.Wizard => 128,
        MapRace.Necromancer => 160,
        _ => 0,
    };

    private static void DrawSprite(PixelBuffer target, IcnType icn, int frame, int x, int y)
    {
        if (GameAssets.GetImage(IcnCatalog.GetFileName(icn), frame) is not { } sprite)
        {
            return;
        }

        // The reference adds sprite.x()/sprite.y() by hand, so the frames may hang over their tile.
        ImageOps.BlitSprite(target, new Point(x + sprite.OffsetX, y + sprite.OffsetY), sprite);
    }
}
