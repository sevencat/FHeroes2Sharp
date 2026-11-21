using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Monsters;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The archive-backed creature artwork every full-size army bar needs: <c>MONS32</c> mini monsters, the
/// <c>STRIP</c> race plates and the creature portraits, <c>army_bar.cpp:282-306</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把这三张图直接写在 <c>ArmyBar::RedrawItem()</c> 里，端口把它们抽成 <see cref="IArmyBarSprites"/> 以便离屏
/// 测试；这个类就是「从档案里取」的那一份实现，懒加载并缓存，一次会话里每张图只解一次。
/// </para>
/// <para>
/// 三处帧号都来自参考：小图标是 <c>MONS32</c> 的「生物编号 - 1」（精灵从农夫开始排）；种族底板是
/// <c>STRIP</c> 的 <c>4 + 种族序号</c>（骑士 0 … 亡灵 5，未识别用 10）；生物头像取
/// <see cref="MonsterIcnIds.GetSpriteIcn"/> 给的档案的第 0 帧。
/// </para>
/// </remarks>
public sealed class ArmyBarArtwork : IArmyBarSprites
{
    /// <summary>First <c>STRIP</c> frame used as a race plate, <c>ICN::STRIP, 4</c>.</summary>
    public const int FirstRaceFrame = 4;

    /// <summary>The plate the reference uses when it cannot tell the race, <c>ICN::STRIP, 10</c>.</summary>
    public const int UnknownRaceFrame = 10;

    private readonly Dictionary<MonsterType, Sprite?> _miniMonsters = [];
    private readonly Dictionary<MonsterType, Sprite?> _monsterFrames = [];
    private readonly Dictionary<MapRace, Sprite?> _raceFrames = [];

    /// <inheritdoc/>
    public Sprite? GetMiniMonster(MonsterType monster)
    {
        if (_miniMonsters.TryGetValue(monster, out Sprite? cached))
        {
            return cached;
        }

        // 参考：ICN::MONS32, GetID() - 1（精灵从农夫开始）。
        Sprite? sprite = monster == MonsterType.Unknown
            ? null
            : GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Mons32), (int)monster - (int)MonsterType.Peasant);

        _miniMonsters[monster] = sprite;

        return sprite;
    }

    /// <inheritdoc/>
    public Sprite? GetRaceFrame(MapRace race)
    {
        if (_raceFrames.TryGetValue(race, out Sprite? cached))
        {
            return cached;
        }

        Sprite? sprite = GameAssets.GetImage(
            IcnCatalog.GetFileName(IcnType.Strip),
            GetRaceFrameIndex(race));

        _raceFrames[race] = sprite;

        return sprite;
    }

    /// <inheritdoc/>
    public Sprite? GetMonsterFrame(MonsterType monster)
    {
        if (_monsterFrames.TryGetValue(monster, out Sprite? cached))
        {
            return cached;
        }

        // 兵栏里画的是住宅里的立绘（`MONH####`），不是战斗动画那一套档案：动画档案的第 0 帧只是
        // 一个 1×1 的锚点，画出来等于什么都没有（`ui_monster.cpp:29` 的 `monster.ICNMonh()`）。
        IcnType icn = MonsterIcnIds.GetDwellingIcn(monster);

        Sprite? sprite = icn == IcnType.Unknown
            ? null
            : GameAssets.GetImage(IcnCatalog.GetFileName(icn), 0);

        _monsterFrames[monster] = sprite;

        return sprite;
    }

    /// <summary>
    /// The <c>STRIP</c> frame that belongs to a race, <c>ICN::STRIP, 4 + race</c> (<c>army_bar.cpp:288</c>).
    /// </summary>
    /// <param name="race">The creature's race.</param>
    /// <returns>The frame index.</returns>
    public static int GetRaceFrameIndex(MapRace race) => race switch
    {
        MapRace.Knight => FirstRaceFrame,
        MapRace.Barbarian => FirstRaceFrame + 1,
        MapRace.Sorceress => FirstRaceFrame + 2,
        MapRace.Warlock => FirstRaceFrame + 3,
        MapRace.Wizard => FirstRaceFrame + 4,
        MapRace.Necromancer => FirstRaceFrame + 5,
        _ => UnknownRaceFrame,
    };
}
