using FHeroes2.Core.Geometry;

namespace FHeroes2.Client.Desktop.Scenes;

/// <summary>Everything the M0 diagnostics overlay displays.</summary>
public readonly record struct DiagnosticsState(
    string? DataDirectory,
    int BaseArchiveEntries,
    int ExpansionArchiveEntries,
    string TextBackend,
    string FontBackend,
    Size OutputSize,
    float Scale,
    double FramesPerSecond,
    long FrameCount);
