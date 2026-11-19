using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// What a kingdom does when the map starts: topping up the tavern and placing the starting hero,
/// <c>kingdom.cpp:455-557</c>.
/// </summary>
/// <remarks>
/// <para>
/// Both functions need the map — the tavern's list is picked from the heroes the map has not hired yet, and the
/// starting hero is placed by looking at the tile under the castle gate. The port therefore takes the
/// <see cref="World"/> as a parameter instead of reading a global, the same arrangement as the rest of this directory
/// (ADR-111).
/// </para>
/// <para>
/// 参考在这里还读两处全局：<c>Settings</c>（是否战役、地图是否要求开局送英雄）与战役存档（雇得到的奖励英雄、
/// 资源奖励）。端口把这三件事变成参数：<c>isCampaignGame</c>、<c>startWithHeroInFirstCastle</c>、
/// <c>campaignHireableHeroes</c>。这样王国目录不必依赖 <c>Settings</c>，而调用方（开局流程）本来就拿着它们。
/// </para>
/// </remarks>
public sealed partial class Kingdom
{
    /// <summary>
    /// Makes sure the tavern offers two heroes, <c>Kingdom::GetRecruits()</c> (<c>kingdom.cpp:455-497</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order of the two refills is the whole of the reference:
    /// </para>
    /// <list type="number">
    /// <item>a campaign award may name a hero, and that hero is offered <b>instead of</b> a native one — but only to a
    /// human player, only in a campaign, only while the tavern is empty, and only during the first week
    /// (<c>kingdom.cpp:463-478</c>);</item>
    /// <item>the first slot is refilled when it is unknown or when the hero it names is no longer available — hired by
    /// somebody, or imprisoned. An <b>empty tavern</b> asks for a hero of the kingdom's own race; a tavern that still
    /// has its other hero asks for any race, because a second native hero may not exist any more
    /// (<c>:480-487</c>);</item>
    /// <item>the second slot is always refilled with <b>any</b> race, ignoring the first hero so the two differ
    /// (<c>:489-491</c>).</item>
    /// </list>
    /// <para>
    /// 「酒馆空着就招本族英雄」这一条是 fheroes2 自己加的（原版不做），注释里写得很清楚。它带来的副作用是：一周之内
    /// 两位英雄都雇走之后，酒馆的下一位才是「随便哪个族」——本族的英雄可能已经没有了。
    /// </para>
    /// <para>
    /// 参考在末尾断言两位不同（除非都是 <c>UNKNOWN</c>）。端口不抛：<see cref="World.GetHeroForHire"/> 找不到人时
    /// 就回 <see langword="null"/>，酒馆少一位英雄总比崩掉好。
    /// </para>
    /// </remarks>
    /// <param name="world">The map and its hero pool.</param>
    /// <param name="isCampaignGame">Whether this is a campaign map, <c>Settings::isCampaignGameType()</c>.</param>
    /// <param name="campaignHireableHeroes">
    /// The heroes of the obtained <c>TYPE_HIREABLE_HERO</c> awards, the reference's walk over
    /// <c>CampaignSaveData::getObtainedCampaignAwards()</c>. Ignored outside a campaign.
    /// </param>
    /// <returns>The tavern roster, the kingdom's own object.</returns>
    public Recruits GetRecruits(World world, bool isCampaignGame = false, IReadOnlyList<Hero>? campaignHireableHeroes = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        // kingdom.cpp:459 — an empty tavern is the case the special hero is allowed to fill.
        bool offerNativeHero = _recruits.Id1 == Hero.UnknownId && _recruits.Id2 == Hero.UnknownId;

        Hero? specialHireableHero = null;

        if (IsControlHuman && isCampaignGame && offerNativeHero && world.WeekCount < 2 && campaignHireableHeroes is not null)
        {
            foreach (Hero hero in campaignHireableHeroes)
            {
                if (hero.IsAvailableForHire)
                {
                    specialHireableHero = hero;
                }
            }
        }

        // kingdom.cpp:480-487 — the first slot.
        Hero? first = _recruits.GetHero1(world.Heroes);

        if (_recruits.Id1 == Hero.UnknownId || (first is not null && !first.IsAvailableForHire))
        {
            _recruits.SetHero1(specialHireableHero ?? world.GetHeroForHire(offerNativeHero ? GetRace() : MapRace.None, _recruits.Id2));
        }

        // kingdom.cpp:489-491 — the second slot, never the hero the first one names.
        Hero? second = _recruits.GetHero2(world.Heroes);

        if (_recruits.Id2 == Hero.UnknownId || (second is not null && !second.IsAvailableForHire))
        {
            _recruits.SetHero2(world.GetHeroForHire(MapRace.None, _recruits.Id1));
        }

        return _recruits;
    }

    /// <summary>
    /// Puts the heroes the map placed at the gates into their towns, <c>Kingdom::ApplyPlayWithStartingHero()</c>
    /// (<c>kingdom.cpp:504-557</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A map may place a hero on the tile under a castle's gate, meaning "this hero starts in this town". The
    /// reference walks the towns of the kingdom and, for every gate tile that holds a hero of this color:
    /// </para>
    /// <list type="bullet">
    /// <item>moves him onto the town's own tile and teaches him the guild's spells, when he is a real hero
    /// (<c>:525-528</c>);</item>
    /// <item>re-hires him into the town when the hero object is not valid — the map referenced a hero id the pool
    /// does not have, and the only way to make him usable is to recruit him (<c>:529-532</c>);</item>
    /// <item>puts his patrol flag back afterwards, now centred on the town (<c>:534-537</c>).</item>
    /// </list>
    /// <para>
    /// 巡逻那一支容易被忽略：地图里的「守城英雄」常常同时是巡逻英雄，不补回去的话他会立刻离开刚被送进的城。
    /// </para>
    /// <para>
    /// If no town had such a hero and the map asks for one, the first castle of the kingdom (the one that <b>is</b> a
    /// castle, not merely a town) hires a hero of its own race (<c>:543-556</c>) — provided the hero limit allows it,
    /// without checking the gold, because a map that asks for a starting hero must not be blocked by an empty
    /// treasury.
    /// </para>
    /// </remarks>
    /// <param name="world">The map.</param>
    /// <param name="startWithHeroInFirstCastle">
    /// <c>Maps::MapData::startWithHeroInFirstCastle</c> — whether the map asks for a starting hero.
    /// </param>
    public void ApplyPlayWithStartingHero(World world, bool startWithHeroInFirstCastle = false)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!IsPlay || _castles.Count == 0)
        {
            return;
        }

        if (world.Geometry is not { } geometry)
        {
            return;
        }

        bool foundHeroes = false;

        foreach (CastleTown castle in _castles)
        {
            if (!geometry.IsValidAbsIndex(castle.TileIndex))
            {
                continue;
            }

            // kingdom.cpp:518-519 — the gate is the tile right under the castle's centre.
            Point center = geometry.GetPoint(castle.TileIndex);
            int gateIndex = geometry.GetIndexFromAbsPoint(center.X, center.Y + 1);

            if (!geometry.IsValidAbsIndex(gateIndex))
            {
                continue;
            }

            Hero? hero = world.Tiles[gateIndex].GetHero(world);

            if (hero is null || hero.Color != _color)
            {
                continue;
            }

            bool patrol = (hero.Modes & HeroModes.Patrol) != 0;

            if (hero.IsValid)
            {
                hero.MoveToDest(castle.TileIndex, world);
                castle.TrainHeroInMageGuild(hero);
            }
            else
            {
                hero.Recruit(this, castle.TileIndex, world);
            }

            if (patrol)
            {
                hero.SetModes(HeroModes.Patrol);
                hero.SetPatrolCenter(castle.TileIndex);
            }

            foundHeroes = true;
        }

        if (foundHeroes || !startWithHeroInFirstCastle)
        {
            return;
        }

        CastleTown? first = _castles.FirstOrDefault(castle => castle.IsCastle) ?? _castles[0];

        if (first is null)
        {
            return;
        }

        Hero? startingHero = world.GetHeroForHire(first.Race);

        // kingdom.cpp:553 — no payment check: the map asked for a hero, so the map gets one.
        if (startingHero is not null && AllowRecruitHero(false))
        {
            startingHero.Recruit(this, first.TileIndex, world);
        }
    }
}
