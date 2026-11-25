using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// The pending display fade-in flag, <c>Game::isFadeInNeeded()</c> / <c>Game::setDisplayFadeIn()</c> /
/// <c>Game::validateDisplayFadeIn()</c> (<c>game.cpp:165-182</c>).
/// </summary>
public sealed class GameDisplayFadeInTests
{
    [Fact]
    public void ValidateDisplayFadeIn_ConsumesTheRequestOnce()
    {
        // The flag is static, so make the test independent of the order: a leftover request is dropped first.
        _ = GameDisplayFadeIn.ValidateDisplayFadeIn();

        Assert.False(GameDisplayFadeIn.IsFadeInNeeded);

        GameDisplayFadeIn.SetDisplayFadeIn();

        Assert.True(GameDisplayFadeIn.IsFadeInNeeded);
        Assert.True(GameDisplayFadeIn.ValidateDisplayFadeIn());

        // The first validate took the request, the second one has nothing left to report.
        Assert.False(GameDisplayFadeIn.IsFadeInNeeded);
        Assert.False(GameDisplayFadeIn.ValidateDisplayFadeIn());
    }
}
