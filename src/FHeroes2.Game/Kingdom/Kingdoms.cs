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
    /// <c>world.CountWeek()</c> 提供，端口提到参数上。
    /// </remarks>
    /// <param name="weekCount">Weeks since the start of the game.</param>
    /// <param name="isDebugMode">Whether the reference's <c>IS_DEVEL()</c> build is in effect.</param>
    public void NewWeek(uint weekCount, bool isDebugMode = false)
    {
        foreach (Kingdom kingdom in _kingdoms)
        {
            kingdom.ActionNewWeek(weekCount, isDebugMode);
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
