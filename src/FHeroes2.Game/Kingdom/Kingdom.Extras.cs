using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The questions the rest of the game asks a kingdom, <c>kingdom.cpp:141-441</c> and <c>:648-686</c>.
/// </summary>
/// <remarks>
/// <para>
/// These are the members that need something the kingdom does not own: the player table
/// (<c>GetControl</c>, <c>GetRace</c>, the two <c>opponents*</c> questions), the map
/// (<see cref="HeroesMayStillMove"/>), or the map geometry (the Crystal Ball). The treasury, the rosters and the day
/// cycle live in <c>Kingdom.cs</c>; the world bound parts of the start-up live in <c>Kingdom.Startup.cs</c>.
/// </para>
/// <para>
/// 参考的同名函数都是「一行转调」，但每一行都在问别人：<c>Players</c>、<c>world.GetKingdom</c>、<c>hero->MayStillMove</c>。
/// 端口把这些「别人」按 ADR-111 的老规矩放到参数上，王国自己仍是可单独构造、可单独测试的。
/// </para>
/// </remarks>
public sealed partial class Kingdom
{
    /// <summary>
    /// Who plays this kingdom, <c>Kingdom::GetControl()</c> (<c>kingdom.cpp:141-144</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考直接问 <c>Players::GetPlayerControl( _color )</c>。端口的 <see cref="Players"/> 只有在开局流程装配过之后
    /// 才有这张表，而王国可以在没有开局流程的测试里单独建出来，所以这里分两步：玩家表里有这个颜色就用它，
    /// 否则退回 <see cref="Init"/> 记下的那个布尔。
    /// </para>
    /// <para>
    /// 退回这一步不会掩盖错误：没有玩家表时，<see cref="Players.GetPlayerControl"/> 对任何颜色都回
    /// <see cref="PlayerControl.None"/>，那正是「表还没建」的意思。
    /// </para>
    /// </remarks>
    /// <returns>The control flags.</returns>
    public PlayerControl GetControl()
    {
        PlayerControl control = Players.GetPlayerControl(_color);

        if (control != PlayerControl.None)
        {
            return control;
        }

        if (_color == PlayerColor.None)
        {
            return PlayerControl.None;
        }

        return _isAiControlled ? PlayerControl.Ai : PlayerControl.Human;
    }

    /// <summary>
    /// Whether the computer plays this kingdom, <c>isControlAI()</c> (<c>kingdom.h:106</c>).
    /// </summary>
    /// <remarks>与 <see cref="IsControlHuman"/> 互补；两者都走 <see cref="GetControl"/> 的同一份判据。</remarks>
    public bool IsControlAi => GetControl().IsAi();

    /// <summary>
    /// The race of the player's castle, <c>Kingdom::GetRace()</c> (<c>kingdom.cpp:146-149</c>).
    /// </summary>
    /// <remarks>
    /// 参考问的是 <c>Players::GetPlayerRace</c>，也就是「开局时选的那个族」，而不是名册里城堡的实际种族——
    /// 一个玩家占下别的族的城，他的 <c>GetRace</c> 不变。<see cref="MapRace.Random"/> 也原样传回。
    /// </remarks>
    /// <returns>The race; <see cref="MapRace.None"/> when there is no player slot for the color.</returns>
    public MapRace GetRace() => Players.GetPlayerRace(_color);

    /// <summary>
    /// How many heroes this kingdom may have, <c>Kingdom::GetMaxHeroes()</c> (<c>kingdom.cpp:559-562</c>).
    /// </summary>
    /// <remarks>
    /// 参考转调 <c>GameStatic::GetKingdomMaxHeroes()</c>，那一支恒为 8，端口的 <see cref="MaxHeroes"/> 就是它。
    /// 单列一个方法是因为参考里它是虚的（战役可以改），调用方（<see cref="AllowRecruitHero"/>、
    /// <see cref="OpponentsCanRecruitMoreHeroes"/>）按方法名读更贴近原文。
    /// </remarks>
    /// <returns>The hero limit.</returns>
    public int GetMaxHeroes() => MaxHeroes;

    /// <summary>
    /// How many artifacts all the heroes of the kingdom carry, <c>Kingdom::GetCountArtifacts()</c>
    /// (<c>kingdom.cpp:376-382</c>).
    /// </summary>
    /// <remarks>
    /// 参考把每位英雄的 <c>GetCountArtifacts()</c> 加起来——是<b>件数</b>，不是种类数。城堡与酒馆的宝物不参与。
    /// </remarks>
    /// <returns>The number of artifacts.</returns>
    public int GetCountArtifacts()
    {
        int count = 0;

        foreach (Hero hero in _heroes)
        {
            count += hero.GetCountArtifacts();
        }

        return count;
    }

    /// <summary>
    /// Takes the kingdom out of the game for good, <c>Kingdom::LossPostActions()</c> (<c>kingdom.cpp:156-182</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reference does three things, in this order, and the order matters:
    /// </para>
    /// <list type="number">
    /// <item>mark the player as out of the game, so that nothing else talks to the kingdom any more;</item>
    /// <item>dismiss every hero — which removes him from the roster on the way — because a hero of a dead player
    /// would otherwise stay on the map with a color nobody controls;</item>
    /// <item>hand every town back to neutral and empty the roster.</item>
    /// </list>
    /// <para>
    /// Then the world is told to forget the objects this color had captured, so that the mines and the windmill flow
    /// to whoever takes them next.
    /// </para>
    /// <para>
    /// 参考直接调 <c>hero->Dismiss()</c> 与 <c>castle->ChangeColor()</c>；端口这两件事分别属于英雄与城堡的装配层
    /// （<see cref="Hero"/> 还没有 <c>Dismiss</c>，<see cref="CastleTown"/> 的改色在占领流程里，ADR-108），因此
    /// 以委托传入。三个委托都可以省：只做名册这一半（把英雄与城从名单里摘掉、把王国标成出局）本身也是自洽的，
    /// 测试常用这种简写。
    /// </para>
    /// <para>
    /// <b>英雄的循环必须防住「委托不动名册」</b>：参考靠 <c>Dismiss</c> 顺手 <c>RemoveHero</c> 让
    /// <c>while ( !heroes.empty() )</c> 收敛，端口不能假设委托会这么做，所以每轮先取最后一位、调委托，再确认
    /// 它真的离开了名册，否则手动摘掉——不然一个没实现的委托就会变成死循环。
    /// </para>
    /// </remarks>
    /// <param name="dismissHero">What to do with a hero, the reference's <c>hero-&gt;Dismiss( RESULT_LOSS )</c>.</param>
    /// <param name="surrenderCastle">What to do with a town, the reference's <c>castle-&gt;ChangeColor( NONE )</c>.</param>
    /// <param name="resetCapturedObjects">
    /// The reference's <c>world.ResetCapturedObjects( color )</c>, told the color that just lost.
    /// </param>
    public void LossPostActions(
        Action<Hero>? dismissHero = null,
        Action<CastleTown>? surrenderCastle = null,
        Action<PlayerColor>? resetCapturedObjects = null)
    {
        if (!IsPlay)
        {
            return;
        }

        PlayerColor color = _color;

        // kingdom.cpp:162 — from here on the player is out, whatever the caller does with the rosters.
        Players.SetPlayerInGame(color, false);
        _isOutOfGame = true;

        // kingdom.cpp:164-171 — the reference drains the list through Dismiss(), which removes the hero itself.
        while (_heroes.Count > 0)
        {
            Hero hero = _heroes[^1];

            dismissHero?.Invoke(hero);

            if (_heroes.Count > 0 && ReferenceEquals(_heroes[^1], hero))
            {
                _heroes.RemoveAt(_heroes.Count - 1);
            }
        }

        // kingdom.cpp:173-179 — the towns go back to neutral, then the roster is emptied.
        foreach (CastleTown castle in _castles)
        {
            surrenderCastle?.Invoke(castle);
        }

        _castles.Clear();

        // kingdom.cpp:181 — the world forgets what this color had captured.
        resetCapturedObjects?.Invoke(color);
    }

    /// <summary>
    /// Whether an opponent could still hire a hero from a town, <c>Kingdom::opponentsCanRecruitMoreHeroes()</c>
    /// (<c>kingdom.cpp:420-427</c>).
    /// </summary>
    /// <remarks>
    /// 参考走 <c>Players::getInPlayOpponents( color )</c>（在局中、且不是本方的盟友），再问那一方的
    /// <c>canRecruitHeroes()</c>——注意问的是「有没有城能出兵」，不是「名额与钱够不够」
    /// （那是 <see cref="AllowRecruitHero"/>，两者的区别在 <see cref="CanRecruitHeroes"/> 的注释里）。
    /// 这个答案决定「英雄消失」这条失败条件会不会触发。
    /// </remarks>
    /// <param name="getKingdom">The kingdom of a color, the reference's <c>world.GetKingdom( color )</c>.</param>
    /// <returns><see langword="true"/> when at least one opponent could raise a hero.</returns>
    public bool OpponentsCanRecruitMoreHeroes(Func<PlayerColor, Kingdom?> getKingdom)
    {
        ArgumentNullException.ThrowIfNull(getKingdom);

        foreach (PlayerColor color in Players.GetInPlayOpponents(_color))
        {
            Kingdom? kingdom = getKingdom(color);

            if (kingdom is not null && kingdom.CanRecruitHeroes)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an opponent still has a hero, <c>Kingdom::opponentsHaveHeroes()</c> (<c>kingdom.cpp:429-436</c>).
    /// </summary>
    /// <remarks>
    /// 与 <see cref="OpponentsCanRecruitMoreHeroes"/> 同一张对手表，问的是 <c>hasHeroes()</c>。两条一起决定
    /// 「英雄消失」的失败条件：本方没有英雄、对手也没有英雄、而对手又招不出新英雄时，这一局就没有人能动了。
    /// </remarks>
    /// <param name="getKingdom">The kingdom of a color, the reference's <c>world.GetKingdom( color )</c>.</param>
    /// <returns><see langword="true"/> when at least one opponent still has a hero.</returns>
    public bool OpponentsHaveHeroes(Func<PlayerColor, Kingdom?> getKingdom)
    {
        ArgumentNullException.ThrowIfNull(getKingdom);

        foreach (PlayerColor color in Players.GetInPlayOpponents(_color))
        {
            Kingdom? kingdom = getKingdom(color);

            if (kingdom is not null && kingdom.HasHeroes)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether any hero of the kingdom can still move today, <c>Kingdom::HeroesMayStillMove()</c>
    /// (<c>kingdom.cpp:438-441</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的 <c>any_of</c> 给每位的判据是 <c>MayStillMove( false, false )</c>：<b>不</b>忽略路线，<b>不</b>忽略
    /// 「原地待命」。所以一位被玩家点了待命的英雄<b>不</b>算能再动——「结束回合」按钮在这种情况下是暗的，这正是
    /// 玩家想要的行为。
    /// </para>
    /// <para>
    /// 移动力是否为 0 只是其中一半，另一半在位图与寻路技能上（<see cref="Hero.CanMove"/>），所以地图要传进来。
    /// </para>
    /// </remarks>
    /// <param name="world">The map the heroes stand on.</param>
    /// <returns><see langword="true"/> when at least one hero may still move.</returns>
    public bool HeroesMayStillMove(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        foreach (Hero hero in _heroes)
        {
            if (hero.MayStillMove(world, false, false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The hero with the strongest army, <c>Kingdom::GetBestHero()</c> (<c>kingdom.cpp:648-651</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考用 <c>std::max_element</c> 配 <c>HeroesStrongestArmy</c>，那个仿函数比的是
    /// <c>GetArmy().GetStrength()</c>——整支军队的强度，含数量。同强度时 <c>max_element</c> 保住<b>先</b>出现的
    /// 那一位，所以这里的 <c>&gt;</c> 不能换成 <c>&gt;=</c>。
    /// </para>
    /// <para>
    /// 与 <see cref="GetStrongestMonster"/> 的区别在于：那个问「最强的一<b>只</b>生物」，这个问「最强的一<b>支</b>部队」。
    /// </para>
    /// </remarks>
    /// <returns>The hero, or <see langword="null"/> when the kingdom has none.</returns>
    public Hero? GetBestHero()
    {
        Hero? best = null;

        foreach (Hero hero in _heroes)
        {
            if (best is null || hero.Army.GetStrength() > best.Army.GetStrength())
            {
                best = hero;
            }
        }

        return best;
    }

    /// <summary>
    /// The strongest single creature any army of the kingdom can field, <c>Kingdom::GetStrongestMonster()</c>
    /// (<c>kingdom.cpp:653-669</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考走两轮：先英雄的军队，再城堡驻军，比较的是<b>单体</b>强度（<c>Monster::GetMonsterStrength()</c>），
    /// 不是整支部队。这就是 AI 判断「对面能不能一回合打穿我」时用的那个数。
    /// </para>
    /// <para>
    /// 起点是 <c>Monster::UNKNOWN</c>，而 <c>UNKNOWN</c> 的单体强度是 0，所以空王国（没有英雄也没有城）回
    /// <see cref="MonsterType.Unknown"/>。
    /// </para>
    /// </remarks>
    /// <returns>The monster, or <see cref="MonsterType.Unknown"/>.</returns>
    public MonsterType GetStrongestMonster()
    {
        MonsterType result = MonsterType.Unknown;
        double strength = MonsterStatistics.GetMonsterStrength(result);

        foreach (Hero hero in _heroes)
        {
            MonsterType current = hero.Army.GetStrongestMonster();
            double currentStrength = MonsterStatistics.GetMonsterStrength(current);

            if (currentStrength > strength)
            {
                result = current;
                strength = currentStrength;
            }
        }

        foreach (CastleTown castle in _castles)
        {
            MonsterType current = castle.Army.GetStrongestMonster();
            double currentStrength = MonsterStatistics.GetMonsterStrength(current);

            if (currentStrength > strength)
            {
                result = current;
                strength = currentStrength;
            }
        }

        return result;
    }

    /// <summary>
    /// The total strength of every army the kingdom has, <c>Kingdom::GetArmiesStrength()</c>
    /// (<c>kingdom.cpp:671-686</c>).
    /// </summary>
    /// <remarks>
    /// 英雄的军队加上城堡驻军，全部照 <see cref="Army.GetStrength"/> 累加——那是「部队强度」，含数量、含攻防
    /// 加成，与 <see cref="GetStrongestMonster"/> 的单体强度不是一回事。参考用它算「谁更该被 AI 照顾」。
    /// </remarks>
    /// <returns>The sum of the strengths.</returns>
    public double GetArmiesStrength()
    {
        double total = 0;

        foreach (Hero hero in _heroes)
        {
            total += hero.Army.GetStrength();
        }

        foreach (CastleTown castle in _castles)
        {
            total += castle.Army.GetStrength();
        }

        return total;
    }

    /// <summary>
    /// Offers a hero that surrendered or retreated in the tavern, <c>Kingdom::appendSurrenderedHero()</c>
    /// (<c>kingdom.cpp:754-757</c>).
    /// </summary>
    /// <remarks>
    /// 参考一行：<c>recruits.appendSurrenderedHero( hero, world.CountDay() )</c>。日子由调用方给——投降发生的那一天
    /// 决定这位英雄下周还在不在酒馆里（<see cref="Kingdoms.ResetRecruits"/> 用同一份判据）。
    /// </remarks>
    /// <param name="hero">The hero that surrendered or retreated.</param>
    /// <param name="surrenderDay">Days since the start of the game, 1 or later.</param>
    public void AppendSurrenderedHero(Hero hero, uint surrenderDay)
    {
        ArgumentNullException.ThrowIfNull(hero);

        _recruits.AppendSurrenderedHero(hero, surrenderDay);
    }

    /// <summary>
    /// Whether a hero with a Crystal Ball can see that tile, <c>Kingdom::IsTileVisibleFromCrystalBall()</c>
    /// (<c>kingdom.cpp:848-863</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 三条件缺一不可：某位英雄带着 <c>查看怪物信息</c> 的宝物（水晶球）、那位英雄站在图上、以及英雄与目标的
    /// <b>直线</b>距离不超过 <see cref="Hero.GetVisionsDistance"/>（8 格）。参考这里用的是
    /// <c>Maps::GetStraightLineDistance</c>，不是 <c>GetApproximateDistance</c>——它不绕障碍，量的是两点之间。
    /// </para>
    /// <para>
    /// 那件宝物是「带在身上就生效」，不是「用了才生效」，所以没有法术点、没有施法者检定的环节。
    /// </para>
    /// </remarks>
    /// <param name="geometry">The map geometry, <c>world.Geometry</c>.</param>
    /// <param name="destination">The tile index to look at.</param>
    /// <returns><see langword="true"/> when a hero's Crystal Ball covers the tile.</returns>
    public bool IsTileVisibleFromCrystalBall(MapGeometry geometry, int destination)
    {
        uint distance = Hero.GetVisionsDistance();

        foreach (Hero hero in _heroes)
        {
            if (hero.MapIndex < 0 || hero.MapIndex >= geometry.TileCount)
            {
                continue;
            }

            if (hero.BagArtifacts.IsBonusPresent(ArtifactBonusType.ViewMonsterInformation) &&
                geometry.GetStraightLineDistance(hero.MapIndex, destination) <= distance)
            {
                return true;
            }
        }

        return false;
    }
}
