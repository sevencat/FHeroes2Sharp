using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Core.Random;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Castle;
using FHeroes2.Game.Maps;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using FHeroes2.Game.System;
using Xunit;

using ArmyStack = FHeroes2.Game.Army.Army;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// A town and the table of them as a save file sees them (<c>castle.cpp:2411-2475</c>).
/// </summary>
/// <remarks>
/// The town's <c>modes</c> word is not a field of the port: it is assembled from
/// <see cref="CastleTown.CanBuildToday"/> and <see cref="CastleTown.HasCustomArmy"/> on the way out and split again on the way
/// in. The <c>UNUSED_ALLOW_CASTLE_CONSTRUCTION</c> bit only survives as the source of the disabled buildings before
/// 1.1.01.
/// </remarks>
public sealed class CastleSaveTests
{
    private const int Width = 10;

    [Fact]
    public void ATownIsWrittenFieldByField()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        WriteTown(
            stream,
            SaveFileFormatInfo.Current,
            modes: ModeCustomArmy,
            constructed: BuildingType.Castle | BuildingType.Marketplace,
            disabled: BuildingType.Tavern);
        stream.Seek(0);

        CastleTown castle = new();
        castle.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(32, castle.TileIndex);
        Assert.False(castle.CanBuildToday);
        Assert.True(castle.HasCustomArmy);
        Assert.Equal(MapRace.Knight, castle.Race);
        Assert.True(castle.IsCastle);
        Assert.True(castle.IsBuild(BuildingType.Marketplace));
        Assert.False(castle.IsBuild(BuildingType.Tavern));
        Assert.Equal(BuildingType.Tavern, castle.DisabledBuildings);
        Assert.True(castle.IsBuildingDisabled(BuildingType.Tavern));
        Assert.Equal(PlayerColor.Blue, castle.Color);
        Assert.Equal("Island", castle.Name);
        Assert.Equal(1, castle.Captain.GetAttack());
        Assert.Equal(2, castle.Captain.GetDefense());
        Assert.Equal(3, castle.Captain.GetKnowledge());
        Assert.Equal(4, castle.Captain.GetPower());
        Assert.Equal(9u, castle.Captain.SpellPoints);
        Assert.True(castle.Captain.MagicBook.IsPresent(new Spell(SpellType.Fireball)));
        Assert.Equal(1u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
        Assert.Equal(6u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster6));
        Assert.Equal(PlayerColor.Blue, castle.Army.Color);
        Assert.Equal(MapRace.Knight, castle.Army.Race);
        Assert.Same(castle.Captain, castle.Army.Commander);
    }

    [Fact]
    public void TheFirstBitOfTheModesWordIsTheGuildAndTheSecondIsTheCannon()
    {
        Assert.Equal(1u << 2, ModeCustomArmy);
        Assert.Equal(1u << 3, ModeAllowToBuildToday);

        // A town that may still be built into a castle (UNUSED_ALLOW_CASTLE_CONSTRUCTION) is not written with the bit
        // after 1.1.01, but the disabled buildings still come out of it when reading an older file.
        CastleTown castle = new();

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        WriteTown(
            stream,
            SaveFileFormat.Release1_1_00,
            modes: ModeAllowToBuildToday | ModeCustomArmy | ModeAllowCastleConstruction,
            constructed: BuildingType.Castle,
            disabled: BuildingType.Tavern);
        stream.Seek(0);
        castle.ReadFrom(stream, SaveFileFormat.Release1_1_00, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.True(castle.CanBuildToday);
        Assert.True(castle.HasCustomArmy);
        Assert.Equal((BuildingType)0, castle.DisabledBuildings);

        stream = new StreamBuf();
        stream.SetBigEndian(true);
        WriteTown(
            stream,
            SaveFileFormat.Release1_1_00,
            modes: ModeAllowToBuildToday,
            constructed: BuildingType.Castle,
            disabled: BuildingType.Tavern);
        stream.Seek(0);
        castle = new CastleTown();
        castle.ReadFrom(stream, SaveFileFormat.Release1_1_00, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.True(castle.CanBuildToday);
        Assert.False(castle.HasCustomArmy);
        Assert.Equal(BuildingType.Castle, castle.DisabledBuildings);
    }

    [Fact]
    public void ATownOfAnotherShapeFailsTheStream()
    {
        StreamBuf stream = new();
        stream.SetBigEndian(true);
        WriteTown(
            stream,
            SaveFileFormatInfo.Current,
            modes: 0,
            constructed: BuildingType.Castle,
            disabled: BuildingType.Nothing,
            dwellings: 5);
        stream.Seek(0);

        CastleTown castle = new();
        castle.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.True(stream.Fail);
        Assert.Equal(0u, castle.GetMonstersInDwelling(BuildingType.DwellingMonster1));
    }

    [Fact]
    public void ATownSurvivesARoundTrip()
    {
        CastleTown written = new(23, MapRace.Wizard)
        {
            Name = "Island",
            DisabledBuildings = BuildingType.Tavern,
        };
        written.AddBuildings(BuildingType.Castle | BuildingType.Marketplace | BuildingType.MageGuild1);
        written.ChangeColor(PlayerColor.Green);

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        written.WriteTo(stream, new MapGeometry(Width, 10));

        byte[] expected = stream.Written.ToArray();
        stream.Seek(0);

        CastleTown restored = new();
        restored.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(23, restored.TileIndex);
        Assert.True(restored.CanBuildToday);
        Assert.False(restored.HasCustomArmy);
        Assert.Equal(MapRace.Wizard, restored.Race);
        Assert.Equal(BuildingType.Castle | BuildingType.Marketplace | BuildingType.MageGuild1, restored.ConstructedBuildings);
        Assert.Equal(BuildingType.Tavern, restored.DisabledBuildings);
        Assert.Equal(PlayerColor.Green, restored.Color);
        Assert.Equal("Island", restored.Name);
        Assert.Equal(PlayerColor.Green, restored.Army.Color);

        StreamBuf again = new();
        again.SetBigEndian(true);
        restored.WriteTo(again, new MapGeometry(Width, 10));

        Assert.Equal(expected, again.Written.ToArray());
    }

    [Fact]
    public void TheWholeTableOfTownsIsReplacedByTheFile()
    {
        AllCastles written = new(Width);
        written.Add(new CastleTown(23, MapRace.Wizard) { Name = "Island" });
        written.Add(new CastleTown(53, MapRace.Knight) { Name = "Two" });

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        written.WriteTo(stream, new MapGeometry(Width, 10));

        byte[] expected = stream.Written.ToArray();
        stream.Seek(0);

        AllCastles restored = new(Width);
        restored.Add(new CastleTown(90, MapRace.Necromancer) { Name = "Stale" });

        restored.ReadFrom(stream, SaveFileFormatInfo.Current, new MapGeometry(Width, 10));

        Assert.Equal(0, stream.Size);
        Assert.Equal(2, restored.Count);
        Assert.Equal("Island", restored[0].Name);
        Assert.Equal("Two", restored[1].Name);
        Assert.Null(restored.Get(90));
        Assert.Equal(23, restored.Get(23)!.TileIndex);
        Assert.Equal(53, restored.Get(53)!.TileIndex);

        StreamBuf again = new();
        again.SetBigEndian(true);
        restored.WriteTo(again, new MapGeometry(Width, 10));

        Assert.Equal(expected, again.Written.ToArray());
    }

    [Fact]
    public void AMageGuildSurvivesARoundTrip()
    {
        // The wizard's library is the only guild with library spells, which live in one slot per level instead of a
        // whole row (castle.cpp:1113-1140, 2496-2525).
        MageGuild written = new();
        written.Initialize(MapRace.Wizard, hasLibrary: true, generator: new Pcg32(1234u));

        StreamBuf stream = new();
        stream.SetBigEndian(true);
        written.WriteTo(stream);

        byte[] expected = stream.Written.ToArray();
        stream.Seek(0);

        MageGuild restored = new();
        restored.ReadFrom(stream);

        Assert.Equal(0, stream.Size);
        Assert.Equal(16, Spells(written.GetSpells(5, hasLibrary: true)).Length);
        Assert.Equal(4, Spells(written.GetSpells(1, hasLibrary: true)).Length);
        Assert.Equal(Spells(written.GetSpells(5, hasLibrary: true)), Spells(restored.GetSpells(5, hasLibrary: true)));
        Assert.Equal(Spells(written.GetSpells(1, hasLibrary: true)), Spells(restored.GetSpells(1, hasLibrary: true)));

        StreamBuf again = new();
        again.SetBigEndian(true);
        restored.WriteTo(again);

        Assert.Equal(expected, again.Written.ToArray());
    }

    /// <summary>The <c>ALLOW_TO_BUILD_TODAY</c> bit of the town's <c>modes</c> word.</summary>
    private const uint ModeAllowToBuildToday = 1 << 3;

    /// <summary>The <c>CUSTOM_ARMY</c> bit of the town's <c>modes</c> word.</summary>
    private const uint ModeCustomArmy = 1 << 2;

    /// <summary>The <c>UNUSED_ALLOW_CASTLE_CONSTRUCTION</c> bit of the town's <c>modes</c> word.</summary>
    private const uint ModeAllowCastleConstruction = 1 << 1;

    /// <summary>Collects a spell storage into a comparable array of ids.</summary>
    /// <param name="spells">The storage.</param>
    /// <returns>The spell ids in the storage's order.</returns>
    private static int[] Spells(SpellStorage spells) => spells.Select(static spell => (int)spell.Type).ToArray();

    /// <summary>
    /// Writes a town in the given version's layout, <c>castle.cpp:2411-2475</c>.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="version">The version whose field widths to use.</param>
    /// <param name="modes">The modes word.</param>
    /// <param name="constructed">The constructed buildings.</param>
    /// <param name="disabled">The disabled buildings, written from 1.1.01 on.</param>
    /// <param name="dwellings">The number of dwelling slots written.</param>
    private static void WriteTown(
        StreamBase stream,
        SaveFileFormat version,
        uint modes,
        BuildingType constructed,
        BuildingType disabled,
        int dwellings = 6)
    {
        if (version < SaveFileFormat.PreRelease2_1_1_03)
        {
            stream.Write(0);
            stream.Write(0u);
            stream.Write((short)2);
            stream.Write((short)3);
        }
        else
        {
            stream.Write((short)2);
            stream.Write((short)3);
            stream.Write(0u);
        }

        stream.Write(modes);
        stream.Write((int)MapRace.Knight);
        stream.Write((uint)constructed);

        if (version >= SaveFileFormat.Release1_1_01)
        {
            stream.Write((uint)disabled);
        }

        WriteCaptain(stream, version);
        WriteColor(stream, version, PlayerColor.Blue);
        stream.Write("Island");

        stream.Write(0u);
        stream.Write(0u);
        stream.Write((uint)dwellings);

        for (int i = 1; i <= dwellings; ++i)
        {
            stream.Write((uint)i);
        }

        stream.Write((uint)ArmyStack.MaximumTroopCount);

        for (int i = 0; i < ArmyStack.MaximumTroopCount; ++i)
        {
            stream.Write(0);
            stream.Write(0u);
        }

        stream.Write(false);
        WriteColor(stream, version, PlayerColor.Blue);
    }

    /// <summary>Writes a captain, <c>heroes_base.cpp:618-680</c>.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="version">The version whose field widths to use.</param>
    private static void WriteCaptain(StreamBase stream, SaveFileFormat version)
    {
        stream.Write(1);
        stream.Write(2);
        stream.Write(3);
        stream.Write(4);

        if (version < SaveFileFormat.PreRelease2_1_1_03)
        {
            stream.Write(0);
            stream.Write(0u);
            stream.Write((short)-1);
            stream.Write((short)-1);
        }
        else
        {
            stream.Write((short)-1);
            stream.Write((short)-1);
            stream.Write(0u);
        }

        stream.Write(0u);
        stream.Write(9u);
        stream.Write(0u);
        stream.Write(1u);
        stream.Write((int)SpellType.Fireball);
        stream.Write((uint)ArtifactBag.MaxCapacity);
        stream.Write((int)ArtifactType.MagicBook);
        stream.Write(0u);

        for (int i = 1; i < ArtifactBag.MaxCapacity; ++i)
        {
            stream.Write((int)ArtifactType.Unknown);
            stream.Write(0u);
        }
    }

    /// <summary>Writes a colour as the version spells it: an <c>int32</c> before 1.1.09, a byte after.</summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="version">The version whose field width to use.</param>
    /// <param name="color">The colour.</param>
    private static void WriteColor(StreamBase stream, SaveFileFormat version, PlayerColor color)
    {
        if (version < SaveFileFormat.Release1_1_09)
        {
            stream.Write((int)color);
        }
        else
        {
            stream.Write((byte)color);
        }
    }
}
