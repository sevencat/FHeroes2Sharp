using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The crown plates a colour picker draws, <c>Assets::getImage( ICN::BRCREST, … )</c> of
/// <c>Dialog::selectPlayerColor()</c> (<c>dialog_selectitems.cpp:2131-2155</c>).
/// </summary>
public interface IColorCrestSprites
{
    /// <summary>The plate of one colour, <c>BRCREST</c> frames 0..6 by the colour's index.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The plate, or <see langword="null"/> when the frame is missing.</returns>
    Sprite? GetCrown(PlayerColor color);

    /// <summary>The neutral plate, <c>BRCREST</c> frame 7.</summary>
    /// <returns>
    /// The plate, or <see langword="null"/> when the archive has no such frame — the 1996 data ships seven frames
    /// (0..6), so the neutral cell then only draws its frame and its name.
    /// </returns>
    Sprite? GetNeutralCrown();

    /// <summary>The frame drawn around the chosen colour, <c>BRCREST</c> frame 6.</summary>
    /// <returns>The frame, or <see langword="null"/> when the frame is missing.</returns>
    Sprite? GetSelectionFrame();

    /// <summary>The same frame seen through the gray + darkening palettes, the unselected one.</summary>
    /// <returns>The frame, or <see langword="null"/> when the frame is missing.</returns>
    Sprite? GetIdleFrame();
}

/// <summary>
/// The colour picker of <c>Dialog::selectPlayerColor()</c> (<c>dialog_selectitems.cpp:2109-2185</c>): one plate per
/// available colour plus a neutral one, OKAY and CANCEL.
/// </summary>
/// <remarks>
/// <para>
/// 布局照参考：窗口宽 <c>max(250, 30 + 70 * (颜色数 + 1))</c>、高 160；标题「Select color:」在活动区顶部，王冠排
/// 从 <c>area.y + 40</c> 起、每格横进 70 像素，第一格是「中立」，之后按 <c>Color::IndexToColor</c> 的顺序只放
/// <paramref name="availableColors"/> 里有的颜色。每格下面一行小字写颜色名。
/// </para>
/// <para>
/// <b>选中的语义与其它列表不同</b>：取消返回**原色**（不是「没有」），确定返回当前高亮的色，而「中立」这一格
/// 返回 <see cref="PlayerColor.None"/>——调用方据此把元数据里那条**删掉**。
/// </para>
/// </remarks>
public sealed class PlayerColorSelectionWindow
{
    /// <summary>Horizontal step between two plates, <c>stepX = 70</c>.</summary>
    public const int ColumnStepX = 70;

    /// <summary>Smallest window width, <c>minWidth = 250</c>.</summary>
    public const int MinimumWidth = 250;

    /// <summary>Width the plates need, <c>colorsWidth = 30 + stepX * (颜色数 + 1)</c>.</summary>
    public const int ColorsInset = 30;

    /// <summary>Height of the window, the <c>160</c> of the reference.</summary>
    public const int WindowHeight = 160;

    /// <summary>Gap between the top of the active area and the plates, <c>area.y + 40</c>.</summary>
    public const int PlateOffsetY = 40;

    /// <summary>Left inset of the first plate, <c>area.x + 20</c>.</summary>
    public const int PlateInsetX = 20;

    /// <summary>Inset of the crown inside its plate, the reference's <c>+ 4</c> blit.</summary>
    public const int CrownInset = 4;

    /// <summary>Distance from the bottom of a plate to its name, the reference's <c>+ 4</c>.</summary>
    public const int NameOffsetY = 4;

    /// <summary>The heading, <c>_( "Select color:" )</c>.</summary>
    public const string Header = "Select color:";

    private readonly List<(PlayerColor Color, Rect Area)> _cells = [];
    private readonly PlayerColor _original;
    private readonly IColorCrestSprites _sprites;
    private readonly IGlyphSource _headerFont;
    private readonly IGlyphSource _nameFont;
    private readonly StandardWindow _window;
    private readonly Sprite _selectionFrame;
    private readonly Sprite _idleFrame;
    private readonly Sprite? _neutralCrown;
    private readonly ButtonGroup _buttons;

    private PlayerColor _current;

    private PlayerColorSelectionWindow(PlayerColor original, PlayerColor current, IColorCrestSprites sprites,
        IGlyphSource headerFont, IGlyphSource nameFont, StandardWindow window, Sprite selectionFrame,
        Sprite idleFrame, Sprite? neutralCrown, ButtonGroup buttons, List<(PlayerColor, Rect)> cells)
    {
        _original = original;
        _current = current;
        _sprites = sprites;
        _headerFont = headerFont;
        _nameFont = nameFont;
        _window = window;
        _selectionFrame = selectionFrame;
        _idleFrame = idleFrame;
        _neutralCrown = neutralCrown;
        _buttons = buttons;
        _cells = cells;
    }

    /// <summary>
    /// Builds the window, the reference's <c>selectPlayerColor()</c>.
    /// </summary>
    /// <param name="original">The colour the object owns now; CANCEL answers it.</param>
    /// <param name="availableColors">The colours the map lets objects be owned by.</param>
    /// <param name="sprites">The crown plates and the two frames.</param>
    /// <param name="buttonArtwork">OKAY and CANCEL artwork.</param>
    /// <param name="headerFont">Font of the title, <c>FontType::normalYellow()</c>.</param>
    /// <param name="nameFont">Font of the names, <c>FontType::smallWhite()</c>.</param>
    /// <param name="screen">Screen to centre the window on, <see cref="ScreenBuffer.DefaultSize"/> when omitted.</param>
    /// <returns>The window, or <see langword="null"/> when a plate is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sprites"/>, <paramref name="buttonArtwork"/>, a font or either frame is <see langword="null"/>.</exception>
    public static PlayerColorSelectionWindow? TryCreate(PlayerColor original, PlayerColor availableColors,
        IColorCrestSprites sprites, IDialogButtonArtworkSource buttonArtwork, IGlyphSource headerFont,
        IGlyphSource nameFont, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(buttonArtwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(nameFont);

        if (sprites.GetSelectionFrame() is not { } selectionFrame || sprites.GetIdleFrame() is not { } idleFrame)
        {
            return null;
        }

        Sprite? neutral = sprites.GetNeutralCrown();

        int count = PlayerColors.Count(availableColors);
        int colorsWidth = ColorsInset + (ColumnStepX * (count + 1));
        Size size = new(Math.Max(MinimumWidth, colorsWidth), WindowHeight);

        StandardWindow window = StandardWindow.CreateCentered(size, renderBackground: true, screen);
        Rect area = window.ActiveArea;
        int startX = area.X + PlateInsetX + ((area.Width - colorsWidth) / 2);

        List<(PlayerColor, Rect)> cells =
        [
            (PlayerColor.None, new Rect(startX, area.Y + PlateOffsetY, selectionFrame.Width,
                selectionFrame.Height)),
        ];

        int x = startX + ColumnStepX;

        for (int index = 0; index < 7; ++index)
        {
            var color = (PlayerColor)(1 << index);

            if ((availableColors & color) == 0)
            {
                continue;
            }

            cells.Add((color, new Rect(x, area.Y + PlateOffsetY, selectionFrame.Width, selectionFrame.Height)));
            x += ColumnStepX;
        }

        ButtonGroup buttons = ButtonGroup.CreateStandard(area, DialogButton.Ok | DialogButton.Cancel,
            buttonArtwork);

        return new PlayerColorSelectionWindow(original, original, sprites, headerFont, nameFont, window,
            selectionFrame, idleFrame, neutral, buttons, cells);
    }

    /// <summary>The window the plates sit in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The active area.</summary>
    public Rect ActiveArea => _window.ActiveArea;

    /// <summary>The plates, in the order they are drawn: neutral first, then the available colours.</summary>
    public IReadOnlyList<(PlayerColor Color, Rect Area)> Cells => _cells;

    /// <summary>The colour that is highlighted now, <c>selectedColorIndex</c> of the reference.</summary>
    public PlayerColor Current => _current;

    /// <summary>The OKAY and CANCEL buttons.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>
    /// The colour the window answered, or <see langword="null"/> while it is still open.
    /// </summary>
    /// <remarks>
    /// 取消交回**原色**（不是「没有」）：参考的 <c>CANCEL</c> 那一支就是 <c>return color</c>。
    /// </remarks>
    public PlayerColor? Answer { get; private set; }

    /// <summary>Whether the window has closed, <see cref="Answer"/> being final.</summary>
    public bool IsClosed => Answer is not null;

    /// <summary>
    /// A left button transition: a click on a plate highlights it, a click on a button answers.
    /// </summary>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns>The answer on release, or <see langword="null"/> while nothing is decided.</returns>
    public PlayerColor? SetLeftButton(bool down, Point point)
    {
        _buttons.SetCursor(point);

        if (down)
        {
            foreach ((PlayerColor color, Rect area) in _cells)
            {
                if (area.Contains(point))
                {
                    _current = color;

                    break;
                }
            }
        }

        DialogButton clicked = _buttons.SetLeftButton(down);

        if (clicked == DialogButton.Ok)
        {
            Answer = _current;
        }
        else if (clicked == DialogButton.Cancel)
        {
            Answer = _original;
        }

        return Answer;
    }

    /// <summary>Answers a standard button hot key.</summary>
    /// <param name="hotKey">Key that was pressed.</param>
    /// <returns>The answer, or <see langword="null"/> when the key is not one of the two.</returns>
    public PlayerColor? ProcessHotKey(ButtonHotKey hotKey)
    {
        DialogButton value = _buttons.ProcessHotKey(hotKey);

        if (value == DialogButton.Ok)
        {
            Answer = _current;
        }
        else if (value == DialogButton.Cancel)
        {
            Answer = _original;
        }

        return Answer;
    }

    /// <summary>Draws the window, the title, the plates and the buttons.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        Rect area = _window.ActiveArea;

        int width = GlyphText.Measure(_headerFont, Header);
        GlyphText.DrawLine(_headerFont, target,
            new Point(area.X + ((area.Width - width) / 2), area.Y + 10), Header);

        foreach ((PlayerColor color, Rect cell) in _cells)
        {
            Sprite? crown = color == PlayerColor.None ? _neutralCrown : _sprites.GetCrown(color);

            bool selected = color == _current;

            ImageOps.BlitSprite(target, cell.Position, selected ? _selectionFrame : _idleFrame);

            if (crown is not null)
            {
                ImageOps.BlitSprite(target,
                    new Point(cell.X + CrownInset, cell.Y + CrownInset), crown);
            }

            string name = color == PlayerColor.None ? "Neutral" : PlayerColors.GetName(color);
            int nameWidth = GlyphText.Measure(_nameFont, name);

            GlyphText.DrawLine(_nameFont, target,
                new Point(cell.X + ((cell.Width - nameWidth) / 2), cell.Bottom + NameOffsetY), name);
        }

        _buttons.DrawShadows(target);
        _buttons.Draw(target);
    }
}
