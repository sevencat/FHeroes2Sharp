using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Resources;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The box that lists a price as icons with amounts under them, <c>Resource::BoxSprite</c>
/// (<c>src/fheroes2/resource/resource.cpp:584-664</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>resource.cpp:584-664</c>。三列一行，行高 45；第一行的图标底边落在框顶往下 35 像素处，
/// 数字再往下 4 像素。框高只由「有几项」决定：1~3 项 45，4~6 项 90，7 项 135——所以买东西的
/// 说明框底下那块高度是可以事先算出来的。
/// </para>
/// <para>
/// 最后一行不满三个时不是简单左对齐：整行按项数重排，两项各占 1/2、一项占满整行；只有一行
/// （前三项之内）也是同样的 1/2 或整行。只有当框里超过两项时，最后的一两项才留在三列网格里居中
/// （<c>isManyResources</c>）——这条不对称是原版的排版，抄下来才能和截图对上。
/// </para>
/// <para>
/// 图标取自 <c>RESOURCE.ICN</c>，帧号是资源枚举的顺序而不是 <see cref="Funds"/> 的字段顺序：
/// 木头 0、水银 1、矿石 2、硫磺 3、水晶 4、宝石 5、金子 6。
/// </para>
/// </remarks>
public static class ResourceCostBox
{
    /// <summary>Height of one row of icons and numbers, <c>offsetY</c> step of the reference.</summary>
    public const int RowHeight = 45;

    /// <summary>Distance from the top of the box to the baseline of the first row's icons, <c>35</c>.</summary>
    public const int FirstRowOffsetY = 35;

    /// <summary>Height of a box with one to three prices.</summary>
    public const int SingleRowHeight = 45;

    /// <summary>Height of a box with four to six prices.</summary>
    public const int DoubleRowHeight = 90;

    /// <summary>Height of a box with all seven prices.</summary>
    public const int TripleRowHeight = 135;

    /// <summary>Frame of the lumber icon in <c>RESOURCE.ICN</c>.</summary>
    public const int WoodFrame = 0;

    /// <summary>Frame of the mercury icon in <c>RESOURCE.ICN</c>.</summary>
    public const int MercuryFrame = 1;

    /// <summary>Frame of the ore icon in <c>RESOURCE.ICN</c>.</summary>
    public const int OreFrame = 2;

    /// <summary>Frame of the sulfur icon in <c>RESOURCE.ICN</c>.</summary>
    public const int SulfurFrame = 3;

    /// <summary>Frame of the crystal icon in <c>RESOURCE.ICN</c>.</summary>
    public const int CrystalFrame = 4;

    /// <summary>Frame of the gems icon in <c>RESOURCE.ICN</c>.</summary>
    public const int GemsFrame = 5;

    /// <summary>Frame of the gold icon in <c>RESOURCE.ICN</c>.</summary>
    public const int GoldFrame = 6;

    /// <summary>How many frames of <c>RESOURCE.ICN</c> the box uses.</summary>
    public const int IconCount = 7;

    /// <summary>
    /// One price as it is drawn: the icon frame and the amount.
    /// </summary>
    /// <param name="Frame">Frame of <c>RESOURCE.ICN</c>.</param>
    /// <param name="Amount">Number printed under the icon.</param>
    public readonly record struct Entry(int Frame, int Amount);

    /// <summary>
    /// The height the box needs for a price, <c>Resource::BoxSprite::BoxSprite()</c>
    /// (<c>resource.cpp:589</c>).
    /// </summary>
    /// <param name="cost">The price.</param>
    /// <returns>45, 90 or 135 pixels.</returns>
    public static int GetHeight(in Funds cost) => GetHeight(CountValidItems(in cost));

    /// <summary>
    /// The height the box needs for a number of prices, <c>height = 4 > count ? 45 : ( 7 > count ? 90 : 135 )</c>.
    /// </summary>
    /// <param name="count">How many resources the price has.</param>
    /// <returns>45, 90 or 135 pixels.</returns>
    public static int GetHeight(int count) => count switch
    {
        <= 3 => SingleRowHeight,
        <= 6 => DoubleRowHeight,
        _ => TripleRowHeight,
    };

    /// <summary>
    /// How many resources of a price are non zero, <c>Funds::GetValidItemsCount()</c>.
    /// </summary>
    /// <param name="cost">The price.</param>
    /// <returns>The count of resources that will be drawn.</returns>
    public static int CountValidItems(in Funds cost)
    {
        int count = 0;

        if (cost.Wood > 0)
        {
            ++count;
        }

        if (cost.Ore > 0)
        {
            ++count;
        }

        if (cost.Mercury > 0)
        {
            ++count;
        }

        if (cost.Sulfur > 0)
        {
            ++count;
        }

        if (cost.Crystal > 0)
        {
            ++count;
        }

        if (cost.Gems > 0)
        {
            ++count;
        }

        if (cost.Gold > 0)
        {
            ++count;
        }

        return count;
    }

    /// <summary>
    /// The prices in the order the box draws them, <c>Resource::BoxSprite::Redraw()</c> (<c>resource.cpp:603-627</c>).
    /// </summary>
    /// <param name="cost">The price.</param>
    /// <returns>The entries; resources with a zero amount are left out.</returns>
    public static IReadOnlyList<Entry> GetEntries(in Funds cost)
    {
        List<Entry> entries = new(IconCount);

        if (cost.Wood > 0)
        {
            entries.Add(new Entry(WoodFrame, cost.Wood));
        }

        if (cost.Ore > 0)
        {
            entries.Add(new Entry(OreFrame, cost.Ore));
        }

        if (cost.Mercury > 0)
        {
            entries.Add(new Entry(MercuryFrame, cost.Mercury));
        }

        if (cost.Sulfur > 0)
        {
            entries.Add(new Entry(SulfurFrame, cost.Sulfur));
        }

        if (cost.Crystal > 0)
        {
            entries.Add(new Entry(CrystalFrame, cost.Crystal));
        }

        if (cost.Gems > 0)
        {
            entries.Add(new Entry(GemsFrame, cost.Gems));
        }

        if (cost.Gold > 0)
        {
            entries.Add(new Entry(GoldFrame, cost.Gold));
        }

        return entries;
    }

    /// <summary>
    /// Where one entry goes, in box local pixels.
    /// </summary>
    /// <param name="width">Width of the box, <c>boxAreaWidthPx</c> (244) for the build dialog.</param>
    /// <param name="entryCount">Total number of entries the box draws.</param>
    /// <param name="index">Index of the entry in <see cref="GetEntries"/>.</param>
    /// <returns>The icon's centre and the row's top; the icon sits above the point, the number below it.</returns>
    public static (Point Center, int RowTop) GetEntryPosition(int width, int entryCount, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (index >= entryCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The entry is outside the box.");
        }

        bool isManyResources = entryCount > 2;

        // The reference walks the price three at a time while at least three entries are left
        // (`while ( valueVsSprite.size() - id > 2 )`), so every complete row of three is a full
        // row; only the last one or two entries go to the narrow rows below them.
        int fullRows = entryCount / 3;
        int drawnInFullRows = fullRows * 3;

        if (index < drawnInFullRows)
        {
            int cell = width / 3;

            return (new Point((cell / 2) + ((index % 3) * cell), FirstRowOffsetY + ((index / 3) * RowHeight)),
                FirstRowOffsetY + ((index / 3) * RowHeight));
        }

        int remaining = entryCount - drawnInFullRows;
        int rowTop = FirstRowOffsetY + (fullRows * RowHeight);

        if (remaining == 2)
        {
            int cell = isManyResources ? width / 3 : width / 2;
            int offsetX = isManyResources ? cell / 2 : 0;

            return (new Point(offsetX + (cell / 2) + ((index - drawnInFullRows) * cell), rowTop), rowTop);
        }

        int singleCell = isManyResources ? width / 3 : width;
        int singleIndex = isManyResources ? 1 : 0;

        return (new Point((singleCell / 2) + (singleIndex * singleCell), rowTop), rowTop);
    }

    /// <summary>
    /// Draws the box, <c>Resource::BoxSprite::Redraw()</c> with its <c>RedrawResourceSprite</c> helper
    /// (<c>resource.cpp:603-664</c>).
    /// </summary>
    /// <param name="target">The frame buffer of the scene.</param>
    /// <param name="position">Top-left corner of the box.</param>
    /// <param name="cost">The price to list.</param>
    /// <param name="icons">Frames of <c>RESOURCE.ICN</c>, at least <see cref="IconCount"/>.</param>
    /// <param name="font">Font of the amounts, <c>FontType::smallWhite()</c> in the reference.</param>
    /// <param name="width">Width of the box, 244 pixels by default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="icons"/> or <paramref name="font"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="icons"/> holds fewer than <see cref="IconCount"/> frames.</exception>
    public static void Draw(
        PixelBuffer target,
        Point position,
        in Funds cost,
        IReadOnlyList<Sprite> icons,
        IGlyphSource font,
        int width = FrameBox.AreaWidth)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(icons);
        ArgumentNullException.ThrowIfNull(font);

        if (icons.Count < IconCount)
        {
            throw new ArgumentException(
                $"A price box needs {IconCount} RESOURCE frames but only {icons.Count} were loaded.",
                nameof(icons));
        }

        IReadOnlyList<Entry> entries = GetEntries(cost);

        for (int index = 0; index < entries.Count; ++index)
        {
            Entry entry = entries[index];
            (Point center, int rowTop) = GetEntryPosition(width, entries.Count, index);
            Sprite icon = icons[entry.Frame];

            int iconX = position.X + center.X - (icon.Width / 2);
            int iconY = position.Y + rowTop - icon.Height;

            ImageOps.BlitSprite(target, new Point(iconX, iconY), icon);

            string text = entry.Amount.ToString();
            int textX = position.X + center.X - (GlyphText.Measure(font, text) / 2);

            GlyphText.DrawLine(font, target, new Point(textX, position.Y + rowTop + 4), text);
        }
    }
}
