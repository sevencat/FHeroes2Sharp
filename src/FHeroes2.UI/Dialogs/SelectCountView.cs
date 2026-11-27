using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The clickable, drawable <c>Dialog::SelectCount()</c> window
/// (<c>src/fheroes2/dialog/dialog_selectcount.cpp:79-131</c>).
/// </summary>
/// <remarks>
/// <para>
/// The window is built from <see cref="FrameBox"/> instead of <see cref="StandardWindow"/>: the reference
/// composes a <c>ResizableFrameBox</c> whose height is the header text plus the element, with the button row
/// reserved at the bottom. The laid out chain is
/// </para>
/// <code>
/// headerHeight = header wrapped to 244 pixels, times the line height
/// box          = FrameBox( headerHeight + 10 + 30, showButtons = true )
/// header         drawn at box.Area
/// value        = ValueSelectionElement at ( box.Area.x + 38, box.Area.y + headerHeight + 10 )
/// ok / cancel    laid out by ButtonGroup against box.Area
/// </code>
/// <para>
/// <b>Two input models, one at a time.</b> <see cref="ProcessEvents"/> is the reference's loop: it takes the frame's
/// <see cref="LocalEvent"/>, so typing digits, the wheel, a held arrow and the two right click messages all work.
/// <see cref="SetLeftButton"/> is the older press/release pair for hosts that only map buttons to booleans; it steps
/// the number once per click and nothing else. A caller uses one or the other.
/// </para>
/// <para>
/// <b>Caller widgets.</b> Two optional <see cref="DialogElement"/>s sit above and below the number, each centred and
/// each taking its own height plus <see cref="ElementOffset"/>; the add-troop window puts the creature there, which
/// is how the chosen monster is visible while its count is typed.
/// </para>
/// <para>
/// <b>Not ported</b>: nothing of the reference's loop is missing. The MAX/MIN shortcuts are generated at startup
/// (<see cref="SelectCountShortcuts"/>); the number pad and the range message are requests the host answers.
/// </para>
/// </remarks>
public sealed class SelectCountView
{
    /// <summary>Gap between the header and the number, <c>elementOffset</c> of the reference.</summary>
    public const int ElementOffset = 10;

    /// <summary>Height the layout reserves for the number, <c>selectionAreaHeight</c> of the reference.</summary>
    public const int SelectionAreaHeight = 30;

    /// <summary>Horizontal inset of the number from the text area, <c>38</c> of the reference.</summary>
    public const int SelectionInset = 38;

    /// <summary>Gap between the number and the MIN/MAX buttons, the <c>+ 6</c> of <c>dialog_selectcount.cpp:135</c>.</summary>
    public const int MaxMinGap = 6;

    private readonly SelectCountDialog _dialog;
    private readonly IGlyphSource _headerFont;
    private readonly string _header;
    private readonly FrameBox _box;
    private readonly ValueSelectionElement _value;
    private readonly ButtonGroup _buttons;
    private readonly ButtonSprite? _maxButton;
    private readonly ButtonSprite? _minButton;
    private readonly DialogElement? _topElement;
    private readonly DialogElement? _bottomElement;
    private readonly Point _topOffset;
    private readonly Point _bottomOffset;
    private readonly Rect _topArea;
    private readonly Rect _bottomArea;
    private readonly string _maxTitle;
    private readonly string _maxHelp;
    private readonly string _minTitle;
    private readonly string _minHelp;

    /// <summary>
    /// Digits typed since the number was last set by something else, the <c>typedValueBuf</c> of the reference
    /// (<c>dialog_selectcount.cpp:172</c>).
    /// </summary>
    private string _typedValue = string.Empty;

    private SelectCountView(SelectCountDialog dialog, IGlyphSource headerFont, string header, FrameBox box,
        ValueSelectionElement value, ButtonGroup buttons, SelectCountShortcuts? shortcuts, SelectCountText text,
        DialogElement? topElement, DialogElement? bottomElement, Point topOffset, Point bottomOffset)
    {
        _dialog = dialog;
        _headerFont = headerFont;
        _header = header;
        _box = box;
        _value = value;
        _buttons = buttons;
        _topElement = topElement;
        _bottomElement = bottomElement;
        _topOffset = topOffset;
        _bottomOffset = bottomOffset;

        _topArea = topElement is null ? Rect.Empty : new Rect(topOffset, topElement.Area);
        _bottomArea = bottomElement is null ? Rect.Empty : new Rect(bottomOffset, bottomElement.Area);

        _maxTitle = text.MaxTitle;
        _maxHelp = text.MaxHelp;
        _minTitle = text.MinTitle;
        _minHelp = text.MinHelp;

        if (shortcuts is null)
        {
            return;
        }

        // 参考：selectionBoxArea 右侧 6 像素，两颗按钮同一位置，同一时刻只显示一颗。
        Point origin = new(_value.Area.X + _value.Area.Width + MaxMinGap, _value.Area.Y);

        _maxButton = new ButtonSprite(shortcuts.Max.Released, shortcuts.Max.Pressed, offsetX: origin.X,
            offsetY: origin.Y);

        _minButton = new ButtonSprite(shortcuts.Min.Released, shortcuts.Min.Pressed, offsetX: origin.X,
            offsetY: origin.Y);

        SwitchMaxMinButtons();
    }

    /// <summary>
    /// Builds the window.
    /// </summary>
    /// <param name="dialog">The parameters and the answer.</param>
    /// <param name="buttonArtwork">OK and CANCEL artwork.</param>
    /// <param name="headerFont">Font of the header, <c>FontType::normalWhite()</c> in the reference.</param>
    /// <param name="townWindowFrames">Frames of <c>TOWNWIND.ICN</c>, at least
    /// <see cref="ValueSelectionElement.RequiredFrameCount"/>.</param>
    /// <param name="initialValue">Number shown first, clamped into the range.</param>
    /// <param name="shortcuts">
    /// The MIN/MAX buttons, or <see langword="null"/> to leave them out (the reference always shows them; the port
    /// keeps the window usable when <c>RECRUIT</c> is missing).
    /// </param>
    /// <param name="text">Labels of the two shortcuts; <see cref="SelectCountText.Default"/> when omitted.</param>
    /// <param name="topElement">Widget drawn above the number, or <see langword="null"/>.</param>
    /// <param name="bottomElement">Widget drawn below the number, or <see langword="null"/>.</param>
    /// <returns>
    /// The window, or <see langword="null"/> when an asset is missing - the frame artwork included.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The archive has no message frame, see <see cref="FrameBox.Create"/>.
    /// </exception>
    public static SelectCountView? TryCreate(SelectCountDialog dialog,
        IDialogButtonArtworkSource buttonArtwork, IGlyphSource headerFont, IReadOnlyList<Sprite> townWindowFrames,
        int initialValue = 0, SelectCountShortcuts? shortcuts = null, SelectCountText? text = null,
        DialogElement? topElement = null, DialogElement? bottomElement = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(buttonArtwork);
        ArgumentNullException.ThrowIfNull(headerFont);
        ArgumentNullException.ThrowIfNull(townWindowFrames);

        if (townWindowFrames.Count < ValueSelectionElement.RequiredFrameCount || !FrameBox.IsArtworkAvailable)
        {
            return null;
        }

        int headerHeight = GlyphText.WrapLines(headerFont, dialog.Header, FrameBox.AreaWidth).Count *
                           headerFont.LineHeight;

        int topHeight = topElement?.Area.Height ?? 0;
        int bottomHeight = bottomElement?.Area.Height ?? 0;

        // 参考的 uiAdditionalOffset：两个部件各自多占一个 elementOffset 的竖直空间。
        int additionalOffset = (topHeight > 0 ? ElementOffset : 0) + (bottomHeight > 0 ? ElementOffset : 0);

        FrameBox box = FrameBox.Create(
            headerHeight + ElementOffset + topHeight + SelectionAreaHeight + bottomHeight + additionalOffset,
            showButtons: true);

        Rect active = box.Area;
        int offsetY = active.Y + headerHeight + ElementOffset;

        Point topOffset = new(active.X + ((active.Width - (topElement?.Area.Width ?? 0)) / 2), offsetY);

        if (topElement is not null)
        {
            offsetY += ElementOffset + topHeight;
        }

        Point selectionOrigin = new(active.X + SelectionInset, offsetY);
        ValueSelectionElement value = new(
            dialog.Minimum, dialog.Maximum, initialValue, dialog.Step, selectionOrigin, headerFont,
            townWindowFrames);

        offsetY += value.Area.Height + ElementOffset;

        Point bottomOffset = new(active.X + ((active.Width - (bottomElement?.Area.Width ?? 0)) / 2), offsetY);

        ButtonGroup buttons = ButtonGroup.CreateStandard(
            box.Area, DialogButton.Ok | DialogButton.Cancel, buttonArtwork);

        return new SelectCountView(dialog, headerFont, dialog.Header, box, value, buttons, shortcuts,
            text ?? SelectCountText.Default, topElement, bottomElement, topOffset, bottomOffset);
    }

    /// <summary>The widget above the number, or <see langword="null"/>.</summary>
    public DialogElement? TopElement => _topElement;

    /// <summary>The widget below the number, or <see langword="null"/>.</summary>
    public DialogElement? BottomElement => _bottomElement;

    /// <summary>Where the widget above the number sits, empty when there is none.</summary>
    public Rect TopElementArea => _topArea;

    /// <summary>Where the widget below the number sits, empty when there is none.</summary>
    public Rect BottomElementArea => _bottomArea;

    /// <summary>The parameters and the answer of the window.</summary>
    public SelectCountDialog Dialog => _dialog;

    /// <summary>Text area of the frame, <c>GetArea()</c> of the reference.</summary>
    public Rect Area => _box.Area;

    /// <summary>Bounding box of the frame artwork.</summary>
    public Rect FrameArea => _box.FrameArea;

    /// <summary>The number element.</summary>
    public ValueSelectionElement Value => _value;

    /// <summary>The OK and CANCEL buttons.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>
    /// The MAX button, or <see langword="null"/> when the window was built without <see cref="SelectCountShortcuts"/>.
    /// </summary>
    public ButtonSprite? MaxButton => _maxButton;

    /// <summary>The MIN button, or <see langword="null"/>.</summary>
    public ButtonSprite? MinButton => _minButton;

    /// <summary>Whether the MAX button is the one on screen, <c>buttonMax.isVisible()</c> of the reference.</summary>
    public bool IsMaxVisible => _maxButton is { IsVisible: true };

    /// <summary>Whether the MIN button is the one on screen.</summary>
    public bool IsMinVisible => _minButton is { IsVisible: true };

    /// <summary>
    /// Performs the update that brings the view in line with the input, one frame of the reference's loop
    /// (<c>dialog_selectcount.cpp:186-215</c>).
    /// </summary>
    /// <param name="events">The events of this frame.</param>
    /// <returns>What the frame changed and asked for.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public SelectCountFrame ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        // :189 —— 键盘直输先吃这一帧：敲数字改值，Backspace 归零。
        // 参考读的是 getPressedKeyValue()，端口用 IsAnyKeyPressed 表示「这一帧有键按下」。
        if (events.IsAnyKeyPressed
            && IntegerValueTyping.Process(_dialog.Minimum, _dialog.Maximum, ref _typedValue,
                events.PressedKeyValue) is { } typed)
        {
            _value.Value = typed;
            SwitchMaxMinButtons();

            return new SelectCountFrame(true);
        }

        // :202 —— 滚轮、上下键（含长按重复）、数字框的两种请求。
        ValueSelectionFrame selection = _value.ProcessEvents(events);

        if (selection.Changed)
        {
            // 参考在控件自己改了值之后清缓冲，否则下一次敲数字会接在旧数字后面。
            _typedValue = string.Empty;
            SwitchMaxMinButtons();

            return new SelectCountFrame(true);
        }

        if (selection.Request != ValueSelectionRequest.None)
        {
            return new SelectCountFrame(false, Request: selection.Request);
        }

        // :197-210 —— 两颗快捷键：点上就把值推到一端，右键弹各自那句说明。
        if (IsMaxVisible)
        {
            _maxButton!.UpdatePressState(events);

            if (events.MouseClickLeft(_maxButton.Area))
            {
                return JumpTo(_dialog.Maximum);
            }

            if (events.IsMouseRightButtonPressedInArea(_maxButton.Area))
            {
                return new SelectCountFrame(false, Popup: new DialogPopup(_maxTitle, _maxHelp));
            }
        }

        if (IsMinVisible)
        {
            _minButton!.UpdatePressState(events);

            if (events.MouseClickLeft(_minButton.Area))
            {
                return JumpTo(_dialog.Minimum);
            }

            if (events.IsMouseRightButtonPressedInArea(_minButton.Area))
            {
                return new SelectCountFrame(false, Popup: new DialogPopup(_minTitle, _minHelp));
            }
        }

        // :213 —— 按钮最后才问，与参考同序。
        DialogButton answer = Apply(_buttons.ProcessEvents(events));

        if (answer != DialogButton.None)
        {
            return new SelectCountFrame(false, answer);
        }

        // :177-188 —— 调用方部件：左键释放或右键按下落在自己的矩形里就交给它自己处理。
        if (ProcessWidget(_topElement, _topArea, events) is { } topResult)
        {
            return topResult;
        }

        return ProcessWidget(_bottomElement, _bottomArea, events) ?? new SelectCountFrame(false);
    }

    /// <summary>
    /// Hands the frame to one caller widget, the two <c>processEvents()</c> calls of
    /// <c>dialog_selectcount.cpp:177-188</c>.
    /// </summary>
    /// <remarks>
    /// 参考只在这两种事件上转给部件（左键释放、右键按下），所以部件不会自己吃到无关的帧。返回值把它要的重绘与弹窗
    /// 原样带出去；没有事件落在它上面时返回 <see langword="null"/>，让调用方去问下一个部件。
    /// </remarks>
    /// <param name="element">The widget, or <see langword="null"/>.</param>
    /// <param name="area">Where it sits, in screen coordinates.</param>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>The frame the element asked for, or <see langword="null"/> when the frame is not for it.</returns>
    private static SelectCountFrame? ProcessWidget(DialogElement? element, Rect area, LocalEvent events)
    {
        if (element is null || area.IsEmpty)
        {
            return null;
        }

        if (!events.IsMouseLeftButtonReleasedInArea(area) && !events.IsMouseRightButtonPressedInArea(area))
        {
            return null;
        }

        DialogElementResult result = element.ProcessEvents(events, area.Position);

        return result.RequiresRedraw || result.Popup is not null
            ? new SelectCountFrame(result.RequiresRedraw, Popup: result.Popup)
            : new SelectCountFrame(false);
    }

    /// <summary>
    /// Sets the number to one end of the range and clears the typed digits, the two <c>setValue()</c> plus
    /// <c>typedValueBuf.clear()</c> pairs of <c>dialog_selectcount.cpp:199-208</c>.
    /// </summary>
    /// <param name="value">The minimum or the maximum.</param>
    /// <returns>The frame that asks for a repaint.</returns>
    private SelectCountFrame JumpTo(int value)
    {
        _value.Value = value;
        _typedValue = string.Empty;
        SwitchMaxMinButtons();

        return new SelectCountFrame(true);
    }

    /// <summary>
    /// Shows the button that makes sense for the current value, <c>SwitchMaxMinButtons()</c>
    /// (<c>dialog_selectcount.cpp:63-77</c>): MAX at the bottom of the range, MIN everywhere else.
    /// </summary>
    private void SwitchMaxMinButtons()
    {
        if (_maxButton is null || _minButton is null)
        {
            return;
        }

        if (_value.Value <= _dialog.Minimum)
        {
            _minButton.Hide();
            _maxButton.Show();
        }
        else
        {
            _maxButton.Hide();
            _minButton.Show();
        }
    }

    /// <summary>
    /// Writes the number the number pad returned, the <c>valueSelectionElement.setValue( temp )</c> of <c>:174</c>.
    /// </summary>
    /// <param name="value">The number the author typed; it is clamped into the range.</param>
    public void SetValue(int value)
    {
        _value.Value = value;
        _typedValue = string.Empty;
        SwitchMaxMinButtons();
    }

    /// <summary>
    /// The message a right click on the number box shows, and the answer to the number pad's error,
    /// <see cref="ValueSelectionElement.RangeMessage"/>.
    /// </summary>
    public string RangeMessage => _value.RangeMessage;

    /// <summary>
    /// A left button transition over the window.
    /// </summary>
    /// <remarks>
    /// The number is fed first: its arrows are 20x10 and do not overlap the button row, so the order only
    /// matters for the pressed state. On OK the chosen number is written into
    /// <see cref="SelectCountDialog.Result"/>, on CANCEL the result stays <see langword="null"/>.
    /// </remarks>
    /// <param name="down">Whether the button went down.</param>
    /// <param name="point">Where the cursor is.</param>
    /// <returns>The pressed button on release, or <see cref="DialogButton.None"/> when nothing was pressed.</returns>
    public DialogButton SetLeftButton(bool down, Point point)
    {
        if (_value.SetLeftButton(down, point))
        {
            _typedValue = string.Empty;
        }

        _buttons.SetCursor(point);

        return Apply(_buttons.SetLeftButton(down));
    }

    /// <summary>
    /// Answers a standard button hot key, the <c>Game::HotKeyPressEvent()</c> half of
    /// <c>ButtonGroup::processEvents()</c> the reference's loop runs.
    /// </summary>
    /// <param name="hotKey">Key that was pressed.</param>
    /// <returns>
    /// The button the key stands for, or <see cref="DialogButton.None"/>; OKAY writes the number into
    /// <see cref="SelectCountDialog.Result"/>, CANCEL leaves it <see langword="null"/>.
    /// </returns>
    public DialogButton ProcessHotKey(ButtonHotKey hotKey) => Apply(_buttons.ProcessHotKey(hotKey));

    /// <summary>Writes an answer into the dialog, the two <c>Choose()</c> calls of the click path.</summary>
    /// <param name="answer">The button that was pressed, or <see cref="DialogButton.None"/>.</param>
    /// <returns><paramref name="answer"/>.</returns>
    private DialogButton Apply(DialogButton answer)
    {
        if (answer == DialogButton.Ok)
        {
            _dialog.Choose(_value.Value);
        }
        else if (answer == DialogButton.Cancel)
        {
            _dialog.Choose(null);
        }

        return answer;
    }

    /// <summary>Draws the frame, the header, the number and the buttons, in the order of the reference.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _box.Draw(target);
        GlyphText.DrawMultiline(_headerFont, target, _box.Area.Position, _header, FrameBox.AreaWidth);

        _topElement?.Draw(target, _topOffset);
        _value.Draw(target);
        _bottomElement?.Draw(target, _bottomOffset);

        _maxButton?.Draw(target);
        _minButton?.Draw(target);

        _buttons.Draw(target);
        _buttons.DrawShadows(target);
    }
}

/// <summary>What one frame of <see cref="SelectCountView.ProcessEvents"/> changed and asked for.</summary>
/// <param name="Changed">Whether the number changed, i.e. whether the window has to be repainted.</param>
/// <param name="Answer">The button a click or a hot key reported, or <see cref="DialogButton.None"/>.</param>
/// <param name="Request">
/// The dialog the reference opens from here: the number pad or the range message, or
/// <see cref="ValueSelectionRequest.None"/>.
/// </param>
/// <param name="Popup">The message a right click on a MIN/MAX button asked for, or <see langword="null"/>.</param>
public readonly record struct SelectCountFrame(
    bool Changed, DialogButton Answer = DialogButton.None, ValueSelectionRequest Request = ValueSelectionRequest.None,
    DialogPopup? Popup = null);

/// <summary>
/// The strings the count window draws for its two shortcuts, <c>_( "MAX" )</c> and the two help texts of
/// <c>dialog_selectcount.cpp:200-210</c>.
/// </summary>
public sealed record SelectCountText
{
    /// <summary>Title of the MAX help, <c>_( "MAX" )</c>.</summary>
    public required string MaxTitle { get; init; }

    /// <summary>Body of the MAX help, <c>_( "Click to select the maximum amount." )</c>.</summary>
    public required string MaxHelp { get; init; }

    /// <summary>Title of the MIN help, <c>_( "MIN" )</c>.</summary>
    public required string MinTitle { get; init; }

    /// <summary>Body of the MIN help, <c>_( "Click to select the minimum amount." )</c>.</summary>
    public required string MinHelp { get; init; }

    /// <summary>The untranslated strings of the reference.</summary>
    public static SelectCountText Default { get; } = new()
    {
        MaxTitle = "MAX",
        MaxHelp = "Click to select the maximum amount.",
        MinTitle = "MIN",
        MinHelp = "Click to select the minimum amount.",
    };
}
