using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using ArmyStack = FHeroes2.Game.Army.Army;
using CastleTown = FHeroes2.Game.Castle.Castle;
using GameKingdom = FHeroes2.Game.Kingdom.Kingdom;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// The spell book, the spell points and the strategic value of the spells of a hero:
/// <c>heroes_base.h:118-150</c>, <c>heroes_base.cpp:172-186</c>, <c>heroes_base.cpp:358-377</c> and
/// <c>heroes.cpp:967-970</c>.
/// </summary>
/// <remarks>
/// <para>
/// A hero knows spells through two things: the <b>spell book</b>, an artifact that must be in the bag before
/// any spell can be cast (<see cref="HasSpellBook"/>), and the <b>spell scrolls</b>, artifacts that carry one
/// spell each. <see cref="GetAllSpells"/> merges both, which is why a hero without a book has no spells at
/// all even when the bag is full of scrolls.
/// </para>
/// <para>
/// The spell points are a plain counter with a maximum of ten per point of knowledge
/// (<c>Heroes::GetMaxSpellPoints()</c>, <c>heroes.cpp:967-970</c>). The reference fills it in
/// <c>LoadFromMP2</c>; the port leaves the default zero and lets <see cref="SetSpellPoints"/> and the
/// adventure map fill it, because the MP2 loader is not ported past the identity of the hero.
/// </para>
/// <para>
/// The spell book itself (<c>SpellBook</c>, <c>spell_book.cpp</c>) is not ported: the screen that edits it and
/// the filters it offers belong to the UI layer. What the game logic asks — which spells a hero has, whether
/// the book is there, and what they are worth — is here.
/// </para>
/// </remarks>
public sealed partial class Hero : ISpellBookOwner
{
    private readonly SpellStorage _spellBook = new();

    private uint _spellPoints;

    /// <summary>
    /// The spells written in the spell book, <c>getMagicBookSpells()</c> (<c>heroes_base.h:140-143</c>).
    /// </summary>
    /// <remarks>
    /// The book on its own, without the scrolls: this is what the spell book screen lists, while
    /// <see cref="GetAllSpells"/> is what the hero can actually cast.
    /// </remarks>
    public SpellStorage MagicBook => _spellBook;

    /// <summary>
    /// The spell points the hero has left, <c>GetSpellPoints()</c> (<c>heroes_base.h:118-121</c>).
    /// </summary>
    public uint SpellPoints => _spellPoints;

    /// <summary>
    /// The spell points a full mana pool holds, <c>Heroes::GetMaxSpellPoints()</c> (<c>heroes.cpp:967-970</c>).
    /// </summary>
    /// <remarks>
    /// Ten per point of knowledge, with the effective knowledge — the artifacts included — and therefore at
    /// least ten, since <see cref="GetKnowledge"/> never goes below one.
    /// </remarks>
    /// <returns>The maximum spell points.</returns>
    public uint GetMaxSpellPoints() => 10 * (uint)GetKnowledge();

    /// <summary>
    /// 法力条的 MANA 帧号，<c>HeroBase::getManaIndexSprite()</c>（<c>heroes_base.cpp:138-149</c>）。
    /// </summary>
    /// <remarks>
    /// 五点法力一像素；没有法力回 0（空槽），封顶 25 帧。
    /// </remarks>
    /// <returns>0 到 25 之间的帧号。</returns>
    public uint GetManaIndexSprite() => _spellPoints == 0 ? 0u : Math.Min((_spellPoints / 5u) + 1u, 25u);

    /// <summary>
    /// Whether the hero carries the spell book, <c>HaveSpellBook()</c> (<c>heroes_base.h:145-148</c>).
    /// </summary>
    /// <remarks>
    /// The book is an artifact like any other: without it the spells the hero "knows" are unreachable and
    /// the scrolls are dead weight.
    /// </remarks>
    public bool HasSpellBook => _bagArtifacts.IsPresent(ArtifactType.MagicBook);

    /// <summary>
    /// Buys the spell book in a castle's mage guild, <c>Heroes::BuySpellBook()</c> (<c>heroes.cpp:1609-1653</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Four refusals come before anything is paid for: the hero already owns a book, he serves nobody, the castle
    /// has no mage guild, or the kingdom cannot afford the book. Then the book goes into the bag, the gold leaves
    /// the treasury, and the guild teaches every spell it knows.
    /// </para>
    /// <para>
    /// The two dialogs of the reference —「钱不够」与「要买吗」— belong to the UI layer, so they are not here:
    /// whoever calls this on behalf of a human player has to ask first.
    /// </para>
    /// </remarks>
    /// <param name="castle">The castle whose mage guild would teach the spells.</param>
    /// <param name="kingdom">The kingdom that pays for the book.</param>
    /// <returns><see langword="true"/> when the book was bought and the spells were learned.</returns>
    public bool BuySpellBook(CastleTown castle, GameKingdom kingdom)
    {
        ArgumentNullException.ThrowIfNull(castle);
        ArgumentNullException.ThrowIfNull(kingdom);

        if (HasSpellBook || Color == PlayerColor.None || castle.GetLevelMageGuild() < 1)
        {
            return false;
        }

        Funds payment = PaymentConditions.BuySpellBook();

        if (!kingdom.AllowPayment(payment))
        {
            return false;
        }

        if (!_bagArtifacts.Push(ArtifactType.MagicBook))
        {
            return false;
        }

        kingdom.OddFundsResource(payment);
        castle.TrainHeroInMageGuild(this);

        return true;
    }

    /// <summary>
    /// Puts a spell book in the bag, <c>HeroBase::SpellBookActivate()</c> (<c>heroes_base.cpp:206-209</c>).
    /// </summary>
    /// <remarks>
    /// 参考先判「本来就没有书」再塞，端口照抄：宝物栏满了也返回 false，但此时的后果与参考一样——书没进包，
    /// 而英雄本来也没有书，状态没变。
    /// </remarks>
    /// <returns>Whether the book went into the bag.</returns>
    public bool SpellBookActivate() => !HasSpellBook && _bagArtifacts.Push(ArtifactType.MagicBook);

    /// <summary>
    /// Takes the book out and forgets every spell, <c>HeroBase::SpellBookDeactivate()</c>
    /// (<c>heroes_base.cpp:211-219</c>).
    /// </summary>
    /// <remarks>
    /// 换英雄、投降后重新雇佣都会走这里：书被收走，已学的法术一并清空，所以再买一本不会白捡旧书的内容。
    /// </remarks>
    public void SpellBookDeactivate()
    {
        _bagArtifacts.Remove(ArtifactType.MagicBook);

        Debug.Assert(!HasSpellBook, "A hero must not be left holding two spell books.");

        _spellBook.Clear();
    }

    /// <summary>
    /// Sets the spell points, <c>SetSpellPoints()</c> (<c>heroes_base.h:131-134</c>).
    /// </summary>
    /// <remarks>
    /// The reference does not clamp here — the callers are the MP2 loader, the mana vortex and the well,
    /// and each of them writes a value it has already bounded. The port keeps that as it is.
    /// </remarks>
    /// <param name="points">The new value.</param>
    public void SetSpellPoints(uint points) => _spellPoints = points;

    /// <summary>
    /// Pays for a spell cast on the adventure map, <c>HeroBase::SpellCasted()</c>
    /// (<c>heroes_base.cpp</c>).
    /// </summary>
    /// <remarks>
    /// Both prices are paid in full or as much as the hero has: a spell that costs more spell points than are left
    /// empties the pool instead of going negative. The artifacts that lower the price of a spell are in the bag, so
    /// the spell point price is the one the hero would see on the spell book screen.
    /// </remarks>
    /// <param name="spell">The spell that was cast.</param>
    public void SpellCasted(Spell spell)
    {
        _spellPoints -= Math.Min(spell.SpellPoints(_bagArtifacts), _spellPoints);
        _movePoints -= Math.Min(spell.MovePoints(), _movePoints);
    }

    /// <summary>
    /// Every spell the hero can cast, <c>HeroBase::getAllSpells()</c> (<c>heroes_base.cpp:172-186</c>).
    /// </summary>
    /// <remarks>
    /// The book first, then the scrolls, and nothing at all without the book — the reference returns an empty
    /// storage in that case rather than the scrolls.
    /// </remarks>
    /// <returns>The book spells and the scroll spells.</returns>
    public SpellStorage GetAllSpells()
    {
        if (!HasSpellBook)
        {
            return new SpellStorage();
        }

        SpellStorage storage = new();

        storage.Append(_spellBook);
        storage.Append(_bagArtifacts);

        return storage;
    }

    /// <summary>
    /// Whether the hero can cast the spell, <c>HaveSpell()</c> (<c>heroes_base.cpp:187-190</c>).
    /// </summary>
    /// <param name="spell">The spell to look for.</param>
    /// <param name="skipBag">Whether to ignore the scrolls, as the spell book screen does.</param>
    /// <returns>Whether the hero has the spell and the book.</returns>
    public bool HasSpell(Spell spell, bool skipBag = false)
        => HasSpellBook
        && (_spellBook.IsPresent(spell) || (!skipBag && _bagArtifacts.ContainSpell(spell.Type)));

    /// <summary>
    /// Whether the mana pool covers the spell, <c>HeroBase::HaveSpellPoints()</c>
    /// (<c>heroes_base.h</c>).
    /// </summary>
    /// <remarks>
    /// 参考把它写成头文件里的单行内联，端口落成方法：施法界面的「法力不足」提示与
    /// <see cref="CanCastSpell"/> 都要问这一句，内联反而要抄两遍。
    /// </remarks>
    /// <param name="spell">The spell to pay for.</param>
    /// <returns>Whether the hero has enough spell points.</returns>
    public bool HaveSpellPoints(Spell spell) => _spellPoints >= spell.SpellPoints(_bagArtifacts);

    /// <summary>
    /// Whether the hero has the movement points an adventure spell costs, <c>HeroBase::haveMovePoints()</c>
    /// (<c>heroes_base.h</c>).
    /// </summary>
    /// <remarks>
    /// 只对冒险法术有意义：战斗法术的 <c>minMovePoints</c> 是 0，所以这一句对它们永远成立，参考也没在战斗里调。
    /// </remarks>
    /// <param name="spell">The spell to pay for.</param>
    /// <returns>Whether the hero has enough movement points left today.</returns>
    public bool HaveMovePoints(Spell spell) => _movePoints >= spell.MinMovePoints();

    /// <summary>
    /// Whether the hero can cast the spell at all, <c>HeroBase::CanCastSpell()</c>
    /// (<c>heroes_base.cpp:379-596</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 只问英雄自己：魔法书、法力、是否学会，以及「冒险法术要英雄在某人麾下」和「船长不能施召船、城镇之门、
    /// 城镇门户」。最后一条是 <c>SHIPMASTER</c> 位（<see cref="IsShipMaster"/>）的用处之一——参考只拦这三个，
    /// 不拦观地之类的冒险法术。
    /// </para>
    /// <para>
    /// 参考同一个函数还继续问世界：移动点够不够、附近有没有海、有没有船可召、最近的城是不是自己站的那座、
    /// 对手还有没有英雄、脚下是不是矿。那些需要 <see cref="World"/>，在 <see cref="CanCastSpell(Spell, World)"/> 里。
    /// </para>
    /// </remarks>
    /// <param name="spell">The spell to cast.</param>
    /// <returns>Whether the spell would be cast.</returns>
    public bool CanCastSpell(Spell spell)
    {
        if (!HasSpellBook || !HaveSpellPoints(spell) || !HasSpell(spell))
        {
            return false;
        }

        if (!spell.IsAdventure())
        {
            return true;
        }

        // Adventure spells are cast on the map: they need a hero in somebody's service.
        return IsValid
            && Color != PlayerColor.None
            && (!IsShipMaster
                || spell.Type is not (SpellType.SummonBoat or SpellType.TownGate or SpellType.TownPortal));
    }

    /// <summary>
    /// Whether the hero can cast the spell <b>right now</b>, the world half of <c>HeroBase::CanCastSpell()</c>
    /// (<c>heroes_base.cpp:412-593</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考把两道门写在一个函数里，端口拆成两层：<see cref="CanCastSpell(Spell)"/> 只问英雄，
    /// 这一层再问世界——每个冒险法术都有一条「现在施了有没有用」的检查，施法界面的灰按钮问的就是它，
    /// <see cref="ActionSpellCast"/> 之前也要过这一关。
    /// </para>
    /// <para>
    /// 逐个法术与参考对齐：召船要近海、有船可召、有落点；城镇之门要最近的城不是自己脚下且没有英雄驻守
    /// （参考只看<b>最近</b>那座，更远的空城不算数）；城镇门户要至少一座空城；鉴定术要求本回合没施过且
    /// 对手还有英雄；视野要求视野内有怪物；守卫术要求脚下是矿、守卫不是鬼魂，且新守卫比旧的强。
    /// </para>
    /// </remarks>
    /// <param name="spell">The spell to cast.</param>
    /// <param name="world">The world the spell acts on.</param>
    /// <returns>Whether the spell would do anything.</returns>
    public bool CanCastSpell(Spell spell, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (!CanCastSpell(spell) || !spell.IsAdventure())
        {
            return false;
        }

        if (!HaveMovePoints(spell))
        {
            return false;
        }

        switch (spell.Type)
        {
            case SpellType.SummonBoat:
            {
                if (!SpellInfo.IsHeroNearWater(world, this))
                {
                    return false;
                }

                int boatSource = SpellInfo.GetSummonableBoat(world, this, GetCurrentMapVersion(), Players.Get(Color));
                int boatDestination = SpellInfo.GetPossibleBoatPosition(world, this);

                return boatSource >= 0 && boatDestination >= 0;
            }

            case SpellType.TownGate:
            {
                CastleTown? castle = FindNearestCastleTownGate(world);

                // 参考先拒「没有城」，再拒「最近的城就是脚下这座」，最后拒「最近的城里有英雄」——
                // 注意它不往更远的空城找，所以有英雄驻守的最近城会让整个法术无效。
                return castle is not null && castle.TileIndex != MapIndex && world.GetHero(castle) is null;
            }

            case SpellType.TownPortal:
                return FindTownPortalTowns(world).Count > 0;

            case SpellType.IdentifyHero:
            {
                GameKingdom kingdom = world.Kingdoms.GetKingdom(Color);

                if (kingdom.Modes.HasFlag(KingdomModes.IdentifyHero))
                {
                    return false;
                }

                // 参考还问 opponentsCanRecruitMoreHeroes()，但那两个分支的答案都是「拒绝」：对手一个英雄都没有时，
                // 无论还能不能招都白施。所以这里只问有没有英雄。
                return kingdom.OpponentsHaveHeroes(color => world.Kingdoms.GetKingdom(color));
            }

            case SpellType.Visions:
                return world.GetVisibleMonstersAroundHero(this).Count > 0;

            case SpellType.Haunt:
            case SpellType.SetEGuardian:
            case SpellType.SetAGuardian:
            case SpellType.SetFGuardian:
            case SpellType.SetWGuardian:
            {
                Tile tile = world.GetTile(MapIndex);

                if (tile.GetMainObjectType(world, false) != Mp2ObjectType.Mine)
                {
                    return false;
                }

                Troop guardians = world.GetCapturedObject(MapIndex).Guardians;

                if (guardians.Monster == MonsterType.Ghost)
                {
                    return false;
                }

                return spell.Type == SpellType.Haunt
                    || SpellInfo.GetGuardianMonsterCount(spell, (uint)GetPower()) > guardians.Count;
            }

            default:
                return true;
        }
    }

    /// <summary>
    /// Casts an adventure-map spell, <c>Heroes::ActionSpellCast()</c> (<c>heroes_spell.cpp:359-421</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考这个函数自己只是分派：先 <c>CanCastSpell()</c>，再按法术转到 <c>ActionSpellViewMines</c>、
    /// <c>ActionSpellSummonBoat</c>、<c>ActionSpellTownGate</c>、<c>ActionSpellDimensionDoor</c> 等，
    /// 最后统一 <c>SpellCasted()</c> 收费。端口的每一支方法在下面，纯状态的部分照参考写，
    /// 需要玩家给答案或开窗口的四类点交给 <see cref="IHeroSpellCastHost"/>。
    /// </para>
    /// <para>
    /// <b>次元门是例外</b>：参考在它自己的方法里就 <c>SpellCasted()</c> 了，并且返回假以免外层再收一次钱，
    /// 端口照抄这个约定（返回值是「外层要不要再收钱」，不是「法术失败」）。
    /// </para>
    /// <para>
    /// 淡入淡出、音效、雷达重绘、屏幕居中都不在这里：按 <see cref="IHeroSpellCastHost"/> 的说法，
    /// 那是调用方按返回值补的效果，不是游戏状态。
    /// </para>
    /// </remarks>
    /// <param name="spell">The spell to cast.</param>
    /// <param name="world">The world the spell acts on.</param>
    /// <param name="host">
    /// Where the player is asked to pick a destination or town and where the View World window and the Visions
    /// dialogs open; null means <see cref="NullHeroSpellCastHost"/>.
    /// </param>
    /// <returns>Whether the cast did something. False when the spell is unknown to the map, when a pick was
    /// cancelled, or when Dimension Door already paid for itself.</returns>
    public bool ActionSpellCast(Spell spell, World world, IHeroSpellCastHost? host = null)
    {
        ArgumentNullException.ThrowIfNull(world);

        host ??= NullHeroSpellCastHost.Instance;

        // 参考这一行是 assert：调用方必须已经问过 CanCastSpell。
        if (spell.Type == SpellType.None || spell.IsCombat() || !CanCastSpell(spell, world))
        {
            Debug.Assert(false, "An adventure spell is cast through ActionSpellCast without CanCastSpell passing.");

            return false;
        }

        bool apply = spell.Type switch
        {
            SpellType.ViewMines
                or SpellType.ViewResources
                or SpellType.ViewArtifacts
                or SpellType.ViewTowns
                or SpellType.ViewHeroes
                or SpellType.ViewAll => ActionSpellView(spell, host),
            SpellType.IdentifyHero => ActionSpellIdentifyHero(world),
            SpellType.SummonBoat => ActionSpellSummonBoat(world),
            SpellType.DimensionDoor => ActionSpellDimensionDoor(world, host),
            SpellType.TownGate => ActionSpellTownGate(world),
            SpellType.TownPortal => ActionSpellTownPortal(world, host),
            SpellType.Visions => ActionSpellVisions(world, host),
            SpellType.Haunt
                or SpellType.SetEGuardian
                or SpellType.SetAGuardian
                or SpellType.SetFGuardian
                or SpellType.SetWGuardian => ActionSpellSetGuardian(spell, world),
            _ => false,
        };

        if (!apply)
        {
            return false;
        }

        SpellCasted(spell);

        return true;
    }

    /// <summary>
    /// The version of the map being played, for the two spell checks that depend on it,
    /// <c>Settings::getCurrentMapFileInfo()</c>.
    /// </summary>
    /// <returns>The version, <see cref="MapVersion.SuccessionWars"/> when no map is chosen yet.</returns>
    internal static MapVersion GetCurrentMapVersion() =>
        Settings.Get().CurrentMapFileInfo?.Version ?? MapVersion.SuccessionWars;

    /// <summary>
    /// Opens the View World window on the mode of a view spell, <c>heroes_spell.cpp:108-142</c>.
    /// </summary>
    /// <remarks>
    /// 关掉窗口也算施法成功——参考的六个 <c>ActionSpellView*</c> 全都无条件返回真，法力照扣。
    /// </remarks>
    /// <param name="spell">The view spell that was cast.</param>
    /// <param name="host">Where the window is opened.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool ActionSpellView(Spell spell, IHeroSpellCastHost host)
    {
        host.OpenViewWorld(spell.Type switch
        {
            SpellType.ViewMines => ViewWorldMode.ViewMines,
            SpellType.ViewResources => ViewWorldMode.ViewResources,
            SpellType.ViewArtifacts => ViewWorldMode.ViewArtifacts,
            SpellType.ViewTowns => ViewWorldMode.ViewTowns,
            SpellType.ViewHeroes => ViewWorldMode.ViewHeroes,
            _ => ViewWorldMode.ViewAll,
        });

        return true;
    }

    /// <summary>
    /// Marks the kingdom as seeing enemy heroes this turn, <c>heroes_spell.cpp:144-152</c>.
    /// </summary>
    /// <remarks>
    /// 参考在这里弹一句「敌人的英雄现在完全可见」；那句话归界面，端口只置位。
    /// </remarks>
    /// <param name="world">The world with the kingdom.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private bool ActionSpellIdentifyHero(World world)
    {
        GameKingdom kingdom = world.Kingdoms.GetKingdom(Color);

        // CanCastSpell() 已经拒掉了本回合施过的那一次。
        Debug.Assert(!kingdom.Modes.HasFlag(KingdomModes.IdentifyHero));

        kingdom.SetModes(KingdomModes.IdentifyHero);

        return true;
    }

    /// <summary>
    /// Moves a boat under the hero, <c>heroes_spell.cpp:154-182</c>.
    /// </summary>
    /// <remarks>
    /// 顺序与参考一致：先把船从原格拿走，再在新格放下，最后清掉原格的船主颜色。参考的原格「拿走」是
    /// 动画结束时由 <c>ObjectFadingOutInfo</c> 做的，端口没有动画，所以直接 <see cref="Tile.RemoveMainObject"/>
    /// ——与 <c>AI::HeroesCastSummonBoat()</c>（<c>AiHeroAction.cs</c>）同一套写法。
    /// </remarks>
    /// <param name="world">The world.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private bool ActionSpellSummonBoat(World world)
    {
        Debug.Assert(!IsShipMaster);

        int boatDestination = SpellInfo.GetPossibleBoatPosition(world, this);
        int boatSource = SpellInfo.GetSummonableBoat(world, this, GetCurrentMapVersion(), Players.Get(Color));

        // CanCastSpell() 已经确认船与落点都在。
        Debug.Assert(boatDestination >= 0 && boatSource >= 0);

        if (boatDestination < 0 || boatSource < 0)
        {
            return false;
        }

        world.GetTile(boatSource).RemoveMainObject(world);
        world.GetTile(boatDestination).SetBoat(world, Direction.Right, Color);
        world.GetTile(boatSource).ResetBoatOwnerColor();

        return true;
    }

    /// <summary>
    /// Teleports the hero where the player picked, <c>heroes_spell.cpp:184-231</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考在这里就 <c>SpellCasted()</c> 并返回假，外层因此不再收费；端口保留这个分工，见
    /// <see cref="ActionSpellCast"/> 的注释。
    /// </para>
    /// <para>
    /// 参考不增 <see cref="DimensionDoorUses"/>：那是 AI 的射程预算（<c>ai_hero_action.cpp:2445</c>），
    /// 人类施法不记。
    /// </para>
    /// </remarks>
    /// <param name="world">The world.</param>
    /// <param name="host">Where the player picks the landing tile.</param>
    /// <returns>Always <see langword="false"/> — the price is already paid here.</returns>
    private bool ActionSpellDimensionDoor(World world, IHeroSpellCastHost host)
    {
        int destination = host.SelectDimensionDoorDestination(MapIndex, Spell.DimensionDoorDistance(), IsShipMaster);

        if (world.Geometry?.IsValidAbsIndex(destination) != true)
        {
            // 玩家取消：不收费，也不动英雄。
            return false;
        }

        MovementHandler.Scout(this, destination);
        MoveToDest(destination, world);
        SpellCasted(new Spell(SpellType.DimensionDoor));
        Path.Reset();
        MovementHandler.ActionNewPosition(this, false);

        return false;
    }

    /// <summary>
    /// Sends the hero to the nearest free town, <c>heroes_spell.cpp:233-253</c>.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <returns>Whether the hero travelled.</returns>
    private bool ActionSpellTownGate(World world)
    {
        Debug.Assert(!IsShipMaster);

        CastleTown? castle = FindNearestCastleTownGate(world);

        if (castle is null)
        {
            // 参考这里是 assert：CanCastSpell() 保证了最近的城存在。
            Debug.Fail("Town Gate is cast by a hero whose kingdom has no town.");

            return false;
        }

        HeroesTownGate(world, castle);

        return true;
    }

    /// <summary>
    /// Sends the hero to the town the player picked, <c>heroes_spell.cpp:255-268</c>.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="host">Where the player picks the town.</param>
    /// <returns>Whether the hero travelled.</returns>
    private bool ActionSpellTownPortal(World world, IHeroSpellCastHost host)
    {
        Debug.Assert(!IsShipMaster);

        IReadOnlyList<CastleTown> towns = FindTownPortalTowns(world);
        int townIndex = host.SelectTownPortalTown(towns);

        if (townIndex < 0)
        {
            return false;
        }

        // 参考拿的是城门的绝对点再换回城；端口的选单直接返回城门下标，于是这里只要确认它是座城。
        CastleTown? castle = world.GetCastleEntrance(townIndex);

        if (castle is null)
        {
            Debug.Fail("Town Portal was given a tile that is not a castle entrance.");

            return false;
        }

        HeroesTownGate(world, castle);

        return true;
    }

    /// <summary>
    /// Shows every monster the hero can see and remembers the stacks, <c>heroes_spell.cpp:270-327</c>.
    /// </summary>
    /// <remarks>
    /// 参考每一堆弹一条消息，然后给英雄置 <c>VISIONS</c> 模式位。那个位<b>没有任何读者</b>（只在
    /// <c>ActionNewPosition()</c> 被清掉），端口因此不建它，免得为一个死位加枚举。
    /// </remarks>
    /// <param name="world">The world.</param>
    /// <param name="host">Where the per-stack messages are shown.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private bool ActionSpellVisions(World world, IHeroSpellCastHost host)
    {
        List<int> monsters = world.GetVisibleMonstersAroundHero(this);

        // CanCastSpell() 已经确认视野里有怪物。
        Debug.Assert(monsters.Count > 0);

        GameKingdom kingdom = world.Kingdoms.GetKingdom(Color);

        foreach (int monsterIndex in monsters)
        {
            kingdom.AddMonsterUnderVision(monsterIndex);

            Tile tile = world.GetTile(monsterIndex);
            Troop troop = tile.GetTroop(world);
            NeutralMonsterJoiningCondition join = ArmyStack.GetJoinSolution(this, tile, troop, world, kingdom);

            host.ShowMonsterVision(troop, join);
        }

        return true;
    }

    /// <summary>
    /// Leaves a guard on the mine the hero stands on, <c>heroes_spell.cpp:329-356</c>.
    /// </summary>
    /// <remarks>
    /// <c>HAUNT</c> 是唯一一支会改矿主颜色的守卫：它只在人类手里把矿变成「无主」，好让雷达上的颜色跟着变
    /// （参考紧接着重绘雷达，那是效果，端口不做）。守卫本身写在 <see cref="CapturedObject.Guardians"/> 上，
    /// 与参考的 <c>world.GetCapturedObject().GetTroop().Set()</c> 一致。
    /// </remarks>
    /// <param name="spell">The guardian spell that was cast.</param>
    /// <param name="world">The world.</param>
    /// <returns>Whether a guard was placed.</returns>
    private bool ActionSpellSetGuardian(Spell spell, World world)
    {
        Tile tile = world.GetTile(MapIndex);

        Debug.Assert(tile.GetMainObjectType(world, false) == Mp2ObjectType.Mine);

        uint count = SpellInfo.GetGuardianMonsterCount(spell, (uint)GetPower());

        if (count == 0)
        {
            return false;
        }

        tile.SetMineSpell(world, spell.Type);

        if (spell.Type == SpellType.Haunt && IsControlHuman)
        {
            world.CaptureObject(MapIndex, PlayerColor.None);
        }

        world.GetCapturedObject(MapIndex).Guardians.Set(MonsterSummons.FromSpell(spell.Type), count);

        return true;
    }

    /// <summary>
    /// Teleports the hero to a town gate and teaches him the guild's spells, <c>heroes_spell.cpp:66-106</c>.
    /// </summary>
    /// <remarks>
    /// 城镇之门与城镇门户共用这一段：跳过去、清掉路线、进魔法行会。参考的淡入淡出、雷达清点、屏幕居中
    /// 都是界面效果，不在这里。
    /// </remarks>
    /// <param name="world">The world.</param>
    /// <param name="castle">The town whose gate the hero lands on.</param>
    private void HeroesTownGate(World world, CastleTown castle)
    {
        Debug.Assert(world.GetHero(castle) is null, "A hero does not port into a town that already has one.");

        int destination = castle.TileIndex;

        Debug.Assert(world.Geometry?.IsValidAbsIndex(destination) == true);

        MovementHandler.Scout(this, destination);
        MoveToDest(destination, world);
        Path.Reset();

        castle.TrainHeroInMageGuild(this);
    }

    /// <summary>
    /// The closest town of the kingdom, occupied or not, <c>getNearestCastleTownGate()</c>
    /// (<c>spell_info.cpp:186-212</c>).
    /// </summary>
    /// <remarks>
    /// 参考<b>不</b>跳过有英雄驻守的城——它挑出最近的一座，再让 <see cref="CanCastSpell(Spell, World)"/> 拒绝。
    /// </remarks>
    /// <param name="world">The world the hero stands in.</param>
    /// <returns>The nearest town, or null when the kingdom has none.</returns>
    internal CastleTown? FindNearestCastleTownGate(World world)
    {
        MapGeometry geometry = world.Geometry!.Value;

        return SpellInfo.GetNearestCastle(geometry, world.Kingdoms.GetKingdom(Color).Castles, geometry.GetPoint(MapIndex));
    }

    /// <summary>
    /// The towns a Town Portal may go to — the kingdom's towns without a guest hero,
    /// <c>heroes_spell.cpp:259</c> through <c>Dialog::selectKingdomCastle( kingdom, true )</c>.
    /// </summary>
    /// <param name="world">The world the hero stands in.</param>
    /// <returns>The free towns, in kingdom order.</returns>
    private List<CastleTown> FindTownPortalTowns(World world)
    {
        var towns = new List<CastleTown>();

        foreach (CastleTown castle in world.Kingdoms.GetKingdom(Color).Castles)
        {
            if (world.GetHero(castle) is null)
            {
                towns.Add(castle);
            }
        }

        return towns;
    }

    /// <summary>
    /// How many dimension doors the hero has opened today, <c>Heroes::getDimensionDoorUses()</c>
    /// (<c>heroes.h:692</c>).
    /// </summary>
    /// <remarks>
    /// The count is per turn and bounds the dimension door routes the AI searches for; the adventure map
    /// casting that increments it arrives with its own system.
    /// </remarks>
    public uint DimensionDoorUses { get; set; }

    /// <summary>
    /// Whether the hero may learn the spell, <c>HeroBase::CanLearnSpell()</c> (<c>heroes_base.cpp:604-611</c>).
    /// </summary>
    /// <remarks>
    /// Wisdom gates the levels: the first two are free, the third needs basic, the fourth advanced and the fifth
    /// expert. The book is <b>not</b> checked here — <see cref="AppendSpellToBook"/> does that — because a map or
    /// an event may hand out a spell to a hero who has none yet.
    /// </remarks>
    /// <param name="spell">The spell.</param>
    /// <returns>Whether wisdom is high enough for its level.</returns>
    public bool CanLearnSpell(Spell spell)
    {
        SkillLevel wisdom = GetLevelSkill(SecondarySkill.Wisdom);

        return (4 < spell.Level() && wisdom == SkillLevel.Expert)
            || (4 == spell.Level() && wisdom >= SkillLevel.Advanced)
            || (3 == spell.Level() && wisdom >= SkillLevel.Basic)
            || 3 > spell.Level();
    }

    /// <summary>
    /// Writes a spell into the book, <c>HeroBase::AppendSpellsToBook()</c> (<c>heroes_base.cpp:590-602</c>).
    /// </summary>
    /// <remarks>
    /// Three refusals in the reference: no valid spell, no wisdom for its level, no book to write into. The
    /// duplicate case is <see cref="SpellStorage.Append(Spell)"/>'s.
    /// </remarks>
    /// <param name="spell">The spell.</param>
    /// <param name="ignoreWisdom">Whether to skip <see cref="CanLearnSpell"/>, the <c>force</c> flag of the reference.</param>
    /// <returns>Whether the book changed.</returns>
    public bool AppendSpellToBook(Spell spell, bool ignoreWisdom = false)
    {
        if (!HasSpellBook || SpellTypes.IsValid(spell.Type) == false || (!ignoreWisdom && !CanLearnSpell(spell)))
        {
            return false;
        }

        int before = _spellBook.Count;
        _spellBook.Append(spell);

        return _spellBook.Count != before;
    }

    /// <summary>
    /// What the hero's spells are worth in a strategic fight, <c>HeroBase::GetMagicStrategicValue()</c>
    /// (<c>heroes_base.cpp:358-377</c>).
    /// </summary>
    /// <remarks>
    /// The best single spell, not the sum: the hero casts one spell per turn, so the contribution to the
    /// army strength is the one that hurts the most. A hero with no spell points or no book is worth nothing,
    /// which is what makes <c>Army.GetStrength()</c> ignore the magic of a spent hero.
    /// </remarks>
    /// <param name="armyStrength">The strength of the army the hero leads.</param>
    /// <returns>The value of the best castable combat spell; zero when there is none.</returns>
    public double GetMagicStrategicValue(double armyStrength)
    {
        uint currentSpellPoints = _spellPoints;

        if (currentSpellPoints == 0)
        {
            return 0;
        }

        SpellStorage spells = GetAllSpells();
        int spellPower = GetPower();

        double bestValue = 0;

        foreach (Spell spell in spells)
        {
            if (spell.IsCombat())
            {
                bestValue = Math.Max(bestValue, spell.GetStrategicValue(armyStrength, currentSpellPoints, spellPower));
            }
        }

        return bestValue;
    }

    /// <summary>
    /// Whether the hero has enough magic to make a difference in a battle, <c>HeroBase::isPotentSpellcaster()</c>
    /// (<c>heroes_base.cpp:96-136</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 知识 5 以下连一次像样的法术都放不起，直接不算。清单里是 AI 用得好的那几个法术：控制类、召唤类、镜像、伤害类
    /// （但要求力量大于 5），以及复活类和招魂——复活类只在队伍里没有亡灵时有用，招魂只在全是亡灵时有用。
    /// </para>
    /// <para>
    /// 参考把它放在 <c>HeroBase</c> 上，因为城堡的船长也问同一句话；端口还没有船长，等船长落地时再往基类挪。
    /// </para>
    /// </remarks>
    /// <returns>True when the hero is worth visiting a magic well for.</returns>
    public bool IsPotentSpellcaster()
    {
        if (GetKnowledge() <= 5)
        {
            return false;
        }

        foreach (Spell spell in _spellBook)
        {
            switch (spell.Type)
            {
            case SpellType.Blind:
            case SpellType.Paralyze:
            case SpellType.DimensionDoor:
            case SpellType.SummonAElement:
            case SpellType.SummonEElement:
            case SpellType.SummonFElement:
            case SpellType.SummonWElement:
            case SpellType.MirrorImage:
                return true;

            case SpellType.ColdRay:
            case SpellType.LightningBolt:
            case SpellType.ChainLightning:
            case SpellType.MeteorShower:
            case SpellType.Armageddon:
                if (GetPower() > 5)
                {
                    return true;
                }

                break;

            case SpellType.Resurrect:
            case SpellType.ResurrectTrue:
                if (!Army.AllTroopsAreUndead())
                {
                    return true;
                }

                break;

            case SpellType.AnimateDead:
                if (Army.AllTroopsAreUndead())
                {
                    return true;
                }

                break;

            default:
                break;
            }
        }

        return false;
    }
}
