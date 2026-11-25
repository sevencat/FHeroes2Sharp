using FHeroes2.Assets.Maps;
using FHeroes2.Game.Battle;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="BattleOpponentAnimation"/>: the pose machine of <c>OpponentSprite::updateAnimationState()</c>.
/// </summary>
public sealed class BattleOpponentAnimationTests
{
    [Fact]
    public void Constructor_StartsOnTheStandingPose()
    {
        BattleOpponentAnimation animation = new(MapRace.Knight, isCaptain: false, clockMs: () => 0);

        Assert.Equal(BattleHeroAnimation.Static, animation.Type);
        Assert.Equal(1, animation.Frame);
        Assert.True(animation.IsFinishFrame);
    }

    [Fact]
    public void UpdateAnimationState_StandsStillUntilTheIdleTimerFires()
    {
        long now = 0;
        BattleOpponentAnimation animation = new(MapRace.Knight, isCaptain: false, clockMs: () => now);

        Assert.False(animation.UpdateAnimationState());

        now = 20_000;

        Assert.True(animation.UpdateAnimationState());
    }

    [Theory]
    [InlineData(1, BattleHeroAnimation.Idle2)]
    [InlineData(3, BattleHeroAnimation.Idle)]
    public void UpdateAnimationState_PicksOneOfTheIdlePoses(int dice, BattleHeroAnimation expected)
    {
        long now = 0;
        BattleOpponentAnimation animation = new(MapRace.Knight, isCaptain: false, clockMs: () => now, random: (_, _) => dice);

        // 第一次问只是让计时器把延迟定下来，还没到点。
        animation.UpdateAnimationState();
        now = 20_000;
        animation.UpdateAnimationState();

        Assert.Equal(expected, animation.Type);
    }

    [Fact]
    public void SetAnimation_RestartsFromTheFirstFrame()
    {
        BattleOpponentAnimation animation = new(MapRace.Knight, isCaptain: false, clockMs: () => 0);

        animation.SetAnimation(BattleHeroAnimation.Joy);

        Assert.Equal(BattleHeroAnimation.Joy, animation.Type);
        Assert.Equal(6, animation.Frame);
        Assert.False(animation.IsFinishFrame);
    }

    [Fact]
    public void UpdateAnimationState_StepsThroughThePose()
    {
        BattleOpponentAnimation animation = new(MapRace.Knight, isCaptain: false, clockMs: () => 0);

        animation.SetAnimation(BattleHeroAnimation.Joy);

        Assert.True(animation.UpdateAnimationState());
        Assert.Equal(7, animation.Frame);
    }

    [Fact]
    public void UpdateAnimationState_ReturnsToStandingWhenThePoseEnds()
    {
        BattleOpponentAnimation animation = new(MapRace.Knight, isCaptain: false, clockMs: () => 0);

        animation.SetAnimation(BattleHeroAnimation.Joy);

        // JOY 有 9 帧，从第 1 帧出发还要走 8 步。
        for (int i = 0; i < 8; i++)
        {
            Assert.True(animation.UpdateAnimationState());
        }

        Assert.Equal(6, animation.Frame);
        Assert.True(animation.IsFinishFrame);

        Assert.True(animation.UpdateAnimationState());
        Assert.Equal(BattleHeroAnimation.Static, animation.Type);
        Assert.Equal(1, animation.Frame);
    }

    [Fact]
    public void UpdateAnimationState_HoldsTheLastFrameOfACastUntilTheCallerReturns()
    {
        BattleOpponentAnimation animation = new(MapRace.Knight, isCaptain: false, clockMs: () => 0);

        animation.SetAnimation(BattleHeroAnimation.CastDown);
        Assert.Equal(2, animation.Frame);

        Assert.True(animation.UpdateAnimationState());
        Assert.Equal(14, animation.Frame);

        // 施法的「去」阶段播完就停在那里，等外部切到 CAST_DOWN_RETURN。
        Assert.False(animation.UpdateAnimationState());
        Assert.Equal(BattleHeroAnimation.CastDown, animation.Type);
        Assert.Equal(14, animation.Frame);
    }

    [Fact]
    public void UpdateAnimationState_SorrowEndsOnStanding()
    {
        BattleOpponentAnimation animation = new(MapRace.Wizard, isCaptain: false, clockMs: () => 0);

        animation.SetAnimation(BattleHeroAnimation.Sorrow);

        for (int i = 0; i < 9; i++)
        {
            animation.UpdateAnimationState();
        }

        Assert.Equal(BattleHeroAnimation.Static, animation.Type);
    }

    [Fact]
    public void UpdateAnimationState_ACaptainSorrowStaysStanding()
    {
        BattleOpponentAnimation animation = new(MapRace.Wizard, isCaptain: true, clockMs: () => 0);

        animation.SetAnimation(BattleHeroAnimation.Sorrow);

        Assert.True(animation.IsFinishFrame);
        Assert.Equal(1, animation.Frame);
    }
}
