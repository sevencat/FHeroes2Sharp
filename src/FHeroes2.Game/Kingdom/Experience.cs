namespace FHeroes2.Game.Kingdom;

/// <summary>
/// The name and the description of a hero's experience, <c>getExperienceName()</c> and
/// <c>getExperienceDescription()</c> (<c>kingdom/experience.cpp:26-34</c>).
/// </summary>
/// <remarks>
/// The reference passes both strings through <c>_()</c>, the translation table. The port has no translation
/// layer yet, so the two members return the original English text — which is what the reference returns when
/// no translation is loaded, and what a UI asks for by name.
/// </remarks>
public static class Experience
{
    /// <summary>The title of the experience field, <c>getExperienceName()</c>.</summary>
    public const string Name = "Experience";

    /// <summary>
    /// What experience does, <c>getExperienceDescription()</c>.
    /// </summary>
    public const string Description =
        "Experience allows your heroes to gain levels, increasing their primary and secondary skills.";
}
