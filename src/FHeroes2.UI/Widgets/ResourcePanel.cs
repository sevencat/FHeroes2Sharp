using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// The seven denominations of the kingdom in the lower right corner of the castle screen,
/// <c>fheroes2::drawResourcePanel</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_castle.cpp:242-301</c>。四行两列的格子：左列木头/水晶/矿石，右列硫磺/水银/宝石，
/// 金子在第四行居中；每格图标底对齐，数字在小号白字基线下方一行。最后把 <c>ICN::BUTTON_EXIT_TOWN</c> 的第一帧
/// 盖在面板左下角，所以面板高度 192 里其实包含 EXIT 按钮上沿的一部分。
/// </para>
/// <para>
/// 面板整体先填黑：参考用 <c>fheroes2::Fill</c>，因为面板底下是城堡背景的建筑层。返回的矩形给调用方算
/// 「点资源面板」的命中区（<c>castle_dialog.cpp:383</c> 用它的高度减去 EXIT 上沿再减 3）。
/// </para>
/// </remarks>
public static class ResourcePanel
{
    /// <summary>Width of the panel, <c>ui_castle.cpp:244</c>.</summary>
    public const int PanelWidth = 82;

    /// <summary>Height of the panel, <c>ui_castle.cpp:244</c>.</summary>
    public const int PanelHeight = 192;

    /// <summary>Width of one icon cell, <c>maxWidth</c> at <c>ui_castle.cpp:248</c>.</summary>
    public const int IconColumnWidth = 39;

    /// <summary>Height of one icon cell, <c>maxHeight</c> at <c>ui_castle.cpp:249</c>.</summary>
    public const int IconMaxHeight = 32;

    /// <summary>Gap between the two columns, <c>ui_castle.cpp:251</c>.</summary>
    public const int ColumnGap = 2;

    /// <summary>Where the EXIT frame sits inside the panel, <c>ui_castle.cpp:298</c>.</summary>
    public const int ExitFrameOffsetY = 166;

    private const int OffsetX = 552;
    private const int OffsetY = 262;

    /// <summary>How many icons the panel draws, <c>ICN::RESOURCE</c> frames 0 to 6.</summary>
    public const int IconCount = 7;

    /// <summary>
    /// The panel's rectangle on screen, <c>Rect( offset.x + 552, offset.y + 262, 82, 192 )</c>.
    /// </summary>
    /// <param name="offset">Origin of the castle screen.</param>
    /// <returns>The rectangle the panel covers.</returns>
    public static Rect GetArea(Point offset) => new(offset.X + OffsetX, offset.Y + OffsetY, PanelWidth, PanelHeight);

    /// <summary>
    /// The vertical offset of each of the four rows, <c>ui_castle.cpp:256</c>.
    /// </summary>
    /// <remarks>
    /// The steps are not uniform: the second row is one pixel lower than twice the row height, the third is one
    /// pixel higher and the fourth two pixels lower, so the seven numbers sit tight against their icons. Keeping the
    /// arithmetic in one place is what stops the panel from drifting when the font changes.
    /// </remarks>
    /// <param name="font">The small white font the numbers use.</param>
    /// <returns>Four offsets, in order.</returns>
    public static IReadOnlyList<int> GetRowOffsets(IGlyphSource font)
    {
        ArgumentNullException.ThrowIfNull(font);

        int rowHeight = IconMaxHeight + font.LineHeight;

        return new[]
        {
            0,
            (IconMaxHeight + font.LineHeight) + 2,
            (rowHeight * 2) - 1,
            (rowHeight * 3) + 1,
        };
    }

    /// <summary>
    /// Where one icon goes, <c>ui_castle.cpp:266-275</c>.
    /// </summary>
    /// <param name="area">The panel rectangle, as returned by <see cref="GetArea"/>.</param>
    /// <param name="row">Row index, zero to three.</param>
    /// <param name="rightColumn"><see langword="true"/> for the right column.</param>
    /// <param name="icon">The icon itself; its height decides how far up it sits.</param>
    /// <param name="font">The font of the numbers.</param>
    /// <returns>The top-left corner of the icon.</returns>
    public static Point GetIconPosition(Rect area, int row, bool rightColumn, Sprite icon, IGlyphSource font)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(font);

        IReadOnlyList<int> offsets = GetRowOffsets(font);

        // Gold is the only icon that is neither column-aligned nor bottom-aligned: the reference blits it centred
        // over the whole panel at the row's top (<c>ui_castle.cpp:275</c>).
        if (row == 3)
        {
            return new Point(area.X + ((PanelWidth - icon.Width) / 2), area.Y + offsets[3]);
        }

        int column = area.X + 1 + (rightColumn ? IconColumnWidth + ColumnGap : 0);

        return new Point(column + ((IconColumnWidth - icon.Width) / 2), area.Y + offsets[row] + IconMaxHeight - icon.Height);
    }

    /// <summary>
    /// Where the number under one icon goes, centred in its cell, <c>ui_castle.cpp:277-296</c>.
    /// </summary>
    /// <param name="area">The panel rectangle.</param>
    /// <param name="row">Row index, zero to three.</param>
    /// <param name="rightColumn"><see langword="true"/> for the right column.</param>
    /// <param name="text">The number as text.</param>
    /// <param name="font">The font of the numbers.</param>
    /// <returns>The top-left corner of the text.</returns>
    public static Point GetNumberPosition(Rect area, int row, bool rightColumn, string text, IGlyphSource font)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);

        IReadOnlyList<int> offsets = GetRowOffsets(font);
        int width = GlyphText.Measure(font, text);

        if (row == 3)
        {
            return new Point(area.X + ((PanelWidth - width) / 2), area.Y + offsets[3] + IconMaxHeight + 1);
        }

        int column = area.X + 1 + (rightColumn ? IconColumnWidth + ColumnGap : 0);

        return new Point(column + ((IconColumnWidth - width) / 2), area.Y + offsets[row] + IconMaxHeight + 1);
    }

    /// <summary>
    /// Draws the panel and returns its rectangle, <c>ui_castle.cpp:242-301</c>.
    /// </summary>
    /// <param name="target">The screen buffer.</param>
    /// <param name="funds">The kingdom's treasury.</param>
    /// <param name="offset">Origin of the castle screen.</param>
    /// <param name="font">The small white font the numbers use.</param>
    /// <param name="icons">
    /// The seven icons in <c>ICN::RESOURCE</c> order: wood, mercury, ore, sulfur, crystal, gems, gold. Exactly
    /// <see cref="IconCount"/> of them are required.
    /// </param>
    /// <param name="exitFrame">
    /// The released EXIT button, drawn at the panel's bottom: <c>ICN::BUTTON_EXIT_TOWN</c> frame 0, which is
    /// <c>TREASURY</c> frame 1 when the original resources are used (<c>game_assets.cpp:918-926</c>).
    /// </param>
    /// <returns>The panel's rectangle.</returns>
    /// <exception cref="ArgumentException"><paramref name="icons"/> does not hold seven icons.</exception>
    public static Rect Draw(
        PixelBuffer target,
        in Funds funds,
        Point offset,
        IGlyphSource font,
        IReadOnlyList<Sprite> icons,
        Sprite exitFrame)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(exitFrame);

        if (icons.Count != IconCount)
        {
            throw new ArgumentException($"The panel needs {IconCount} icons, got {icons.Count}.", nameof(icons));
        }

        Rect area = GetArea(offset);

        ImageOps.FillRect(target, area, 0);

        // Left column: wood, crystal, ore. Right column: sulfur, mercury, gems.
        DrawCell(target, area, row: 0, rightColumn: false, icons[0], funds.Wood, font);
        DrawCell(target, area, row: 0, rightColumn: true, icons[3], funds.Sulfur, font);
        DrawCell(target, area, row: 1, rightColumn: false, icons[4], funds.Crystal, font);
        DrawCell(target, area, row: 1, rightColumn: true, icons[1], funds.Mercury, font);
        DrawCell(target, area, row: 2, rightColumn: false, icons[2], funds.Ore, font);
        DrawCell(target, area, row: 2, rightColumn: true, icons[5], funds.Gems, font);
        DrawCell(target, area, row: 3, rightColumn: false, icons[6], funds.Gold, font);

        ImageOps.BlitSprite(target, new Point(area.X + 1, area.Y + ExitFrameOffsetY), exitFrame);

        return area;
    }

    private static void DrawCell(
        PixelBuffer target,
        Rect area,
        int row,
        bool rightColumn,
        Sprite icon,
        int amount,
        IGlyphSource font)
    {
        ImageOps.BlitSprite(target, GetIconPosition(area, row, rightColumn, icon, font), icon);

        string text = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);

        GlyphText.DrawLine(font, target, GetNumberPosition(area, row, rightColumn, text, font), text);
    }
}
