using FHeroes2.Game.AdventureMap;
using FHeroes2.Game.Heroes;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the movement penalties and the terrain image predicates of <c>Maps::Ground</c>
/// (<c>ground.h:72-76</c>, <c>ground.cpp:96-141</c>, <c>ground.cpp:169-242</c>).
/// </summary>
public sealed class MapGroundPenaltyTests
{
    [Fact]
    public void ThePenaltyTableMatchesTheCommentInTheReference()
    {
        // none  basc  advd  expr
        AssertPenalty(MapGround.Desert, 200, 175, 150, 100);
        AssertPenalty(MapGround.Swamp, 175, 150, 125, 100);
        AssertPenalty(MapGround.Snow, 150, 125, 100, 100);
        AssertPenalty(MapGround.Wasteland, 125, 100, 100, 100);
        AssertPenalty(MapGround.Beach, 125, 100, 100, 100);
        AssertPenalty(MapGround.Lava, 100, 100, 100, 100);
        AssertPenalty(MapGround.Dirt, 100, 100, 100, 100);
        AssertPenalty(MapGround.Grass, 100, 100, 100, 100);
        AssertPenalty(MapGround.Water, 100, 100, 100, 100);
    }

    [Fact]
    public void AnUnknownGroundCostsTheDefault()
    {
        // The reference's switch has a default that breaks out with defaultGroundPenalty intact; a ground
        // the port does not recognise behaves the same way rather than throwing.
        Assert.Equal(MapGrounds.DefaultGroundPenalty, MapGrounds.GetPenalty(MapGround.Unknown, SkillLevel.None));
    }

    [Fact]
    public void TheRoadIsTheCheapestStepAndNothingIsSlowerThanTwoHundred()
    {
        Assert.Equal(75u, MapGrounds.RoadPenalty);
        Assert.Equal(75u, MapGrounds.FastestMovePenalty);
        Assert.Equal(100u, MapGrounds.DefaultGroundPenalty);
        Assert.Equal(200u, MapGrounds.SlowestMovePenalty);

        // The slowest cell of the table is the desert without pathfinding, which equals the constant.
        Assert.Equal(MapGrounds.SlowestMovePenalty, MapGrounds.GetPenalty(MapGround.Desert, SkillLevel.None));
    }

    [Fact]
    public void WaterHasNoTransitionImagesAndNoEmbeddedObjects()
    {
        // The water block is the first one and is entirely plain.
        Assert.True(MapGrounds.IsTerrainTransitionImage(MapGrounds.WaterStartImageIndex));
        Assert.False(MapGrounds.ContainsEmbeddedObjects(MapGrounds.WaterStartImageIndex));
        Assert.False(MapGrounds.ContainsEmbeddedObjects((ushort)(MapGrounds.WaterStartImageIndex + 10)));
    }

    [Fact]
    public void TheFirstSixteenDirtImagesAreTransitions()
    {
        ushort start = MapGrounds.DirtStartImageIndex;

        Assert.True(MapGrounds.IsTerrainTransitionImage(start));
        Assert.True(MapGrounds.IsTerrainTransitionImage((ushort)(start + 15)));
        Assert.False(MapGrounds.IsTerrainTransitionImage((ushort)(start + 16)));
    }

    [Fact]
    public void TheFirstThirtyEightImagesOfTheCommonGroundsAreTransitions()
    {
        foreach (var (start, ground) in new[]
                 {
                     (MapGrounds.GrassStartImageIndex, MapGround.Grass),
                     (MapGrounds.SnowStartImageIndex, MapGround.Snow),
                     (MapGrounds.SwampStartImageIndex, MapGround.Swamp),
                     (MapGrounds.LavaStartImageIndex, MapGround.Lava),
                     (MapGrounds.DesertStartImageIndex, MapGround.Desert),
                     (MapGrounds.WastelandStartImageIndex, MapGround.Wasteland),
                 })
        {
            Assert.Equal(ground, MapGrounds.FromTerrainImageIndex(start));
            Assert.True(MapGrounds.IsTerrainTransitionImage((ushort)(start + 37)), $"{ground}");
            Assert.False(MapGrounds.IsTerrainTransitionImage((ushort)(start + 38)), $"{ground}");
        }
    }

    [Fact]
    public void BeachHasNoTransitionImages()
    {
        // The whole beach block is plain sand: the editor never has to compose a transition for it.
        Assert.False(MapGrounds.IsTerrainTransitionImage(MapGrounds.BeachStartImageIndex));
        Assert.False(MapGrounds.IsTerrainTransitionImage((ushort)(MapGrounds.BeachStartImageIndex + 20)));
    }

    [Fact]
    public void TheDecorationBoundariesArePerGround()
    {
        // 45 is the grass offset, 23 the dirt one and 7 the beach one; the first image past the boundary
        // carries a decoration.
        Assert.False(MapGrounds.ContainsEmbeddedObjects((ushort)(MapGrounds.GrassStartImageIndex + 45)));
        Assert.True(MapGrounds.ContainsEmbeddedObjects((ushort)(MapGrounds.GrassStartImageIndex + 46)));

        Assert.False(MapGrounds.ContainsEmbeddedObjects((ushort)(MapGrounds.DirtStartImageIndex + 23)));
        Assert.True(MapGrounds.ContainsEmbeddedObjects((ushort)(MapGrounds.DirtStartImageIndex + 24)));

        Assert.False(MapGrounds.ContainsEmbeddedObjects((ushort)(MapGrounds.BeachStartImageIndex + 7)));
        Assert.True(MapGrounds.ContainsEmbeddedObjects((ushort)(MapGrounds.BeachStartImageIndex + 8)));
    }

    private static void AssertPenalty(MapGround ground, uint none, uint basic, uint advanced, uint expert)
    {
        Assert.Equal(none, MapGrounds.GetPenalty(ground, SkillLevel.None));
        Assert.Equal(basic, MapGrounds.GetPenalty(ground, SkillLevel.Basic));
        Assert.Equal(advanced, MapGrounds.GetPenalty(ground, SkillLevel.Advanced));
        Assert.Equal(expert, MapGrounds.GetPenalty(ground, SkillLevel.Expert));
    }
}
