using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Kingdom;
using FHeroes2.UI.Dialogs;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Battle;

/// <summary>
/// The six line battle log that opens over the battlefield, a port of <c>Battle::StatusListBox</c>
/// (<c>battle_interface.cpp:803-980</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>布局</b>：窗口高 <c>6 * 17 - 4 = 98</c> 行内容，四周再由 <c>FrameBorder</c> 加上 16 像素的边，
/// 所以整块 130 行，底边正好贴住状态栏的顶边（<c>_border.SetPosition( px, py - total - 32, 640 - 32, total )</c>）。
/// 内容区因此是 608 宽，右侧留 16 列给 <c>DROPLISL</c> 的滚动条，行宽 592 = <c>battleLogElementWidth</c>。
/// </para>
/// <para>
/// <b>滚动条</b>：上下两个按钮是 <c>DROPLISL</c> 的 6/7 与 8/9，滑块是 13 号按内容长度拉伸的，滑块槽两侧的
/// 装饰段是 10（上）、11（中，按槽长重复）与 12（下）。参考在 <c>SetPosition</c> 里先把它藏起来，
/// 日志合上时也不画——端口用「没有滑块图」表达同一件事。
/// </para>
/// <para>
/// <b>断行</b>：<c>AddMessage()</c> 只切一次——找到「最长的、放得下的、以空格结尾的前缀」放进第一行，
/// 剩下的整段作为第二行（<c>battle_interface.cpp:849-890</c>）。单个词就比行宽还长时用
/// <see cref="GlyphText.FitToOneRow"/> 截断并补省略号，然后整段结束。
/// </para>
/// </remarks>
public sealed class BattleStatusLog : ListBox<string>
{
    /// <summary>Rows the window shows at once, <c>maxElementsInBattleLog</c> (<c>battle_interface.cpp:107</c>).</summary>
    public const int MaxElements = 6;

    /// <summary>Height of one row, the line height of the normal font (<c>battle_interface.cpp:110</c>).</summary>
    public const int ElementHeight = 17;

    /// <summary>Rows the last element overlaps the frame by, <c>battleLogLastElementOffset</c>.</summary>
    public const int LastElementOffset = 4;

    /// <summary>Width of one row, <c>battleLogElementWidth</c> (<c>battle_interface.cpp:114</c>).</summary>
    public const int ElementWidth = ScreenBuffer.DefaultWidth - (FrameBorder.DefaultBorderWidth * 2) - 16;

    /// <summary>Inset of the scroll bar from the right edge of the content area, <c>- 12</c> in <c>SetPosition()</c>.</summary>
    private const int ScrollBarInset = 12;

    /// <summary>Distance of the step-up button above the content area, <c>area.y - 10</c>.</summary>
    private const int ScrollButtonUpOffset = 10;

    /// <summary>Distance of the step-down button above the bottom of the content area, <c>area.height - 11</c>.</summary>
    private const int ScrollButtonDownOffset = 11;

    /// <summary>Gap between the two buttons and the groove, <c>- 7</c> in <c>SetPosition()</c>.</summary>
    private const int ScrollGrooveGap = 7;

    /// <summary>Width of the groove, <c>12</c> in <c>setScrollBarArea()</c>.</summary>
    private const int ScrollGrooveWidth = 12;

    /// <summary>Horizontal inset of the groove inside the button column, <c>ax + 5</c>.</summary>
    private const int ScrollGrooveInsetX = 5;

    /// <summary>Vertical inset of the groove under the step-up button, <c>+ 3</c>.</summary>
    private const int ScrollGrooveInsetY = 3;

    private readonly IViewWorldSpriteSource _sprites;
    private readonly IGlyphSource? _font;
    private readonly List<string> _messages = [];

    private Rect _frame;
    private Rect _area;
    private int _buttonColumn;
    private int _scrollAreaLength;
    private bool _isOpen;

    /// <summary>Creates an empty, closed log, <c>StatusListBox</c> of the reference before <c>SetPosition()</c>.</summary>
    /// <param name="sprites">Where the <c>TEXTBAK2</c> and <c>DROPLISL</c> frames come from.</param>
    /// <param name="font">
    /// The font the rows are measured and drawn with, <c>FontType::normalWhite()</c>;
    /// <see langword="null"/> keeps the messages but draws no text.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="sprites"/> is <see langword="null"/>.</exception>
    public BattleStatusLog(IViewWorldSpriteSource sprites, IGlyphSource? font)
        : base(Point.Zero)
    {
        ArgumentNullException.ThrowIfNull(sprites);

        _sprites = sprites;
        _font = font;
    }

    /// <summary>The lines the log holds, <c>StatusListBox::_messages</c>.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>Outer rectangle of the window, border included, <c>_border.GetRect()</c>.</summary>
    public Rect Frame => _frame;

    /// <summary>Rectangle the rows and the scroll bar go in, <c>_border.GetArea()</c>.</summary>
    public Rect Area => _area;

    /// <summary>Whether the log is expanded over the battlefield, <c>isOpenLog()</c>.</summary>
    public bool IsOpen => _isOpen;


    /// <summary>
    /// Places the window so that its bottom edge sits on the status bar, <c>StatusListBox::SetPosition()</c>
    /// (<c>battle_interface.cpp:810-840</c>).
    /// </summary>
    /// <param name="px">Left edge of the window.</param>
    /// <param name="py">Row the bottom of the window rests on, i.e. the top of the status bar.</param>
    public void SetPosition(int px, int py)
    {
        int border = FrameBorder.DefaultBorderWidth;
        int totalElementHeight = (MaxElements * ElementHeight) - LastElementOffset;

        _frame = new Rect(px, py - totalElementHeight - (border * 2),
            ScreenBuffer.DefaultWidth - (border * 2), totalElementHeight);
        _area = new Rect(_frame.X + border, _frame.Y + border, _frame.Width, _frame.Height);

        SetTopLeft(_area.Position);
        SetAreaMaxItems(MaxElements);

        _buttonColumn = _area.X + _area.Width - ScrollBarInset;

        SetScrollButtonUp(CreateButton(6, 7, new Point(_buttonColumn, _area.Y - ScrollButtonUpOffset)));
        SetScrollButtonDown(CreateButton(8, 9,
            new Point(_buttonColumn, _area.Y + _area.Height - ScrollButtonDownOffset)));

        Rect up = ButtonPageUp?.Area ?? default;
        Rect down = ButtonPageDown?.Area ?? default;

        _scrollAreaLength = down.Y - (up.Y + up.Height) - ScrollGrooveGap;

        if (_scrollAreaLength > 0)
        {
            SetScrollBarArea(new Rect(_buttonColumn + ScrollGrooveInsetX, up.Y + up.Height + ScrollGrooveInsetY,
                ScrollGrooveWidth, _scrollAreaLength));
        }

        // SetAreaItems() 用的是 area.height + battleLogLastElementOffset：最后一行会盖住边框的两三行，
        // 这是有意的，日志窗口本来就贴着状态栏。
        SetAreaItems(new Rect(_area.X, _area.Y, _area.Width - 16, _area.Height + LastElementOffset));

        SetListContent(_messages);
        RebuildSlider();
    }

    /// <summary>Expands or collapses the window, <c>SetOpenLog()</c> (<c>battle_interface.cpp:964-967</c>).</summary>
    /// <param name="open">Whether the log is shown.</param>
    /// <remarks>
    /// 参考只在打开时画这个窗口（<c>RedrawInterface()</c> 的 <c>isOpenLog()</c> 分支），滑块则是按
    /// 「合上就藏起来」处理；端口把两件事合成一处：合上时不持有滑块图，打开时按当前行数重做一张。
    /// </remarks>
    public void SetOpen(bool open)
    {
        _isOpen = open;

        RebuildSlider();
    }

    /// <summary>
    /// Appends one message, wrapped into at most two lines, <c>StatusListBox::AddMessage()</c>
    /// (<c>battle_interface.cpp:847-905</c>).
    /// </summary>
    /// <param name="message">The message; an empty one is ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public void AddMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Length == 0)
        {
            return;
        }

        AppendMessage(message);

        SetListContent(_messages);

        // The reference jumps to the last line so the newest message is always visible.
        SetCurrent(_messages.Count - 1);

        RebuildSlider();
    }

    /// <inheritdoc/>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _ = destination; // The frame is laid out by SetPosition(), not by the list origin.

        if (_frame.IsEmpty)
        {
            return;
        }

        if (_sprites.GetSprite(IcnType.Textbak2, 0) is { } artwork)
        {
            FrameBorder.RenderOther(output, artwork.Image, _frame);
        }

        Rect up = ButtonPageUp?.Area ?? default;
        Rect down = ButtonPageDown?.Area ?? default;

        if (up.Height == 0 || down.Height == 0)
        {
            return;
        }

        int top = up.Y + up.Height;
        int height = down.Y - top;

        if (_sprites.GetSprite(IcnType.Droplisl, 11) is { } middle && middle.Image.Height > 0)
        {
            for (int i = 0; i < height / middle.Image.Height; ++i)
            {
                ImageOps.Blit(output, new Point(_buttonColumn, top + (middle.Image.Height * i)),
                    middle.Image, middle.Image.Bounds);
            }
        }

        if (_sprites.GetSprite(IcnType.Droplisl, 10) is { } topCap)
        {
            ImageOps.Blit(output, new Point(_buttonColumn, top), topCap.Image, topCap.Image.Bounds);
        }

        if (_sprites.GetSprite(IcnType.Droplisl, 12) is { } bottomCap)
        {
            ImageOps.Blit(output, new Point(_buttonColumn, down.Y - bottomCap.Image.Height),
                bottomCap.Image, bottomCap.Image.Bounds);
        }
    }

    /// <inheritdoc/>
    public override void RedrawItem(string item, int x, int y, bool current, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(output);

        _ = current; // The reference paints every log row the same way.

        if (_font is null)
        {
            return;
        }

        GlyphText.DrawLine(_font, output, new Point(x, y), item);
    }

    /// <inheritdoc/>
    public override void ActionCurrentUp()
    {
        // Do nothing, as in the reference.
    }

    /// <inheritdoc/>
    public override void ActionCurrentDn()
    {
        // Do nothing, as in the reference.
    }

    /// <inheritdoc/>
    public override void ActionListSingleClick(string item) => _ = item;

    /// <inheritdoc/>
    public override void ActionListDoubleClick(string item) => _ = item;

    /// <inheritdoc/>
    public override void ActionListPressRight(string item) => _ = item;


    /// <summary>
    /// Splits <paramref name="message"/> the way <c>AddMessage()</c> does and appends the pieces.
    /// </summary>
    /// <param name="message">The message to append.</param>
    private void AppendMessage(string message)
    {
        if (_font is null)
        {
            _messages.Add(message);

            return;
        }

        string text = message;

        // -1 is std::string::npos: no split position chosen yet.
        int position = -1;

        while (GlyphText.Measure(_font, text) > ElementWidth)
        {
            position = position < 0
                ? text.LastIndexOf(GlyphText.SpaceCharacter)
                : text.LastIndexOf(GlyphText.SpaceCharacter, position - 1);

            if (position < 0)
            {
                // A single word wider than the row: cut it with the trailing ellipsis.
                int next = text.IndexOf(GlyphText.SpaceCharacter);
                string cut = GlyphText.FitToOneRow(_font, text, ElementWidth);

                if (next < 0)
                {
                    text = cut;
                }
                else
                {
                    _messages.Add(cut);
                    text = text[(next + 1)..];

                    // The rest of the phrase is not split any more.
                    position = -1;
                }

                break;
            }

            text = message[..position];
        }

        if (position < 0)
        {
            _messages.Add(text);
        }
        else
        {
            // The reference splits the original phrase at the space, not at the narrowed copy.
            _messages.Add(message[..position]);
            _messages.Add(message[(position + 1)..]);
        }
    }

    /// <summary>
    /// Builds one of the two step buttons, <c>SetScrollButtonUp()</c>/<c>SetScrollButtonDn()</c> of the reference.
    /// </summary>
    /// <param name="idleFrame">Frame of the released button.</param>
    /// <param name="pressedFrame">Frame of the pressed button.</param>
    /// <param name="position">Top-left corner of the button.</param>
    /// <returns>The button, or <see langword="null"/> when the artwork is missing.</returns>
    private ButtonBase? CreateButton(int idleFrame, int pressedFrame, Point position)
    {
        if (_sprites.GetSprite(IcnType.Droplisl, idleFrame) is not { } idle ||
            _sprites.GetSprite(IcnType.Droplisl, pressedFrame) is not { } pressed)
        {
            return null;
        }

        ButtonSprite button = new(idle, pressed);
        button.SetPosition(position.X, position.Y);

        return button;
    }

    /// <summary>
    /// Rebuilds the thumb for the current row count, <c>generateScrollbarSlider()</c> in <c>SetPosition()</c>
    /// and <c>AddMessage()</c> (<c>battle_interface.cpp:831-836</c>, <c>:899-903</c>).
    /// </summary>
    private void RebuildSlider()
    {
        if (!_isOpen || _scrollAreaLength <= 0 || _sprites.GetSprite(IcnType.Droplisl, 13) is not { } original)
        {
            SetScrollBarImage(null);

            return;
        }

        int width = original.Width;

        SetScrollBarImage(ScrollSlider.Create(original, false, _scrollAreaLength, VisibleItemCount, Size,
            new Rect(0, 0, width, 4), new Rect(0, 4, width, 8)));

        ScrollBar.MoveToIndex(TopId);
    }
}

