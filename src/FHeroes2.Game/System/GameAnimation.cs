namespace FHeroes2.Game.System;

/// <summary>
/// The frame counter of the map animations, <c>maps_animation_frame</c> and the two accessors of
/// <c>game.cpp:184-192</c>.
/// </summary>
/// <remarks>
/// The reference keeps the counter in an unnamed namespace of <c>game.cpp</c> and exposes it through
/// <c>Game::getAdventureMapAnimationIndex()</c>/<c>Game::updateAdventureMapAnimationIndex()</c>. The port has no
/// free functions, so the counter and its accessors live in a static class of their own; the value wraps around
/// like the <c>uint32_t</c> it is.
/// </remarks>
public static class GameAnimation
{
    /// <summary>The current map animation frame, <c>getAdventureMapAnimationIndex()</c>.</summary>
    public static uint AdventureMapAnimationIndex { get; private set; }

    /// <summary>Advances the counter by one, <c>updateAdventureMapAnimationIndex()</c>.</summary>
    public static void UpdateAdventureMapAnimationIndex() => ++AdventureMapAnimationIndex;
}
