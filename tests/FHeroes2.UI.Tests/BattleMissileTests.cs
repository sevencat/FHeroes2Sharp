using FHeroes2.Assets;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Core.Random;
using FHeroes2.Game.Battle;
using FHeroes2.Game.Kingdom;
using FHeroes2.Game.Monsters;
using FHeroes2.Game.System;
using FHeroes2.UI.Battle;

using GameArmy = FHeroes2.Game.Army.Army;

namespace FHeroes2.UI.Tests;

/// <summary>
/// The geometry of a shot, <see cref="BattleMissile"/>: the angle of <c>GetAngle()</c>
/// (<c>math_tools.cpp:20-33</c>), the three directions of <c>battle_interface.cpp:4130-4140</c> and the route
/// <c>RedrawMissileAnimation()</c> walks (<c>:1062-1136</c>).
/// </summary>
public sealed class BattleMissileTests
{
    /// <summary>A flat shot to the right is zero degrees.</summary>
    [Fact]
    public void AFlatShotIsZeroDegrees()
    {
        Assert.Equal(0.0, BattleMissile.GetAngle(new Point(100, 100), new Point(300, 100)));

        // 向左且同高时参考的归一化把它也折回 0：水平线的方向没有意义。
        Assert.Equal(0.0, BattleMissile.GetAngle(new Point(300, 100), new Point(100, 100)));
    }

    /// <summary>Up on the screen is a positive angle, down is negative (<c>math_tools.cpp:25-32</c>).</summary>
    [Fact]
    public void UpIsPositiveAndDownIsNegative()
    {
        Assert.Equal(45.0, BattleMissile.GetAngle(new Point(100, 100), new Point(200, 0)), 6);
        Assert.Equal(-45.0, BattleMissile.GetAngle(new Point(100, 100), new Point(200, 200)), 6);
    }

    /// <summary>The reference splits the three shooting sequences at 25 degrees (<c>:4134-4140</c>).</summary>
    [Fact]
    public void TheDirectionsSplitAtTwentyFiveDegrees()
    {
        Assert.Equal(MonsterAttackDirection.Top, BattleMissile.GetDirection(25.0));
        Assert.Equal(MonsterAttackDirection.Top, BattleMissile.GetDirection(80.0));
        Assert.Equal(MonsterAttackDirection.Front, BattleMissile.GetDirection(24.9));
        Assert.Equal(MonsterAttackDirection.Front, BattleMissile.GetDirection(0.0));
        Assert.Equal(MonsterAttackDirection.Bottom, BattleMissile.GetDirection(-25.0));
        Assert.Equal(MonsterAttackDirection.Bottom, BattleMissile.GetDirection(-80.0));
    }

    /// <summary>
    /// The route stops short of the target by half a projectile and drops the first sample, so the shot does not
    /// start inside the archer (<c>:1080-1096</c>).
    /// </summary>
    [Fact]
    public void AFlatRouteLandsHalfAProjectileShort()
    {
        BattleMissile missile = new(
            new Point(100, 100), new Point(300, 100), 0.0, MonsterType.Archer, width: 10, height: 20, spriteOffsetX: 0, spriteOffsetY: 0);

        Assert.False(missile.IsMage);
        Assert.False(missile.IsMirrored);
        Assert.NotEqual(new Point(100, 100), missile.Points[0]);
        Assert.Equal(new Point(300 - 5, 100 - 10), missile.Points[^1]);
        Assert.Equal(new Point(0, 0), missile.BlitOffset);
    }

    /// <summary>A target drawn lower is reached from above: the route ends half a sprite above it.</summary>
    [Fact]
    public void ARouteTowardsALowerTargetEndsAboveIt()
    {
        BattleMissile missile = new(
            new Point(100, 100), new Point(300, 200), -27.0, MonsterType.Archer, width: 10, height: 20, spriteOffsetX: 0, spriteOffsetY: 0);

        // 起点在目标上方（start.Y &lt; target.Y），参考减半格，让弹体落在目标中心之上。
        Assert.Equal(new Point(300 - 5, 200 - 10), missile.Points[^1]);

        // 角度为负时 y 偏移取 sprite 自己的那个方向，这里是 0。
        Assert.Equal(new Point(0, 0), missile.BlitOffset);
    }

    /// <summary>A shot to the left is mirrored and offset by the whole projectile width (<c>:1121-1131</c>).</summary>
    [Fact]
    public void AShotToTheLeftIsMirrored()
    {
        BattleMissile missile = new(
            new Point(300, 100), new Point(100, 100), 180.0, MonsterType.Archer, width: 10, height: 20, spriteOffsetX: 3, spriteOffsetY: -4);

        Assert.True(missile.IsMirrored);
        Assert.Equal(new Point(100 + 5, 100 - 10), missile.Points[^1]);

        // 向左时 x 往负方向让出整个弹体宽；角度 180 算「向上」，y 也整个让出去。
        Assert.Equal(new Point(-10 - 3, -20 - -4), missile.BlitOffset);
    }

    /// <summary>A shot going up keeps its sprite below the point it stands on (<c>:1134-1135</c>).</summary>
    [Fact]
    public void AShotGoingUpHangsItsSpriteBelow()
    {
        BattleMissile missile = new(
            new Point(100, 200), new Point(200, 100), 45.0, MonsterType.Archer, width: 10, height: 20, spriteOffsetX: 3, spriteOffsetY: -4);

        Assert.Equal(new Point(3, -20 - -4), missile.BlitOffset);
    }

    /// <summary>A mage's lightning is drawn as a line: it has no projectile, so the route is not shortened.</summary>
    [Fact]
    public void AMagesLightningReachesTheCentre()
    {
        BattleMissile missile = new(
            new Point(100, 100), new Point(300, 100), 0.0, MonsterType.Mage, width: 0, height: 0, spriteOffsetX: 0, spriteOffsetY: 0);

        Assert.True(missile.IsMage);

        // 法师不跳首点也不缩终点。
        Assert.Equal(new Point(100, 100), missile.Points[0]);
        Assert.Equal(new Point(300, 100), missile.Points[^1]);
    }

    /// <summary>A troll keeps the first sample: its rock is thrown from the hand, not from a flash (<c>:1083-1086</c>).</summary>
    [Fact]
    public void ATrollKeepsTheFirstSample()
    {
        BattleMissile missile = new(
            new Point(100, 100), new Point(300, 100), 0.0, MonsterType.Troll, width: 10, height: 20, spriteOffsetX: 0, spriteOffsetY: 0);

        Assert.Equal(new Point(100, 100), missile.Points[0]);
        Assert.Equal(new Point(300 - 5, 100 - 10), missile.Points[^1]);
    }

    /// <summary>
    /// The shot leaves the middle of the shooter's cell, and a defender on the archer's own row is shot flat
    /// (<c>battle_interface.cpp:4120-4146</c>).
    /// </summary>
    [Fact]
    public void TwoUnitsOnOneRowTradeAFlatShot()
    {
        BattleArena arena = Arena();
        BattleUnit archer = arena.AttackingForce.First();
        BattleUnit peasant = arena.DefendingForce.First();

        archer.SetPosition(30);
        peasant.SetPosition(32);

        using AssetBundle? bundle = OpenAssets();

        if (bundle is null)
        {
            return;
        }

        AnimationState animation = new(MonsterType.Archer, new MonsterAnimInfoCache(bundle));
        Sprite archerSprite = new(30, 40, offsetX: 2, offsetY: -25);
        Sprite peasantSprite = new(30, 40, offsetX: 1, offsetY: -20);

        Point target = BattleTroopSprite.GetCenterPoint(peasant, peasantSprite);

        BattleMissile.Aim aim = BattleMissile.AimShot(archer, target, archerSprite, animation, isSameRow: true);

        Assert.Equal(0.0, aim.Angle);
        Assert.Equal(MonsterAttackDirection.Front, aim.Direction);
        Assert.Equal(target, aim.Target);
    }

    /// <summary>A defender on the row below is shot downwards, whichever way the row arithmetic rounds.</summary>
    [Fact]
    public void ADefenderBelowIsShotDownwards()
    {
        BattleArena arena = Arena();
        BattleUnit archer = arena.AttackingForce.First();
        BattleUnit peasant = arena.DefendingForce.First();

        archer.SetPosition(30);
        peasant.SetPosition(41);

        using AssetBundle? bundle = OpenAssets();

        if (bundle is null)
        {
            return;
        }

        AnimationState animation = new(MonsterType.Archer, new MonsterAnimInfoCache(bundle));
        Sprite archerSprite = new(30, 40, offsetX: 2, offsetY: -25);
        Sprite peasantSprite = new(30, 40, offsetX: 1, offsetY: -20);

        Point target = BattleTroopSprite.GetCenterPoint(peasant, peasantSprite);

        BattleMissile.Aim aim = BattleMissile.AimShot(archer, target, archerSprite, animation, isSameRow: false);

        Assert.Equal(MonsterAttackDirection.Bottom, aim.Direction);
        Assert.True(aim.Angle < 0);
    }

    /// <summary>The real archives, so a fresh clone without the game data can still run the suite.</summary>
    /// <returns>The bundle, or <see langword="null"/> when the data is not installed.</returns>
    private static AssetBundle? OpenAssets() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    private static BattleArena Arena()
    {
        SetControl(PlayerColor.Blue, PlayerControl.Human);
        SetControl(PlayerColor.Red, PlayerControl.Ai);

        GameArmy blue = new()
        {
            Color = PlayerColor.Blue,
        };

        blue.GetTroop(0).Set(MonsterType.Archer, 10);

        GameArmy red = new()
        {
            Color = PlayerColor.Red,
        };

        red.GetTroop(0).Set(MonsterType.Peasant, 20);

        return new BattleArena(blue, red, new Pcg32(61));
    }

    private static void SetControl(PlayerColor color, PlayerControl control)
    {
        Player player = Players.Get(color) ?? new Player(color);
        player.Control = control;

        Players.Set(color, player);
    }
}
