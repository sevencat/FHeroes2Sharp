using FHeroes2.Assets.Audio;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Game.Maps;

/// <summary>
/// The ambient sound a tile of the adventure map loops, the reference's
/// <c>M82::getAdventureMapTileSound</c> (<c>src/fheroes2/agg/m82.cpp:205</c>).
/// </summary>
/// <remarks>
/// 参考把这个函数放在 <c>agg</c> 目录里，但它按瓦片和对象的 <c>MP2::</c> 值分支，也就是游戏层的类型，所以
/// 端口把它放在这里，音效 id 本身仍然是 <see cref="M82Sound"/>。
/// 与参考的两点差异：
/// <list type="bullet">
/// <item>参考的 <c>tile.getMainObjectType( false )</c> 是「把站在对象上的英雄也算进去」的那个重载；端口的对应
/// 参数是 <c>ignoreObjectUnderHero: false</c>，另外还要传 <see cref="World"/>（端口从世界而不是从瓦片取
/// 对象下的东西）。</item>
/// <item>熔岩那一支在参考里按 <c>icnType</c> 再分：只有岩浆蒸汽（<c>OBJ_ICN_TYPE_UNKNOWN</c>）不发声，小火山
/// （<c>OBJNLAVA</c>）是 <c>LOOP0005</c>，其余火山 <c>LOOP0027</c>。</item>
/// </list>
/// </remarks>
public static class TileAmbientSound
{
    /// <summary>
    /// The ambient sound of a tile, or <see cref="M82Sound.Unknown"/> when the tile is silent.
    /// </summary>
    /// <param name="tile">The tile to ask about.</param>
    /// <param name="world">The world the tile belongs to, needed to see through a hero standing on it.</param>
    /// <returns>The looping sound, or <see cref="M82Sound.Unknown"/>.</returns>
    /// <remarks>参考：<c>m82.cpp:205-286</c>。</remarks>
    public static M82Sound Get(Tile tile, World world)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(world);

        // 河流与溪流在任何对象之上，参考也把它放在 switch 之前。
        if (tile.IsStream)
        {
            return M82Sound.Loop0014;
        }

        switch (tile.GetMainObjectType(world, ignoreObjectUnderHero: false))
        {
        case Mp2ObjectType.Buoy:
            return M82Sound.Loop0000;
        case Mp2ObjectType.Shipwreck:
        case Mp2ObjectType.DerelictShip:
            return M82Sound.Loop0001;
        case Mp2ObjectType.Coast:
            return M82Sound.Loop0002;
        case Mp2ObjectType.Oracle:
            return M82Sound.Loop0003;
        case Mp2ObjectType.StoneLiths:
            return M82Sound.Loop0004;
        case Mp2ObjectType.Volcano:
            switch (tile.MainObjectPart.IcnType)
            {
            // Tile with volcanic steam only
            case Mp2ObjectIcnType.Unknown:
                return M82Sound.Unknown;
            // Small volcanoes
            case Mp2ObjectIcnType.ObjnLava:
                return M82Sound.Loop0005;
            default:
                break;
            }

            // Other volcanoes
            return M82Sound.Loop0027;
        case Mp2ObjectType.LavaPool:
            return M82Sound.Loop0006;
        case Mp2ObjectType.AlchemistLab:
            return M82Sound.Loop0007;
        case Mp2ObjectType.WaterWheel:
            return M82Sound.Loop0009;
        case Mp2ObjectType.Campfire:
            return M82Sound.Loop0010;
        case Mp2ObjectType.Windmill:
            return M82Sound.Loop0011;
        case Mp2ObjectType.ArtesianSpring:
        case Mp2ObjectType.Fountain:
            return M82Sound.Loop0012;
        case Mp2ObjectType.WaterLake:
        case Mp2ObjectType.WateringHole:
            return M82Sound.Loop0013;
        case Mp2ObjectType.Mine:
            return M82Sound.Loop0015;
        case Mp2ObjectType.Sawmill:
            return M82Sound.Loop0016;
        case Mp2ObjectType.DaemonCave:
            return M82Sound.Loop0017;
        case Mp2ObjectType.ShrineFirstCircle:
        case Mp2ObjectType.ShrineSecondCircle:
        case Mp2ObjectType.ShrineThirdCircle:
            return M82Sound.Loop0018;
        case Mp2ObjectType.Rock:
            // This sound should only be played for a specific sprite belonging to Rock
            if (tile.ContainsSprite(Mp2ObjectIcnType.ObjnWatr, 183))
            {
                return M82Sound.Loop0019;
            }

            break;
        case Mp2ObjectType.TarPit:
            return M82Sound.Loop0021;
        case Mp2ObjectType.TradingPost:
            return M82Sound.Loop0022;
        case Mp2ObjectType.Ruins:
            return M82Sound.Loop0024;
        case Mp2ObjectType.PeasantHut:
        case Mp2ObjectType.DwarfCottage:
        case Mp2ObjectType.ArcherHouse:
            return M82Sound.Loop0025;
        case Mp2ObjectType.AbandonedMine:
        case Mp2ObjectType.FreemansFoundry:
            return M82Sound.Loop0026;
        default:
            break;
        }

        return M82Sound.Unknown;
    }
}
