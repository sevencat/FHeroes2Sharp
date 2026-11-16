namespace FHeroes2.Game.System;

/// <summary>
/// How an AI player plays, <c>enum class AI::Personality</c> (<c>ai_personality.h:29-35</c>).
/// </summary>
/// <remarks>
/// The values are the reference's and are part of a save file: a player stores its personality as this integer.
/// <see cref="None"/> is zero because a human player has no personality at all, and the three that follow are
/// drawn in order by <c>Rand::Get</c> in <see cref="AiPersonality.Random"/>.
/// </remarks>
public enum AiPersonality
{
    /// <summary>No personality — a human player, or a player not initialised yet.</summary>
    None = 0,

    /// <summary>Fights first and builds second.</summary>
    Warrior = 1,

    /// <summary>Builds first and fights second.</summary>
    Builder = 2,

    /// <summary>Looks for resources and treasure.</summary>
    Explorer = 3,
}

/// <summary>
/// The helpers of <c>ai/ai_personality.cpp</c>, kept next to <see cref="Player"/> because a player owns one.
/// </summary>
public static class AiPersonalityHelper
{
    /// <summary>
    /// A random personality for a computer player, <c>AI::getRandomPersonality()</c>
    /// (<c>ai_personality.cpp:26-29</c>).
    /// </summary>
    /// <remarks>
    /// The reference rolls <c>Rand::Get( WARRIOR, EXPLORER )</c>, that is the half-open-by-construction range of
    /// the three personalities — <see cref="AiPersonality.None"/> is never drawn.
    /// </remarks>
    /// <returns>One of the three computer personalities.</returns>
    public static AiPersonality Random() =>
        (AiPersonality)Core.Random.Rand.Get((int)AiPersonality.Warrior, (int)AiPersonality.Explorer);

    /// <summary>
    /// The printable name of a personality, <c>AI::getPersonalityString()</c>
    /// (<c>ai_personality.cpp:31-47</c>).
    /// </summary>
    /// <remarks>
    /// The reference wraps each word in <c>_()</c>, the translation lookup; the port has no translation layer, so
    /// the English text is returned — which is what the reference shows with no translation loaded (ADR-137).
    /// </remarks>
    /// <param name="personality">The personality.</param>
    /// <returns>The name, <c>"None"</c> for anything unrecognised.</returns>
    public static string GetString(AiPersonality personality) => personality switch
    {
        AiPersonality.Warrior => "Warrior",
        AiPersonality.Builder => "Builder",
        AiPersonality.Explorer => "Explorer",
        _ => "None"
    };
}
