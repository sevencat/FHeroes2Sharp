using FHeroes2.Assets;
using FHeroes2.Game.Gui;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the 1:1 port of <c>ui_tool.cpp</c>'s two <c>getLocalizedText()</c> overloads.
/// </summary>
[Collection(GlobalArchiveCollection.Name)]
public sealed class LocalizedTextTests
{
    [Fact]
    public void EmptyListHasNoText()
    {
        Assert.Null(LocalizedText.Create([], FontType.NormalWhite()));
        Assert.Null(LocalizedText.Create(Array.Empty<LocalizedTextRun>()));
    }

    [Fact]
    public void SingleRunIsAPlainText()
    {
        // The reference returns a Text, not a one-element MultiFontText; callers rely on the type.
        TextBase? text = LocalizedText.Create([new LocalizedString("Hello")], FontType.SmallYellow());

        Text single = Assert.IsType<Text>(text);
        Assert.Equal("Hello", single.Content);
        Assert.False(single.IsEmpty);
    }

    [Fact]
    public void SingleRunWithItsOwnFontKeepsThatFont()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            LocalizedTextRun run = new(new LocalizedString("AAA"), FontType.SmallWhite());

            Text single = Assert.IsType<Text>(LocalizedText.Create([run]));

            Assert.Equal(new Text("AAA", FontType.SmallWhite()).Width(), single.Width());
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void SeveralRunsBecomeOneMultiFontText()
    {
        TextBase? text = LocalizedText.Create(
        [
            new LocalizedString("You captured "),
            new LocalizedString("Stonehaven"),
            new LocalizedString("!\nYou are victorious."),
        ],
            FontType.NormalWhite());

        MultiFontText multi = Assert.IsType<MultiFontText>(text);
        Assert.Equal("You captured Stonehaven!\nYou are victorious.", multi.Content);
    }

    [Fact]
    public void EveryRunUsesTheFontItWasGiven()
    {
        using AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        Assets.Current = assets;
        try
        {
            LocalizedTextRun first = new(new LocalizedString("AAA"), FontType.SmallWhite());
            LocalizedTextRun second = new(new LocalizedString("BBB"), FontType.NormalWhite());

            MultiFontText multi = Assert.IsType<MultiFontText>(LocalizedText.Create([first, second]));

            // SMALL and NORMAL glyphs differ in size, so a run drawn with the wrong font changes the sum.
            Assert.Equal(new Text("AAA", FontType.SmallWhite()).Width() + new Text("BBB", FontType.NormalWhite()).Width(),
                multi.Width());

            // height() takes the tallest run: 15 for NORMAL, against 13 for SMALL.
            Assert.Equal(15, multi.Height());
        }
        finally
        {
            Assets.Current = null;
        }
    }

    [Fact]
    public void EmptyRunsAreDroppedLikeMultiFontTextDoes()
    {
        // MultiFontText::add ignores empty runs, so two empty strings leave an empty block.
        TextBase? text = LocalizedText.Create(
            [new LocalizedString(string.Empty), new LocalizedString(string.Empty)],
            FontType.NormalWhite());

        MultiFontText multi = Assert.IsType<MultiFontText>(text);
        Assert.True(multi.IsEmpty);
        Assert.Equal(string.Empty, multi.Content);
    }
}
