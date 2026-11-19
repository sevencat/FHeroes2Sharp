namespace FHeroes2.Game.Spells;

/// <summary>
/// One row of the spell table, <c>SpellStats</c> (<c>spell.cpp:42-51</c>).
/// </summary>
/// <param name="SpellPoints">Spell points the spell costs, <c>spellPoints</c>.</param>
/// <param name="MovePoints">Movement points casting it on the adventure map costs, <c>movePoints</c>.</param>
/// <param name="MinMovePoints">Movement points the hero must have left to cast it, <c>minMovePoints</c>.</param>
/// <param name="ImageId">The spell's icon in <c>ICN::SPELLS</c>, <c>imageId</c>.</param>
/// <param name="ExtraValue">
/// The number the spell's behaviour reads: damage per spell power, the hit points restored, the
/// creature count of a summon, the attack bonus of Blood Lust. Zero where the spell has none.
/// </param>
public readonly record struct SpellStats(uint SpellPoints, uint MovePoints, uint MinMovePoints, uint ImageId, uint ExtraValue);

/// <summary>
/// The numbers of every spell, the <c>spells</c> array of <c>spell.cpp:55-150</c>.
/// </summary>
/// <remarks>
/// <para>
/// The names and the descriptions of the same rows are in <see cref="SpellText"/>, so the numbers stay readable in
/// tests that do not care about words. Everything else is here, in <c>SpellType</c> order, so the row of an id is an
/// index and not a lookup — the reference does the same with a <c>std::array</c>.
/// </para>
/// <para>
/// <see cref="SpellType.None"/> has a row of zeros, and so do the six <c>RANDOM*</c> placeholders and
/// <see cref="SpellType.Petrify"/>: they exist for the number of the ids, not to be cast.
/// </para>
/// </remarks>
public static class SpellTable
{
    private static readonly SpellStats[] Rows =
    [
        new(0, 0, 0, 0, 0), // 0 Unknown
        new(9, 0, 0, 8, 10), // 1 Fireball
        new(15, 0, 0, 9, 10), // 2 Fireblast
        new(7, 0, 0, 4, 25), // 3 Lightning Bolt
        new(15, 0, 0, 5, 40), // 4 Chain Lightning
        new(9, 0, 0, 10, 0), // 5 Teleport
        new(6, 0, 0, 6, 5), // 6 Cure
        new(15, 0, 0, 60, 5), // 7 Mass Cure
        new(12, 0, 0, 13, 50), // 8 Resurrect
        new(15, 0, 0, 12, 50), // 9 Resurrect True
        new(3, 0, 0, 14, 2), // 10 Haste
        new(10, 0, 0, 61, 2), // 11 Mass Haste
        new(3, 0, 0, 1, 0), // 12 spell|Slow
        new(15, 0, 0, 62, 0), // 13 Mass Slow
        new(6, 0, 0, 21, 50), // 14 spell|Blind
        new(3, 0, 0, 7, 0), // 15 Bless
        new(12, 0, 0, 63, 0), // 16 Mass Bless
        new(3, 0, 0, 31, 3), // 17 Stoneskin
        new(6, 0, 0, 30, 5), // 18 Steelskin
        new(3, 0, 0, 3, 0), // 19 Curse
        new(12, 0, 0, 64, 0), // 20 Mass Curse
        new(9, 0, 0, 22, 10), // 21 Holy Word
        new(12, 0, 0, 23, 20), // 22 Holy Shout
        new(7, 0, 0, 17, 0), // 23 Anti-Magic
        new(5, 0, 0, 18, 0), // 24 Dispel Magic
        new(12, 0, 0, 73, 0), // 25 Mass Dispel
        new(3, 0, 0, 38, 10), // 26 Magic Arrow
        new(12, 0, 0, 19, 0), // 27 Berserker
        new(20, 0, 0, 16, 50), // 28 Armageddon
        new(15, 0, 0, 11, 25), // 29 Elemental Storm
        new(15, 0, 0, 24, 25), // 30 Meteor Shower
        new(9, 0, 0, 20, 0), // 31 Paralyze
        new(15, 0, 0, 37, 25), // 32 Hypnotize
        new(6, 0, 0, 36, 20), // 33 Cold Ray
        new(9, 0, 0, 35, 10), // 34 Cold Ring
        new(7, 0, 0, 34, 3), // 35 Disrupting Ray
        new(6, 0, 0, 29, 5), // 36 Death Ripple
        new(10, 0, 0, 28, 10), // 37 Death Wave
        new(6, 0, 0, 32, 5), // 38 Dragon Slayer
        new(3, 0, 0, 27, 3), // 39 Blood Lust
        new(10, 0, 0, 25, 50), // 40 Animate Dead
        new(25, 0, 0, 26, 0), // 41 Mirror Image
        new(3, 0, 0, 15, 2), // 42 Shield
        new(7, 0, 0, 65, 0), // 43 Mass Shield
        new(30, 0, 0, 56, 3), // 44 Summon Earth Elemental
        new(30, 0, 0, 57, 3), // 45 Summon Air Elemental
        new(30, 0, 0, 58, 3), // 46 Summon Fire Elemental
        new(30, 0, 0, 59, 3), // 47 Summon Water Elemental
        new(15, 0, 0, 33, 0), // 48 Earthquake
        new(1, 0, 0, 39, 0), // 49 View Mines
        new(1, 0, 0, 40, 0), // 50 View Resources
        new(2, 0, 0, 41, 0), // 51 View Artifacts
        new(2, 0, 0, 42, 0), // 52 View Towns
        new(2, 0, 0, 43, 0), // 53 View Heroes
        new(3, 0, 0, 44, 0), // 54 View All
        new(3, 0, 0, 45, 0), // 55 Identify Hero
        new(5, 0, 0, 46, 0), // 56 Summon Boat
        new(10, 225, 69, 47, 0), // 57 Dimension Door
        new(10, 225, 69, 48, 0), // 58 Town Gate
        new(20, 225, 69, 49, 0), // 59 Town Portal
        new(6, 0, 0, 50, 3), // 60 Visions
        new(8, 0, 0, 51, 4), // 61 Haunt
        new(15, 0, 0, 52, 4), // 62 Set Earth Guardian
        new(15, 0, 0, 53, 4), // 63 Set Air Guardian
        new(15, 0, 0, 54, 4), // 64 Set Fire Guardian
        new(15, 0, 0, 55, 4), // 65 Set Water Guardian
        new(0, 0, 0, 67, 0), // 66 Random Spell
        new(0, 0, 0, 68, 0), // 67 Random 1st Level Spell
        new(0, 0, 0, 69, 0), // 68 Random 2nd Level Spell
        new(0, 0, 0, 70, 0), // 69 Random 3rd Level Spell
        new(0, 0, 0, 71, 0), // 70 Random 4th Level Spell
        new(0, 0, 0, 72, 0), // 71 Random 5th Level Spell
        new(0, 0, 0, 66, 0), // 72 Petrification
    ];

    /// <summary>How many rows there are: one per <see cref="SpellType"/> up to the sentinel.</summary>
    public static int Count => Rows.Length;

    /// <summary>The numbers of the spell.</summary>
    /// <param name="spell">The id; one past the end reads as <see cref="SpellType.None"/>.</param>
    /// <returns>The row of the spell.</returns>
    public static SpellStats Get(SpellType spell)
    {
        int id = (int)spell;

        return id > 0 && id < Rows.Length ? Rows[id] : Rows[0];
    }
}
