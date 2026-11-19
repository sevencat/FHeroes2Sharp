using FHeroes2.Game.Monsters;

namespace FHeroes2.Game.Castle;

/// <summary>
/// One of the six rows of the well window, <c>Castle::_wellRedrawBackground()</c>
/// (<c>castle_well.cpp:405-547</c>).
/// </summary>
/// <remarks>
/// <para>
/// Everything the window says about a dwelling except the words that need a font: the creature and its statistics,
/// whether the dwelling stands, what is waiting in it and what it will grow next week.
/// </para>
/// <para>
/// The reference resolves the dwelling twice and inconsistently — the creature, its name and the picture come from
/// <see cref="ActualDwelling"/> (the upgrade when one stands), while the <c>Growth</c> block is guarded by the
/// <b>base</b> dwelling bit. Both answers are kept here, because a town that carries only the upgrade bit is a legal
/// save and the two then differ: such a dwelling shows no growth line at all.
/// </para>
/// </remarks>
/// <param name="Level">Dwelling level, one to six, which is also the row of the window.</param>
/// <param name="Dwelling">The base dwelling, <c>DWELLING_MONSTER1</c> and friends.</param>
/// <param name="ActualDwelling">The dwelling as built, <c>Castle::GetActualDwelling()</c>.</param>
/// <param name="Monster">The creature of <paramref name="ActualDwelling"/>.</param>
/// <param name="IsBuilt">Whether the town can recruit from it.</param>
/// <param name="Available">The creatures waiting in the dwelling, <c>Castle::getMonstersInDwelling()</c>.</param>
/// <param name="WeeklyGrowth">What the dwelling adds every week, both wells included.</param>
public readonly record struct CastleWellDwelling(
    int Level,
    BuildingType Dwelling,
    BuildingType ActualDwelling,
    MonsterType Monster,
    bool IsBuilt,
    uint Available,
    uint WeeklyGrowth)
{
    /// <summary>The battle statistics of the creature, <c>Monster::GetAttack()</c> and friends.</summary>
    public MonsterBattleStats Stats => MonsterStatistics.GetBattleStats(Monster);

    /// <summary>The name of the creature in the plural, <c>Monster::GetMultiName()</c>.</summary>
    public string MonsterName => MonsterNames.GetMulti(Monster);
}

/// <summary>
/// The six rows of the well window, <c>Castle::_wellRedrawBackground()</c> (<c>castle_well.cpp:405-547</c>).
/// </summary>
public sealed partial class Castle
{
    /// <summary>
    /// The dwellings of the window in drawing order, <c>castleDwellings</c> (<c>castle_well.cpp:63-64</c>).
    /// </summary>
    private static readonly BuildingType[] WellDwellingOrder =
    [
        BuildingType.DwellingMonster1,
        BuildingType.DwellingMonster2,
        BuildingType.DwellingMonster3,
        BuildingType.DwellingMonster4,
        BuildingType.DwellingMonster5,
        BuildingType.DwellingMonster6,
    ];

    /// <summary>
    /// What the well window prints about every dwelling, <c>Castle::_wellRedrawBackground()</c>
    /// (<c>castle_well.cpp:405-547</c>).
    /// </summary>
    /// <remarks>
    /// The rows are always six, built or not: the window draws the picture and the statistics of every dwelling and
    /// only leaves out the growth block of the ones that do not stand.
    /// </remarks>
    /// <returns>The six rows, first level first.</returns>
    public IReadOnlyList<CastleWellDwelling> GetWellDwellings()
    {
        CastleWellDwelling[] rows = new CastleWellDwelling[MaxNumOfDwellings];

        for (int index = 0; index < rows.Length; ++index)
        {
            BuildingType dwelling = WellDwellingOrder[index];
            BuildingType actual = GetActualDwelling(dwelling);

            rows[index] = new CastleWellDwelling(
                index + 1,
                dwelling,
                actual,
                DwellingMonsters.FromDwelling(Race, actual),
                IsBuild(dwelling),
                GetMonstersInDwelling(actual),
                GetWellGrowth(dwelling));
        }

        return rows;
    }

    /// <summary>
    /// What a dwelling adds every week, the growth block of the well window (<c>castle_well.cpp:526-531</c>).
    /// </summary>
    /// <remarks>
    /// The well gives its two to every dwelling, the landmark building its eight to the first level only. The
    /// reference tests the base dwelling there, so the answer is the same whether the first dwelling is upgraded.
    /// <see cref="DwellingsBar.GetGrowth"/> asks the same question for the castle dialog; it resolves the dwelling
    /// from its own item type, which lands on the same two constants.
    /// </remarks>
    /// <param name="dwelling">A base dwelling, <c>DWELLING_MONSTER1</c> and friends.</param>
    /// <returns>The growth, wells included.</returns>
    public uint GetWellGrowth(BuildingType dwelling)
    {
        uint growth = GetGrowth(dwelling);

        if (IsBuild(BuildingType.Well))
        {
            growth += GrownWell;
        }

        if (dwelling == BuildingType.DwellingMonster1 && IsBuild(BuildingType.Well2))
        {
            growth += GrownWel2;
        }

        return growth;
    }
}
