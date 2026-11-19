using FHeroes2.Game.Spells;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="SpellText"/>: the two text columns of the <c>spells</c> table (<c>spell.cpp:55-150</c>) and the two
/// <see cref="Spell"/> methods that read them.
/// </summary>
public sealed class SpellTextTests
{
    [Theory]
    [InlineData(SpellType.None, "Unknown")]
    [InlineData(SpellType.Fireball, "Fireball")]
    // The reference's word, not the enum's: the id is FireBlast.
    [InlineData(SpellType.FireBlast, "Fireblast")]
    [InlineData(SpellType.LightningBolt, "Lightning Bolt")]
    [InlineData(SpellType.ResurrectTrue, "Resurrect True")]
    [InlineData(SpellType.StoneSkin, "Stoneskin")]
    // The gettext context prefix is not part of the word.
    [InlineData(SpellType.Slow, "Slow")]
    [InlineData(SpellType.Blind, "Blind")]
    [InlineData(SpellType.AntiMagic, "Anti-Magic")]
    public void ANameIsTheReferencesWord(SpellType spell, string name)
    {
        Assert.Equal(name, SpellText.GetName(spell));
    }

    [Theory]
    [InlineData(SpellType.None, "Unknown spell.")]
    [InlineData(SpellType.Earthquake, "Damages castle walls.")]
    [InlineData(SpellType.ViewAll, "Causes the entire land to become visible.")]
    [InlineData(SpellType.Slow, "Slows target to half movement rate.")]
    public void ADescriptionIsTheReferencesSentence(SpellType spell, string description)
    {
        Assert.Equal(description, SpellText.GetDescription(spell));
    }

    [Fact]
    public void TheCountPlaceholderIsLeftForTheCaller()
    {
        // spell.cpp:60-100 marks the spells whose sentence carries a number; the reference substitutes %{count} when
        // it shows the text, and so does the port.
        Assert.Contains("%{count}", SpellText.GetDescription(SpellType.Cure));
        Assert.Contains("%{count}", SpellText.GetDescription(SpellType.Haste));
        Assert.Contains("%{count}", SpellText.GetDescription(SpellType.Blind));

        Assert.DoesNotContain("%{count}", SpellText.GetDescription(SpellType.Fireball));
        Assert.DoesNotContain("%{count}", SpellText.GetDescription(SpellType.Teleport));
    }

    [Fact]
    public void EverySpellIncludingThePlaceholdersHasWords()
    {
        // The six random placeholders and Petrify are not castable, but they are described too: the table is what a
        // corrupt save or a debug window reads.
        Assert.Equal("Random 1st Level Spell", SpellText.GetName(SpellType.Random1));
        Assert.Equal("Petrification", SpellText.GetName(SpellType.Petrify));

        for (int id = 0; id < (int)SpellType.Count; ++id)
        {
            SpellType spell = (SpellType)id;

            Assert.False(string.IsNullOrWhiteSpace(SpellText.GetName(spell)), spell.ToString());
            Assert.False(string.IsNullOrWhiteSpace(SpellText.GetDescription(spell)), spell.ToString());
        }
    }

    [Fact]
    public void NoTwoSpellsShareAName()
    {
        // A duplicated word would be a mistyped row that no other test would catch.
        string[] names = Enumerable.Range(0, (int)SpellType.Count).Select(id => SpellText.GetName((SpellType)id)).ToArray();

        Assert.Equal(names.Length, names.Distinct().Count());
    }

    [Fact]
    public void TheSpellWrapperAsPutToTheSameTable()
    {
        Assert.Equal("Teleport", new Spell(SpellType.Teleport).GetName());
        Assert.Equal("Teleports the creature you select to any open position on the battlefield.", new Spell(5).GetDescription());

        // An id the wrapper rejects becomes NONE, so the two lines of the table answer.
        Assert.Equal("Unknown", new Spell(0).GetName());
        Assert.Equal("Unknown spell.", new Spell(0).GetDescription());
        Assert.Equal("Unknown", new Spell((int)SpellType.Count).GetName());
        Assert.Equal("Unknown", new Spell(-3).GetName());
    }
}
