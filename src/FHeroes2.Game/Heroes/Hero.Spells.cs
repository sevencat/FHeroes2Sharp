using System.Diagnostics;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
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
    /// Whether the hero can cast the spell right now, <c>HeroBase::CanCastSpell()</c>
    /// (<c>heroes_base.cpp:378-460</c>).
    /// </summary>
    /// <remarks>
    /// 参考按顺序六道拒绝：没有魔法书、法力不够、没学会、冒险法术要求英雄在某人麾下、船长不能施冒险法术、
    /// 船长不能再召一条船。后两条是 <c>SHIPMASTER</c> 位（<see cref="IsShipMaster"/>）的另外两个用处。
    /// </remarks>
    /// <param name="spell">The spell to cast.</param>
    /// <returns>Whether the spell would be cast.</returns>
    public bool CanCastSpell(Spell spell)
    {
        if (!HasSpellBook || !HaveSpellPoints(spell) || !HasSpell(spell))
        {
            return false;
        }

        if (spell.IsAdventure())
        {
            // Adventure spells are cast on the map: they need a hero in somebody's service, and a ship master
            // already stands on water, where the spell would have nothing to act on.
            return Color != PlayerColor.None && !IsShipMaster;
        }

        // A ship master has a boat already.
        return !IsShipMaster || spell.Type != SpellType.SummonBoat;
    }

    /// <summary>
    /// Casts an adventure-map spell, <c>Heroes::ActionSpellCast()</c> (<c>heroes_spell.cpp:305-436</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考这个函数本身只是分派：先 <see cref="CanCastSpell"/>，再按法术类型转到 <c>ActionSpellViewMines</c>、
    /// <c>ActionSpellSummonBoat</c>、<c>ActionSpellTownGate</c>、<c>ActionSpellDimensionDoor</c> 等 —— 每一个都要
    /// 让玩家在地图上点目的地、再放动画与提示框，它们全在 <c>heroes_spell.cpp</c> 的界面段里。
    /// </para>
    /// <para>
    /// 那些界面动作随冒险地图一起归 S12；端口在这里先立入口，好让地图层的调用点有一个固定的落点。
    /// </para>
    /// <para>
    /// <b>当前状态：抛 <see cref="NotImplementedException"/>。</b>
    /// </para>
    /// </remarks>
    /// <param name="spell">The spell to cast.</param>
    /// <param name="world">The world the spell acts on.</param>
    /// <returns>Whether the spell was cast.</returns>
    /// <exception cref="NotImplementedException">The adventure-map casting dialogs are not ported yet (S12).</exception>
    public bool ActionSpellCast(Spell spell, World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        throw new NotImplementedException("Heroes::ActionSpellCast() 依赖冒险地图的目的地选取与动画（planv4 S12）。");
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
