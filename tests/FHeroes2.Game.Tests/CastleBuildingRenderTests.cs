using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Game.Castle;
using CastleTown = FHeroes2.Game.Castle.Castle;

namespace FHeroes2.Game.Tests;

/// <summary>
/// <see cref="FadeBuilding"/>, <see cref="BuildingsRenderQueue"/> and <see cref="CastleBuildingRender"/>: the parts of
/// <c>castle_building.cpp</c> that are not a blit.
/// </summary>
public sealed class CastleBuildingRenderTests
{
    [Fact]
    public void AFadeStartsInvisibleAndEndsOpaque()
    {
        FadeBuilding fade = new();

        Assert.True(fade.IsFadeDone);
        Assert.Equal(BuildingType.Nothing, fade.Building);

        fade.StartFadeBuilding(BuildingType.DwellingMonster1);

        Assert.Equal(BuildingType.DwellingMonster1, fade.Building);
        Assert.Equal(0, fade.Alpha);
        Assert.True(fade.IsFadeInProgress);
        Assert.False(fade.IsOnlyBoat);

        // A frame that is not due changes nothing.
        Assert.False(fade.Update(false));
        Assert.Equal(0, fade.Alpha);

        Assert.True(fade.Update(true));
        Assert.Equal(9, fade.Alpha);

        int steps = 1;
        while (fade.Update(true))
        {
            ++steps;
        }

        Assert.Equal(byte.MaxValue, fade.Alpha);
        Assert.True(fade.IsFadeDone);

        // 0 -> 243 in steps of 9, then the last two jumps land exactly on 255.
        Assert.Equal(29, steps);

        Assert.False(fade.Update(true));
    }

    [Fact]
    public void ABoatsFadeBelongsToTheShipyardButOnlyTheBoat()
    {
        FadeBuilding fade = new();

        fade.StartFadeBoat();

        Assert.Equal(BuildingType.Shipyard, fade.Building);
        Assert.True(fade.IsOnlyBoat);
        Assert.Equal(0, fade.Alpha);
    }

    [Fact]
    public void StoppingTheFadeLeavesNothingToDraw()
    {
        FadeBuilding fade = new();
        fade.StartFadeBuilding(BuildingType.Well);

        fade.StopFade();

        Assert.Equal(BuildingType.Nothing, fade.Building);
        Assert.Equal(byte.MaxValue, fade.Alpha);
        Assert.False(fade.IsOnlyBoat);
        Assert.False(fade.Update(true));
    }

    [Fact]
    public void TheQueueFollowsTheDrawingOrderAndMovesTheAreasToTheScreen()
    {
        CastleTown castle = new(0, MapRace.Knight);
        Point top = new(320, 40);

        BuildingsRenderQueue queue = new(castle, top);
        IReadOnlyList<BuildingType> order = CastleBuildingInfo.GetDrawingPriorities(castle.Race, castle.Version);

        Assert.Equal(order.Count, queue.Count);

        for (int i = 0; i < order.Count; ++i)
        {
            Assert.Equal(order[i], queue[i].Id);

            IReadOnlyList<Rect> source = CastleBuildingInfo.GetArea(castle.Race, order[i]);

            Assert.Equal(source.Count, queue[i].Areas.Count);

            for (int areaIndex = 0; areaIndex < source.Count; ++areaIndex)
            {
                Assert.Equal(source[areaIndex].X + top.X, queue[i].Areas[areaIndex].X);
                Assert.Equal(source[areaIndex].Y + top.Y, queue[i].Areas[areaIndex].Y);
                Assert.Equal(source[areaIndex].Width, queue[i].Areas[areaIndex].Width);
                Assert.Equal(source[areaIndex].Height, queue[i].Areas[areaIndex].Height);
            }
        }
    }

    [Fact]
    public void ACastleTentIsDrawnOnlyOnceTheBuildingIsThere()
    {
        CastleTown castle = new(0, MapRace.Knight);

        Assert.False(CastleBuildingRender.ShouldDrawBuilding(castle, BuildingType.Well, BuildingType.Nothing));

        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.Well);

        Assert.True(CastleBuildingRender.ShouldDrawBuilding(castle, BuildingType.Well, BuildingType.Nothing));
    }

    [Fact]
    public void AnUpgradeHidesItsBaseUnlessTheUpgradeIsStillFadingIn()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(
            BuildingType.Tent | BuildingType.Castle | BuildingType.DwellingMonster2 | BuildingType.DwellingUpgrade2);

        Assert.False(
            CastleBuildingRender.ShouldDrawBuilding(castle, BuildingType.DwellingMonster2, BuildingType.Nothing));
        Assert.True(
            CastleBuildingRender.ShouldDrawBuilding(
                castle,
                BuildingType.DwellingMonster2,
                BuildingType.DwellingUpgrade2));
        Assert.True(
            CastleBuildingRender.ShouldDrawBuilding(
                castle,
                BuildingType.DwellingUpgrade2,
                BuildingType.DwellingUpgrade2));
    }

    [Fact]
    public void AFadingInBuildingIsNotYetFullyBuilt()
    {
        CastleTown castle = new(0, MapRace.Knight);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.Well);

        Assert.True(CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Well, BuildingType.Nothing));
        Assert.False(CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Well, BuildingType.Well));
        Assert.False(CastleBuildingRender.IsBuildingFullyBuilt(castle, BuildingType.Tavern, BuildingType.Nothing));
    }

    [Fact]
    public void OnlyTheHighestMageGuildLevelOwnsTheBarbarianWall()
    {
        CastleTown castle = new(0, MapRace.Barbarian);
        castle.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.MageGuild1);

        Assert.True(CastleBuildingRender.IsBuildingConnectionNeeded(castle, BuildingType.MageGuild1));
        Assert.False(CastleBuildingRender.IsBuildingConnectionNeeded(castle, BuildingType.MageGuild2));

        castle.AddBuildings(BuildingType.MageGuild2);

        Assert.False(CastleBuildingRender.IsBuildingConnectionNeeded(castle, BuildingType.MageGuild1));
        Assert.True(CastleBuildingRender.IsBuildingConnectionNeeded(castle, BuildingType.MageGuild2));
    }

    [Fact]
    public void ABarbarianThievesGuildAndANecromancerCaptainHaveAPiece()
    {
        CastleTown barbarian = new(0, MapRace.Barbarian);
        barbarian.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.ThievesGuild);

        Assert.True(CastleBuildingRender.IsBuildingConnectionNeeded(barbarian, BuildingType.ThievesGuild));
        Assert.False(CastleBuildingRender.IsBuildingConnectionNeeded(barbarian, BuildingType.Well));

        CastleTown necromancer = new(0, MapRace.Necromancer);
        necromancer.AddBuildings(BuildingType.Tent | BuildingType.Castle | BuildingType.Captain);

        Assert.True(CastleBuildingRender.IsBuildingConnectionNeeded(necromancer, BuildingType.Captain));
        Assert.False(CastleBuildingRender.IsBuildingConnectionNeeded(necromancer, BuildingType.Castle));
    }
}
