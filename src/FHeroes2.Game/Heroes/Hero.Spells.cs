using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;

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
    /// Sets the spell points, <c>SetSpellPoints()</c> (<c>heroes_base.h:131-134</c>).
    /// </summary>
    /// <remarks>
    /// The reference does not clamp here — the callers are the MP2 loader, the mana vortex and the well,
    /// and each of them writes a value it has already bounded. The port keeps that as it is.
    /// </remarks>
    /// <param name="points">The new value.</param>
    public void SetSpellPoints(uint points) => _spellPoints = points;

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
}
