using System.Globalization;
using FHeroes2.Assets.Fonts;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>Artwork of <see cref="ResolutionDialog"/>, gathered by the caller from the assets.</summary>
/// <remarks>
/// The same shape as <see cref="HotKeysArtwork"/>: the five frames of <c>SCROLL</c>, the board the window is drawn on
/// and the standard buttons.
/// </remarks>
public sealed class ResolutionArtwork
{
    /// <summary>Frames of <c>SCROLL</c>: up, pressed up, down, pressed down, slider.</summary>
    public required IReadOnlyList<Sprite> ScrollBarFrames { get; init; }

    /// <summary>The board behind the scroll bar groove, <c>ADVBORD</c> frame 0.</summary>
    public required Sprite ScrollBarBackground { get; init; }

    /// <summary>Source of the OK and Cancel buttons.</summary>
    public required IDialogButtonArtworkSource Buttons { get; init; }

    /// <summary>Whether every frame the window reads was supplied.</summary>
    public bool IsComplete => ScrollBarFrames.Count > 4;
}

/// <summary>One row of the resolution list.</summary>
/// <remarks>
/// <see cref="ListBox{T}"/> is constrained to reference types and <see cref="ResolutionInfo"/> is a value type, so
/// the rows are wrapped; the reference stores the structures directly (the <c>ListBox&lt;ResolutionInfo&gt;</c> of
/// <c>dialog_resolution.cpp</c>).
/// </remarks>
/// <param name="Resolution">The resolution of the row.</param>
public sealed record ResolutionRow(ResolutionInfo Resolution);

/// <summary>
/// The resolution chooser, <c>fheroes2::selectResolution()</c> (<c>src/fheroes2/dialog/dialog_resolution.cpp</c>).
/// </summary>
/// <remarks>
/// <para>
/// One list of every resolution the display can be switched to, the selected one repeated in yellow under it, and an
/// OK / Cancel pair. A row reads <c>640 x 480</c> when the window is exactly the frame buffer, or
/// <c>640 x 480 (x2.0)</c> when the window magnifies it; the factor is the integer part plus one decimal, exactly as
/// <c>getResolutionStrings()</c> builds it.
/// </para>
/// <para>
/// A double click or OK answers the chosen resolution, Cancel and Escape answer <see cref="DialogButton.Cancel"/>. A
/// right click asks for a <see cref="DialogPopup"/> explaining the row; <see cref="Show"/> presents it with a message
/// box.
/// </para>
/// </remarks>
public sealed class ResolutionDialog : ListBox<ResolutionRow>
{
    /// <summary>Width of the text area, <c>textAreaWidth</c>.</summary>
    public const int TextAreaWidth = 270;

    /// <summary>Width of the scroll bar area, <c>scrollBarAreaWidth</c>.</summary>
    public const int ScrollBarAreaWidth = 48;

    /// <summary>Left padding of the window, <c>paddingLeftSide</c>.</summary>
    public const int PaddingLeftSide = 24;

    /// <summary>Height of a row, <c>19</c>.</summary>
    public const int ItemHeight = 19;

    /// <summary>Vertical correction of the row text, <c>textOffsetYCorrection</c>.</summary>
    public const int TextOffsetYCorrection = 6;

    /// <summary>The text between the two numbers of a row, <c>" x "</c>.</summary>
    public const string MiddleText = " x ";

    /// <summary>Height the list and the selected line take from the window, <c>112</c>.</summary>
    public const int ListHeightDeduction = 112;

    /// <summary>Top inset of the list, <c>listRoi.y = roi.y + 37</c>.</summary>
    public const int ListTop = 37;

    /// <summary>Top inset of the rows inside the list, <c>listAreaOffsetY</c>.</summary>
    public const int ListAreaOffsetY = 3;

    /// <summary>Height the rows lose at the bottom, <c>listAreaHeightDeduction</c>.</summary>
    public const int ListAreaHeightDeduction = 4;

    /// <summary>Width the rows lose on the right, <c>listAreaWidthDeduction</c>.</summary>
    public const int ListAreaWidthDeduction = 3;

    /// <summary>Distance from the right edge of the window to the scroll bar, <c>35</c>.</summary>
    public const int ScrollBarRightInset = 35;

    /// <summary>Height of a scroll bar button, <c>19</c>.</summary>
    public const int ScrollBarButtonHeight = 19;

    /// <summary>Width of the scroll bar groove, <c>10</c>.</summary>
    public const int ScrollBarGrooveWidth = 10;

    /// <summary>Bottom inset of the scroll bar's down button, <c>15</c>.</summary>
    public const int ScrollBarBottomInset = 15;

    /// <summary>Top inset of the title, <c>roi.y + 16</c>.</summary>
    public const int TitleTop = 16;

    /// <summary>Gap between the list and the selected line, <c>+ 12</c>.</summary>
    public const int SelectedGap = 12;

    /// <summary>Height of the selected line, <c>21</c>.</summary>
    public const int SelectedHeight = 21;

    /// <summary>Smallest number of rows the window makes room for, <c>std::clamp( ..., 11, 22 )</c>.</summary>
    public const int MinVisibleItems = 11;

    /// <summary>Largest number of rows the window makes room for, <c>std::clamp( ..., 11, 22 )</c>.</summary>
    public const int MaxVisibleItems = 22;

    /// <summary>Width of the window, <c>paddingLeftSide + textAreaWidth + scrollBarAreaWidth + 3</c>.</summary>
    public const int WindowWidth = PaddingLeftSide + TextAreaWidth + ScrollBarAreaWidth + 3;

    private readonly StandardWindow _window;
    private readonly ButtonGroup _buttons;
    private readonly ResolutionArtwork _artwork;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _rowFont;
    private readonly IGlyphSource _selectedFont;
    private readonly Rect _listRoi;
    private readonly Rect _selectedRoi;
    private readonly int _scrollBarX;
    private readonly List<ResolutionRow> _rows;

    private bool _isDoubleClicked;
    private DialogPopup? _popup;

    private ResolutionDialog(Size screen, ResolutionArtwork artwork, IGlyphSource titleFont,
        IGlyphSource rowFont, IGlyphSource selectedFont, IReadOnlyList<ResolutionInfo> resolutions,
        ResolutionInfo currentResolution)
        : base(new Point(0, 0))
    {
        _artwork = artwork;
        _titleFont = titleFont;
        _rowFont = rowFont;
        _selectedFont = selectedFont;
        _rows = [.. resolutions.Select(resolution => new ResolutionRow(resolution))];

        int lineHeight = Math.Max(1, rowFont.LineHeight);
        int visible = Math.Clamp(resolutions.Count, MinVisibleItems, MaxVisibleItems);
        int height = Math.Min(screen.Height - 100,
            (lineHeight * visible) + ListAreaOffsetY + ListAreaHeightDeduction + ListHeightDeduction);

        _window = StandardWindow.CreateCentered(new Size(WindowWidth, height), renderBackground: true, screen);

        Rect roi = _window.ActiveArea;

        _listRoi = new Rect(roi.X + PaddingLeftSide, roi.Y + ListTop, TextAreaWidth,
            roi.Height - ListHeightDeduction);
        _selectedRoi = new Rect(_listRoi.X, _listRoi.Bottom + SelectedGap, TextAreaWidth, SelectedHeight);

        SetAreaItems(new Rect(_listRoi.X, _listRoi.Y + ListAreaOffsetY,
            _listRoi.Width - ListAreaWidthDeduction, _listRoi.Height - ListAreaHeightDeduction));
        SetAreaMaxItems((_listRoi.Height - 7) / lineHeight);

        _scrollBarX = roi.X + roi.Width - ScrollBarRightInset;
        Rect grooveArea = new(_scrollBarX + 2, _listRoi.Y + ScrollBarButtonHeight, ScrollBarGrooveWidth,
            _listRoi.Height - (ScrollBarButtonHeight * 2));

        SetScrollButtonUp(new ButtonSprite(artwork.ScrollBarFrames[0], artwork.ScrollBarFrames[1], null,
            _scrollBarX + 1, _listRoi.Y + 1));
        SetScrollButtonDown(new ButtonSprite(artwork.ScrollBarFrames[2], artwork.ScrollBarFrames[3], null,
            _scrollBarX + 1, _listRoi.Y + _listRoi.Height - ScrollBarBottomInset));
        SetScrollBarArea(grooveArea);
        SetScrollBarImage(artwork.ScrollBarFrames[4]);

        _buttons = ButtonGroup.CreateStandard(roi, DialogButton.Ok | DialogButton.Cancel, artwork.Buttons);

        SetListContent(_rows);

        for (int index = 0; index < _rows.Count; ++index)
        {
            if (_rows[index].Resolution == currentResolution)
            {
                SetCurrent(index);
                break;
            }
        }

        UpdateScrollBarImage();
    }

    /// <summary>The window the dialog is drawn in.</summary>
    public StandardWindow Window => _window;

    /// <summary>The OK / Cancel pair of the dialog.</summary>
    public ButtonGroup Buttons => _buttons;

    /// <summary>The rows of the list.</summary>
    public IReadOnlyList<ResolutionInfo> Resolutions =>
        [.. _rows.Select(row => row.Resolution)];

    /// <summary>Title, <c>_( "Select Game Resolution:" )</c> of the reference.</summary>
    public string Title => "Select Game Resolution:";

    /// <summary>Area of the list, in screen coordinates.</summary>
    public Rect ListArea => _listRoi;

    /// <summary>Area of the selected resolution line, in screen coordinates.</summary>
    public Rect SelectedArea => _selectedRoi;

    /// <summary>The selected resolution, <c>GetCurrent()</c> of the <c>Interface::ListBox</c>.</summary>
    public ResolutionInfo SelectedResolution =>
        IsSelected ? GetCurrent().Resolution : throw new InvalidOperationException("Nothing is selected in the resolution list.");

    /// <summary>Font of the rows.</summary>
    public IGlyphSource RowFont => _rowFont;

    /// <summary>Font of the selected line.</summary>
    public IGlyphSource SelectedFont => _selectedFont;

    /// <summary>Popup asked for by the last frame, or <see langword="null"/>.</summary>
    public DialogPopup? Popup => _popup;

    /// <summary>Reads <see cref="Popup"/> and clears it.</summary>
    /// <returns>The popup, or <see langword="null"/> when none was asked for.</returns>
    public DialogPopup? TakePopup()
    {
        DialogPopup? popup = _popup;
        _popup = null;
        return popup;
    }

    /// <summary>
    /// Builds the dialog, or returns <see langword="null"/> when the artwork or the resolution list is unusable
    /// (<c>dialog_resolution.cpp:114-183</c>).
    /// </summary>
    /// <param name="screen">Screen to centre on.</param>
    /// <param name="artwork">Scroll bar frames, board and buttons.</param>
    /// <param name="titleFont">Font of the title.</param>
    /// <param name="rowFont">Font of the rows, <c>FontType::normalWhite()</c>.</param>
    /// <param name="selectedFont">Font of the selected resolution, <c>FontType::normalYellow()</c>.</param>
    /// <param name="resolutions">The resolutions to offer, largest window first.</param>
    /// <param name="currentResolution">The resolution that is selected when the dialog opens.</param>
    /// <returns>The dialog, or <see langword="null"/> when a frame is missing or the list is empty.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static ResolutionDialog? TryCreate(Size screen, ResolutionArtwork artwork, IGlyphSource titleFont,
        IGlyphSource rowFont, IGlyphSource selectedFont, IReadOnlyList<ResolutionInfo> resolutions,
        ResolutionInfo currentResolution)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(rowFont);
        ArgumentNullException.ThrowIfNull(selectedFont);
        ArgumentNullException.ThrowIfNull(resolutions);

        if (!artwork.IsComplete || resolutions.Count == 0)
        {
            return null;
        }

        return new ResolutionDialog(screen, artwork, titleFont, rowFont, selectedFont, resolutions,
            currentResolution);
    }

    /// <summary>Draws the window, the list, the selected resolution and the buttons.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _window.Draw(target);

        GlyphText.DrawLine(_titleFont, target, new Point(_listRoi.X, _window.ActiveArea.Y + TitleTop), Title);

        Redraw(target);
        RedrawInfo(target);

        _buttons.DrawShadows(target);
        _buttons.Draw(target);
    }

    /// <summary>Handles one frame: the buttons, the list and its right clicks.</summary>
    /// <param name="events">Input state of the frame.</param>
    /// <returns>
    /// <see cref="DialogButton.Ok"/> for the chosen resolution, <see cref="DialogButton.Cancel"/> to leave without one,
    /// <see cref="DialogButton.None"/> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public DialogButton ProcessEvents(LocalEvent events)
    {
        ArgumentNullException.ThrowIfNull(events);

        // 双击 = 选中并确认，参考的 actionListDoubleClick + `_isDoubleClicked`。
        if (_isDoubleClicked)
        {
            _isDoubleClicked = false;

            if (IsSelected)
            {
                return DialogButton.Ok;
            }
        }

        DialogButton pressed = _buttons.ProcessEvents(events, events.IsKeyPressed(Key.Enter) ? ButtonHotKey.Okay : null);

        if (pressed != DialogButton.None)
        {
            return pressed;
        }

        if (events.IsKeyPressed(Key.Escape))
        {
            return DialogButton.Cancel;
        }

        QueueEventProcessing(events);
        ShowExplanation(events);

        return DialogButton.None;
    }

    /// <summary>
    /// Runs the dialog until it closes, <c>selectResolution()</c> of the reference.
    /// </summary>
    /// <param name="target">Indexed frame buffer the scene draws and renders.</param>
    /// <param name="dialog">The dialog to run.</param>
    /// <param name="events">Input source; defaults to <see cref="LocalEvent.Current"/>.</param>
    /// <returns>The chosen button, <see cref="DialogButton.None"/> when the loop was interrupted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="dialog"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">There is no event engine to run the loop on.</exception>
    public static DialogButton Show(PixelBuffer target, ResolutionDialog dialog, LocalEvent? events = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(dialog);

        LocalEvent eventState = events ?? LocalEvent.Current;
        IEventEngine engine = eventState.Engine
            ?? throw new InvalidOperationException("The resolution dialog needs an event engine to run.");

        while (eventState.HandleEvents(sleepAfterEventProcessing: false))
        {
            DialogButton button = dialog.ProcessEvents(eventState);

            if (dialog.TakePopup() is { } popup)
            {
                MessageBox.Show(target, popup.Header, popup.Body, dialog._titleFont, dialog._selectedFont,
                    DialogButton.Ok, dialog._artwork.Buttons, eventState);
            }

            if (button != DialogButton.None)
            {
                return button;
            }

            dialog.Draw(target);
            engine.Render(target.Bounds);
            engine.Sleep(LocalEvent.LoopSleepTimeMs);
        }

        return DialogButton.None;
    }

    /// <summary>Draws one row, the <c>drawResolutionText()</c> of the reference.</summary>
    /// <param name="item">The row.</param>
    /// <param name="x">Left edge of the rows area.</param>
    /// <param name="y">Top edge of the row.</param>
    /// <param name="current">Whether the row is selected.</param>
    /// <param name="output">Frame buffer.</param>
    public override void RedrawItem(ResolutionRow item, int x, int y, bool current, PixelBuffer output)
    {
        IGlyphSource font = current ? _selectedFont : _rowFont;
        (string left, string middle, string right) = GetResolutionStrings(item.Resolution);

        int leftWidth = GlyphText.Measure(font, left);
        int middleWidth = GlyphText.Measure(font, middle);

        // 参考：整串从 textAreaWidth / 2 - leftTextSize - middleTextSize / 2 起画。
        int offsetX = x + (TextAreaWidth / 2) - leftWidth - (middleWidth / 2);

        // 参考用 Text::height()，端口手上只有字形源，退一步用 LineHeight（同一处的 TODO 注释也承认这个高度是估算的）。
        int textY = y + ((ItemHeight - font.LineHeight + TextOffsetYCorrection) / 2);

        GlyphText.DrawLine(font, output, new Point(offsetX, textY), left);

        if (middle.Length > 0)
        {
            GlyphText.DrawLine(font, output, new Point(offsetX + leftWidth, textY), middle);
        }

        GlyphText.DrawLine(font, output, new Point(offsetX + leftWidth + middleWidth, textY), right);
    }

    /// <summary>Stamps the two shading plates and the scroll bar groove behind the rows.</summary>
    /// <param name="destination">Unused; the window repaints the whole list area.</param>
    /// <param name="output">Frame buffer.</param>
    public override void RedrawBackground(Point destination, PixelBuffer output)
    {
        _ = destination;

        StandardWindow.ApplyTextBackgroundShading(output, _listRoi);
        StandardWindow.ApplyTextBackgroundShading(output, _selectedRoi);
        StandardWindow.RenderScrollbarBackground(output,
            new Rect(_scrollBarX, _listRoi.Y, _listRoi.Width, _listRoi.Height), _artwork.ScrollBarBackground);
    }

    /// <summary>Remembers the double click; <see cref="ProcessEvents"/> turns it into an answer.</summary>
    /// <param name="item">The row that was clicked twice.</param>
    public override void ActionListDoubleClick(ResolutionRow item)
    {
        _ = item;
        _isDoubleClicked = true;
    }

    /// <summary>Explains the resolution of the row under the cursor, the right click of the reference.</summary>
    /// <param name="item">The row that was right clicked.</param>
    public override void ActionListPressRight(ResolutionRow item)
    {
        (string header, string body) = GetDescription(item.Resolution);
        _popup = new DialogPopup(header, body);
    }

    /// <summary>Does nothing; a single click only moves the selection in the reference.</summary>
    /// <param name="item">The clicked row.</param>
    public override void ActionListSingleClick(ResolutionRow item) => _ = item;

    /// <summary>Does nothing; the reference leaves the arrow keys to the list base class.</summary>
    public override void ActionCurrentUp()
    {
    }

    /// <summary>Does nothing; the reference leaves the arrow keys to the list base class.</summary>
    public override void ActionCurrentDn()
    {
    }

    /// <summary>Draws the selected resolution under the list, in yellow.</summary>
    /// <param name="target">Frame buffer.</param>
    internal void RedrawInfo(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!IsSelected)
        {
            return;
        }

        ResolutionInfo resolution = GetCurrent().Resolution;

        if (resolution.GameWidth <= 0 || resolution.GameHeight <= 0)
        {
            return;
        }

        (string left, string middle, string right) = GetResolutionStrings(resolution);
        int leftWidth = GlyphText.Measure(_selectedFont, left);
        int middleWidth = GlyphText.Measure(_selectedFont, middle);

        int offsetX = _selectedRoi.X + (TextAreaWidth / 2) - leftWidth - (middleWidth / 2);
        int offsetY = _selectedRoi.Y +
                      ((ItemHeight - _selectedFont.LineHeight + TextOffsetYCorrection) / 2);

        GlyphText.DrawLine(_selectedFont, target, new Point(offsetX, offsetY), string.Concat(left, middle, right));
    }

    /// <summary>
    /// The three pieces of a row: the frame buffer size, the " x " and the window size or the magnification, the
    /// <c>getResolutionStrings()</c> of the reference (<c>dialog_resolution.cpp:61-72</c>).
    /// </summary>
    private static (string Left, string Middle, string Right) GetResolutionStrings(ResolutionInfo resolution)
    {
        string gameWidth = resolution.GameWidth.ToString(CultureInfo.InvariantCulture);
        string gameHeight = resolution.GameHeight.ToString(CultureInfo.InvariantCulture);

        if (resolution.ScreenWidth != resolution.GameWidth && resolution.ScreenHeight != resolution.GameHeight)
        {
            int integer = resolution.ScreenWidth / resolution.GameWidth;
            int fraction = ((resolution.ScreenWidth * 10) / resolution.GameWidth) - (10 * integer);

            return (gameWidth, MiddleText,
                $"{gameHeight} (x{integer.ToString(CultureInfo.InvariantCulture)}.{fraction.ToString(CultureInfo.InvariantCulture)})");
        }

        return (gameWidth, MiddleText, gameHeight);
    }

    /// <summary>Text of a row, e.g. <c>640 x 480</c> or <c>640 x 480 (x2.0)</c>.</summary>
    /// <param name="resolution">The resolution to describe.</param>
    /// <returns>The row text.</returns>
    public static string FormatRow(ResolutionInfo resolution)
    {
        (string left, string middle, string right) = GetResolutionStrings(resolution);

        return string.Concat(left, middle, right);
    }

    /// <summary>
    /// Header and explanation of a row, <c>ActionListPressRight()</c> of the reference
    /// (<c>dialog_resolution.cpp:124-145</c>).
    /// </summary>
    private static (string Header, string Body) GetDescription(ResolutionInfo resolution)
    {
        (string left, string middle, string right) = GetResolutionStrings(resolution);
        string header = string.Concat(left, middle, right);

        if (resolution.GameHeight == resolution.ScreenHeight && resolution.GameWidth == resolution.ScreenWidth)
        {
            return (header, "Select this game resolution.");
        }

        int integer = resolution.ScreenWidth / resolution.GameWidth;
        int fraction = ((resolution.ScreenWidth * 10) / resolution.GameWidth) - (10 * integer);
        string resolutionName = resolution.GameWidth.ToString(CultureInfo.InvariantCulture) + middle +
                                resolution.GameHeight.ToString(CultureInfo.InvariantCulture);
        string scale = integer.ToString(CultureInfo.InvariantCulture) + "." +
                       fraction.ToString(CultureInfo.InvariantCulture);

        string body =
            "Selecting this resolution will set a resolution that is scaled from the original resolution " +
            $"(%{{resolution}}) by multiplying it with the number in the brackets (%{{scale}}).\n\n" +
            "A resolution with a clean integer number (2.0x, 3.0x etc.) will usually look better because the " +
            "pixels are upscaled evenly in both horizontal and vertical directions.";

        return (header, body
            .Replace("%{resolution}", resolutionName, StringComparison.Ordinal)
            .Replace("%{scale}", scale, StringComparison.Ordinal));
    }

    /// <summary>Fills <see cref="_popup"/> for a right click on the buttons.</summary>
    private void ShowExplanation(LocalEvent events)
    {
        if (events.IsMouseRightButtonPressedInArea(_buttons[0].Area))
        {
            _popup = new DialogPopup("Okay", "Click to select this resolution.");
        }
        else if (_buttons.Count > 1 && events.IsMouseRightButtonPressedInArea(_buttons[1].Area))
        {
            _popup = new DialogPopup("Cancel", "Click to cancel the resolution selection.");
        }
    }
}
