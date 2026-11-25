using FHeroes2.Core.Geometry;
using FHeroes2.Game.Battle;
using FHeroes2.UI.Battle;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The sprite offset of a unit on its way, <see cref="BattleTroopDisplacement"/>
/// (<c>_drawTroopSprite()</c>, <c>battle_interface.cpp:2069-2110</c>).
/// </summary>
public sealed class BattleTroopDisplacementTests
{
    private static readonly Rect Area = new(100, 100, 44, 44);

    /// <summary>A unit at rest is drawn where it stands.</summary>
    [Fact]
    public void AUnitAtRestHasNoOffset()
    {
        Assert.Equal(
            Point.Zero,
            BattleTroopDisplacement.GetOffset(Area, isFlying: true, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: null, flyingPosition: null));
    }

    /// <summary>A horizontal step is carried by the animation frames themselves (<c>:2082-2088</c>).</summary>
    [Fact]
    public void AHorizontalStepNeedsNoOffset()
    {
        Assert.Equal(
            Point.Zero,
            BattleTroopDisplacement.GetOffset(Area, isFlying: false, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: new Point(144, 100), flyingPosition: null));
    }

    /// <summary>A diagonal step down-right drifts left by half the frame's own offset (<c>:2090-2096</c>).</summary>
    [Fact]
    public void ADiagonalStepDownRightDriftsLeft()
    {
        Point offset = BattleTroopDisplacement.GetOffset(
            Area, isFlying: false, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: new Point(144, 144), flyingPosition: null);

        Assert.Equal(new Point(-4, 22), offset);
    }

    /// <summary>Going up-left mirrors the drift (<c>sign( moveX )</c>).</summary>
    [Fact]
    public void ADiagonalStepUpLeftDriftsRight()
    {
        Point offset = BattleTroopDisplacement.GetOffset(
            Area, isFlying: false, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: new Point(56, 56), flyingPosition: null);

        Assert.Equal(new Point(4, -22), offset);
    }

    /// <summary>A flying unit glides both ways instead of stepping (<c>:2084-2088</c>).</summary>
    [Fact]
    public void AFlyingUnitGlidesDiagonally()
    {
        Point offset = BattleTroopDisplacement.GetOffset(
            Area, isFlying: true, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: new Point(156, 132), flyingPosition: null);

        Assert.Equal(new Point(28, 16), offset);
    }

    /// <summary>In the air the sprite sits between the two ends of the current stretch (<c>:2099-2110</c>).</summary>
    [Fact]
    public void AFlyerInTheAirSitsBetweenTheStretchEnds()
    {
        Point offset = BattleTroopDisplacement.GetOffset(
            Area,
            isFlying: true,
            animationLength: 6,
            movementProgress: 0.5,
            frameXOffset: 8,
            movingPosition: new Point(140, 160),
            flyingPosition: new Point(120, 120));

        Assert.Equal(new Point(30, 40), offset);
    }

    /// <summary>A sequence with no frames has no progress to apply (<c>:2072</c>).</summary>
    [Fact]
    public void ASequenceWithoutFramesHasNoOffset()
    {
        Assert.Equal(
            Point.Zero,
            BattleTroopDisplacement.GetOffset(Area, isFlying: false, animationLength: 0, movementProgress: 1, frameXOffset: 8, movingPosition: new Point(144, 144), flyingPosition: null));
    }

    /// <summary>A diagonal step drifts the sprite, and the moat mask has to follow it (<c>:2090-2096</c>).</summary>
    [Fact]
    public void ADiagonalStepReportsItsDrift()
    {
        BattleTroopDisplacement.Movement movement = BattleTroopDisplacement.GetMovement(
            Area, isFlying: false, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: new Point(144, 144));

        Assert.Equal(new Point(-4, 22), movement.Delta);
        Assert.Equal(CellDirection.BottomRight, movement.Direction);
    }

    /// <summary>A horizontal step drifts nothing but still reports where it goes.</summary>
    [Fact]
    public void AHorizontalStepReportsOnlyItsDirection()
    {
        BattleTroopDisplacement.Movement movement = BattleTroopDisplacement.GetMovement(
            Area, isFlying: false, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: new Point(144, 100));

        Assert.Equal(Point.Zero, movement.Delta);
        Assert.Equal(CellDirection.Right, movement.Direction);
    }

    /// <summary>A flyer glides, so it never drifts.</summary>
    [Fact]
    public void AFlyerNeverDrifts()
    {
        BattleTroopDisplacement.Movement movement = BattleTroopDisplacement.GetMovement(
            Area, isFlying: true, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: new Point(144, 144));

        Assert.Equal(Point.Zero, movement.Delta);
        Assert.Equal(CellDirection.BottomRight, movement.Direction);
    }

    /// <summary>A unit at rest has no direction at all.</summary>
    [Fact]
    public void AUnitAtRestHasNoDirection()
    {
        BattleTroopDisplacement.Movement movement = BattleTroopDisplacement.GetMovement(
            Area, isFlying: false, animationLength: 6, movementProgress: 0.5, frameXOffset: 8, movingPosition: null);

        Assert.Equal(Point.Zero, movement.Delta);
        Assert.Equal(CellDirection.Center, movement.Direction);
    }
}
