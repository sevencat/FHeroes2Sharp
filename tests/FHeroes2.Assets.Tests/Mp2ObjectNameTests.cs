using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="Mp2ObjectName"/> and <see cref="Mp2MonsterName"/>: the two name tables the
/// reference keeps for what a tile holds and for the creatures of a stack.
/// </summary>
public sealed class Mp2ObjectNameTests
{
    [Fact]
    public void EveryObjectOfTheGameHasAName()
    {
        foreach (Mp2ObjectType type in Enum.GetValues<Mp2ObjectType>())
        {
            string name = Mp2ObjectName.Of(type);

            Assert.False(
                int.TryParse(name, out _),
                FormattableString.Invariant($"'{type}' has no name, it fell back to the number {name}"));
        }
    }

    [Fact]
    public void TheNameOfAnActionObjectIsTheNameOfItsKind()
    {
        Assert.Equal("Artifact", Mp2ObjectName.Of(Mp2ObjectType.ActionArtifact));
        Assert.Equal("Monster", Mp2ObjectName.Of(Mp2ObjectType.ActionMonster));
        Assert.Equal("Treasure Chest", Mp2ObjectName.Of(Mp2ObjectType.ActionTreasureChest));
        Assert.Equal(Mp2ObjectName.Of(Mp2ObjectType.Mine), Mp2ObjectName.Of(Mp2ObjectType.ActionMine));
    }

    [Fact]
    public void TheNamesFollowTheReference()
    {
        Assert.Equal("No object", Mp2ObjectName.Of(Mp2ObjectType.None));
        Assert.Equal("Beach", Mp2ObjectName.Of(Mp2ObjectType.Coast));
        Assert.Equal("Trees", Mp2ObjectName.Of(Mp2ObjectType.Trees));
        Assert.Equal("Archer's House", Mp2ObjectName.Of(Mp2ObjectType.ArcherHouse));
        Assert.Equal("Random Monster - very strong", Mp2ObjectName.Of(Mp2ObjectType.ActionRandomMonsterVeryStrong));
    }

    [Fact]
    public void TheObjectsOfThePriceOfLoyaltyKeepBitEight()
    {
        // They live above OBJ_RESURRECTION_OBJECT_TYPE, so masking the action bit must not eat bit 8.
        Assert.Equal("Swampy Lake", Mp2ObjectName.Of(Mp2ObjectType.SwampyLake));
        Assert.Equal("Barrel", Mp2ObjectName.Of(Mp2ObjectType.Barrel));
    }

    [Fact]
    public void ATypeTheTableDoesNotKnowKeepsItsNumber()
    {
        Assert.Equal("300", Mp2ObjectName.Of((Mp2ObjectType)300));
    }

    [Fact]
    public void CreatureNamesStartAtTheUnknownMonster()
    {
        Assert.Equal("Unknown Monster", Mp2MonsterName.Of(Mp2MonsterName.Unknown));
        Assert.Equal("Peasant", Mp2MonsterName.Of(1));
        Assert.Equal("Black Dragon", Mp2MonsterName.Of(38));
        Assert.Equal("Halfling", Mp2MonsterName.Of(39));
        Assert.Equal("Water Elemental", Mp2MonsterName.Of(66));
        Assert.Equal("Random Monster 4", Mp2MonsterName.Of(71));
    }

    [Fact]
    public void ACreatureIsNamedInTheNumberOfTheStack()
    {
        Assert.Equal("Peasant", Mp2MonsterName.Plural(1, 1));
        Assert.Equal("Peasants", Mp2MonsterName.Plural(1, 2));
        Assert.Equal("Peasants", Mp2MonsterName.Plural(1, 0));
        Assert.Equal("Magi", Mp2MonsterName.Plural(44, 3));
        Assert.Equal("Archmagi", Mp2MonsterName.Plural(45, 4));
    }

    [Fact]
    public void AnIdOutsideTheTableIsTheUnknownMonster()
    {
        Assert.Equal("Unknown Monster", Mp2MonsterName.Of(72));
        Assert.Equal("Unknown Monster", Mp2MonsterName.Of(-1));
        Assert.Equal("Unknown Monsters", Mp2MonsterName.Plural(999, 2));
    }
}
