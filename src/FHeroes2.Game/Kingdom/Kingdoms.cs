using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Heroes;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The seven kingdoms of a world, the reference's <c>Kingdoms</c> (<c>kingdom.h:294</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>kingdom.h:294</c> class Kingdoms（<c>GetKingdom</c> 297–305、<c>AddHeroes</c> 308、<c>AddCastles</c> 309、
/// <c>Init</c> 302）、<c>kingdom.cpp:688</c>（<c>Init</c>）、<c>:696</c>（<c>clear</c>）、<c>:710/732</c>（两个
/// <c>GetKingdom</c>）、<c>:769</c>（<c>GetNotLossColors</c>）、<c>:790/802</c>（<c>AddHeroes</c>/<c>AddCastles</c>）。
/// </para>
/// <para>
/// 参考的表有七个槽：六个玩家色按 map 顺序排，第七个是「没有颜色」的垃圾桶，任何非法或 <c>NONE</c> 的颜色都落
/// 在那里。端口照抄，因为调用方常拿一个可能为 <c>NONE</c> 的颜色来查，返回 <see langword="null"/> 会让每处都写
/// 空判断。
/// </para>
/// </remarks>
public sealed class Kingdoms
{
    /// <summary>
    /// How many playable colors a world can have, <c>Color::Count()</c>.
    /// </summary>
    public const int MaxPlayers = 6;

    /// <summary>Index of the bin that holds the kingdoms of the unknown colors.</summary>
    private const int NoneIndex = MaxPlayers;

    private readonly Kingdom[] _kingdoms = CreateKingdoms();

    /// <summary>
    /// The kingdom of <paramref name="color"/>, <c>Kingdoms::GetKingdom()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.cpp:710/732</c>。非法颜色（包括 <see cref="PlayerColor.None"/> 与多色位掩码）返回第七个
    /// 槽，与参考一致。
    /// </remarks>
    /// <param name="color">A single player color.</param>
    /// <returns>The kingdom; never <see langword="null"/>.</returns>
    public Kingdom GetKingdom(PlayerColor color) => _kingdoms[IndexOf(color)];

    /// <summary>
    /// Advances every kingdom by a day, <c>World::NewDay()</c>'s <c>_kingdoms.NewDay()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>world.cpp:443</c>（<c>kingdoms.NewDay()</c>）→<c>kingdom.cpp:770</c>（<c>Kingdoms::NewDay()</c>）。
    /// 七个槽全走一遍，包括「没有颜色」的垃圾桶——它里面的王国颜色是 <see cref="PlayerColor.None"/>，
    /// <see cref="Kingdom.IsPlay"/> 为假，因此只会清掉一天寿命的访问记录，其余什么都不做。
    /// </remarks>
    /// <param name="dayCount">Days since the start of the game.</param>
    /// <param name="lossPostActions">
    /// What to do for a kingdom that just went out of the game; the reference's <c>LossPostActions()</c>.
    /// </param>
    public void NewDay(uint dayCount, Action<Kingdom>? lossPostActions = null)
    {
        foreach (Kingdom kingdom in _kingdoms)
        {
            kingdom.ActionNewDay(dayCount, lossPostActions is null ? null : () => lossPostActions(kingdom));
        }
    }

    /// <summary>
    /// Advances every kingdom by a week, <c>Kingdoms::NewWeek()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>world.cpp:426</c>（<c>kingdoms.NewWeek()</c>）→<c>kingdom.cpp:778</c>。周数在参考里由
    /// <c>world.CountWeek()</c> 提供，端口提到参数上；<paramref name="world"/> 只为了让每个王国补位酒馆的那两位
    /// 英雄（<c>kingdom.cpp:268</c>），七个王国本身仍然不持有世界（ADR-111）。
    /// </remarks>
    /// <param name="world">The map and its hero pool.</param>
    /// <param name="weekCount">Weeks since the start of the game.</param>
    /// <param name="isDebugMode">Whether the reference's <c>IS_DEVEL()</c> build is in effect.</param>
    public void NewWeek(World world, uint weekCount, bool isDebugMode = false)
    {
        foreach (Kingdom kingdom in _kingdoms)
        {
            kingdom.ActionNewWeek(world, weekCount, isDebugMode);
        }
    }

    /// <summary>
    /// Whether any kingdom has visited that tile for that object, <c>World::isAnyKingdomVisited()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考：<c>world.cpp</c> 的 <c>World::isAnyKingdomVisited( objectType, index )</c>：遍历全部七个槽，
    /// 任意一个记过就算。泉眼（<c>OBJ_ARTESIAN_SPRING</c>）用它回答「本周是不是已经有人喝过了」。
    /// </para>
    /// <para>
    /// 注意它问的<b>不是</b>「我方势力」，而是「任何一方」——参考的实现没有任何颜色过滤，端口照抄。
    /// </para>
    /// </remarks>
    /// <param name="index">Tile index on the map.</param>
    /// <param name="objectType">The object type.</param>
    /// <returns><see langword="true"/> when some kingdom has the pair in its visit list.</returns>
    public bool IsAnyVisited(int index, Mp2ObjectType objectType) =>
        _kingdoms.Any(kingdom => kingdom.IsVisited(index, objectType));

    /// <summary>
    /// Empties the tavern of every kingdom and reports the heroes that must stay on offer,
    /// <c>Kingdoms::resetRecruits()</c> (<c>kingdom.cpp:814</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference calls this from <c>World::NewWeek()</c>, between clearing the recruit mode of every hero and
    /// restoring it: clearing is a blanket operation, so the world has to be told which heroes are still offered
    /// afterwards.
    /// </para>
    /// <para>
    /// <b>A hero that surrendered on the last day of the previous week is offered again</b> — even though the new
    /// week has just emptied the slots. That is an fheroes2 extension to the original game, which offers only the hero
    /// who surrendered last; the day check is what keeps an old surrender from lingering forever.
    /// </para>
    /// <para>
    /// A kingdom that is no longer in play has both slots emptied regardless of the day, because nobody may hire from
    /// it any more.
    /// </para>
    /// </remarks>
    /// <param name="dayCount">Days since the start of the game, <c>world.CountDay()</c>.</param>
    /// <param name="heroes">
    /// The hero table the slots are resolved against; the reference reaches <c>world.GetHeroes()</c>, the port takes it
    /// as a parameter so that the seven kingdoms stay free of the world (ADR-111).
    /// </param>
    /// <returns>The heroes that stay on offer, without duplicates.</returns>
    public HashSet<Hero> ResetRecruits(uint dayCount, AllHeroes heroes)
    {
        var remainingRecruits = new HashSet<Hero>();

        foreach (Kingdom kingdom in _kingdoms)
        {
            Recruits recruits = kingdom.GetRecruits();

            // The subtraction stays signed: a saved game may hold a surrender day after the current one, and the
            // reference's unsigned arithmetic would then consider the hero fresh forever.
            if (MustEmptySlot(kingdom, recruits.GetSurrenderDayOfHero1(), dayCount))
            {
                recruits.SetHero1(null);
            }
            else
            {
                Hero? hero = recruits.GetHero1(heroes);

                if (hero is not null)
                {
                    remainingRecruits.Add(hero);
                }
            }

            if (MustEmptySlot(kingdom, recruits.GetSurrenderDayOfHero2(), dayCount))
            {
                recruits.SetHero2(null);
            }
            else
            {
                Hero? hero = recruits.GetHero2(heroes);

                if (hero is not null)
                {
                    remainingRecruits.Add(hero);
                }
            }
        }

        return remainingRecruits;
    }

    /// <summary>
    /// Whether a recruit slot must be emptied, <c>Kingdoms::resetRecruits()</c>'s condition
    /// (<c>kingdom.cpp:823/833</c>).
    /// </summary>
    /// <param name="kingdom">The kingdom the slot belongs to.</param>
    /// <param name="surrenderDay">The day the hero surrendered on, 0 when it never did.</param>
    /// <param name="dayCount">Days since the start of the game.</param>
    /// <returns>Whether the slot goes.</returns>
    /// <remarks>
    /// A kingdom out of play empties both slots at once: nobody may hire from it any more, so keeping the heroes on
    /// offer would leak the recruit mode onto heroes that no tavern shows.
    /// </remarks>
    private static bool MustEmptySlot(Kingdom kingdom, uint surrenderDay, uint dayCount) =>
        !kingdom.IsPlay || (int)dayCount - (int)surrenderDay > 1;

    /// <summary>
    /// Wipes every kingdom, <c>Kingdoms::clear()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:696</c>；<c>Init</c> 的第一步。</remarks>
    public void Clear()
    {
        foreach (Kingdom kingdom in _kingdoms)
        {
            kingdom.Clear();
        }
    }

    /// <summary>
    /// Starts a kingdom for every color of <paramref name="colors"/>, <c>Kingdoms::Init()</c>.
    /// </summary>
    /// <remarks>
    /// <para>参考：<c>kingdom.cpp:688</c>——先 <c>clear()</c>，再只给「在局中」的颜色 <c>Init</c>，其余留在清空态。</para>
    /// <para>
    /// 参考的颜色表来自 <c>Settings::Get().GetPlayers()</c>，端口把「哪些颜色在局中」「哪些颜色是电脑」提到参数
    /// 上（<c>Mp2FileInfo.KingdomColors</c> / <c>ComputerPlayers</c> 就是这两张表），难度同理。
    /// </para>
    /// </remarks>
    /// <param name="colors">The colors in play, as a bit mask of <see cref="PlayerColors.All"/>.</param>
    /// <param name="difficulty">Map difficulty, which decides the starting resources.</param>
    /// <param name="computerColors">The colors the computer plays, as a bit mask.</param>
    public void Init(PlayerColor colors, GameDifficulty difficulty, PlayerColor computerColors)
    {
        Clear();

        foreach (PlayerColor color in PlayerColors.ToList(colors))
        {
            GetKingdom(color).Init(color, difficulty, (computerColors & color) != 0);
        }
    }

    /// <summary>
    /// Puts every non-neutral hero into the kingdom of its color, <c>Kingdoms::AddHeroes()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:790</c>；中立英雄没有主人，跳过。</remarks>
    /// <param name="heroes">The hero table of the world.</param>
    public void AddHeroes(AllHeroes heroes)
    {
        ArgumentNullException.ThrowIfNull(heroes);

        foreach (Hero hero in heroes)
        {
            if (hero.Color != PlayerColor.None)
            {
                GetKingdom(hero.Color).AddHero(hero);
            }
        }
    }

    /// <summary>
    /// Puts every non-neutral town into the kingdom of its color, <c>Kingdoms::AddCastles()</c>.
    /// </summary>
    /// <remarks>参考：<c>kingdom.cpp:802</c>；中立城镇没有主人，跳过。</remarks>
    /// <param name="castles">The town table of the world.</param>
    public void AddCastles(AllCastles castles)
    {
        ArgumentNullException.ThrowIfNull(castles);

        foreach (CastleTown castle in castles)
        {
            if (castle.Color != PlayerColor.None)
            {
                GetKingdom(castle.Color).AddCastle(castle);
            }
        }
    }

    /// <summary>
    /// The colors of the kingdoms that are still in the game, <c>Kingdoms::GetNotLossColors()</c>.
    /// </summary>
    /// <remarks>
    /// 参考：<c>kingdom.cpp:769</c>。颜色为 <see cref="PlayerColor.None"/> 的槽不参与，出局（<see cref="Kingdom.IsLoss"/>）
    /// 的也不参与。
    /// </remarks>
    /// <returns>A bit mask of the surviving colors; <see cref="PlayerColor.None"/> when nobody is left.</returns>
    public PlayerColor GetNotLossColors()
    {
        PlayerColor result = PlayerColor.None;

        foreach (Kingdom kingdom in _kingdoms)
        {
            if (kingdom.Color != PlayerColor.None && !kingdom.IsLoss)
            {
                result |= kingdom.Color;
            }
        }

        return result;
    }

    /// <summary>
    /// The colors of the kingdoms that take part, <c>Game::GetActualKingdomColors()</c> 的替身
    /// (<c>game.cpp:456</c> 经 <c>Players::GetActualColors()</c>).
    /// </summary>
    /// <remarks>
    /// 参考问的是 <c>Settings</c> 里的玩家槽，端口还没有开局流程去填那些槽；而槽里的颜色正是
    /// <see cref="Init"/> 建王国时用的同一份表，所以这里直接问王国自己。颜色为
    /// <see cref="PlayerColor.None"/> 的槽不算。
    /// </remarks>
    /// <returns>A bit mask of the colors in play; <see cref="PlayerColor.None"/> when the world has no kingdom.</returns>
    public PlayerColor GetColors()
    {
        PlayerColor result = PlayerColor.None;

        foreach (Kingdom kingdom in _kingdoms)
        {
            result |= kingdom.Color;
        }

        return result;
    }

    /// <summary>
    /// The color of the first kingdom that met <paramref name="condition"/>,
    /// <c>Kingdoms::FindWins()</c> (<c>kingdom.cpp:780</c>).
    /// </summary>
    /// <remarks>
    /// 「对手先赢了」这三条失败条件要问所有王国，参考因此把它反过来：给定一条胜利条件，找出谁先满足。只有颜色不为
    /// <see cref="PlayerColor.None"/> 的槽参与，顺序就是槽的顺序（蓝、绿、红、黄、橙、紫），所以同一天多人满足时
    /// 报的是槽位靠前的那个。
    /// </remarks>
    /// <param name="condition">One <see cref="GameOver"/> winning flag.</param>
    /// <param name="world">The world the conditions are read from.</param>
    /// <returns>The color, or <see cref="PlayerColor.None"/> when nobody has met it.</returns>
    public PlayerColor FindWins(uint condition, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (Kingdom kingdom in _kingdoms)
        {
            if (kingdom.Color != PlayerColor.None && world.KingdomIsWins(kingdom, condition))
            {
                return kingdom.Color;
            }
        }

        return PlayerColor.None;
    }

    /// <summary>
    /// Slot of a color, <c>kingdom.cpp:710-730</c> 的 <c>switch</c>。
    /// </summary>
    /// <param name="color">A single player color.</param>
    /// <returns>0 for blue … 5 for purple, and <see cref="NoneIndex"/> for anything else.</returns>
    private static int IndexOf(PlayerColor color) => color switch
    {
        PlayerColor.Blue => 0,
        PlayerColor.Green => 1,
        PlayerColor.Red => 2,
        PlayerColor.Yellow => 3,
        PlayerColor.Orange => 4,
        PlayerColor.Purple => 5,
        _ => NoneIndex,
    };

    /// <summary>Builds the seven slots; the array is fixed, the kingdoms inside are not.</summary>
    /// <returns>The array.</returns>
    private static Kingdom[] CreateKingdoms()
    {
        Kingdom[] kingdoms = new Kingdom[MaxPlayers + 1];

        for (int index = 0; index < kingdoms.Length; ++index)
        {
            kingdoms[index] = new Kingdom();
        }

        return kingdoms;
    }
}
