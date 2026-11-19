namespace FHeroes2.Game.Tests;

/// <summary>
/// The rumor pool of <see cref="World"/>: its size, its nine built-in texts and the direction words.
/// </summary>
/// <remarks>
/// The selection itself is not written yet (<c>GetCurrentRumor()</c> throws), so nothing here calls it; these tests pin
/// down the content the class declares, which is what the renderer and the tavern will read.
/// </remarks>
public sealed class RumorPoolTests
{
    [Fact]
    public void TheBuiltInPoolHasNineRumors()
    {
        Assert.Equal(9, World.StandardRumorCount);
    }

    [Fact]
    public void AMapWithNoRumorsOfItsOwnHasTheBuiltInOnesOnly()
    {
        World world = new();

        Assert.Empty(world.Rumors);
        Assert.Equal(World.StandardRumorCount, world.TotalRumorCount);
    }

    [Fact]
    public void TheTwoArtifactRumorsAreTemplates()
    {
        Assert.Equal("The ultimate artifact is really the %{name}.", World.UltimateArtifactNameRumor);
        Assert.Equal("The ultimate artifact may be found in the %{name} regions of the world.",
            World.UltimateArtifactRegionRumor);
    }

    [Fact]
    public void TheFixedRumorsAreTheReferencesWords()
    {
        Assert.Equal("The truth is out there.", World.TruthRumor);
        Assert.Equal("The dark side is stronger.", World.DarkSideRumor);
        Assert.Equal("The end of the world is near.", World.EndOfWorldRumor);
        Assert.Equal("The bones of Lord Slayer are buried in the foundation of the arena.", World.LordSlayerRumor);
        Assert.Equal("A Black Dragon will take out a Titan any day of the week.", World.BlackDragonRumor);
        Assert.Equal("He told her: Yada yada yada... and then she said: Blah, blah, blah...", World.SmallTalkRumor);
        Assert.Equal("An unknown force is being resurrected...", World.UnknownForceRumor);
    }

    [Fact]
    public void ThereAreNineDirectionWords()
    {
        string[] directions =
        [
            World.RegionNorthWest,
            World.RegionNorth,
            World.RegionNorthEast,
            World.RegionWest,
            World.RegionCenter,
            World.RegionEast,
            World.RegionSouthWest,
            World.RegionSouth,
            World.RegionSouthEast,
        ];

        Assert.Equal(9, directions.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["north-west", "north", "north-east", "west", "center", "east", "south-west", "south", "south-east"],
            directions);
    }

    [Fact]
    public void ATextWithoutALanguagePrintsAsItself()
    {
        Assert.Equal("The truth is out there.", new LocalizedString(World.TruthRumor).ToString());
        Assert.Null(new LocalizedString(World.TruthRumor).Language);
    }

    [Fact]
    public void ACustomRumorKeepsTheLanguageOfItsMap()
    {
        LocalizedString rumor = new("Слух", SupportedLanguage.Russian);

        Assert.Equal("Слух", rumor.Text);
        Assert.Equal(SupportedLanguage.Russian, rumor.Language);
        Assert.Equal("Слух", rumor.ToString());
    }

    [Fact]
    public void TheIdStaysInsideThePoolAndIgnoresNothing()
    {
        Assert.Equal(0, World.ChooseRumorId(12345u, 1));

        for (uint seed = 0; seed < 200; ++seed)
        {
            int id = World.ChooseRumorId(seed, World.StandardRumorCount);

            Assert.InRange(id, 0, World.StandardRumorCount - 1);
        }
    }

    [Fact]
    public void TheSameSeedGivesTheSameIdEveryTime()
    {
        // The id is drawn from a throwaway generator seeded with the week, so the tavern may be visited twice a day
        // and still tell the same thing.
        int first = World.ChooseRumorId(0x9E3779B9u, 12);

        Assert.Equal(first, World.ChooseRumorId(0x9E3779B9u, 12));
        Assert.Equal(first, World.ChooseRumorId(0x9E3779B9u, 12));
    }

    [Fact]
    public void AnEmptyPoolIsRefused()
    {
        // Cannot happen from the world (the nine built-in rumors are always there) but the reference would hand -1 to
        // an unsigned generator.
        Assert.Throws<ArgumentOutOfRangeException>(() => World.ChooseRumorId(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => World.ChooseRumorId(0, -1));
    }

    [Theory]
    [InlineData(4, 4, World.RegionNorthWest)]
    [InlineData(15, 4, World.RegionNorth)]
    [InlineData(25, 4, World.RegionNorthEast)]
    [InlineData(4, 15, World.RegionWest)]
    [InlineData(15, 15, World.RegionCenter)]
    [InlineData(25, 15, World.RegionEast)]
    [InlineData(4, 25, World.RegionSouthWest)]
    [InlineData(15, 25, World.RegionSouth)]
    [InlineData(25, 25, World.RegionSouthEast)]
    public void TheMapIsCutIntoThirds(int x, int y, string region)
    {
        Assert.Equal(region, World.GetRegionName(x, y, 30, 30));
    }

    [Theory]
    [InlineData(9, 5, World.RegionNorthWest)]
    [InlineData(10, 5, World.RegionNorth)]
    [InlineData(19, 5, World.RegionNorth)]
    [InlineData(20, 5, World.RegionNorthEast)]
    [InlineData(5, 9, World.RegionNorthWest)]
    [InlineData(15, 10, World.RegionCenter)]
    [InlineData(15, 19, World.RegionCenter)]
    [InlineData(15, 20, World.RegionSouth)]
    public void ATileExactlyOnAThirdFallsBeyondIt(int x, int y, string region)
    {
        // The comparisons are strict in the reference ( height / 3 > y ), so tile 10 of 30 is already the middle band.
        Assert.Equal(region, World.GetRegionName(x, y, 30, 30));
    }

    [Fact]
    public void ASmallMapStillHasNineRegions()
    {
        Assert.Equal(World.RegionNorthWest, World.GetRegionName(0, 0, 3, 3));
        Assert.Equal(World.RegionCenter, World.GetRegionName(1, 1, 3, 3));
        Assert.Equal(World.RegionSouthEast, World.GetRegionName(2, 2, 3, 3));
    }

    [Fact]
    public void TheArtifactRumorsFillInTheName()
    {
        LocalizedString named = World.Describe(0, "Ultimate Sword of Dominion", string.Empty, []);

        Assert.Equal("The ultimate artifact is really the Ultimate Sword of Dominion.", named.Text);
        Assert.Null(named.Language);

        LocalizedString placed = World.Describe(1, "Ultimate Sword of Dominion", World.RegionNorthWest, []);

        Assert.Equal("The ultimate artifact may be found in the north-west regions of the world.", placed.Text);
        Assert.Null(placed.Language);
    }

    [Theory]
    [InlineData(2, World.TruthRumor)]
    [InlineData(3, World.DarkSideRumor)]
    [InlineData(4, World.EndOfWorldRumor)]
    [InlineData(5, World.LordSlayerRumor)]
    [InlineData(6, World.BlackDragonRumor)]
    [InlineData(7, World.SmallTalkRumor)]
    [InlineData(8, World.UnknownForceRumor)]
    public void TheSevenFixedRumorsAreReturnedAsTheyAre(int rumorId, string expected)
    {
        Assert.Equal(expected, World.Describe(rumorId, string.Empty, string.Empty, []).Text);
    }

    [Fact]
    public void ACustomRumorComesOutOfTheMapListAfterTheNine()
    {
        string[] customRumors = ["Слух", "And another one."];

        Assert.Equal("Слух", World.Describe(World.StandardRumorCount, "x", "y", customRumors).Text);
        Assert.Equal("And another one.", World.Describe(World.StandardRumorCount + 1, "x", "y", customRumors).Text);

        // The language tag is the map's and the port has none yet (ADR-170).
        Assert.Null(World.Describe(World.StandardRumorCount, "x", "y", customRumors).Language);
    }

    [Fact]
    public void AnIdOutsideThePoolIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => World.Describe(-1, "x", "y", []));
        Assert.Throws<ArgumentOutOfRangeException>(() => World.Describe(World.StandardRumorCount, "x", "y", []));
    }

    [Fact]
    public void TheRumorOfTheWeekIsTheSameWhenAskedTwice()
    {
        World world = new();
        world.GenerateUninitializedMap(20);

        LocalizedString first = world.GetCurrentRumor();
        LocalizedString second = world.GetCurrentRumor();

        Assert.False(string.IsNullOrWhiteSpace(first.Text));
        Assert.Equal(first, second);
        Assert.Null(first.Language);
    }
}
