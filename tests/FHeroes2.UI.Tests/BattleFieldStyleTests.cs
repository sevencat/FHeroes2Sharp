using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Game.AdventureMap;
using FHeroes2.UI.Battle;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The battle background tables of <see cref="BattleFieldStyle"/> against
/// <c>battle_interface.cpp:1406-1451</c> (the ground switch) and <c>:2502-2525</c> (the castle ground).
/// </summary>
public sealed class BattleFieldStyleTests
{
    [Theory]
    [InlineData(MapGround.Desert, IcnType.Cbkgdsrt, IcnType.Frng0004)]
    [InlineData(MapGround.Swamp, IcnType.Cbkgswmp, IcnType.Frng0008)]
    [InlineData(MapGround.Wasteland, IcnType.Cbkgcrck, IcnType.Frng0003)]
    [InlineData(MapGround.Beach, IcnType.Cbkgbeac, IcnType.Frng0002)]
    [InlineData(MapGround.Lava, IcnType.Cbkglava, IcnType.Frng0005)]
    [InlineData(MapGround.Water, IcnType.Cbkgwatr, IcnType.Frng0013)]
    public void GroundsWithoutATreeVersionPickOneBackground(MapGround ground, IcnType expectedGround, IcnType expectedBorder)
    {
        BattleFieldStyle style = BattleFieldStyle.ForGround(ground, hasNearbyTrees: false);

        Assert.Equal(expectedGround, style.Ground);
        Assert.Equal(expectedBorder, style.Border);
        Assert.Equal(IcnType.Unknown, style.CastleBackground);
    }

    [Theory]
    [InlineData(MapGround.Grass, IcnType.Cbkggrmt, IcnType.Frng0012, IcnType.Cbkggrtr, IcnType.Frng0011)]
    [InlineData(MapGround.Dirt, IcnType.Cbkgdimt, IcnType.Frng0009, IcnType.Cbkgditr, IcnType.Frng0010)]
    [InlineData(MapGround.Snow, IcnType.Cbkgsnmt, IcnType.Frng0007, IcnType.Cbkgsntr, IcnType.Frng0006)]
    public void GrassDirtAndSnowChangeWithNearbyTrees(
        MapGround ground,
        IcnType plainGround,
        IcnType plainBorder,
        IcnType woodedGround,
        IcnType woodedBorder)
    {
        BattleFieldStyle plain = BattleFieldStyle.ForGround(ground, hasNearbyTrees: false);
        BattleFieldStyle wooded = BattleFieldStyle.ForGround(ground, hasNearbyTrees: true);

        Assert.Equal(plainGround, plain.Ground);
        Assert.Equal(plainBorder, plain.Border);
        Assert.Equal(woodedGround, wooded.Ground);
        Assert.Equal(woodedBorder, wooded.Border);
    }

    [Fact]
    public void AGroundTheReferenceDoesNotHandleDrawsNothing()
    {
        BattleFieldStyle style = BattleFieldStyle.ForGround(MapGround.Unknown, hasNearbyTrees: false);

        Assert.Equal(BattleFieldStyle.Empty, style);
    }

    [Fact]
    public void EveryGroundGetsAnAnswer()
    {
        foreach (MapGround ground in Enum.GetValues<MapGround>())
        {
            // No throw, and only the unknown ground is left without a background.
            BattleFieldStyle style = BattleFieldStyle.ForGround(ground, hasNearbyTrees: true);

            if (ground == MapGround.Unknown)
            {
                Assert.Equal(IcnType.Unknown, style.Ground);
            }
            else
            {
                Assert.NotEqual(IcnType.Unknown, style.Ground);
            }
        }
    }

    [Theory]
    [InlineData(MapRace.Barbarian, IcnType.Castbkgb)]
    [InlineData(MapRace.Knight, IcnType.Castbkgk)]
    [InlineData(MapRace.Necromancer, IcnType.Castbkgn)]
    [InlineData(MapRace.Sorceress, IcnType.Castbkgs)]
    [InlineData(MapRace.Warlock, IcnType.Castbkgw)]
    [InlineData(MapRace.Wizard, IcnType.Castbkgz)]
    public void TheCastleGroundFollowsTheRace(MapRace race, IcnType expected)
    {
        Assert.Equal(expected, BattleFieldStyle.CastleBackgroundFor(race));
    }

    /// <summary>
    /// The battle castle sprites are <c>CASTLE*</c> (<c>ICN::getCastleIcnId</c>), not the town screen's
    /// <c>CSTL*</c> (<c>ICN::getBuildingIcnId</c>): the two archives hold different frames.
    /// </summary>
    [Theory]
    [InlineData(MapRace.Barbarian, IcnType.Castleb)]
    [InlineData(MapRace.Knight, IcnType.Castlek)]
    [InlineData(MapRace.Necromancer, IcnType.Castlen)]
    [InlineData(MapRace.Sorceress, IcnType.Castles)]
    [InlineData(MapRace.Warlock, IcnType.Castlew)]
    [InlineData(MapRace.Wizard, IcnType.Castlez)]
    public void TheCastleSpritesAreTheBattleOnes(MapRace race, IcnType expected)
    {
        Assert.Equal(expected, BattleFieldStyle.CastleFor(race));
    }

    [Fact]
    public void ARaceWithoutACastleHasNoSprites()
    {
        Assert.Equal(IcnType.Unknown, BattleFieldStyle.CastleBackgroundFor(MapRace.None));
        Assert.Equal(IcnType.Unknown, BattleFieldStyle.CastleFor(MapRace.Multiple));
        Assert.Equal(BattleFieldStyle.Empty, BattleFieldStyle.ForCastle(MapGround.Unknown, false, MapRace.None));
    }

    [Fact]
    public void ACastleKeepsTheGroundOfTheTile()
    {
        BattleFieldStyle style = BattleFieldStyle.ForCastle(MapGround.Grass, hasNearbyTrees: true, MapRace.Knight);

        Assert.Equal(IcnType.Cbkggrtr, style.Ground);
        Assert.Equal(IcnType.Frng0011, style.Border);
        Assert.Equal(IcnType.Castbkgk, style.CastleBackground);
    }
}
