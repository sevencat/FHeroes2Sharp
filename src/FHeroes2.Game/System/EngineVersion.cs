namespace FHeroes2.Game.System;

/// <summary>
/// The version this port reports, the constants of <c>version.h</c>.
/// </summary>
/// <remarks>
/// <para>
/// The reference builds <c>ENGINE_VERSION</c> by stringifying three macros, so the value is decided at compile
/// time and never parsed back. The port keeps the same three numbers and the same printable form, because a
/// save file and a bug report both quote it.
/// </para>
/// <para>
/// <see cref="Build"/> is the reference's <c>BUILD_VERSION</c>: zero for a source build, and the number the
/// packaging scripts stamp onto a release. It is deliberately not part of <see cref="Version"/> — the reference
/// prints it separately, or not at all.
/// </para>
/// </remarks>
public static class EngineVersion
{
    /// <summary>The major version, <c>MAJOR_VERSION</c>.</summary>
    public const int Major = 1;

    /// <summary>The minor version, <c>MINOR_VERSION</c>.</summary>
    public const int Minor = 1;

    /// <summary>The intermediate version, <c>INTERMEDIATE_VERSION</c>.</summary>
    public const int Intermediate = 18;

    /// <summary>The build number, <c>BUILD_VERSION</c>; zero unless a release stamps it.</summary>
    public const int Build = 0;

    /// <summary>
    /// The engine version as it is printed, <c>ENGINE_VERSION</c> — <c>"1.1.18"</c> for this tree.
    /// </summary>
    public static string Version { get; } = $"{Major}.{Minor}.{Intermediate}";
}
