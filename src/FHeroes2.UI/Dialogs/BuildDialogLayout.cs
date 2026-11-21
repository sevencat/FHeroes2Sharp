using FHeroes2.Core.Geometry;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// Where the parts of the building dialog go, <c>Dialog::BuyBuilding()</c>
/// (<c>src/fheroes2/castle/buildinginfo.cpp:467-518</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的顺序是「相框、说明、需求、价格框」四段，段间 10 像素，说明与需求都是 244 像素宽的自动换行文本，
/// 高度先算出来才决定整个框有多高（<c>FrameBox</c> 的 <c>showButtons</c> 再往下让出按钮条）。
/// </para>
/// <para>
/// 相框里的建筑图上边比相框低 1 像素、名字在相框顶往下 58 + 2 像素处居中；说明从文本框左边缘开始
/// （不居中），需求标题居中、需求正文又回到左边缘；价格框贴着左边缘。
/// </para>
/// </remarks>
public sealed class BuildDialogLayout
{
    /// <summary>Gap between the parts of the dialog, <c>elementOffset</c>.</summary>
    public const int ElementOffset = 10;

    /// <summary>Distance from the frame's top to the building name, <c>58</c>.</summary>
    public const int NameOffsetY = 58;

    /// <summary>Width the building name may take before it is cut, <c>fitToOneRow( 125 )</c>.</summary>
    public const int NameMaxWidth = 125;

    private BuildDialogLayout(
        Rect area,
        Point frame,
        Point image,
        Point name,
        Point description,
        Point? requirementTitle,
        Point? requirementText,
        Point cost)
    {
        Area = area;
        Frame = frame;
        Image = image;
        Name = name;
        Description = description;
        RequirementTitle = requirementTitle;
        RequirementText = requirementText;
        Cost = cost;
    }

    /// <summary>Text area of the dialog frame, <c>dialogRoi</c>.</summary>
    public Rect Area { get; }

    /// <summary>Top-left corner of the 135x70 building frame.</summary>
    public Point Frame { get; }

    /// <summary>Top-left corner of the building image inside the frame.</summary>
    public Point Image { get; }

    /// <summary>Top-left corner of the building name.</summary>
    public Point Name { get; }

    /// <summary>Top-left corner of the description text.</summary>
    public Point Description { get; }

    /// <summary>Top-left corner of the "Requires:" title, or <see langword="null"/> when there is no requirement.</summary>
    public Point? RequirementTitle { get; }

    /// <summary>Top-left corner of the requirement text, or <see langword="null"/> when there is no requirement.</summary>
    public Point? RequirementText { get; }

    /// <summary>Top-left corner of the price box.</summary>
    public Point Cost { get; }

    /// <summary>
    /// Height the frame's text area has to provide, <c>totalDialogHeight</c> of the reference.
    /// </summary>
    /// <param name="frameHeight">Height of the 135x70 building frame.</param>
    /// <param name="descriptionHeight">Height of the wrapped description.</param>
    /// <param name="requirementTitleHeight">Height of the "Requires:" title.</param>
    /// <param name="requirementTextHeight">Height of the wrapped requirement.</param>
    /// <param name="costHeight">Height of the price box, <see cref="ResourceCostBox.GetHeight(in FHeroes2.Game.Resources.Funds)"/>.</param>
    /// <param name="requirementsPresent">Whether the dialog shows a requirement at all.</param>
    /// <returns>The height to hand to <see cref="FrameBox.Create"/>.</returns>
    public static int GetContentHeight(
        int frameHeight,
        int descriptionHeight,
        int requirementTitleHeight,
        int requirementTextHeight,
        int costHeight,
        bool requirementsPresent)
    {
        int requirementHeight = requirementsPresent
            ? requirementTitleHeight + requirementTextHeight + ElementOffset
            : 0;

        return ElementOffset + frameHeight + ElementOffset + descriptionHeight + ElementOffset +
               requirementHeight + costHeight;
    }

    /// <summary>
    /// Places the parts inside a frame.
    /// </summary>
    /// <param name="area">Text area of the frame, <c>FrameBox.Area</c>.</param>
    /// <param name="frameSize">Size of the 135x70 building frame.</param>
    /// <param name="imageSize">Size of the building image.</param>
    /// <param name="nameSize">Size of the building name after fitting.</param>
    /// <param name="descriptionHeight">Height of the wrapped description.</param>
    /// <param name="requirementTitleSize">Size of the "Requires:" title.</param>
    /// <param name="requirementTextHeight">Height of the wrapped requirement.</param>
    /// <param name="requirementsPresent">Whether the dialog shows a requirement at all.</param>
    /// <returns>The layout.</returns>
    public static BuildDialogLayout Create(
        Rect area,
        Size frameSize,
        Size imageSize,
        Size nameSize,
        int descriptionHeight,
        Size requirementTitleSize,
        int requirementTextHeight,
        bool requirementsPresent)
    {
        int frameX = area.X + ((area.Width - frameSize.Width) / 2);
        int frameY = area.Y + ElementOffset;

        Point frame = new(frameX, frameY);
        Point image = new(area.X + ((area.Width - imageSize.Width) / 2), frameY + 1);
        Point name = new(area.X + ((area.Width - nameSize.Width) / 2), frameY + NameOffsetY + 2);

        int descriptionY = area.Y + ElementOffset + frameSize.Height + ElementOffset;
        Point description = new(area.X, descriptionY + 2);

        int cursorY = descriptionY + descriptionHeight + ElementOffset;
        Point? titlePoint = null;
        Point? textPoint = null;

        if (requirementsPresent)
        {
            titlePoint = new(area.X + ((area.Width - requirementTitleSize.Width) / 2), cursorY + 2);
            cursorY += requirementTitleSize.Height;
            textPoint = new(area.X, cursorY + 2);
            cursorY += requirementTextHeight + ElementOffset;
        }

        return new BuildDialogLayout(
            area, frame, image, name, description, titlePoint, textPoint, new Point(area.X, cursorY));
    }
}
