using FHeroes2.Game.Artifacts;
using FHeroes2.Game.Resources;
using FHeroes2.Game.Spells;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Pins the spell container (<c>spell_storage.cpp:30-108</c>) and the artifact path into it
/// (<c>artifact.cpp:417-430</c>).
/// </summary>
public class SpellStorageTests
{
    [Fact]
    public void AnEmptyStorageHasNothing()
    {
        SpellStorage storage = new();

        Assert.Equal(0, storage.Count);
        Assert.False(storage.IsPresent(new Spell(SpellType.Fireball)));
        Assert.Empty(storage);
    }

    [Fact]
    public void AppendingTheSameSpellTwiceKeepsOne()
    {
        // The storage is a set with a history: the order is what the spell book screen draws, but a spell is
        // written down once.
        SpellStorage storage = new();

        storage.Append(new Spell(SpellType.Fireball));
        storage.Append(new Spell(SpellType.Fireball));
        storage.Append(new Spell(SpellType.Haste));

        Assert.Equal(2, storage.Count);
        Assert.Equal(SpellType.Fireball, storage[0].Type);
        Assert.Equal(SpellType.Haste, storage[1].Type);
        Assert.True(storage.IsPresent(new Spell(SpellType.Fireball)));
    }

    [Fact]
    public void AppendingNoSpellDoesNothing()
    {
        SpellStorage storage = new();

        storage.Append(new Spell(SpellType.None));
        storage.Append(new Spell(999));

        Assert.Equal(0, storage.Count);
    }

    [Fact]
    public void MergingTwoStoragesKeepsTheOrderAndTheUniqueness()
    {
        SpellStorage first = new();
        first.Append(new Spell(SpellType.Fireball));
        first.Append(new Spell(SpellType.Haste));

        SpellStorage second = new();
        second.Append(new Spell(SpellType.Haste));
        second.Append(new Spell(SpellType.Bless));

        first.Append(second);

        Assert.Equal(3, first.Count);
        Assert.Equal(SpellType.Fireball, first[0].Type);
        Assert.Equal(SpellType.Haste, first[1].Type);
        Assert.Equal(SpellType.Bless, first[2].Type);
    }

    [Fact]
    public void RemovingASpellReportsWhetherItWasThere()
    {
        SpellStorage storage = new();
        storage.Append(new Spell(SpellType.Fireball));

        Assert.True(storage.Remove(new Spell(SpellType.Fireball)));
        Assert.False(storage.Remove(new Spell(SpellType.Fireball)));
        Assert.False(storage.Remove(new Spell(SpellType.None)));
        Assert.Equal(0, storage.Count);
    }

    [Fact]
    public void TheSpellsOfOneLevelAreFiltered()
    {
        SpellStorage storage = new();
        storage.Append(new Spell(SpellType.Fireball));   // level 3
        storage.Append(new Spell(SpellType.Haste));      // level 1
        storage.Append(new Spell(SpellType.Blind));      // level 2
        storage.Append(new Spell(SpellType.TownPortal)); // level 5

        Assert.Equal(1, storage.GetSpells(1).Count);
        Assert.Equal(1, storage.GetSpells(2).Count);
        Assert.Equal(1, storage.GetSpells(3).Count);
        Assert.Equal(0, storage.GetSpells(4).Count);
        Assert.Equal(1, storage.GetSpells(5).Count);

        // A negative level is "all of them", and it is a copy: changing the copy leaves the original alone.
        SpellStorage all = storage.GetSpells(-1);
        Assert.Equal(4, all.Count);
        all.Remove(new Spell(SpellType.Haste));
        Assert.Equal(4, storage.Count);
    }

    [Fact]
    public void TheScrollsOfTheBagAreTheArtifactsWithAnAddSpellBonus()
    {
        // The battle garb grants Town Portal through ADD_SPELL, so putting it on is enough.
        ArtifactBag bag = new();
        bag.Push(ArtifactType.BattleGarb);

        SpellStorage storage = new();
        storage.Append(bag);

        Assert.Equal(1, storage.Count);
        Assert.Equal(SpellType.TownPortal, storage[0].Type);
    }

    [Fact]
    public void AnArtifactWithoutASpellAddsNothing()
    {
        // The spell book has no ADD_SPELL bonus, and neither has a sword.
        ArtifactBag bag = new();
        bag.Push(ArtifactType.MagicBook);
        bag.Push(ArtifactType.UltimateSword);

        SpellStorage storage = new();
        storage.Append(bag);

        Assert.Equal(0, storage.Count);
    }

    [Fact]
    public void ASpellScrollCarriesNoSpellInThePort()
    {
        // A scroll keeps the spell in the per-instance `ext` byte of the artifact (artifact.cpp:417-430),
        // which the flat artifact bag of the port does not have. The static row of SPELL_SCROLL therefore
        // says NONE, and a scroll adds nothing — a documented gap, not an accident.
        ArtifactBag bag = new();
        bag.Push(ArtifactType.SpellScroll);

        Assert.False(bag.ContainSpell(SpellType.TownPortal));

        SpellStorage storage = new();
        storage.Append(bag);

        Assert.Equal(0, storage.Count);
    }
}
