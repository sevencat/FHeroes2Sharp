using System.Buffers.Binary;
using System.Text;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="Mp2HeroInfo"/>, the reader of the 76 byte <c>MP2::mp2hero_t</c> block
/// (<c>mp2.h</c>) that <c>Heroes::LoadFromMP2()</c> (<c>heroes.cpp:421</c>) unpacks.
/// </summary>
public sealed class Mp2HeroInfoTests
{
    [Fact]
    public void TryReadRejectsABlockOfTheWrongSize()
    {
        Assert.False(Mp2HeroInfo.TryRead(new byte[Mp2HeroInfo.StructureSize - 1], out _));
        Assert.False(Mp2HeroInfo.TryRead(new byte[Mp2HeroInfo.StructureSize + 1], out _));
        Assert.False(Mp2HeroInfo.TryRead([], out _));
    }

    [Fact]
    public void TryReadReturnsAnEmptyBlockForAZeroedStructure()
    {
        Assert.True(Mp2HeroInfo.TryRead(new byte[Mp2HeroInfo.StructureSize], out Mp2HeroInfo info));

        Assert.False(info.HasCustomArmy);
        Assert.Equal(Mp2HeroInfo.TroopCount, info.MonsterIds.Count);
        Assert.Equal(Mp2HeroInfo.TroopCount, info.MonsterCounts.Count);
        Assert.All(info.MonsterCounts, count => Assert.Equal(0, count));

        Assert.False(info.HasCustomPortrait);
        Assert.Equal(0, info.PortraitId);
        Assert.Equal(Mp2HeroInfo.ArtifactCount, info.ArtifactIds.Count);
        Assert.Equal(0u, info.Experience);
        Assert.False(info.HasCustomSecondarySkills);
        Assert.Equal(Mp2HeroInfo.SecondarySkillCount, info.SecondarySkillIds.Count);
        Assert.Equal(Mp2HeroInfo.SecondarySkillCount, info.SecondarySkillLevels.Count);
        Assert.False(info.HasCustomName);
        Assert.Equal(string.Empty, info.Name);
        Assert.False(info.IsOnPatrol);
        Assert.Equal(0, info.PatrolDistanceOrJailRace);
    }

    [Fact]
    public void TryReadReadsTheFieldsAtTheReferenceOffsets()
    {
        Assert.True(Mp2HeroInfo.TryRead(CreateBlock(), out Mp2HeroInfo info));

        // The army: a custom flag, five monster ids and five little endian counts (heroes.cpp:451-478).
        Assert.True(info.HasCustomArmy);
        Assert.Equal([4, 5, 6, 7, 8], info.MonsterIds);
        Assert.Equal([10, 20, 300, 400, 500], info.MonsterCounts);

        // The portrait, pblock[17] and pblock[18]; the engine adds one to the id (heroes.cpp:486-492).
        Assert.True(info.HasCustomPortrait);
        Assert.Equal(42, info.PortraitId);

        Assert.Equal([2, 3, 4], info.ArtifactIds);
        Assert.Equal(123456u, info.Experience);

        Assert.True(info.HasCustomSecondarySkills);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], info.SecondarySkillIds);
        Assert.Equal([0, 1, 2, 3, 0, 1, 2, 3], info.SecondarySkillLevels);

        Assert.True(info.HasCustomName);
        Assert.Equal("Zhanna", info.Name);

        Assert.True(info.IsOnPatrol);
        Assert.Equal(5, info.PatrolDistanceOrJailRace);
    }

    [Fact]
    public void TryReadStopsTheNameAtTheTerminator()
    {
        byte[] data = new byte[Mp2HeroInfo.StructureSize];
        data[45] = 1;
        Encoding.ASCII.GetBytes("abc").CopyTo(data, 46);

        Assert.True(Mp2HeroInfo.TryRead(data, out Mp2HeroInfo info));
        Assert.Equal("abc", info.Name);

        // A name without a terminator fills the field, which is what the original editor writes when the
        // name is exactly as long as the field.
        byte[] full = new byte[Mp2HeroInfo.StructureSize];
        Encoding.ASCII.GetBytes("abcdefghijklm").CopyTo(full, 46);

        Assert.True(Mp2HeroInfo.TryRead(full, out Mp2HeroInfo longName));
        Assert.Equal("abcdefghijklm", longName.Name);
    }

    /// <summary>
    /// Builds a block whose every field has a recognizable value.
    /// </summary>
    private static byte[] CreateBlock()
    {
        byte[] data = new byte[Mp2HeroInfo.StructureSize];

        data[1] = 1;

        int[] counts = [10, 20, 300, 400, 500];
        for (int slot = 0; slot < Mp2HeroInfo.TroopCount; ++slot)
        {
            data[2 + slot] = (byte)(4 + slot);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(7 + (slot * 2)), (ushort)counts[slot]);
        }

        data[17] = 1;
        data[18] = 42;
        data[19] = 2;
        data[20] = 3;
        data[21] = 4;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(23), 123456);

        data[27] = 1;
        for (int slot = 0; slot < Mp2HeroInfo.SecondarySkillCount; ++slot)
        {
            data[28 + slot] = (byte)(slot + 1);
            data[36 + slot] = (byte)(slot % 4);
        }

        data[45] = 1;
        Encoding.ASCII.GetBytes("Zhanna").CopyTo(data, 46);
        data[59] = 1;
        data[60] = 5;

        return data;
    }
}
