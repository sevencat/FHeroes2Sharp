namespace FHeroes2.Core.Diagnostics;

/// <summary>Version information of the port itself and of the reference implementation.</summary>
public static class BuildInfo
{
    /// <summary>Version of this C# port.</summary>
    public const string Version = "0.1.0";

    /// <summary>Milestone that is currently implemented.</summary>
    public const string Milestone = "M2";

    /// <summary>Reference C++ implementation the port is derived from.</summary>
    public const string ReferenceImplementation = "fheroes2 1.1.18 (commit 6c51acfdc)";

    /// <summary>Short product name shown in window titles and diagnostics.</summary>
    public const string ProductName = "FHeroes2Sharp";

    public static string DisplayName => $"{ProductName} {Version} ({Milestone})";
}
