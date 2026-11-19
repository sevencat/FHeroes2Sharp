using FHeroes2.Assets.Maps;
using FHeroes2.Core.Io;
using FHeroes2.Game.Army;
using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Heroes;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;

using ArmyStack = FHeroes2.Game.Army.Army;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The save operators of <c>army_troop.cpp:211-219</c> and <c>army.cpp:1977-2032</c>, ported by
/// <c>Troop.Persistence.cs</c> and <c>Army.Persistence.cs</c>.
/// </summary>
public sealed class ArmyPersistenceTests
{
    [Fact]
    public void ATroopTakesEightBytes()
    {
        Troop troop = new(MonsterType.Archer, 42);
        StreamBuf stream = new();

        troop.WriteTo(stream);

        Assert.Equal(8, Troop.SerializedSize);
        Assert.Equal(Troop.SerializedSize, stream.Written.Length);
    }

    [Fact]
    public void ATroopRoundTrips()
    {
        Troop written = new(MonsterType.Archer, 42);
        StreamBuf stream = new();

        written.WriteTo(stream);

        Troop read = new();
        Assert.True(read.ReadFrom(new RoStreamBuf(stream.Written.ToArray())));
        Assert.Equal(MonsterType.Archer, read.Monster);
        Assert.Equal(42u, read.Count);
    }

    [Fact]
    public void AnUnknownCreatureIsReadWithoutFailing()
    {
        // The reference reads whatever id the save holds and lets isValid() answer false afterwards.
        StreamBuf stream = new();
        stream.Write(9999);
        stream.Write(7u);

        Troop troop = new();
        Assert.True(troop.ReadFrom(new RoStreamBuf(stream.Written.ToArray())));
        Assert.Equal((MonsterType)9999, troop.Monster);
        Assert.False(troop.IsValid);
    }

    [Fact]
    public void AnArmyRoundTripsItsStacksFormationColourAndSize()
    {
        ArmyStack written = new();
        written.JoinTroop(MonsterType.Peasant, 10);
        written.JoinTroop(MonsterType.Archer, 5);
        written.IsSpreadFormation = true;
        written.Color = PlayerColor.Red;

        StreamBuf stream = new();
        written.WriteTo(stream);

        Assert.Equal(ArmyStack.SerializedSize, stream.Written.Length);

        ArmyStack read = new();
        Assert.True(read.ReadFrom(new RoStreamBuf(stream.Written.ToArray()), SaveFileFormatInfo.Current));

        Assert.Equal(PlayerColor.Red, read.Color);
        Assert.True(read.IsSpreadFormation);
        Assert.Equal(2u, read.OccupiedSlotCount);
        Assert.Equal(MonsterType.Peasant, read.GetTroop(0).Monster);
        Assert.Equal(10u, read.GetTroop(0).Count);
        Assert.Equal(MonsterType.Archer, read.GetTroop(1).Monster);
        Assert.Equal(5u, read.GetTroop(1).Count);
    }

    [Fact]
    public void ReadingAnArmyDropsTheCommander()
    {
        ArmyStack written = new();
        written.JoinTroop(MonsterType.Peasant, 10);

        StreamBuf stream = new();
        written.WriteTo(stream);

        ArmyStack read = new() { Commander = new CountingCommander() };
        Assert.True(read.ReadFrom(new RoStreamBuf(stream.Written.ToArray()), SaveFileFormatInfo.Current));
        Assert.Null(read.Commander);
    }

    [Fact]
    public void TheColourWasFourBytesBeforeOneOneNine()
    {
        ArmyStack written = new();
        written.JoinTroop(MonsterType.Peasant, 10);
        written.Color = PlayerColor.Blue;

        StreamBuf stream = new();
        WriteInOldFormat(written, stream);

        ArmyStack read = new();
        Assert.True(read.ReadFrom(new RoStreamBuf(stream.Written.ToArray()), SaveFileFormat.Release1_1_08));
        Assert.Equal(PlayerColor.Blue, read.Color);
        Assert.Equal(10u, read.GetTroop(0).Count);
    }

    [Fact]
    public void AnUnknownSlotCountFailsAndEmptiesTheArmy()
    {
        StreamBuf stream = new();
        stream.Write(4u);

        ArmyStack army = new();
        army.JoinTroop(MonsterType.Peasant, 10);

        RoStreamBuf reader = new(stream.Written.ToArray());

        Assert.False(army.ReadFrom(reader, SaveFileFormatInfo.Current));
        Assert.True(reader.Fail);
        Assert.False(army.IsValid);
        Assert.Equal(0u, army.OccupiedSlotCount);
    }

    [Fact]
    public void AFailedStreamIsNotReadAgain()
    {
        ArmyStack army = new();
        RoStreamBuf reader = new([1, 2, 3]);

        reader.SetFail();

        Assert.False(army.ReadFrom(reader, SaveFileFormatInfo.Current));
    }

    /// <summary>Writes an army the way the format before 1.1.09 laid it out: a four byte colour.</summary>
    private static void WriteInOldFormat(ArmyStack army, StreamBuf stream)
    {
        stream.Write((uint)ArmyStack.MaximumTroopCount);

        foreach (Troop troop in army.Slots)
        {
            troop.WriteTo(stream);
        }

        stream.Write(army.IsSpreadFormation);
        stream.Write((int)army.Color);
    }

    /// <summary>A commander that only says it is a hero: the army's save code asks nothing else.</summary>
    private sealed class CountingCommander : IArmyCommander
    {
        public bool IsHeroes => true;

        public ArtifactBag BagArtifacts { get; } = new();

        public int GetAttack() => 0;

        public int GetDefense() => 0;

        public uint GetSecondarySkillValue(SecondarySkill skill) => 0;

        public double GetMagicStrategicValue(double armyStrength) => 0;

        public PlayerControl GetControl() => PlayerControl.None;

        public PlayerColor GetColor() => PlayerColor.None;

        public int GetPower() => 0;

        public int GetMorale() => 0;

        public int GetLuck() => 0;
    }
}
