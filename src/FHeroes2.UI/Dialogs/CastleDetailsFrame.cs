using FHeroes2.Assets;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The chrome of the editor's castle details window: the two-piece background, the name bar, the two text
/// adapted buttons and the status bar with OKAY and EXIT, <c>castleDetailsDialog()</c>
/// (<c>editor_castle_details_window.cpp:619-798</c>).
/// </summary>
/// <remarks>
/// <para>
/// 满屏的标准窗里再拼一层自己的背景：左边 438 像素是 <c>CASLWIND</c> 的面板，右边补 <c>STONEBAK</c>，
/// 两条 4 像素横线盖住接缝；名字条与底部状态栏都取 <c>CASLBAR</c>。参考在这之上才画勾选框、军队条与十九个
/// 建筑格，那些归 <c>CastleDetailsGrid</c> 与军队条，不在这个类里。
/// </para>
/// <para>
/// 画的顺序就是参考的顺序：背景 → 名字 → SET SPELLS → （勾选框与军队） → RESTRICT → 两个复位按钮 →
/// OKAY/EXIT → 状态栏。状态栏要盖在两个按钮之间，所以必须最后画，端口把它单独拆成
/// <see cref="PaintBottomButtons"/>。
/// </para>
/// <para>
/// 参考用 <c>fheroes2::Copy()</c>（不透明拷贝）拼右半、分隔线与状态栏，用 <c>fheroes2::Blit()</c>（带透明）
/// 画左半，端口对应 <see cref="ImageOps.Blit(PixelBuffer, Point, PixelBuffer, in Rect)"/> 与
/// <see cref="ImageOps.BlitSpriteClipped"/>，两种语义不能混。
/// </para>
/// </remarks>
public sealed class CastleDetailsFrame
{
    /// <summary>Left edge of the right panel, <c>rightPartOffsetX</c> (<c>:629</c>).</summary>
    public const int RightPanelOffsetX = CastleDetailsGrid.RightPanelOffsetX;

    /// <summary>Column of <c>CASLBAR</c> the name bar starts at, <c>Copy( statusBarSprite, 17, 0, ... )</c>.</summary>
    public const int NameBarSourceX = 17;

    /// <summary>Row of <c>CASLWIND</c> the two separators are taken from, <c>:636-637</c>.</summary>
    public const int SeparatorSourceY = 251;

    /// <summary>Height of a separator, <c>4</c>.</summary>
    public const int SeparatorHeight = 4;

    /// <summary>Offset of the upper separator from the top of the active area, <c>:636</c>.</summary>
    public const int TopSeparatorOffsetY = 226;

    /// <summary>Offset of the lower separator from the top of the active area, <c>:637</c>.</summary>
    public const int BottomSeparatorOffsetY = 306;

    /// <summary>Horizontal offset of the two reset buttons from the right edge, <c>{ 56, 24 }</c>.</summary>
    public const int ResetButtonOffsetX = 56;

    /// <summary>Vertical offset of the two reset buttons from the top and the bottom edge.</summary>
    public const int ResetButtonOffsetY = 24;

    /// <summary>Clear space between the status message and OKAY or EXIT, <c>statusBarDecorationsWidth</c>.</summary>
    public const int StatusBarDecorationsWidth = 16;

    /// <summary>Label of the button that opens the mages guild spells dialog.</summary>
    public const string SpellsButtonText = "SET SPELLS";

    /// <summary>Label of the button that switches the building grid into the restriction mode.</summary>
    public const string RestrictButtonText = "RESTRICT";

    private readonly CastleDetailsArtwork _artwork;
    private readonly StandardWindow _window;
    private readonly bool _isEvilInterface;

    private (Sprite Released, Sprite Pressed)? _spellsPlate;
    private (Sprite Released, Sprite Pressed)? _restrictPlate;

    private CastleDetailsFrame(CastleDetailsArtwork artwork, StandardWindow window, bool isEvilInterface)
    {
        _artwork = artwork;
        _window = window;
        _isEvilInterface = isEvilInterface;
    }

    /// <summary>
    /// Offset of the SET SPELLS button from the centre of the active area, <c>{ 219, 78 }</c> with
    /// <see cref="WindowPadding.TopCenter"/> (<c>:661</c>).
    /// </summary>
    public static Point SpellsButtonOffset => new(219, 78);

    /// <summary>
    /// Offset of the RESTRICT button from the centre of the active area, <c>{ 219, -32 }</c> with
    /// <see cref="WindowPadding.CenterCenter"/> (<c>:689</c>).
    /// </summary>
    public static Point RestrictButtonOffset => new(219, -32);

    /// <summary>The full screen window the dialog is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>Area every control is placed in, <c>dialogRoi</c>.</summary>
    public Rect DialogArea => _window.ActiveArea;

    /// <summary>
    /// The strip the castle name goes into, <c>nameArea</c> (<c>:642</c>). Its height is two pixels short
    /// of the bar, and it starts one pixel below the top of the active area.
    /// </summary>
    public Rect NameArea => new(
        DialogArea.X + RightPanelOffsetX,
        DialogArea.Y + 1,
        DialogArea.Width - RightPanelOffsetX,
        _artwork.StatusBar.Height - 2);

    /// <summary>
    /// The strip OKAY and EXIT sit in: the bottom of the active area, as tall as <c>CASLBAR</c>
    /// (<c>:781</c>).
    /// </summary>
    public Rect StatusBarArea => new(
        DialogArea.X,
        DialogArea.Y + DialogArea.Height - _artwork.StatusBar.Height,
        DialogArea.Width,
        _artwork.StatusBar.Height);

    /// <summary>
    /// The strip the status message goes into: between OKAY and EXIT, 16 pixels clear of each,
    /// <c>statusBar.setRoi( { dialogRoi.x + buttonOkayWidth + statusBarDecorationsWidth,
    /// dialogRoi.y + dialogRoi.height - statusBarHeight, dialogRoi.width - buttonExitWidth - buttonOkayWidth
    /// - 2 * statusBarDecorationsWidth, 0 } )</c> (<c>:801-805</c>).
    /// </summary>
    /// <remarks>
    /// 高度是 0：状态栏自己按消息高度往下画，参考给的就是一个没有高度的条。
    /// </remarks>
    public Rect StatusBarMessageArea
    {
        get
        {
            Rect bar = StatusBarArea;
            int okayWidth = _artwork.OkayReleased.Width;
            int exitWidth = _artwork.ExitReleased.Width;

            return new Rect(bar.X + okayWidth + StatusBarDecorationsWidth, bar.Y,
                bar.Width - exitWidth - okayWidth - (2 * StatusBarDecorationsWidth), 0);
        }
    }

    /// <summary>The button that opens the mages guild spells dialog, created by <see cref="PaintSpellsButton"/>.</summary>
    public ButtonSprite? Spells { get; private set; }

    /// <summary>The button that switches the grid into the restriction mode, created by <see cref="PaintRestrictButton"/>.</summary>
    public ButtonSprite? Restrict { get; private set; }

    /// <summary>The button that clears the custom castle name, created by <see cref="PaintBottomButtons"/>.</summary>
    public ButtonSprite? ResetName { get; private set; }

    /// <summary>The button that clears the army, created by <see cref="PaintBottomButtons"/>.</summary>
    public ButtonSprite? ResetArmy { get; private set; }

    /// <summary>OKAY, created by <see cref="PaintBottomButtons"/>.</summary>
    public ButtonSprite? Okay { get; private set; }

    /// <summary>EXIT, created by <see cref="PaintBottomButtons"/>.</summary>
    public ButtonSprite? Exit { get; private set; }

    /// <summary>
    /// Creates the full screen window of the dialog, the
    /// <c>StandardWindow( DEFAULT_WIDTH, DEFAULT_HEIGHT, false )</c> of <c>:619</c>: the active area keeps
    /// the 16 pixel border of a standard window, so it is 608x448 in the middle of the 640x480 screen.
    /// </summary>
    /// <returns>The laid out window.</returns>
    public static StandardWindow CreateWindow()
    {
        Size screen = ScreenBuffer.DefaultSize;

        return StandardWindow.Create(
            new Rect(StandardWindow.BorderWidth, StandardWindow.BorderWidth, screen.Width - (2 * StandardWindow.BorderWidth),
                screen.Height - (2 * StandardWindow.BorderWidth)),
            renderBackground: false);
    }

    /// <summary>
    /// Loads the artwork and lays the window out.
    /// </summary>
    /// <param name="assets">The loaded archives.</param>
    /// <param name="isEvilInterface">Whether the gray <c>*_EVIL</c> artwork is used.</param>
    /// <returns>The frame, or <see langword="null"/> when the artwork is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assets"/> is <see langword="null"/>.</exception>
    public static CastleDetailsFrame? TryCreate(AssetBundle assets, bool isEvilInterface)
    {
        ArgumentNullException.ThrowIfNull(assets);

        return CastleDetailsArtwork.TryCreate(assets, isEvilInterface) is { } artwork
            ? new CastleDetailsFrame(artwork, CreateWindow(), isEvilInterface)
            : null;
    }

    /// <summary>
    /// Draws the window border, the two-piece background, the two separators and the name bar, in this
    /// order, <c>:619-655</c>.
    /// </summary>
    /// <param name="output">Frame the dialog goes into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void PaintBackdrop(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _window.Draw(output);

        Rect dialog = DialogArea;
        int rightPartWidth = dialog.Width - RightPanelOffsetX;
        int backgroundHeight = _artwork.ConstructionBackground.Height;

        // Left half of the town construction panel, transparency included.
        ImageOps.BlitSpriteClipped(output, new Point(dialog.X, dialog.Y), _artwork.ConstructionBackground,
            new Rect(0, 0, RightPanelOffsetX, backgroundHeight), output.Bounds);

        // Right half of the stone texture and the two separators are opaque copies.
        ImageOps.Blit(output, new Point(dialog.X + RightPanelOffsetX, dialog.Y), _artwork.StoneBackground.Image,
            new Rect(RightPanelOffsetX, 0, rightPartWidth, backgroundHeight));
        ImageOps.Blit(output, new Point(dialog.X + RightPanelOffsetX, dialog.Y + TopSeparatorOffsetY),
            _artwork.ConstructionBackground.Image,
            new Rect(RightPanelOffsetX, SeparatorSourceY, rightPartWidth, SeparatorHeight));
        ImageOps.Blit(output, new Point(dialog.X + RightPanelOffsetX, dialog.Y + BottomSeparatorOffsetY),
            _artwork.ConstructionBackground.Image,
            new Rect(RightPanelOffsetX, SeparatorSourceY, rightPartWidth, SeparatorHeight));

        ImageOps.Blit(output, new Point(NameArea.X, dialog.Y), _artwork.StatusBar.Image,
            new Rect(NameBarSourceX, 0, rightPartWidth, _artwork.StatusBar.Height));
    }

    /// <summary>
    /// Writes the castle name into the middle of the name bar, <c>drawCastleName()</c> (<c>:648-655</c>).
    /// </summary>
    /// <remarks>
    /// 参考先 <c>fitToOneRow()</c> 把过长的名字截断，再按实际宽度居中，垂直位置是名字区上沿加 2。
    /// </remarks>
    /// <param name="output">Frame the name goes into.</param>
    /// <param name="castleName">Already resolved name, see <c>CastleDetailsLabels.GetCastleName()</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> or <paramref name="castleName"/> is <see langword="null"/>.</exception>
    public void PaintName(PixelBuffer output, string castleName)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(castleName);

        Rect nameArea = NameArea;
        Text text = new(castleName, FontType.NormalWhite());

        text.FitToOneRow(nameArea.Width);
        text.DrawInRoi(nameArea.X + ((nameArea.Width - text.Width()) / 2), nameArea.Y + 2, output, nameArea);
    }

    /// <summary>
    /// Creates, places and draws the SET SPELLS button, <c>:657-662</c>.
    /// </summary>
    /// <remarks>
    /// 板子只切一次（切一次要读三份素材，没必要每帧重做），但按钮每帧都要重画：窗口每帧从背景开始整块重绘，
    /// 阴影也得跟着重画，所以每次调用都走一遍 <see cref="StandardWindow.RenderButton"/>。
    /// </remarks>
    /// <param name="output">Frame the button goes into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void PaintSpellsButton(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _spellsPlate ??= CreateTextSprite(SpellsButtonText);
        Spells ??= new ButtonSprite(_spellsPlate.Value.Released, _spellsPlate.Value.Pressed);

        _window.RenderButton(Spells, output, _spellsPlate.Value.Released, _spellsPlate.Value.Pressed,
            SpellsButtonOffset, WindowPadding.TopCenter);
    }

    /// <summary>
    /// Creates, places and draws the RESTRICT button, <c>:685-689</c>.
    /// </summary>
    /// <remarks>
    /// 参考用 <c>CENTER_CENTER</c> 而不是顶对齐，正是为了让各国语言里宽窄不一的按钮仍然居中。
    /// </remarks>
    /// <param name="output">Frame the button goes into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void PaintRestrictButton(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _restrictPlate ??= CreateTextSprite(RestrictButtonText);
        Restrict ??= new ButtonSprite(_restrictPlate.Value.Released, _restrictPlate.Value.Pressed);

        _window.RenderButton(Restrict, output, _restrictPlate.Value.Released, _restrictPlate.Value.Pressed,
            RestrictButtonOffset, WindowPadding.CenterCenter);
    }

    /// <summary>
    /// Draws the two reset buttons, OKAY and EXIT, and then closes the status bar between them,
    /// <c>:770-798</c>.
    /// </summary>
    /// <remarks>
    /// 状态栏必须最后画：它压在两个按钮之间，画早了会被按钮盖住。参考只取 <c>CASLBAR</c> 的左右两头，
    /// 各占一半宽度，中间那段是原图里用不上的长度。
    /// </remarks>
    /// <param name="output">Frame the buttons and the bar go into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void PaintBottomButtons(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        Point resetOffset = new(ResetButtonOffsetX, ResetButtonOffsetY);

        ResetName ??= new ButtonSprite(_artwork.ResetReleased, _artwork.ResetPressed);
        ResetArmy ??= new ButtonSprite(_artwork.ResetReleased, _artwork.ResetPressed);

        _window.RenderButton(ResetName, output, _artwork.ResetReleased, _artwork.ResetPressed, resetOffset,
            WindowPadding.TopRight);
        _window.RenderButton(ResetArmy, output, _artwork.ResetReleased, _artwork.ResetPressed, resetOffset,
            WindowPadding.BottomRight);

        Rect statusBar = StatusBarArea;

        Okay ??= new ButtonSprite(_artwork.OkayReleased, _artwork.OkayPressed);
        Okay.SetPosition(statusBar.X, statusBar.Y);
        Okay.Draw(output);

        Exit ??= new ButtonSprite(_artwork.ExitReleased, _artwork.ExitPressed);
        Exit.SetPosition(statusBar.Right - _artwork.ExitReleased.Width, statusBar.Y);
        Exit.Draw(output);

        int okayWidth = _artwork.OkayReleased.Width;
        int exitWidth = _artwork.ExitReleased.Width;
        int barWidth = statusBar.Width - exitWidth - okayWidth;
        int firstPart = barWidth / 2;
        int secondPart = barWidth - firstPart;

        ImageOps.Blit(output, new Point(statusBar.X + okayWidth, statusBar.Y), _artwork.StatusBar.Image,
            new Rect(0, 0, firstPart, statusBar.Height));
        ImageOps.Blit(output, new Point(statusBar.X + okayWidth + firstPart, statusBar.Y),
            _artwork.StatusBar.Image, new Rect(_artwork.StatusBar.Width - secondPart, 0, secondPart,
                statusBar.Height));
    }

    /// <summary>
    /// Cuts the plate of a text adapted button, once per label, from <c>EMPTY_*_BUTTON</c> and the keep's
    /// own stone texture, <c>getTextAdaptedSprite()</c> of the reference.
    /// </summary>
    /// <param name="text">Label the plate is built around.</param>
    /// <returns>The released and the pressed plate.</returns>
    private (Sprite Released, Sprite Pressed) CreateTextSprite(string text) =>
        ButtonFactory.GetTextAdaptedSprite(text,
            _isEvilInterface ? DialogButtonTemplate.Evil : DialogButtonTemplate.Good, _artwork.StoneBackground);
}
