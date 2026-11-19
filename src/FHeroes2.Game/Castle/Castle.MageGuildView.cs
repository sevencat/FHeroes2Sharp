namespace FHeroes2.Game.Castle;

/// <summary>
/// What the mage guild screen shows, the spell rows of <c>Castle::_openMageGuild()</c>
/// (<c>castle_mageguild.cpp:119-132</c>).
/// </summary>
public sealed partial class Castle
{
    /// <summary>Number of spell levels, i.e. of rows on the mage guild screen, <c>spellRows.size()</c>.</summary>
    public const int MageGuildSpellLevelCount = 5;

    /// <summary>
    /// The spells of each level, as the mage guild screen draws them, <c>SpellsInOneRow</c> per level.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Row <c>0</c> is the first level and is drawn at the <b>bottom</b> of the screen: the reference places row
    /// <c>i</c> at <c>y = 365 - 90 * i</c>, so the highest level is on top. The order of this list is the level
    /// order, not the screen order.
    /// </para>
    /// <para>
    /// Each row is padded with <see cref="Spells.SpellType.None"/> up to the size the guild owes that level, so a
    /// screen can draw a fixed number of slots. The number comes from
    /// <see cref="MageGuild.GetMaxSpellsCount"/> with <see cref="HasLibraryCapability"/> (the race), while the
    /// spells themselves are rolled with <see cref="IsLibraryBuilt"/> (the race <i>and</i> the built special) —
    /// two different questions that look alike.
    /// </para>
    /// </remarks>
    /// <returns>Five rows, index <c>0</c> being the first spell level.</returns>
    public IReadOnlyList<IReadOnlyList<Spells.SpellType>> GetMageGuildSpellRows()
    {
        int guildLevel = GetLevelMageGuild();
        bool hasLibrary = IsLibraryBuilt;
        bool hasLibraryCapability = HasLibraryCapability;

        Spells.SpellType[][] rows = new Spells.SpellType[MageGuildSpellLevelCount][];

        for (int levelIndex = 0; levelIndex < rows.Length; ++levelIndex)
        {
            int spellLevel = levelIndex + 1;
            int slotCount = MageGuild.GetMaxSpellsCount(spellLevel, hasLibraryCapability);
            Spells.SpellStorage spells = _mageGuild.GetSpells(guildLevel, hasLibrary, spellLevel);

            Spells.SpellType[] row = new Spells.SpellType[slotCount];
            Array.Fill(row, Spells.SpellType.None);

            for (int i = 0; i < Math.Min(slotCount, spells.Count); ++i)
            {
                row[i] = spells[i].Type;
            }

            rows[levelIndex] = row;
        }

        return rows;
    }
}
