using FHeroes2.Assets.Maps;
using FHeroes2.Core.Random;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Monsters;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Reading a town out of its 70-byte block in an MP2 map, and the repairs that follow.
/// </summary>
/// <remarks>
/// The block is built by hand here rather than taken from a map file: the layout is the thing under test, and a
/// hand-built block can say "this town has a shipyard and no water" without a map that has one.
/// </remarks>
public sealed class CastleLoadTests
{
    [Fact]
    public void ABlockTakesItsOwnerAndItsRace()
    {
        byte[] block = Block();

        block[0] = 2; // Red.
        block[38] = 3; // Warlock.
        block[39] = 1; // Castle, not tent.

        CastleTown castle = Load(block);

        Assert.Equal(PlayerColor.Red, castle.Color);
        Assert.Equal(PlayerColor.Red, castle.Army.Color);
        Assert.Equal(MapRace.Warlock, castle.Race);
        Assert.True(castle.IsCastle);
        Assert.False(castle.IsBuild(BuildingType.Tent));
    }

    [Fact]
    public void ATownWithoutACastleIsATent()
    {
        byte[] block = Block();

        block[0] = 0;
        block[39] = 0;

        CastleTown castle = Load(block);

        Assert.False(castle.IsCastle);
        Assert.True(castle.IsBuild(BuildingType.Tent));
    }

    [Fact]
    public void TheCommonBuildingsComeFromTheirBits()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1; // Custom buildings.
        PutUInt16(block, 2, 0x0004 | 0x0010 | 0x0080 | 0x0100 | 0x0200 | 0x0400 | 0x1000 | 0x2000);
        block[38] = 0;
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.True(castle.IsBuild(BuildingType.Tavern));
        Assert.True(castle.IsBuild(BuildingType.Well));
        Assert.True(castle.IsBuild(BuildingType.Statue));
        Assert.True(castle.IsBuild(BuildingType.LeftTurret));
        Assert.True(castle.IsBuild(BuildingType.RightTurret));
        Assert.True(castle.IsBuild(BuildingType.Marketplace));
        Assert.True(castle.IsBuild(BuildingType.Moat));
        Assert.True(castle.IsBuild(BuildingType.Spec));

        // The bits that were not set stay unset — in particular the thieves' guild and the second well.
        Assert.False(castle.IsBuild(BuildingType.ThievesGuild));
        Assert.False(castle.IsBuild(BuildingType.Well2));
    }

    [Fact]
    public void AnUpgradeBitBringsItsDwellingAlong()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        PutUInt16(block, 4, 0x0200); // The second dwelling, upgraded.
        block[38] = 0;
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.True(castle.IsBuild(BuildingType.DwellingMonster2));
        Assert.True(castle.IsBuild(BuildingType.DwellingUpgrade2));
    }

    [Fact]
    public void TheMageGuildStoresALevelNotABitPerGuild()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        block[6] = 3;
        block[38] = 0;
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.Equal(3, castle.GetLevelMageGuild());
        Assert.True(castle.IsBuild(BuildingType.MageGuild3));
        Assert.False(castle.IsBuild(BuildingType.MageGuild4));
    }

    [Fact]
    public void ADifficultyRollStandsInForMissingBuildingBits()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 0; // No custom buildings, so the five bytes are skipped.
        block[38] = 0;
        block[39] = 1;

        // Nothing is ever rolled on impossible: the first dwelling and nothing else.
        CastleTown castle = Load(block, GameDifficulty.Impossible);

        Assert.True(castle.IsBuild(BuildingType.DwellingMonster1));
        Assert.False(castle.IsBuild(BuildingType.DwellingMonster2));
        Assert.Equal(1, castle.CountBuildings() - 1); // The castle itself aside.

        // On easy the second one is a 75% chance, so a good seed gives it.
        Assert.True(Load(block, GameDifficulty.Easy, seed: 1).IsBuild(BuildingType.DwellingMonster2)
            || Load(block, GameDifficulty.Easy, seed: 2).IsBuild(BuildingType.DwellingMonster2)
            || Load(block, GameDifficulty.Easy, seed: 3).IsBuild(BuildingType.DwellingMonster2));
    }

    [Fact]
    public void EveryBuiltDwellingIsStockedWithOneWeekOfCreatures()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        PutUInt16(block, 4, 0x0008 | 0x0010 | 0x0020); // The first three dwellings.
        block[38] = 0; // Knight.
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.Equal(Growth(MapRace.Knight, BuildingType.DwellingMonster1), castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(Growth(MapRace.Knight, BuildingType.DwellingMonster2), castle.GetMonstersInDwelling(BuildingType.DwellingMonster2));
        Assert.Equal(Growth(MapRace.Knight, BuildingType.DwellingMonster3), castle.GetMonstersInDwelling(BuildingType.DwellingMonster3));
        Assert.Equal(0u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster4));
    }

    [Fact]
    public void AnUpgradedDwellingIsStockedWithTheUpgradedCreature()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        PutUInt16(block, 4, 0x2000); // The sixth dwelling, upgraded.
        block[38] = 0; // Knight, whose sixth upgrade is the crusader.
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.Equal(
            Growth(MapRace.Knight, BuildingType.DwellingUpgrade6),
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster6));

        // The shelf answers to all three names of the shelf, so the base name shows the crusaders too.
        Assert.Equal(
            castle.GetMonstersInDwelling(BuildingType.DwellingMonster6),
            castle.GetMonstersInDwelling(BuildingType.DwellingUpgrade6));
    }

    [Fact]
    public void TheRaceDropsTheUpgradesItDoesNotHave()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        PutUInt16(block, 4, 0x0400); // Third dwelling, upgraded — the barbarians have no such upgrade.
        block[38] = 1; // Barbarian.
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.True(castle.IsBuild(BuildingType.DwellingMonster3));
        Assert.False(castle.IsBuild(BuildingType.DwellingUpgrade3));

        // The stock is the wolf's, not the upgrade's.
        Assert.Equal(Growth(MapRace.Barbarian, BuildingType.DwellingMonster3), castle.GetMonstersInDwelling(BuildingType.DwellingMonster3));
    }

    [Fact]
    public void CustomDefendersReplaceTheGarrisonInOrder()
    {
        byte[] block = Block();

        block[0] = 3; // Yellow.
        block[1] = 1;
        block[7] = 1; // Custom defenders.
        block[8] = 0; // Peasant.
        block[9] = 0; // Peasant again — two separate stacks, which is why this is not a join.
        block[10] = 5; // Swordsman, whose id is one less than its enum value.
        PutUInt16(block, 13, 20);
        PutUInt16(block, 15, 30);
        PutUInt16(block, 17, 5);
        block[38] = 0;
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.True(castle.HasCustomArmy);
        Assert.Equal(3u, castle.Army.OccupiedSlotCount);
        Assert.Equal(MonsterType.Peasant, castle.Army.GetTroop(0).Monster);
        Assert.Equal(20u, castle.Army.GetTroop(0).Count);
        Assert.Equal(MonsterType.Peasant, castle.Army.GetTroop(1).Monster);
        Assert.Equal(30u, castle.Army.GetTroop(1).Count);
        Assert.Equal(MonsterType.Swordsman, castle.Army.GetTroop(2).Monster);
        Assert.Equal(PlayerColor.Yellow, castle.Army.Color);
    }

    [Fact]
    public void ANeutralTownGrowsAGarrisonWhenTheMapGivesItNone()
    {
        byte[] block = Block();

        block[0] = 255; // Nobody's.
        block[1] = 1;
        block[38] = 0; // Knight.
        block[39] = 1;

        CastleTown castle = Load(block, dayCount: 100);

        Assert.Equal(PlayerColor.None, castle.Color);
        Assert.False(castle.HasCustomArmy);
        Assert.True(castle.Army.GetCount() > 0, "four reinforcement rolls should leave something behind");

        // Whatever the rolls produced, it is this town's own race that grew.
        for (int slot = 0; slot < CastleTown.MaxNumOfDwellings - 1; ++slot)
        {
            if (castle.Army.GetTroop(slot).IsValid)
            {
                Assert.Equal(MapRace.Knight, MonsterStatistics.GetGeneralStats(castle.Army.GetTroop(slot).Monster).Race);
            }
        }
    }

    [Fact]
    public void ACustomGarrisonIsNotReinforced()
    {
        byte[] block = Block();

        block[0] = 255;
        block[1] = 1;
        block[7] = 1;
        block[8] = 0;
        PutUInt16(block, 13, 7);
        block[38] = 0;
        block[39] = 1;

        CastleTown castle = Load(block, dayCount: 300);

        Assert.True(castle.HasCustomArmy);
        Assert.Equal(1u, castle.Army.OccupiedSlotCount);
        Assert.Equal(7u, castle.Army.GetTroop(0).Count);
    }

    [Fact]
    public void TheShipyardNeedsWater()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        PutUInt16(block, 2, 0x0008);
        block[38] = 0;
        block[39] = 1;

        Assert.False(Load(block).IsBuild(BuildingType.Shipyard));
        Assert.True(Load(block, seaAccess: true).IsBuild(BuildingType.Shipyard));
    }

    [Fact]
    public void TheNecromancersTavernBecomesAShrine()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        PutUInt16(block, 2, 0x0004); // A tavern in the block.
        block[38] = 5; // Necromancer.
        block[39] = 1;

        CastleTown priceOfLoyalty = Load(block);

        Assert.False(priceOfLoyalty.IsBuild(BuildingType.Tavern));
        Assert.True(priceOfLoyalty.IsBuild(BuildingType.Shrine));

        // Succession Wars has no shrine at all, so the tavern is simply gone.
        CastleTown successionWars = Load(block, version: MapVersion.SuccessionWars);

        Assert.False(successionWars.IsBuild(BuildingType.Tavern));
        Assert.False(successionWars.IsBuild(BuildingType.Shrine));
    }

    [Fact]
    public void ACustomNameComesFromTheBlock()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        block[24] = 1; // Custom name.
        "Stonehelm"u8.CopyTo(block.AsSpan(25));
        block[38] = 0;
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.Equal("Stonehelm", castle.Name);
    }

    [Fact]
    public void ACaptainIsOnlyTheBuildingBitForNow()
    {
        byte[] block = Block();

        block[0] = 1;
        block[1] = 1;
        block[23] = 1; // Captain.
        block[38] = 0;
        block[39] = 1;

        CastleTown castle = Load(block);

        Assert.True(castle.IsBuild(BuildingType.Captain));
    }

    [Fact]
    public void ARandomCastleTakesTheOwnersRaceWhenItHasOne()
    {
        byte[] block = Block();

        block[0] = 4; // Orange.
        block[1] = 1;
        block[38] = 255; // No faction in the block.
        block[39] = 1;

        CastleTown castle = Load(block, ownerRace: MapRace.Sorceress);

        Assert.Equal(MapRace.Sorceress, castle.Race);
    }

    [Fact]
    public void AnOwnerlessRandomCastleRollsOneOfTheSix()
    {
        byte[] block = Block();

        block[0] = 255;
        block[1] = 1;
        block[38] = 255;
        block[39] = 1;

        CastleTown castle = Load(block, dayCount: 0);

        Assert.Contains(castle.Race, new[]
        {
            MapRace.Knight, MapRace.Barbarian, MapRace.Sorceress, MapRace.Warlock, MapRace.Wizard, MapRace.Necromancer,
        });
    }

    [Fact]
    public void ABlockOfTheWrongSizeIsRefused()
    {
        CastleTown castle = new(0, MapRace.Knight);

        Assert.Throws<ArgumentException>(() => castle.LoadFromMP2(new byte[CastleTown.Mp2BlockSize - 1], GameDifficulty.Normal));
    }

    /// <summary>A 70-byte town block that says nothing at all.</summary>
    /// <returns>The block, all zeroes.</returns>
    private static byte[] Block() => new byte[CastleTown.Mp2BlockSize];

    /// <summary>
    /// Loads a town from a block, with the world's answers filled in as parameters.
    /// </summary>
    /// <param name="block">The 70-byte block.</param>
    /// <param name="difficulty">The map's difficulty.</param>
    /// <param name="version">The map's version.</param>
    /// <param name="ownerRace">The owner's kingdom race, when the block leaves the faction random.</param>
    /// <param name="seaAccess">Whether the town is next to water.</param>
    /// <param name="seed">The generator's seed.</param>
    /// <param name="dayCount">Days elapsed on the map.</param>
    /// <returns>The loaded town.</returns>
    private static CastleTown Load(
        byte[] block,
        GameDifficulty difficulty = GameDifficulty.Normal,
        MapVersion version = MapVersion.PriceOfLoyalty,
        MapRace? ownerRace = null,
        bool seaAccess = false,
        uint seed = 7,
        uint dayCount = 0)
    {
        CastleTown castle = new(0, MapRace.Knight, version) { HasSeaAccess = seaAccess };

        castle.LoadFromMP2(block, difficulty, ownerRace, new Pcg32(seed), dayCount);

        return castle;
    }

    /// <summary>One week's growth of a dwelling's creature.</summary>
    /// <param name="race">The town's race.</param>
    /// <param name="dwelling">The dwelling.</param>
    /// <returns>The growth.</returns>
    private static uint Growth(MapRace race, BuildingType dwelling) =>
        MonsterStatistics.GetGeneralStats(DwellingMonsters.FromDwelling(race, dwelling)).BaseGrowth;

    /// <summary>Writes a little-endian 16-bit field.</summary>
    /// <param name="block">The block.</param>
    /// <param name="offset">Where to write.</param>
    /// <param name="value">The value.</param>
    private static void PutUInt16(byte[] block, int offset, ushort value)
    {
        block[offset] = (byte)value;
        block[offset + 1] = (byte)(value >> 8);
    }
}
