using System.Collections;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Every hero of the game, <c>AllHeroes</c> (<c>heroes.h:823</c> / <c>heroes.cpp:2265</c>): the container
/// <c>World::vec_heroes</c> (<c>world.h:555</c>) and the recruitment pool of a map.
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps a dense vector of <c>HEROES_COUNT</c> heroes and indexes it by id, so an id is a
/// slot, not a search key. The port does the same, with one difference: a slot that the reference fills
/// with a <c>Heroes( UNKNOWN, KNGT )</c> placeholder — The Price of Loyalty heroes of a map that does not
/// have them, the debug hero of a release build — is simply <see langword="null"/> here. The placeholder
/// exists only because the reference cannot store an empty slot; every consumer immediately tests
/// <c>isValid()</c>, which the null does on its own.
/// </para>
/// <para>
/// <see cref="Init"/> is what <c>World::Defaults()</c> reaches through <c>AllHeroes::Init()</c>
/// (<c>heroes.cpp:2265</c>), and it depends on the map version: The Price of Loyalty heroes only exist
/// for Price of Loyalty and Resurrection maps (<c>heroes.cpp:2291</c>).
/// </para>
/// </remarks>
public sealed partial class AllHeroes : IReadOnlyCollection<Hero>
{
    private readonly Hero?[] _slots = new Hero?[Hero.IdCount];

    /// <summary>Number of heroes in the container, <c>Size()</c>. Absent slots do not count.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// The hero in the slot of <paramref name="heroId"/>, <c>AllHeroes::Get( const int )</c>
    /// (<c>heroes.cpp:2411</c>).
    /// </summary>
    /// <param name="heroId">Hero id.</param>
    /// <returns>
    /// The hero, or <see langword="null"/> when the id is not a hero id or the slot is not filled for this
    /// map.
    /// </returns>
    public Hero? this[int heroId] => Hero.IsValidId(heroId) ? _slots[heroId] : null;

    /// <summary>
    /// The same lookup as the indexer; kept because the reference spells the call <c>Get()</c>.
    /// </summary>
    /// <param name="heroId">Hero id.</param>
    /// <returns>The hero, or <see langword="null"/>.</returns>
    public Hero? Get(int heroId) => this[heroId];

    /// <summary>
    /// The hero standing on a tile, <c>AllHeroes::Get( const fheroes2::Point & )</c>
    /// (<c>heroes.cpp:2404</c>).
    /// </summary>
    /// <remarks>
    /// The reference compares the center points of the heroes with the point it is given; the port stores
    /// the tile index instead of the point (<see cref="Hero.MapIndex"/>), so the test is the same with the
    /// map geometry already applied.
    /// </remarks>
    /// <param name="mapIndex">Tile index.</param>
    /// <returns>The hero, or <see langword="null"/> when no hero stands there.</returns>
    public Hero? GetAtMapIndex(int mapIndex)
    {
        if (mapIndex < 0)
        {
            return null;
        }

        foreach (Hero hero in this)
        {
            if (hero.MapIndex == mapIndex)
            {
                return hero;
            }
        }

        return null;
    }

    /// <summary>
    /// Clears a mode bit on every hero, <c>AllHeroes::ResetModes()</c> (<c>heroes.cpp:2385</c>).
    /// </summary>
    /// <remarks>
    /// The reference has this because the new week needs it: the recruit bit is a property of "every hero at once",
    /// since a hero may have been hired by any kingdom during the week, and only the heroes the kingdoms still offer
    /// get it back. The port could loop the heroes in <c>World::NewWeek</c> itself, but keeping the loop here mirrors
    /// the reference and keeps the world out of the hero table's internals.
    /// </remarks>
    /// <param name="mode">The bit to clear.</param>
    public void ResetModes(HeroModes mode)
    {
        foreach (Hero hero in this)
        {
            hero.ResetModes(mode);
        }
    }

    /// <summary>
    /// The hero imprisoned on a tile, <c>AllHeroes::FromJail()</c> (<c>heroes.cpp:2475</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:2475-2486</c>。两个条件缺一不可：<c>JAIL</c> 模式位，以及位置<b>正好</b>是这个瓦片。
    /// </para>
    /// <para>
    /// 关在监狱里的英雄 <see cref="Hero.Color"/> 仍是 <see cref="PlayerColor.None"/>，看上去和「等着被雇」一样，
    /// 所以「关着」这条信息只能由模式位带上——<see cref="GetHeroForHire"/> 也正是靠 <see cref="HeroModes.Jail"/>
    /// 把他从酒馆候选里剔掉。
    /// </para>
    /// </remarks>
    /// <param name="tileIndex">Tile index of the jail.</param>
    /// <returns>The prisoner, or <see langword="null"/> when no jailed hero sits there.</returns>
    public Hero? FromJail(int tileIndex)
    {
        foreach (Hero hero in this)
        {
            if (hero.Modes.HasFlag(HeroModes.Jail) && hero.MapIndex == tileIndex)
            {
                return hero;
            }
        }

        return null;
    }

    /// <summary>
    /// Fills the slots for a map of the given version, <c>AllHeroes::Init()</c> (<c>heroes.cpp:2265</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The six original races get nine heroes each, then the six campaign heroes, then the eleven The Price
    /// of Loyalty heroes — but only for maps that support them; otherwise those slots stay empty, exactly
    /// like the reference fills them with <c>UNKNOWN</c> placeholders. The debug hero is created only by
    /// <c>IS_DEVEL()</c> builds, so a port that mirrors release builds never creates it
    /// (<c>heroes.cpp:2312</c>).
    /// </para>
    /// <para>
    /// The campaign and The Price of Loyalty heroes start with 5000 experience points
    /// (<c>heroes.cpp:2283</c>); experience belongs to the level-up system, which is not ported, so the
    /// port creates them without it and reports nothing.
    /// </para>
    /// </remarks>
    /// <param name="version">Version of the map being loaded.</param>
    public void Init(MapVersion version)
    {
        Clear();

        foreach (MapRace race in HeroRaces.RegularRaces)
        {
            (int min, int max) = HeroRaces.GetHeroIdRange(race);

            for (int heroId = min; heroId <= max; ++heroId)
            {
                Place(heroId, race);
            }
        }

        // The Succession Wars campaign.
        Place(Hero.Roland, MapRace.Wizard, campaign: true);
        Place(Hero.Corlagon, MapRace.Knight, campaign: true);
        Place(Hero.Eliza, MapRace.Sorceress, campaign: true);
        Place(Hero.Archibald, MapRace.Warlock, campaign: true);
        Place(Hero.Halton, MapRace.Knight, campaign: true);
        Place(Hero.Brax, MapRace.Necromancer, campaign: true);

        if (version is MapVersion.PriceOfLoyalty or MapVersion.Resurrection)
        {
            Place(Hero.Solmyr, MapRace.Wizard, campaign: true);
            Place(Hero.Dainwin, MapRace.Warlock, campaign: true);
            Place(Hero.Mog, MapRace.Necromancer, campaign: true);
            Place(Hero.UncleIvan, MapRace.Barbarian, campaign: true);
            Place(Hero.Joseph, MapRace.Wizard, campaign: true);
            Place(Hero.Gallavant, MapRace.Knight, campaign: true);
            Place(Hero.Elderian, MapRace.Warlock, campaign: true);
            Place(Hero.Ceallach, MapRace.Knight, campaign: true);
            Place(Hero.Drakonia, MapRace.Wizard, campaign: true);
            Place(Hero.Martine, MapRace.Sorceress, campaign: true);
            Place(Hero.Jarkonas, MapRace.Barbarian, campaign: true);
        }
    }

    /// <summary>Empties the container, <c>AllHeroes::Clear()</c> (<c>heroes.h:845</c>).</summary>
    public void Clear()
    {
        Array.Clear(_slots);
        Count = 0;
    }

    /// <summary>
    /// Picks a hero for a tavern, <c>AllHeroes::GetHeroForHire()</c> (<c>heroes.cpp:2322</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference fills a candidate list in up to three passes — the requested race while avoiding
    /// custom portraits, then any race under the same restriction, then any hero at all — and only falls
    /// through when the previous pass found nothing. Only the 54 regular heroes are ever hired: a campaign
    /// or The Price of Loyalty hero never appears in a tavern (<c>heroes.cpp:2349</c>).
    /// </para>
    /// <para>
    /// Among the candidates the heroes already offered by some kingdom's tavern are dropped when possible
    /// (<c>Heroes::RECRUIT</c>), so that two kingdoms do not advertise the same hero; if every candidate is
    /// reserved, the reference allows the sharing rather than returning nothing.
    /// </para>
    /// <para>
    /// The pick itself is <c>Rand::Get()</c> over the candidate ids — with the thread generator, so it is
    /// not reproducible; the map seed does not enter here.
    /// </para>
    /// </remarks>
    /// <param name="race">Race to prefer; <see cref="MapRace.None"/> accepts any.</param>
    /// <param name="heroIdToIgnore">Hero that must not be returned, e.g. the one already offered.</param>
    /// <returns>The hero, or <see langword="null"/> when every hero is hired or imprisoned.</returns>
    public Hero? GetHeroForHire(MapRace race, int heroIdToIgnore = Hero.UnknownId)
    {
        HashSet<int> customPortraits = [];
        foreach (Hero hero in this)
        {
            if (hero.Modes.HasFlag(HeroModes.Custom))
            {
                customPortraits.Add(hero.PortraitId);
            }
        }

        List<int> candidates = [];

        void Fill(MapRace raceFilter, bool avoidCustomHeroes)
        {
            foreach (Hero hero in this)
            {
                // Only regular, non-campaign heroes can be hired.
                if (hero.Id > HeroRaces.MaxRegularHeroId || hero.Id == heroIdToIgnore)
                {
                    continue;
                }

                if (raceFilter != MapRace.None && hero.Race != raceFilter)
                {
                    continue;
                }

                if (!hero.IsAvailableForHire)
                {
                    continue;
                }

                if (avoidCustomHeroes && customPortraits.Contains(hero.PortraitId))
                {
                    continue;
                }

                candidates.Add(hero.Id);
            }
        }

        Fill(race, avoidCustomHeroes: true);

        if (candidates.Count == 0 && race != MapRace.None)
        {
            Fill(MapRace.None, avoidCustomHeroes: true);
        }

        if (candidates.Count == 0)
        {
            Fill(MapRace.None, avoidCustomHeroes: false);
        }

        if (candidates.Count == 0)
        {
            // Every hero is hired or imprisoned. The reference logs a warning and gives up.
            return null;
        }

        List<int> notReserved = [];
        foreach (int heroId in candidates)
        {
            if (!_slots[heroId]!.Modes.HasFlag(HeroModes.Recruit))
            {
                notReserved.Add(heroId);
            }
        }

        int pickedId = notReserved.Count > 0 ? Rand.Get(notReserved) : Rand.Get(candidates);

        return _slots[pickedId];
    }

    /// <summary>
    /// Iterates the heroes that exist in this map, in id order. The reference iterates all slots and
    /// asserts none is <see langword="null"/>; the port skips the empty ones, which is the same list of
    /// real heroes.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public IEnumerator<Hero> GetEnumerator()
    {
        for (int heroId = Hero.LordKilburn; heroId < Hero.IdCount; ++heroId)
        {
            Hero? hero = _slots[heroId];

            if (hero is not null)
            {
                yield return hero;
            }
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Scouts around every hero of the given colors, <c>AllHeroes::Scout()</c> (<c>heroes.cpp:2435-2446</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>heroes.cpp:2435-2446</c>。逐个英雄，<b>颜色不在 <paramref name="colors"/> 里就跳过</b>，否则
    /// <c>hero->Scout( hero->GetIndex() )</c>——以英雄<b>脚下</b>那一格为心，不是他要去的下一格。
    /// </para>
    /// <para>
    /// 参考的 <paramref name="colors"/> 是 <c>Players::GetPlayerFriends( color )</c>，即「我和我的盟友」；端口
    /// 没有联盟，传进来的通常就是一位玩家的颜色。位掩码本身在
    /// <see cref="PlayerColor"/> 上是 <c>[Flags]</c>，所以将来有了盟友不用改签名。
    /// </para>
    /// <para>
    /// 参考按<b>颜色</b>筛，不按别的状态：监狱里的英雄只要还挂着某个颜色也会为他开图，端口照抄——监狱里的英雄
    /// 究竟该不该开图是另一件事，不该在这里悄悄决定。
    /// </para>
    /// </remarks>
    /// <param name="world">The world holding the fog.</param>
    /// <param name="colors">The colors that lose the fog.</param>
    /// <returns>Number of tiles uncovered over all those heroes.</returns>
    public int Scout(World world, PlayerColor colors)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (colors == PlayerColor.None)
        {
            return 0;
        }

        int revealed = 0;

        foreach (Hero hero in this)
        {
            if ((colors & hero.Color) == 0)
            {
                continue;
            }

            revealed += hero.Scout(world, hero.MapIndex);
        }

        return revealed;
    }

    /// <summary>Creates a hero and puts it into its slot, the <c>emplace_back</c> of <c>Init()</c>.</summary>
    /// <param name="heroId">Hero id.</param>
    /// <param name="race">Race of the hero.</param>
    /// <param name="campaign">Whether this is a campaign or The Price of Loyalty hero.</param>
    private void Place(int heroId, MapRace race, bool campaign = false)
    {
        Hero hero = new Hero(heroId, race);

        // Campaign heroes start at 5000, everyone else at the reference's random 40..90
        // (<c>heroes.cpp:2265-2320</c>).
        hero.SetExperience(campaign ? 5000u : Hero.GetStartingExperience());

        _slots[heroId] = hero;
        ++Count;
    }
}
