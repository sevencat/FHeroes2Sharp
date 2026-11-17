using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.Castle;

using CastleHelper = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The town screen's building-to-ICN table: one sprite file per building per race, including the reference's own
/// quirks, which are the part a later reader is most likely to "fix" by accident.
/// </summary>
public sealed class CastleIcnTableTests
{
    private static readonly MapRace[] Races =
    [
        MapRace.Barbarian,
        MapRace.Knight,
        MapRace.Necromancer,
        MapRace.Sorceress,
        MapRace.Warlock,
        MapRace.Wizard,
    ];

    /// <summary>The buildings every race can raise, and therefore must have a sprite for.</summary>
    private static readonly BuildingType[] CommonBuildings =
    [
        BuildingType.Castle,
        BuildingType.Tent,
        BuildingType.Spec,
        BuildingType.Captain,
        BuildingType.Well2,
        BuildingType.LeftTurret,
        BuildingType.RightTurret,
        BuildingType.Moat,
        BuildingType.Marketplace,
        BuildingType.ThievesGuild,
        BuildingType.Well,
        BuildingType.Statue,
        BuildingType.Shipyard,
        BuildingType.MageGuild,
        BuildingType.DwellingMonster1,
        BuildingType.DwellingMonster2,
        BuildingType.DwellingMonster3,
        BuildingType.DwellingMonster4,
        BuildingType.DwellingMonster5,
        BuildingType.DwellingMonster6,
    ];

    [Fact]
    public void EveryRaceHasItsOwnSpriteForTheCommonBuildings()
    {
        foreach (MapRace race in Races)
        {
            foreach (BuildingType building in CommonBuildings)
            {
                Assert.NotEqual(IcnType.Unknown, CastleHelper.GetICNBuilding(building, race));
            }

            if (race != MapRace.Necromancer)
            {
                // The necromancer drinks at its shrine instead; see the shrine test.
                Assert.NotEqual(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.Tavern, race));
            }
        }
    }

    [Fact]
    public void TheTownsOfTwoRacesNeverShareASpriteFile()
    {
        IcnType knight = CastleHelper.GetICNBuilding(BuildingType.Castle, MapRace.Knight);

        foreach (MapRace race in Races)
        {
            if (race == MapRace.Knight)
            {
                continue;
            }

            Assert.NotEqual(knight, CastleHelper.GetICNBuilding(BuildingType.Castle, race));
        }
    }

    [Fact]
    public void TheMageGuildLooksTheSameAtEveryLevel()
    {
        // The guild is one building with five levels; the reference names a single sprite for all of them, and the
        // port's table is keyed by the mask, so every level has to fold into it.
        BuildingType[] levels =
        [
            BuildingType.MageGuild1,
            BuildingType.MageGuild2,
            BuildingType.MageGuild3,
            BuildingType.MageGuild4,
            BuildingType.MageGuild5,
        ];

        foreach (MapRace race in Races)
        {
            IcnType mask = CastleHelper.GetICNBuilding(BuildingType.MageGuild, race);

            Assert.NotEqual(IcnType.Unknown, mask);

            foreach (BuildingType level in levels)
            {
                Assert.Equal(mask, CastleHelper.GetICNBuilding(level, race));
            }
        }
    }

    [Fact]
    public void TheKnightsSecondDwellingAndItsUpgradeAreSwapped()
    {
        // The reference's own quirk: the base dwelling is drawn with the upgraded sprite and vice versa.
        Assert.Equal(IcnType.Twnkup1, CastleHelper.GetICNBuilding(BuildingType.DwellingMonster2, MapRace.Knight));
        Assert.Equal(IcnType.Twnkdw1, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade2, MapRace.Knight));

        // The other races are not swapped.
        Assert.Equal(IcnType.Twnbdw1, CastleHelper.GetICNBuilding(BuildingType.DwellingMonster2, MapRace.Barbarian));
        Assert.Equal(IcnType.Twnbup1, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade2, MapRace.Barbarian));
    }

    [Fact]
    public void TheKnightsSecondWellIsAFarm()
    {
        // Well2 is the "second well" of the town's original graphics; for the knight it is a farm of its own file.
        Assert.Equal(IcnType.KnightCastleRightFarm, CastleHelper.GetICNBuilding(BuildingType.Well2, MapRace.Knight));

        foreach (MapRace race in Races)
        {
            if (race == MapRace.Knight)
            {
                continue;
            }

            Assert.NotEqual(IcnType.KnightCastleRightFarm, CastleHelper.GetICNBuilding(BuildingType.Well2, race));
        }
    }

    [Fact]
    public void TheNecromancersShrineIsDrawnAsATavern()
    {
        // The necromancer has no tavern; the shrine takes its slot.
        Assert.Equal(IcnType.Twnntvrn, CastleHelper.GetICNBuilding(BuildingType.Shrine, MapRace.Necromancer));
        Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.Tavern, MapRace.Necromancer));

        // No other race has a shrine at all.
        foreach (MapRace race in Races)
        {
            if (race == MapRace.Necromancer)
            {
                continue;
            }

            Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.Shrine, race));
        }
    }

    [Fact]
    public void TheNecromancerCaptainIsAStandaloneBuilding()
    {
        Assert.Equal(
            IcnType.NecromancerCastleStandaloneCaptainQuarters,
            CastleHelper.GetICNBuilding(BuildingType.Captain, MapRace.Necromancer));
    }

    [Fact]
    public void TheSorceressHasNoUpgradedFifthOrSixthDwelling()
    {
        // Her last two dwellings are the only ones in the game that cannot be upgraded.
        Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade5, MapRace.Sorceress));
        Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade6, MapRace.Sorceress));

        Assert.NotEqual(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade5, MapRace.Knight));
        Assert.NotEqual(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade6, MapRace.Knight));
    }

    [Fact]
    public void TheWarlockHasTheOnlySeventhDwellingUpgrade()
    {
        // The warlock's green tower is a dwelling of its own level, and this is its sprite.
        Assert.Equal(IcnType.Twnwup5b, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade7, MapRace.Warlock));

        foreach (MapRace race in Races)
        {
            if (race == MapRace.Warlock)
            {
                continue;
            }

            Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade7, race));
        }
    }

    [Fact]
    public void TheWizardHasNoSecondDwellingUpgrade()
    {
        Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade2, MapRace.Wizard));
        Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade4, MapRace.Wizard));
        Assert.NotEqual(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.DwellingUpgrade3, MapRace.Wizard));
    }

    [Fact]
    public void AnEmptySlotAndAnUnknownRaceHaveNoSprite()
    {
        foreach (MapRace race in Races)
        {
            Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.Nothing, race));
        }

        Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.Castle, MapRace.None));
        Assert.Equal(IcnType.Unknown, CastleHelper.GetICNBuilding(BuildingType.Castle, MapRace.Random));
    }
}
