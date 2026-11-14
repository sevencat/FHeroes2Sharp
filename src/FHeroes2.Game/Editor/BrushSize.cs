using FHeroes2.Core.Geometry;

namespace FHeroes2.Game.Editor;

/// <summary>
/// The four sizes of the ground brush, <c>EditorPanel::BrushSize</c> of the reference. The order is not
/// arbitrary: <c>EDITBTNS</c> lists the four buttons in it (frames 24 to 31, a released and a pressed
/// frame each), so a button index is a <see cref="BrushSize"/>.
/// </summary>
public enum BrushSize
{
    /// <summary>One tile, the one under the pointer.</summary>
    Small = 0,

    /// <summary>2x2 tiles, the pointer at the top left corner of the square.</summary>
    Medium = 1,

    /// <summary>4x4 tiles, centred on the tile under the pointer.</summary>
    Large = 2,

    /// <summary>
    /// The rectangle dragged from where the stroke started, which is why
    /// <see cref="BrushSizeExtensions.Area(BrushSize)"/> is empty for it: the size depends on the drag.
    /// </summary>
    Area = 3,
}

/// <summary>The geometry of the four brush sizes, <c>EditorPanel::getBrushArea()</c> of the reference.</summary>
public static class BrushSizeExtensions
{
    /// <summary>The four sizes, in the order the panel lists them.</summary>
    public static IReadOnlyList<BrushSize> All { get; } =
    [
        BrushSize.Small,
        BrushSize.Medium,
        BrushSize.Large,
        BrushSize.Area,
    ];

    /// <summary>
    /// The area a brush covers around the tile under the pointer, in tile offsets: the reference
    /// returns the same rectangles from <c>getBrushArea()</c> and shifts them by the pointed tile.
    /// </summary>
    /// <param name="size">The size to ask about.</param>
    /// <returns>The offsets, empty for <see cref="BrushSize.Area"/>.</returns>
    public static Rect Area(this BrushSize size) => size switch
    {
        BrushSize.Small => new Rect(0, 0, 1, 1),
        BrushSize.Medium => new Rect(0, 0, 2, 2),
        BrushSize.Large => new Rect(-1, -1, 4, 4),
        _ => Rect.Empty,
    };
}
