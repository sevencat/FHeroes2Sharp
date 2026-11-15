using FHeroes2.Assets.Maps;
using Xunit;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers the two object classifications the week cycle and the battle bonuses use,
/// <c>MP2::isWeekLife</c> and <c>MP2::isBattleLife</c> (<c>src/fheroes2/maps/mp2.cpp:511-576</c>).
/// </summary>
public sealed class Mp2ObjectTypesTests
{
    [Theory]
    [InlineData(Mp2ObjectType.ArtesianSpring)]
    [InlineData(Mp2ObjectType.MagicGarden)]
    [InlineData(Mp2ObjectType.Stables)]
    [InlineData(Mp2ObjectType.WaterWheel)]
    [InlineData(Mp2ObjectType.Windmill)]
    [InlineData(Mp2ObjectType.AirAltar)]
    [InlineData(Mp2ObjectType.ArcherHouse)]
    [InlineData(Mp2ObjectType.BarrowMounds)]
    [InlineData(Mp2ObjectType.Cave)]
    [InlineData(Mp2ObjectType.CityOfDead)]
    [InlineData(Mp2ObjectType.DesertTent)]
    [InlineData(Mp2ObjectType.DragonCity)]
    [InlineData(Mp2ObjectType.DwarfCottage)]
    [InlineData(Mp2ObjectType.EarthAltar)]
    [InlineData(Mp2ObjectType.Excavation)]
    [InlineData(Mp2ObjectType.FireAltar)]
    [InlineData(Mp2ObjectType.GoblinHut)]
    [InlineData(Mp2ObjectType.HalflingHole)]
    [InlineData(Mp2ObjectType.PeasantHut)]
    [InlineData(Mp2ObjectType.Ruins)]
    [InlineData(Mp2ObjectType.TreeCity)]
    [InlineData(Mp2ObjectType.TreeHouse)]
    [InlineData(Mp2ObjectType.TrollBridge)]
    [InlineData(Mp2ObjectType.WagonCamp)]
    [InlineData(Mp2ObjectType.WatchTower)]
    [InlineData(Mp2ObjectType.WaterAltar)]
    public void WeeklyObjectsAreWeekLife(Mp2ObjectType type) => Assert.True(Mp2ObjectTypes.IsWeekLife(type));

    [Fact]
    public void AHeroIsWeekLifeForTheAI()
    {
        // The hero is in the reference list "for AI": it is how the planner knows when the movement
        // points come back (ai_planner_hero.cpp:823).
        Assert.True(Mp2ObjectTypes.IsWeekLife(Mp2ObjectType.Hero));
    }

    [Theory]
    [InlineData(Mp2ObjectType.FaerieRing)]
    [InlineData(Mp2ObjectType.Fountain)]
    [InlineData(Mp2ObjectType.Idol)]
    [InlineData(Mp2ObjectType.Pyramid)]
    [InlineData(Mp2ObjectType.Buoy)]
    [InlineData(Mp2ObjectType.DerelictShip)]
    [InlineData(Mp2ObjectType.Graveyard)]
    [InlineData(Mp2ObjectType.Mermaid)]
    [InlineData(Mp2ObjectType.Oasis)]
    [InlineData(Mp2ObjectType.Shipwreck)]
    [InlineData(Mp2ObjectType.Temple)]
    [InlineData(Mp2ObjectType.WateringHole)]
    [InlineData(Mp2ObjectType.BlackCat)]
    public void BattleBonusesAreBattleLife(Mp2ObjectType type) => Assert.True(Mp2ObjectTypes.IsBattleLife(type));

    [Theory]
    [InlineData(Mp2ObjectType.None)]
    [InlineData(Mp2ObjectType.Obelisk)]
    [InlineData(Mp2ObjectType.Mine)]
    [InlineData(Mp2ObjectType.Castle)]
    [InlineData(Mp2ObjectType.ActionWindmill)]
    public void EverythingElseIsNeither(Mp2ObjectType type)
    {
        Assert.False(Mp2ObjectTypes.IsWeekLife(type));
        Assert.False(Mp2ObjectTypes.IsBattleLife(type));
    }

    [Fact]
    public void ActionVariantsAreNotWeekLife()
    {
        // The reference compares the non-action value because it is called with getMainObjectType( false );
        // an Action* value falls through to the default branch and that is the behaviour to keep.
        Assert.False(Mp2ObjectTypes.IsWeekLife(Mp2ObjectType.ActionWindmill));
        Assert.False(Mp2ObjectTypes.IsWeekLife(Mp2ObjectType.ActionHero));
        Assert.False(Mp2ObjectTypes.IsBattleLife(Mp2ObjectType.ActionOasis));
    }

    [Fact]
    public void TheTwoListsDoNotOverlap()
    {
        foreach (Mp2ObjectType type in Enum.GetValues<Mp2ObjectType>())
        {
            Assert.False(
                Mp2ObjectTypes.IsWeekLife(type) && Mp2ObjectTypes.IsBattleLife(type),
                $"{type} cannot be both a weekly object and a battle bonus.");
        }
    }
}
