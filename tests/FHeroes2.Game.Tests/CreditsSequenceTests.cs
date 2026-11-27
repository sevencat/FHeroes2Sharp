using FHeroes2.Game.Credits;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers <see cref="CreditsSequence"/>, the fade and hold rhythm of <c>AnimationSequence</c>
/// (<c>game_credits.cpp:78-150</c>).
/// </summary>
public sealed class CreditsSequenceTests
{
    [Fact]
    public void AFreshSequenceIsBlackOnTheFirstPage()
    {
        CreditsSequence sequence = new(3);

        Assert.Equal(0, sequence.PageId);
        Assert.Equal(0, sequence.Alpha);
        Assert.Equal(CreditsAnimationState.FadingIn, sequence.State);
        Assert.True(sequence.IsFirstFadeIn);
    }

    [Fact]
    public void ThePageBrightensFiveAtATimeAndHoldsAtFull()
    {
        CreditsSequence sequence = new(3);

        sequence.Increment();
        Assert.Equal(5, sequence.Alpha);

        sequence.Increment();
        Assert.Equal(10, sequence.Alpha);

        // 5 步一档，第 51 步正好到 255；再走一步就越界被夹住，并转入停顿。
        for (int step = 0; step < 49; ++step)
        {
            sequence.Increment();
        }

        Assert.Equal(CreditsSequence.FullAlpha, sequence.Alpha);
        Assert.Equal(CreditsAnimationState.FadingIn, sequence.State);

        sequence.Increment();

        Assert.Equal(CreditsSequence.FullAlpha, sequence.Alpha);
        Assert.Equal(CreditsAnimationState.NoAction, sequence.State);
        Assert.False(sequence.IsFirstFadeIn);
    }

    [Fact]
    public void ItHoldsForAHundredAndOneStepsThenDarkens()
    {
        CreditsSequence sequence = new(3);
        ReachFullBrightness(sequence);

        // 参考的判据是「计数器 > 100」：满亮度一共保持 101 步，第 101 步那一格转入淡出、亮度还没减。
        for (int step = 0; step < CreditsSequence.NoActionLimit; ++step)
        {
            sequence.Increment();
            Assert.Equal(CreditsAnimationState.NoAction, sequence.State);
        }

        sequence.Increment();
        Assert.Equal(CreditsAnimationState.FadingOut, sequence.State);
        Assert.Equal(CreditsSequence.FullAlpha, sequence.Alpha);

        sequence.Increment();
        Assert.Equal(CreditsSequence.FullAlpha - CreditsSequence.AlphaStep, sequence.Alpha);
    }

    [Fact]
    public void TheNextPageStartsWhenTheFadeOutReachesBlack()
    {
        CreditsSequence sequence = new(3);
        ReachFullBrightness(sequence);
        ReachFadeOut(sequence);

        while (sequence.Alpha > 0)
        {
            int page = sequence.PageId;

            sequence.Increment();

            Assert.Equal(page, sequence.PageId);
        }

        Assert.Equal(0, sequence.Alpha);
        Assert.Equal(CreditsAnimationState.FadingOut, sequence.State);

        // 归零那一格才换页：Alpha 减到负数时把页号推一格并回到淡入。
        sequence.Increment();

        Assert.Equal(1, sequence.PageId);
        Assert.Equal(0, sequence.Alpha);
        Assert.Equal(CreditsAnimationState.FadingIn, sequence.State);
    }

    [Fact]
    public void TheLastPageWrapsBackToTheFirst()
    {
        CreditsSequence sequence = new(2);
        ReachFullBrightness(sequence);

        AdvanceToNextPage(sequence);
        Assert.Equal(1, sequence.PageId);

        AdvanceToNextPage(sequence);
        Assert.Equal(0, sequence.PageId);
    }

    [Fact]
    public void TheHeaderFlagStaysOffAfterTheFirstPage()
    {
        CreditsSequence sequence = new(3);
        ReachFullBrightness(sequence);
        Assert.False(sequence.IsFirstFadeIn);

        AdvanceToNextPage(sequence);
        Assert.False(sequence.IsFirstFadeIn);
    }

    [Fact]
    public void APageCountBelowOneIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CreditsSequence(0));
    }

    /// <summary>Steps until the page is fully bright and holding.</summary>
    private static void ReachFullBrightness(CreditsSequence sequence)
    {
        while (sequence.State == CreditsAnimationState.FadingIn)
        {
            sequence.Increment();
        }
    }

    /// <summary>Steps until the hold is over and the page is fading out.</summary>
    private static void ReachFadeOut(CreditsSequence sequence)
    {
        while (sequence.State == CreditsAnimationState.NoAction)
        {
            sequence.Increment();
        }
    }

    /// <summary>Runs one whole page: fade in, hold, fade out, and the step that turns the page.</summary>
    private static void AdvanceToNextPage(CreditsSequence sequence)
    {
        ReachFullBrightness(sequence);
        ReachFadeOut(sequence);

        while (sequence.State == CreditsAnimationState.FadingOut)
        {
            sequence.Increment();
        }
    }
}
