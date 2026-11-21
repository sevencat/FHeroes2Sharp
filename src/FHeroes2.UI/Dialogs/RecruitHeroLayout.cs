using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where the parts of the recruit hero dialog go, <c>Castle::DialogBuyHero()</c>
/// (<c>src/fheroes2/castle/castle_town.cpp:66-160</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的顺序是「标题、相框、说明、价格框」四段，段间 10 像素。与其他确认框不同，标题不占
/// <c>elementOffset</c>：标题写在 <c>dialogRoi.y + 2</c>，相框从标题高度之后才开始，所以整框高度就是
/// 这四段之和，没有领先的那 10 像素。
/// </para>
/// <para>
/// 相框是 <c>SURRENDR.ICN</c> 帧 4（111x105），大肖像贴进相框内 (+5, +6) 处；说明是 244 像素宽的自动换行
/// 文本，从文本框左边缘写起；价格框也贴着左边缘。
/// </para>
/// </remarks>
public sealed class RecruitHeroLayout
{
    /// <summary>Gap between the parts of the dialog, <c>spacer</c> (10).</summary>
    public const int ElementOffset = 10;

    /// <summary>Distance from the frame's top to the title, <c>dialogRoi.y + 2</c>.</summary>
    public const int TitleOffsetY = 2;

    /// <summary>Distance from a part's top to its text, <c>...</c> + 2 of the reference.</summary>
    public const int TextOffsetY = 2;

    /// <summary>How far the big portrait sits from the frame's left edge, <c>pos.x + 5</c>.</summary>
    public const int PortraitOffsetX = 5;

    /// <summary>How far the big portrait sits from the frame's top edge, <c>pos.y + 6</c>.</summary>
    public const int PortraitOffsetY = 6;

    private RecruitHeroLayout(
        Rect area,
        Point title,
        Point frame,
        Size frameSize,
        Point portrait,
        Point description,
        Point cost)
    {
        Area = area;
        Title = title;
        Frame = frame;
        FrameSize = frameSize;
        Portrait = portrait;
        Description = description;
        Cost = cost;
    }

    /// <summary>Text area of the dialog frame, <c>dialogRoi</c>.</summary>
    public Rect Area { get; }

    /// <summary>Top-left corner of the heading, <c>recruitHeroText</c>.</summary>
    public Point Title { get; }

    /// <summary>Top-left corner of the 111x105 portrait frame, <c>portrait_frame</c>.</summary>
    public Point Frame { get; }

    /// <summary>Size of the portrait frame, <c>portrait_frame.GetSize()</c>.</summary>
    public Size FrameSize { get; }

    /// <summary>Bounding box of the portrait frame, the area whose right click asks for the quick info.</summary>
    public Rect FrameArea => new(Frame, FrameSize);

    /// <summary>Top-left corner of the big hero portrait inside the frame.</summary>
    public Point Portrait { get; }

    /// <summary>Top-left corner of the hero description, <c>heroDescriptionText</c>.</summary>
    public Point Description { get; }

    /// <summary>Top-left corner of the price box.</summary>
    public Point Cost { get; }

    /// <summary>
    /// Height the frame's text area has to provide, <c>dialogHeight</c> of the reference.
    /// </summary>
    /// <param name="titleHeight">Height of the heading.</param>
    /// <param name="frameHeight">Height of the 111x105 portrait frame.</param>
    /// <param name="descriptionHeight">Height of the wrapped description.</param>
    /// <param name="costHeight">Height of the price box, <see cref="ResourceCostBox.GetHeight(in FHeroes2.Game.Resources.Funds)"/>.</param>
    /// <returns>The height to hand to <see cref="FrameBox.Create"/>.</returns>
    public static int GetContentHeight(int titleHeight, int frameHeight, int descriptionHeight, int costHeight) =>
        titleHeight + ElementOffset + frameHeight + ElementOffset + descriptionHeight + ElementOffset + costHeight;

    /// <summary>
    /// Places the parts inside a frame.
    /// </summary>
    /// <param name="area">Text area of the frame, <c>FrameBox.Area</c>.</param>
    /// <param name="frameSize">Size of the 111x105 portrait frame.</param>
    /// <param name="titleHeight">Height of the heading.</param>
    /// <param name="descriptionHeight">Height of the wrapped description.</param>
    /// <returns>The layout.</returns>
    public static RecruitHeroLayout Create(Rect area, Size frameSize, int titleHeight, int descriptionHeight)
    {
        int frameX = area.X + ((area.Width - frameSize.Width) / 2);
        int frameY = area.Y + titleHeight + ElementOffset;

        int descriptionY = frameY + frameSize.Height + ElementOffset;

        return new RecruitHeroLayout(
            area,
            new Point(area.X, area.Y + TitleOffsetY),
            new Point(frameX, frameY),
            frameSize,
            new Point(frameX + PortraitOffsetX, frameY + PortraitOffsetY),
            new Point(area.X, descriptionY + TextOffsetY),
            new Point(area.X, descriptionY + descriptionHeight + ElementOffset));
    }
}
