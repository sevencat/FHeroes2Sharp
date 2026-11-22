namespace FHeroes2.Core.Geometry;

/// <summary>
/// A resolution the display can be switched to: the logic size the game draws in and the size of the window it is
/// presented in. Plays the role of <c>fheroes2::ResolutionInfo</c> (<c>math_base.h</c>).
/// </summary>
/// <remarks>
/// The reference keeps both halves apart: <c>gameWidth</c>/<c>gameHeight</c> are the size of the frame buffer every
/// scene draws into, <c>screenWidth</c>/<c>screenHeight</c> the size of the window. The two argument constructor sets
/// both to the same value, the <c>ResolutionInfo( w, h )</c> of the reference.
/// </remarks>
/// <param name="GameWidth">Width of the frame buffer.</param>
/// <param name="GameHeight">Height of the frame buffer.</param>
/// <param name="ScreenWidth">Width of the window.</param>
/// <param name="ScreenHeight">Height of the window.</param>
public readonly record struct ResolutionInfo(int GameWidth, int GameHeight, int ScreenWidth, int ScreenHeight)
    : IComparable<ResolutionInfo>
{
    /// <summary>A resolution whose frame buffer and window are the same size, <c>ResolutionInfo( w, h )</c>.</summary>
    /// <param name="width">Width of both halves.</param>
    /// <param name="height">Height of both halves.</param>
    public ResolutionInfo(int width, int height)
        : this(width, height, width, height)
    {
    }

    /// <summary>Size of the frame buffer.</summary>
    public Size GameSize => new(GameWidth, GameHeight);

    /// <summary>Size of the window.</summary>
    public Size ScreenSize => new(ScreenWidth, ScreenHeight);

    /// <summary>
    /// Orders by window area first and frame buffer area second, the <c>operator&lt;</c> of the reference: a sorted
    /// set of the available resolutions walks from the largest window to the smallest.
    /// </summary>
    /// <param name="other">The resolution to compare with.</param>
    /// <returns>Negative when this resolution is smaller, positive when it is larger.</returns>
    public int CompareTo(ResolutionInfo other)
    {
        long screenArea = (long)ScreenWidth * ScreenHeight;
        long otherScreenArea = (long)other.ScreenWidth * other.ScreenHeight;

        if (screenArea != otherScreenArea)
        {
            return screenArea.CompareTo(otherScreenArea);
        }

        long gameArea = (long)GameWidth * GameHeight;
        long otherGameArea = (long)other.GameWidth * other.GameHeight;

        return gameArea.CompareTo(otherGameArea);
    }

    /// <summary>Whether the frame buffer and the window have the same size.</summary>
    public bool IsUniform => GameWidth == ScreenWidth && GameHeight == ScreenHeight;

    /// <summary>Whether the resolution can be presented at all.</summary>
    public bool IsValid => GameWidth > 0 && GameHeight > 0 && ScreenWidth > 0 && ScreenHeight > 0;

    /// <summary>Whether two resolutions present the same window size, the equality the dialog uses.</summary>
    /// <param name="other">The resolution to compare with.</param>
    public bool HasSameScreenSize(ResolutionInfo other) =>
        ScreenWidth == other.ScreenWidth && ScreenHeight == other.ScreenHeight;

    /// <summary>"1280 x 720".</summary>
    public override string ToString() => $"{GameWidth}x{GameHeight}";
}
