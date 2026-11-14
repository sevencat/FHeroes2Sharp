using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.AdventureMap;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the mapping from a map tile to the tile sets of <c>HeroSpriteSet</c> and, when the game data
/// is available, checks that the archives really hold those sets and that the sprites hang over the
/// tile the way the renderer expects.
/// </summary>
public sealed class HeroSpriteSetTests
{
    [Theory]
    [InlineData(MapRace.Knight, "KNGT32.ICN")]
    [InlineData(MapRace.Barbarian, "BARB32.ICN")]
    [InlineData(MapRace.Sorceress, "SORC32.ICN")]
    [InlineData(MapRace.Warlock, "WRLK32.ICN")]
    [InlineData(MapRace.Wizard, "WZRD32.ICN")]
    [InlineData(MapRace.Necromancer, "NECR32.ICN")]
    public void BodyTileSet_IsTheOneOfTheRace(MapRace race, string expected)
    {
        Assert.Equal(expected, HeroSpriteSet.GetArchiveName(race));
    }

    [Theory]
    [InlineData(MapRace.None)]
    [InlineData(MapRace.Random)]
    public void BodyTileSet_IsUnknownWithoutAKnownRace(MapRace race)
    {
        // A random hero takes the race of its kingdom, which the map file does not store.
        Assert.Null(HeroSpriteSet.GetArchiveName(race));
    }

    [Theory]
    [InlineData(PlayerColor.Blue, "B-FLAG32.ICN")]
    [InlineData(PlayerColor.Green, "G-FLAG32.ICN")]
    [InlineData(PlayerColor.Red, "R-FLAG32.ICN")]
    [InlineData(PlayerColor.Yellow, "Y-FLAG32.ICN")]
    [InlineData(PlayerColor.Orange, "O-FLAG32.ICN")]
    [InlineData(PlayerColor.Purple, "P-FLAG32.ICN")]
    public void FlagTileSet_IsTheOneOfTheKingdom(PlayerColor color, string expected)
    {
        Assert.Equal(expected, HeroSpriteSet.GetArchiveName(color));
    }

    [Fact]
    public void FlagTileSet_IsMissingWithoutAKingdom()
    {
        Assert.Null(HeroSpriteSet.GetArchiveName(PlayerColor.None));
    }

    [Fact]
    public void StandingFrame_IsTheFirstFrameOfTheRightDirection()
    {
        // TOP, TOP_RIGHT, RIGHT: the third group of nine frames.
        Assert.Equal(9, HeroSpriteSet.FramesPerDirection);
        Assert.Equal(2 * HeroSpriteSet.FramesPerDirection, HeroSpriteSet.StandingFrame);
    }

    [Fact]
    public void WithoutArchives_ThereIsNoSpriteSet()
    {
        Assert.Null(HeroSpriteSet.TryCreate(null));
        Assert.Null(ObjectSpriteSet.TryCreate(null));
    }

    [Fact]
    public void RealData_HoldsAHeroAndAFlagForEveryRaceAndKingdom()
    {
        AssetBundle? assets = GameData.TryOpen();
        if (assets is null)
        {
            return;
        }

        using (assets)
        {
            HeroSpriteSet sprites = HeroSpriteSet.TryCreate(assets)!;

            foreach (MapRace race in Enum.GetValues<MapRace>())
            {
                if (HeroSpriteSet.GetArchiveName(race) is null)
                {
                    continue;
                }

                Sprite body = sprites.GetBody(race)!;
                Assert.Equal(32, body.Width);

                // The body stands on the tile and reaches about half a tile above it.
                Assert.True(body.Height > 32, $"{race}: the hero body is {body.Height} pixels tall");
                Assert.True(body.OffsetY < 0, $"{race}: the hero body starts {body.OffsetY} pixels above its tile");
            }

            foreach (PlayerColor color in Enum.GetValues<PlayerColor>())
            {
                if (HeroSpriteSet.GetArchiveName(color) is null)
                {
                    continue;
                }

                Sprite flag = sprites.GetFlag(color)!;
                Assert.True(flag.OffsetY < 0, $"{color}: the flag starts {flag.OffsetY} pixels above its tile");
            }

            Assert.NotNull(sprites.GetShadow());
            Assert.Empty(sprites.Missing);
        }
    }
}
