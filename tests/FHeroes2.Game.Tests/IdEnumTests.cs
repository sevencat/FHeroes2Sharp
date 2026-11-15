using FHeroes2.Game.Monsters;
using FHeroes2.Game.Resources;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the two id enumerations the object tables need: <see cref="MonsterType"/> and
/// <see cref="ArtifactType"/>. Their values are part of the map and save format, so the anchors below
/// are pinned from the reference (<c>monster/monster.h</c>, <c>resource/artifact.h</c>) and a
/// regeneration of either enumeration fails here instead of shifting an id by one.
/// </summary>
public sealed class IdEnumTests
{
    [Fact]
    public void MonsterTypeKeepsTheReferenceNumbering()
    {
        Assert.Equal(0, (int)MonsterType.Unknown);
        Assert.Equal(1, (int)MonsterType.Peasant);
        Assert.Equal(66, (int)MonsterType.WaterElement);

        // The four editor placeholders follow the creatures, in the order of the reference.
        Assert.Equal(67, (int)MonsterType.RandomMonster);
        Assert.Equal(68, (int)MonsterType.RandomMonsterLevel1);
        Assert.Equal(69, (int)MonsterType.RandomMonsterLevel2);
        Assert.Equal(70, (int)MonsterType.RandomMonsterLevel3);
        Assert.Equal(71, (int)MonsterType.RandomMonsterLevel4);

        // MONSTER_COUNT is the id after the last member, not a monster.
        Assert.Equal(72, (int)MonsterType.MonsterCount);
        Assert.Equal((int)MonsterType.MonsterCount, Enum.GetValues<MonsterType>().Length - 1);
    }

    [Fact]
    public void MonsterTypeHasNoGapsOrDuplicates()
    {
        int[] ids = Enum.GetValues<MonsterType>().Select(value => (int)value).Order().ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(ids.Length - 1, ids[^1]);

        for (int i = 0; i < ids.Length; ++i)
        {
            Assert.Equal(i, ids[i]);
        }
    }

    [Fact]
    public void ArtifactTypeKeepsTheReferenceNumbering()
    {
        // The reference notes that every id is one bigger than in the original game.
        Assert.Equal(0, (int)ArtifactType.Unknown);
        Assert.Equal(1, (int)ArtifactType.UltimateBook);
        Assert.Equal(8, (int)ArtifactType.GoldenGoose);
        Assert.Equal(9, (int)ArtifactType.ArcaneNecklace);

        // The last artifact before Magic Book, which has no image of its own.
        Assert.Equal(81, (int)ArtifactType.BlackPearl);
        Assert.Equal(82, (int)ArtifactType.MagicBook);
        Assert.Equal(83, (int)ArtifactType.EditorAnyUltimateArtifact);
        Assert.Equal(84, (int)ArtifactType.Unused84);
        Assert.Equal(85, (int)ArtifactType.Unused85);
        Assert.Equal(86, (int)ArtifactType.Unused86);

        // The Price of Loyalty artifacts and the end of the table.
        Assert.Equal(87, (int)ArtifactType.SpellScroll);
        Assert.Equal(103, (int)ArtifactType.SpadeNecromancy);
        Assert.Equal(104, (int)ArtifactType.ArtifactCount);
        Assert.Equal((int)ArtifactType.ArtifactCount, Enum.GetValues<ArtifactType>().Length - 1);
    }

    [Fact]
    public void ArtifactTypeHasNoGapsOrDuplicates()
    {
        int[] ids = Enum.GetValues<ArtifactType>().Select(value => (int)value).Order().ToArray();

        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(ids.Length - 1, ids[^1]);

        for (int i = 0; i < ids.Length; ++i)
        {
            Assert.Equal(i, ids[i]);
        }
    }
}
