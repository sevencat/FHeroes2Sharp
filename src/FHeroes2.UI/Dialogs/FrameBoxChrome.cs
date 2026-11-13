using FHeroes2.Core.Imaging;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The six pieces of <c>BUYBUILD.ICN</c> a message frame is composed of: the right half is sprites
/// <c>0</c>, <c>1</c>, <c>2</c> and the left half sprites <c>4</c>, <c>5</c>, <c>6</c>, each triple
/// being the top, the middle and the bottom of a column.
/// </summary>
/// <remarks>
/// <para>
/// The original artwork is two decorative columns whose middle piece is a 35 pixel tall slice that
/// can be repeated (<c>Dialog::ResizableFrameBox</c>). Nothing here knows about a size: it only
/// answers how wide each half is, which is all <see cref="FrameBox"/> needs to place the pieces.
/// </para>
/// <para>
/// The middle slices of the left column are one pixel wider than its top and bottom (161 against
/// 160), so <see cref="LeftWidth"/> - like <c>leftWidth()</c> of the reference - is the maximum of
/// the three and not the width of a single sprite.
/// </para>
/// </remarks>
public sealed class FrameBoxChrome
{
    /// <summary>Archive entry that holds the frame pieces.</summary>
    public const string AssetName = "BUYBUILD.ICN";

    /// <summary>Frame of the right column's top piece; the middle and the bottom follow it.</summary>
    public const int RightFrame = 0;

    /// <summary>Frame of the left column's top piece; the middle and the bottom follow it.</summary>
    public const int LeftFrame = 4;

    /// <summary>Number of sprites the table must have: the left column ends at <c>6</c>.</summary>
    public const int RequiredSpriteCount = LeftFrame + 3;

    private FrameBoxChrome(Sprite rightTop, Sprite rightMiddle, Sprite rightBottom, Sprite leftTop,
        Sprite leftMiddle, Sprite leftBottom)
    {
        RightTop = rightTop;
        RightMiddle = rightMiddle;
        RightBottom = rightBottom;
        LeftTop = leftTop;
        LeftMiddle = leftMiddle;
        LeftBottom = leftBottom;
    }

    public Sprite RightTop { get; }

    public Sprite RightMiddle { get; }

    public Sprite RightBottom { get; }

    public Sprite LeftTop { get; }

    public Sprite LeftMiddle { get; }

    public Sprite LeftBottom { get; }

    /// <summary>Width of the left column, <c>leftWidth()</c> of the reference: the widest of its three pieces.</summary>
    public int LeftWidth => Math.Max(LeftTop.Width, Math.Max(LeftMiddle.Width, LeftBottom.Width));

    /// <summary>Width of the right column, <c>rightWidth()</c> of the reference.</summary>
    public int RightWidth => Math.Max(RightTop.Width, Math.Max(RightMiddle.Width, RightBottom.Width));

    /// <summary>Height of the top row of pieces, the <c>topHeight()</c> of the reference.</summary>
    public int TopHeight => Math.Max(LeftTop.Height, RightTop.Height);

    /// <summary>Height of the bottom row of pieces, the <c>bottomHeight()</c> of the reference.</summary>
    public int BottomHeight => Math.Max(LeftBottom.Height, RightBottom.Height);

    /// <summary>
    /// Takes the six pieces out of a decoded <c>BUYBUILD.ICN</c>.
    /// </summary>
    /// <param name="sprites">Sprites of the table, in file order.</param>
    /// <returns>
    /// The chrome, or <see langword="null"/> when the table is too short to hold the left column
    /// (the caller then draws the message without a frame, or not at all).
    /// </returns>
    public static FrameBoxChrome? TryCreate(IReadOnlyList<Sprite>? sprites)
    {
        if (sprites is null || sprites.Count < RequiredSpriteCount)
        {
            return null;
        }

        return new FrameBoxChrome(
            sprites[RightFrame],
            sprites[RightFrame + 1],
            sprites[RightFrame + 2],
            sprites[LeftFrame],
            sprites[LeftFrame + 1],
            sprites[LeftFrame + 2]);
    }
}
