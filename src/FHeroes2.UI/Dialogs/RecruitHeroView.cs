using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The drawable recruit hero dialog, <c>Castle::DialogBuyHero()</c>
/// (<c>src/fheroes2/castle/castle_town.cpp:66-160</c>).
/// </summary>
/// <remarks>
/// <para>
/// 自上而下是：黄色标题 <c>Recruit Hero</c>、111x105 的肖像相框（内贴 102x93 的大肖像）、一句英雄说明、
/// <c>PaymentConditions::RecruitHero()</c> 的价格框，最底下一排 OKAY/CANCEL。
/// </para>
/// <para>
/// 照参考，<c>AllowBuyHero()</c> 为假时 OKAY 是灰的；英雄为空时整框不建。
/// 右键在相框上出英雄速览、在 OKAY 上出「Recruit X the Y.」、在 CANCEL 上出「Exit this menu without doing
/// anything.」——本视图只管按钮，相框的右键由场景处理。
/// </para>
/// </remarks>
public sealed class RecruitHeroView
{
    private readonly RecruitHeroDialog _dialog;
    private readonly RecruitHeroArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _descriptionFont;
    private readonly IGlyphSource _smallFont;
    private readonly Sprite? _portrait;
    private readonly RecruitHeroLayout _layout;
    private readonly FrameBox _box;
    private readonly ButtonGroup _buttons;

    private RecruitHeroView(
        RecruitHeroDialog dialog,
        RecruitHeroArtwork artwork,
        IGlyphSource titleFont,
        IGlyphSource descriptionFont,
        IGlyphSource smallFont,
        Sprite? portrait,
        RecruitHeroLayout layout,
        FrameBox box,
        ButtonGroup buttons)
    {
        _dialog = dialog;
        _artwork = artwork;
        _titleFont = titleFont;
        _descriptionFont = descriptionFont;
        _smallFont = smallFont;
        _portrait = portrait;
        _layout = layout;
        _box = box;
        _buttons = buttons;
    }

    /// <summary>Text area of the frame, <c>dialogRoi</c> of the reference.</summary>
    public Rect Area => _box.Area;

    /// <summary>Bounding box of the frame artwork.</summary>
    public Rect FrameArea => _box.FrameArea;

    /// <summary>The hero on offer and what the dialog says about them.</summary>
    public RecruitHeroDialog Dialog => _dialog;

    /// <summary>The OK and CANCEL buttons.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>The dialog layout: where each part was placed.</summary>
    public RecruitHeroLayout Layout => _layout;

    /// <summary>Whether OKAY may be pressed, <c>AllowBuyHero()</c> of the reference.</summary>
    public bool OkayEnabled => _dialog.CanRecruit;

    /// <summary>
    /// Builds the dialog.
    /// </summary>
    /// <param name="dialog">The hero, the sentence and the price.</param>
    /// <param name="artwork">Frames of the dialog.</param>
    /// <param name="portrait">The hero's 102x93 portrait, <c>PORT00xx</c> frame 0; may be <see langword="null"/>.</param>
    /// <param name="titleFont">Font of the heading, <c>FontType::normalYellow()</c>.</param>
    /// <param name="descriptionFont">Font of the sentence, <c>FontType::normalWhite()</c>.</param>
    /// <param name="smallFont">Font of the amounts, <c>FontType::smallWhite()</c>.</param>
    /// <param name="buttonArtwork">OK and CANCEL artwork.</param>
    /// <returns>
    /// The dialog, or <see langword="null"/> when the text is too tall for the frame or the archive has
    /// no message frame.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static RecruitHeroView? TryCreate(
        RecruitHeroDialog dialog,
        RecruitHeroArtwork artwork,
        Sprite? portrait,
        IGlyphSource titleFont,
        IGlyphSource descriptionFont,
        IGlyphSource smallFont,
        IDialogButtonArtworkSource buttonArtwork)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(descriptionFont);
        ArgumentNullException.ThrowIfNull(smallFont);
        ArgumentNullException.ThrowIfNull(buttonArtwork);

        if (!FrameBox.IsArtworkAvailable)
        {
            return null;
        }

        string description = dialog.Description;
        int descriptionHeight =
            GlyphText.WrapLines(descriptionFont, description, FrameBox.AreaWidth).Count * descriptionFont.LineHeight;
        int costHeight = ResourceCostBox.GetHeight(dialog.Cost);
        int titleHeight = titleFont.LineHeight;

        int contentHeight = RecruitHeroLayout.GetContentHeight(
            titleHeight, artwork.Frame.Height, descriptionHeight, costHeight);

        FrameBox box = FrameBox.Create(contentHeight, showButtons: true);

        RecruitHeroLayout layout = RecruitHeroLayout.Create(
            box.Area, new Size(artwork.Frame.Width, artwork.Frame.Height), titleHeight, descriptionHeight);

        if (layout.Cost.Y + costHeight > box.Area.Bottom)
        {
            return null;
        }

        ButtonGroup buttons = ButtonGroup.CreateStandard(
            box.Area, DialogButton.Ok | DialogButton.Cancel, buttonArtwork);

        if (!dialog.CanRecruit)
        {
            buttons[0].Disable();
        }

        return new RecruitHeroView(
            dialog, artwork, titleFont, descriptionFont, smallFont, portrait, layout, box, buttons);
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
        GlyphText.DrawLine(_titleFont, target, _layout.Title, RecruitHeroDialog.Title);
        ImageOps.BlitSprite(target, _layout.Frame, _artwork.Frame);

        if (_portrait is { } portrait)
        {
            ImageOps.BlitSprite(target, _layout.Portrait, portrait);
        }

        GlyphText.DrawMultiline(
            _descriptionFont, target, _layout.Description, _dialog.Description, FrameBox.AreaWidth);

        ResourceCostBox.Draw(target, _layout.Cost, _dialog.Cost, _artwork.ResourceIcons, _smallFont);
        _buttons.Draw(target);
    }
}
