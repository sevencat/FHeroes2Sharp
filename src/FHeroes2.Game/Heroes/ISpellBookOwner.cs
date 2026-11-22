using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Heroes;

/// <summary>
/// Whoever carries a spell book — a hero or a castle captain, <c>HeroBase</c> in the reference
/// (<c>heroes_base.h:118-150</c>).
/// </summary>
/// <remarks>
/// <para>
/// The port has no <c>HeroBase</c>: <see cref="Hero"/> owns everything the reference puts in it, and the captain
/// is a much smaller thing that only needs the book. This interface is the part both share, so that
/// <see cref="FHeroes2.Game.Castle.MageGuild.TrainHero"/> can teach either one without asking which it is.
/// </para>
/// <para>
/// The wisdom rule lives in the implementation, not in the caller: <c>HeroBase::AppendSpellsToBook</c> asks
/// <c>CanLearnSpell</c>, and that method reads the hero's wisdom. A captain has no secondary skills at all, so it
/// can only ever learn the first two levels of a guild — which is exactly what the reference's
/// <c>Captain::GetLevelSkill()</c> returning zero means.
/// </para>
/// </remarks>
public interface ISpellBookOwner
{
    /// <summary>The spells written in the book, without the scrolls.</summary>
    SpellStorage MagicBook { get; }

    /// <summary>Whether the book artifact is carried.</summary>
    bool HasSpellBook { get; }

    /// <summary>
    /// The spell power, <c>HeroBase::GetPower()</c> (<c>heroes_base.h:141</c>).
    /// </summary>
    /// <remarks>
    /// The reference declares it pure virtual next to the two skills, and every spell formula takes it. Both
    /// implementations clamp it to 1..255 already, so the number is never zero.
    /// </remarks>
    /// <returns>The effective spell power, at least one.</returns>
    int GetPower();

    /// <summary>
    /// The mana the owner has left, <c>HeroBase::GetSpellPoints()</c> (<c>heroes_base.h:150</c>).
    /// </summary>
    /// <remarks>
    /// The book draws it on the bookmark of the first page and compares it with the cost of a spell to
    /// grey out the ones the owner cannot pay for.
    /// </remarks>
    uint SpellPoints { get; }

    /// <summary>
    /// The mana cap, <c>HeroBase::GetMaxSpellPoints()</c> — ten per point of knowledge.
    /// </summary>
    /// <returns>The cap, as the reference prints it in "… out of %{max}".</returns>
    uint GetMaxSpellPoints();

    /// <summary>
    /// Whether the owner can cast the spell right now, <c>HeroBase::CanCastSpell( const Spell &amp; )</c>
    /// (<c>heroes_base.cpp:369-458</c>).
    /// </summary>
    /// <remarks>
    /// The reference has a second overload that fills in the reason a spell cannot be cast; the port keeps
    /// the yes/no form and leaves the sentence to whoever shows it.
    /// </remarks>
    /// <param name="spell">The spell.</param>
    /// <returns>Whether it can be cast.</returns>
    bool CanCastSpell(Spell spell);

    /// <summary>
    /// The artifacts the owner carries, <c>HeroBase::GetBagArtifacts()</c>.
    /// </summary>
    /// <remarks>
    /// Two spell book rules read the bag: the spell scrolls in it are listed as known spells
    /// (<c>SpellBook::SetFilter()</c>), and some artifacts lower the cost of a spell
    /// (<c>Spell::spellPoints( const HeroBase * )</c>).
    /// </remarks>
    ArtifactBag BagArtifacts { get; }

    /// <summary>
    /// Whether the owner may learn the spell, <c>HeroBase::CanLearnSpell()</c> (<c>heroes_base.cpp:604-611</c>).
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <returns>Whether wisdom is high enough for its level.</returns>
    bool CanLearnSpell(Spell spell);

    /// <summary>
    /// Writes a spell into the book, <c>HeroBase::AppendSpellsToBook()</c> (<c>heroes_base.cpp:590-602</c>).
    /// </summary>
    /// <param name="spell">The spell.</param>
    /// <param name="ignoreWisdom">Whether to skip <see cref="CanLearnSpell"/>, the <c>force</c> flag of the reference.</param>
    /// <returns>Whether the book changed.</returns>
    bool AppendSpellToBook(Spell spell, bool ignoreWisdom = false);
}
