namespace FHeroes2.Game.Resources;

/// <summary>
/// The names of the seven resources, <c>Resource::String()</c> (<c>resource.cpp:335-357</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference returns a translated <c>const char *</c>; the port holds the English source text, which is what
/// <c>_()</c> falls back to when no catalogue is loaded. The marketplace dialog is the first consumer: its two
/// sentences name the resource given away and the one asked for.
/// </para>
/// <para>
/// <b>Not ported</b>: <c>Resource::getDescription()</c> (<c>resource.cpp:359-364</c>), the paragraph the resource
/// dialog shows when the icon itself is clicked; no caller in the port needs it yet.
/// </para>
/// </remarks>
public static class ResourceText
{
    /// <summary>What the reference answers for anything that is not one of the seven, <c>"Unknown"</c>.</summary>
    public const string Unknown = "Unknown";

    /// <summary>
    /// The name of a resource, <c>Resource::String()</c>.
    /// </summary>
    /// <param name="resource">One of the <see cref="Resource"/> flags; anything else answers
    /// <see cref="Unknown"/>.</param>
    /// <returns>The English name, e.g. <c>"Wood"</c> or <c>"Gold"</c>.</returns>
    /// <remarks>
    /// The reference switches on the flag value, and so does the port: <see cref="Resource.Wood"/> is 1,
    /// <see cref="Resource.Mercury"/> 2, <see cref="Resource.Ore"/> 4 and so on, so a switch on the values is the
    /// only correct form.
    /// </remarks>
    public static string Of(int resource) => resource switch
    {
        Resource.Wood => "Wood",
        Resource.Mercury => "Mercury",
        Resource.Ore => "Ore",
        Resource.Sulfur => "Sulfur",
        Resource.Crystal => "Crystal",
        Resource.Gems => "Gems",
        Resource.Gold => "Gold",
        _ => Unknown,
    };
}
