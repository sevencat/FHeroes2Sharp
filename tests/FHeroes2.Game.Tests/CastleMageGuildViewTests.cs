using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Spells;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="CastleTown.GetMageGuildSpellRows"/>: the five rows the mage guild screen draws.
/// </summary>
public sealed class CastleMageGuildViewTests
{
    [Fact]
    public void ATownWithoutAGuildShowsFiveEmptyRows()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle);

        IReadOnlyList<IReadOnlyList<SpellType>> rows = castle.GetMageGuildSpellRows();

        Assert.Equal(5, rows.Count);

        for (int levelIndex = 0; levelIndex < rows.Count; ++levelIndex)
        {
            // The slot count comes from the race, not from the built special, which is why a knight gets no extra.
            Assert.Equal(
                MageGuild.GetMaxSpellsCount(levelIndex + 1, castle.HasLibraryCapability),
                rows[levelIndex].Count);
            Assert.All(rows[levelIndex], spell => Assert.Equal(SpellType.None, spell));
        }
    }

    [Fact]
    public void TheFirstLevelOfAGuildFillsTheBottomRowOnly()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.MageGuild1);

        // A town built by hand has no map block to roll its spells from, so the storage is rolled explicitly.
        castle.InitializeMageGuild();

        IReadOnlyList<IReadOnlyList<SpellType>> rows = castle.GetMageGuildSpellRows();

        // A first level guild holds first level spells, so the row index 0 is the one with content.
        Assert.Contains(rows[0], spell => spell != SpellType.None);

        for (int levelIndex = 1; levelIndex < rows.Count; ++levelIndex)
        {
            Assert.All(rows[levelIndex], spell => Assert.Equal(SpellType.None, spell));
        }
    }

    [Fact]
    public void TheLibraryCapabilityOfAWizardAddsASlotWithoutTheLibraryBeingBuilt()
    {
        CastleTown wizard = new(0, MapRace.Wizard);
        wizard.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.MageGuild1);

        CastleTown knight = new(0, MapRace.Knight);
        knight.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.MageGuild1);

        // The slot count asks about the race; the spells themselves would ask about the built special too.
        Assert.True(wizard.HasLibraryCapability);
        Assert.False(wizard.IsLibraryBuilt);
        Assert.Equal(
            MageGuild.GetMaxSpellsCount(1, true),
            wizard.GetMageGuildSpellRows()[0].Count);
        Assert.Equal(
            MageGuild.GetMaxSpellsCount(1, false),
            knight.GetMageGuildSpellRows()[0].Count);
    }

    [Fact]
    public void EveryRowIsAsLongAsTheGuildOwesThatLevel()
    {
        CastleTown castle = new(0, MapRace.Sorceress);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.MageGuild1 | BuildingType.MageGuild2);

        IReadOnlyList<IReadOnlyList<SpellType>> rows = castle.GetMageGuildSpellRows();

        for (int levelIndex = 0; levelIndex < rows.Count; ++levelIndex)
        {
            Assert.Equal(
                MageGuild.GetMaxSpellsCount(levelIndex + 1, castle.HasLibraryCapability),
                rows[levelIndex].Count);
        }
    }
}
