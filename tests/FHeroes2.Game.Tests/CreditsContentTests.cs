using FHeroes2.Game.Credits;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="CreditsContent"/>: how many pages one run shows and what the first and last of them say
/// (<c>game_credits.cpp:724-741</c>).
/// </summary>
public sealed class CreditsContentTests
{
    [Fact]
    public void WithoutTheExpansionThereAreFivePages()
    {
        IReadOnlyList<CreditsPage> pages = CreditsContent.Create(isPriceOfLoyaltySupported: false);

        // 三页引擎 + 两页原版《王位战争》。
        Assert.Equal(5, pages.Count);
    }

    [Fact]
    public void TheExpansionAddsItsThreePages()
    {
        IReadOnlyList<CreditsPage> pages = CreditsContent.Create(isPriceOfLoyaltySupported: true);

        Assert.Equal(8, pages.Count);
    }

    [Fact]
    public void TheFirstPageIsTheThreeColumnOneWithTheTeam()
    {
        CreditsPage first = CreditsContent.Create(isPriceOfLoyaltySupported: false)[0];

        Assert.False(first.IsCentered);
        Assert.Equal(3, first.ColumnCount);
        Assert.Equal(CreditsPage.ColumnWidth, first.TextWidth);
        Assert.Equal(CreditsPage.ColumnPageTop, first.TextTop);

        Assert.Equal("Ihar Hubchyk", first.Entries[0].Body);
        Assert.Equal("Project Coordination and Core Development", first.Entries[0].Title);
        Assert.Contains(first.Entries, entry => entry.Body.Length == 0 ? false : entry.Body.StartsWith("https://github.com/", StringComparison.Ordinal));
    }

    [Fact]
    public void TheThanksPageIsOneCentredWideColumn()
    {
        CreditsPage thanks = CreditsContent.Create(isPriceOfLoyaltySupported: false)[1];

        Assert.True(thanks.IsCentered);
        Assert.Equal(1, thanks.ColumnCount);
        Assert.Equal(CreditsPage.CenteredWidth, thanks.TextWidth);
        Assert.Equal(CreditsPage.CenteredPageTop, thanks.TextTop);

        Assert.Equal("Special Thanks to", thanks.Entries[0].Title);
        Assert.Contains("LeHerosInconnu", thanks.Entries[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPageCarriesText()
    {
        foreach (CreditsPage page in CreditsContent.Create(isPriceOfLoyaltySupported: true))
        {
            Assert.NotEmpty(page.Entries);
            Assert.Contains(page.Entries, entry => entry.Title.Length > 0 || entry.Body.Length > 0);
        }
    }
}
