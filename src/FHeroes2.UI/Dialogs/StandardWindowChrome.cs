using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The three pieces of original artwork a standard window is made of.
/// </summary>
/// <remarks>
/// <para>
/// The frame of every dialog in Heroes II is assembled from two images, not from a nine slice grid:
/// <c>SURDRBKG.ICN</c> holds the horizontal borders (<see cref="Horizontal"/>, 476x215 with the top
/// border at the top and the bottom border at the bottom) and <c>WINLOSE.ICN</c> the vertical ones
/// together with all four corners (<see cref="Vertical"/>, 319x456). The window interior is tiled
/// from <see cref="Background"/>, a single 640x480 <c>STONEBAK.ICN</c> texture.
/// </para>
/// <para>
/// The evil interface uses a second set of archives (<c>SURDRBKE</c>, <c>WINLOSEE</c>,
/// <c>STONEBAK_EVIL</c>); it is not part of the port yet because there is no interface type setting.
/// </para>
/// </remarks>
public sealed class StandardWindowChrome
{
    /// <summary>Archive entry of the horizontal borders.</summary>
    public const string HorizontalAssetName = "SURDRBKG.ICN";

    /// <summary>Archive entry of the vertical borders and the corners.</summary>
    public const string VerticalAssetName = "WINLOSE.ICN";

    /// <summary>Archive entry of the tiled interior.</summary>
    public const string BackgroundAssetName = "STONEBAK.ICN";

    private StandardWindowChrome(Sprite horizontal, Sprite vertical, Sprite background)
    {
        Horizontal = horizontal;
        Vertical = vertical;
        Background = background;
    }

    /// <summary>Top and bottom border of the window, <c>SURDRBKG.ICN</c> frame 0.</summary>
    public Sprite Horizontal { get; }

    /// <summary>Left and right border plus the four corners, <c>WINLOSE.ICN</c> frame 0.</summary>
    public Sprite Vertical { get; }

    /// <summary>Texture the interior of the window is filled with, <c>STONEBAK.ICN</c> frame 0.</summary>
    public Sprite Background { get; }

    /// <summary>
    /// Takes the three pieces out of the decoded archives.
    /// </summary>
    /// <param name="horizontal">Frames of <c>SURDRBKG.ICN</c>.</param>
    /// <param name="vertical">Frames of <c>WINLOSE.ICN</c>.</param>
    /// <param name="background">Frames of <c>STONEBAK.ICN</c>.</param>
    /// <returns>The chrome, or <see langword="null"/> when one of the archives is missing.</returns>
    public static StandardWindowChrome? TryCreate(IReadOnlyList<Sprite>? horizontal, IReadOnlyList<Sprite>? vertical,
        IReadOnlyList<Sprite>? background)
    {
        if (horizontal is null || horizontal.Count == 0 ||
            vertical is null || vertical.Count == 0 ||
            background is null || background.Count == 0)
        {
            return null;
        }

        return new StandardWindowChrome(horizontal[0], vertical[0], background[0]);
    }
}
