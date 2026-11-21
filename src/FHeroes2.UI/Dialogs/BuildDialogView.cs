using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The clickable, drawable building dialog, <c>Dialog::BuyBuilding()</c>
/// (<c>src/fheroes2/castle/buildinginfo.cpp:435-537</c>).
/// </summary>
/// <remarks>
/// <para>
/// 对话框自上而下是：135x70 的建筑相框（图 + 名字）、说明、<c>Requires:</c> 与需求、价格框，
/// 最底下一排 OKAY/CANCEL。整框高度是「相框 + 说明 + 需求 + 价格」四段算出来的，
/// 见 <see cref="BuildDialogLayout.GetContentHeight"/>。
/// </para>
/// <para>
/// 两处细节照抄参考：买不起或已建好时说明文字后面会追加一段条件说明
/// （<c>GetConditionDescription()</c>，与说明相同就不重复），并且 OKAY 是灰的；
/// 需求只在状态不是「禁止建造」时才写，采邑/船坞这类由地形决定的建筑因此没有需求行。
/// </para>
/// </remarks>
public sealed class BuildDialogView
{
    /// <summary>Title of the requirement block, <c>_( "Requires:" )</c>.</summary>
    public const string RequirementTitle = "Requires:";

    /// <summary>How the name is cut when it is wider than <see cref="BuildDialogLayout.NameMaxWidth"/>.</summary>
    private const string Ellipsis = "...";

    private readonly BuildingInfo _info;
    private readonly BuildDialogArtwork _artwork;
    private readonly IGlyphSource _normalFont;
    private readonly IGlyphSource _smallFont;
    private readonly string _description;
    private readonly string? _requirement;
    private readonly Sprite? _image;
    private readonly string _name;
    private readonly BuildDialogLayout _layout;
    private readonly FrameBox _box;
    private readonly ButtonGroup _buttons;

    private BuildDialogView(
        BuildingInfo info,
        BuildDialogArtwork artwork,
        IGlyphSource normalFont,
        IGlyphSource smallFont,
        string description,
        string? requirement,
        Sprite? image,
        string name,
        BuildDialogLayout layout,
        FrameBox box,
        ButtonGroup buttons)
    {
        _info = info;
        _artwork = artwork;
        _normalFont = normalFont;
        _smallFont = smallFont;
        _description = description;
        _requirement = requirement;
        _image = image;
        _name = name;
        _layout = layout;
        _box = box;
        _buttons = buttons;
    }

    /// <summary>Text area of the frame, <c>GetArea()</c> of the reference.</summary>
    public Rect Area => _box.Area;

    /// <summary>Bounding box of the frame artwork.</summary>
    public Rect FrameArea => _box.FrameArea;

    /// <summary>The building the dialog is about.</summary>
    public BuildingInfo Info => _info;

    /// <summary>The OK and CANCEL buttons.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>The dialog layout: where each part was placed.</summary>
    public BuildDialogLayout Layout => _layout;

    /// <summary>Whether OKAY may be pressed, <c>BuildingStatus::ALLOW_BUILD == status</c>.</summary>
    public bool OkayEnabled => _info.Status == BuildingStatus.AllowBuild;

    /// <summary>The description as drawn, including the condition text when there is one.</summary>
    public string Description => _description;

    /// <summary>The requirement as drawn, or <see langword="null"/> when the dialog has no requirement row.</summary>
    public string? Requirement => _requirement;

    /// <summary>The name as drawn, fitted into 125 pixels.</summary>
    public string Name => _name;

    /// <summary>
    /// Builds the dialog.
    /// </summary>
    /// <param name="info">The building, its status and its price.</param>
    /// <param name="artwork">Frames of the dialog.</param>
    /// <param name="normalFont">Font of the description and the requirement, <c>FontType::normalWhite()</c>.</param>
    /// <param name="smallFont">Font of the name and the amounts, <c>FontType::smallWhite()</c>.</param>
    /// <param name="buttonArtwork">OK and CANCEL artwork.</param>
    /// <returns>The dialog, or <see langword="null"/> when a text is too tall for the frame.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static BuildDialogView? TryCreate(
        BuildingInfo info,
        BuildDialogArtwork artwork,
        IGlyphSource normalFont,
        IGlyphSource smallFont,
        IDialogButtonArtworkSource buttonArtwork)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(normalFont);
        ArgumentNullException.ThrowIfNull(smallFont);
        ArgumentNullException.ThrowIfNull(buttonArtwork);

        string description = info.Description;

        if (info.Status != BuildingStatus.AllowBuild)
        {
            string condition = info.GetConditionDescription();

            if (!string.IsNullOrEmpty(condition) && condition != description)
            {
                description = string.Concat(description, "\n\n", condition);
            }
        }

        string? requirement = info.Status == BuildingStatus.BuildDisabled
            ? null
            : CastleBuildingInfo.GetRequirementString(info.Castle.Race, info.Type);

        if (string.IsNullOrEmpty(requirement))
        {
            requirement = null;
        }

        int descriptionHeight =
            GlyphText.WrapLines(normalFont, description, FrameBox.AreaWidth).Count * normalFont.LineHeight;
        Size titleSize = new(GlyphText.Measure(normalFont, RequirementTitle), normalFont.LineHeight);
        int requirementHeight = requirement is null
            ? 0
            : GlyphText.WrapLines(normalFont, requirement, FrameBox.AreaWidth).Count * normalFont.LineHeight;

        int costHeight = ResourceCostBox.GetHeight(info.Cost);
        int contentHeight = BuildDialogLayout.GetContentHeight(
            artwork.Frame.Height,
            descriptionHeight,
            titleSize.Height,
            requirementHeight,
            costHeight,
            requirement is not null);

        FrameBox box = FrameBox.Create(artwork.Chrome, contentHeight, showButtons: true);

        int spriteIndex = CastleBuildingInfo.GetSpriteIndex(info.Type);
        Sprite? image = spriteIndex >= 0 && spriteIndex < artwork.BuildingImages.Count
            ? artwork.BuildingImages[spriteIndex]
            : null;

        Size imageSize = image is null ? Size.Empty : new Size(image.Width, image.Height);
        string name = FitToOneRow(smallFont, info.Name, BuildDialogLayout.NameMaxWidth);
        Size nameSize = new(GlyphText.Measure(smallFont, name), smallFont.LineHeight);

        BuildDialogLayout layout = BuildDialogLayout.Create(
            box.Area, new Size(artwork.Frame.Width, artwork.Frame.Height), imageSize, nameSize, descriptionHeight,
            titleSize, requirementHeight, requirement is not null);

        if (layout.Cost.Y + costHeight > box.Area.Bottom)
        {
            return null;
        }

        ButtonGroup buttons = ButtonGroup.CreateStandard(
            box.Area, DialogButton.Ok | DialogButton.Cancel, buttonArtwork);

        if (info.Status != BuildingStatus.AllowBuild)
        {
            buttons[0].Disable();
        }

        return new BuildDialogView(
            info, artwork, normalFont, smallFont, description, requirement, image, name, layout, box, buttons);
    }

    /// <summary>Records the pointer over the buttons.</summary>
    /// <param name="cursor">Pointer in logical pixels, or <see langword="null"/> outside the window.</param>
    public void SetCursor(Point? cursor) => _buttons.SetCursor(cursor);

    /// <summary>
    /// A left button transition over the dialog.
    /// </summary>
    /// <param name="down">Whether the button went down.</param>
    /// <returns>Which button the transition answered, or <see cref="DialogButton.None"/>.</returns>
    public DialogButton SetLeftButton(bool down) => _buttons.SetLeftButton(down);

    /// <summary>Draws the dialog over the town screen.</summary>
    /// <param name="target">The frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _box.Draw(target);
        ImageOps.BlitSprite(target, _layout.Frame, _artwork.Frame);

        if (_image is { } image)
        {
            ImageOps.BlitSprite(target, _layout.Image, image);
        }

        GlyphText.DrawLine(_smallFont, target, _layout.Name, _name);
        GlyphText.DrawMultiline(_normalFont, target, _layout.Description, _description, FrameBox.AreaWidth);

        if (_layout.RequirementTitle is { } title && _layout.RequirementText is { } text &&
            _requirement is not null)
        {
            GlyphText.DrawLine(_normalFont, target, title, RequirementTitle);
            GlyphText.DrawMultiline(_normalFont, target, text, _requirement, FrameBox.AreaWidth);
        }

        ResourceCostBox.Draw(target, _layout.Cost, _info.Cost, _artwork.ResourceIcons, _smallFont);
        _buttons.Draw(target);
    }

    /// <summary>
    /// Cuts a text until it fits a width, appending an ellipsis, <c>Text::fitToOneRow()</c>.
    /// </summary>
    /// <param name="font">Font of the text.</param>
    /// <param name="text">The text to fit.</param>
    /// <param name="maxWidth">Width the text may take, 125 pixels for the building name.</param>
    /// <returns>The text, possibly cut.</returns>
    public static string FitToOneRow(IGlyphSource font, string text, int maxWidth)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(text);

        if (GlyphText.Measure(font, text) <= maxWidth)
        {
            return text;
        }

        for (int length = text.Length - 1; length > 0; --length)
        {
            string candidate = string.Concat(text.AsSpan(0, length), Ellipsis);

            if (GlyphText.Measure(font, candidate) <= maxWidth)
            {
                return candidate;
            }
        }

        return Ellipsis;
    }
}
