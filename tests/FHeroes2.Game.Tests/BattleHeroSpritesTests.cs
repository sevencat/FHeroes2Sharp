using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="BattleHeroSprites"/>: the archive of each race, the frames of each pose and the
/// geometry of where a hero stands (<c>matchHeroType</c>, <c>getHeroAnimation</c>, <c>OpponentSprite</c>).
/// </summary>
public sealed class BattleHeroSpritesTests
{
    [Theory]
    [InlineData(MapRace.Knight, false, IcnType.Cmbthrok)]
    [InlineData(MapRace.Barbarian, false, IcnType.Cmbthrob)]
    [InlineData(MapRace.Sorceress, false, IcnType.Cmbthros)]
    [InlineData(MapRace.Warlock, false, IcnType.Cmbthrow)]
    [InlineData(MapRace.Wizard, false, IcnType.Cmbthroz)]
    [InlineData(MapRace.Necromancer, false, IcnType.Cmbthron)]
    [InlineData(MapRace.Knight, true, IcnType.Cmbtcapk)]
    [InlineData(MapRace.Barbarian, true, IcnType.Cmbtcapb)]
    [InlineData(MapRace.Sorceress, true, IcnType.Cmbtcaps)]
    [InlineData(MapRace.Warlock, true, IcnType.Cmbtcapw)]
    [InlineData(MapRace.Wizard, true, IcnType.Cmbtcapz)]
    [InlineData(MapRace.Necromancer, true, IcnType.Cmbtcapn)]
    public void GetIcn_ReturnsTheArchiveOfEachRaceAndCaptain(MapRace race, bool isCaptain, IcnType expected) =>
        Assert.Equal(expected, BattleHeroSprites.GetIcn(race, isCaptain));

    [Fact]
    public void GetIcn_ReturnsUnknownForARaceWithoutSprites() =>
        Assert.Equal(IcnType.Unknown, BattleHeroSprites.GetIcn(MapRace.None, isCaptain: false));

    [Fact]
    public void GetFrames_StaticIsTheSingleStandingFrame()
    {
        Assert.Equal([1], BattleHeroSprites.GetFrames(MapRace.Knight, isCaptain: false, BattleHeroAnimation.Static));
        Assert.Equal([1], BattleHeroSprites.GetFrames(MapRace.Knight, isCaptain: true, BattleHeroAnimation.Static));
    }

    [Fact]
    public void GetFrames_JoyOfAKnightStartsAndEndsOnTheSameFrame() =>
        Assert.Equal([6, 7, 8, 9, 8, 9, 8, 7, 6],
            BattleHeroSprites.GetFrames(MapRace.Knight, isCaptain: false, BattleHeroAnimation.Joy));

    [Fact]
    public void GetFrames_SorrowIsTheSameForEveryHeroButNotForACaptain()
    {
        Assert.Equal([2, 3, 4, 5, 4, 5, 4, 3, 2],
            BattleHeroSprites.GetFrames(MapRace.Wizard, isCaptain: false, BattleHeroAnimation.Sorrow));
        Assert.Equal([1], BattleHeroSprites.GetFrames(MapRace.Wizard, isCaptain: true, BattleHeroAnimation.Sorrow));
    }

    [Fact]
    public void GetFrames_ACaptainHasItsOwnPoses() =>
        Assert.Equal([5, 6], BattleHeroSprites.GetFrames(MapRace.Knight, isCaptain: true, BattleHeroAnimation.CastUp));

    [Theory]
    [InlineData(MapRace.Knight, false, 13, -7)]
    [InlineData(MapRace.Knight, true, 0, 3)]
    [InlineData(MapRace.Wizard, false, 1, -9)]
    [InlineData(MapRace.Wizard, true, 5, 8)]
    public void GetCastOffset_PointsWhereTheHandIs(MapRace race, bool isCaptain, int x, int y) =>
        Assert.Equal(new Point(x, y), BattleHeroSprites.GetCastOffset(race, isCaptain));

    [Fact]
    public void GetCastOffset_IsZeroForARaceWithoutSprites() =>
        Assert.Equal(Point.Zero, BattleHeroSprites.GetCastOffset(MapRace.None, isCaptain: false));

    [Fact]
    public void GetPosition_PlacesTheLeftHeroAtTheArchiveOffsets()
    {
        Sprite frame = new(55, 109, offsetX: -25, offsetY: -109);

        Assert.Equal(new Rect(5, 74, 55, 109),
            BattleHeroSprites.GetPosition(new Point(0, 0), 640, frame, isFlipped: false, isCaptain: false));
    }

    [Fact]
    public void GetPosition_MeasuresTheRightHeroFromTheRightEdge()
    {
        Sprite frame = new(55, 109, offsetX: -25, offsetY: -109);

        Assert.Equal(new Rect(581, 39, 55, 109),
            BattleHeroSprites.GetPosition(new Point(0, 0), 640, frame, isFlipped: true, isCaptain: false));
    }

    [Fact]
    public void GetPosition_ShiftsACaptainAwayFromTheEdge()
    {
        Sprite frame = new(55, 109);

        Assert.Equal(new Rect(24, 170, 55, 109),
            BattleHeroSprites.GetPosition(new Point(0, 0), 640, frame, isFlipped: false, isCaptain: true));
        Assert.Equal(new Rect(562, 135, 55, 109),
            BattleHeroSprites.GetPosition(new Point(0, 0), 640, frame, isFlipped: true, isCaptain: true));
    }

    [Fact]
    public void GetPosition_AddsTheInterfaceOffset()
    {
        Sprite frame = new(55, 109);

        Assert.Equal(new Rect(30, 183, 55, 109),
            BattleHeroSprites.GetPosition(new Point(0, 0), 640, frame, isFlipped: false, isCaptain: false));
        Assert.Equal(new Rect(40, 193, 55, 109),
            BattleHeroSprites.GetPosition(new Point(10, 10), 640, frame, isFlipped: false, isCaptain: false));
    }

    [Fact]
    public void GetCastPosition_MirrorsTheHandOffsetOnTheRight()
    {
        Rect area = new(5, 74, 55, 109);
        Point hand = new(13, -7);

        Assert.Equal(new Point(47, 121), BattleHeroSprites.GetCastPosition(area, isFlipped: false, hand));
        Assert.Equal(new Point(18, 121), BattleHeroSprites.GetCastPosition(area, isFlipped: true, hand));
    }
}
