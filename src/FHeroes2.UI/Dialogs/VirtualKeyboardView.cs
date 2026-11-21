using System.Globalization;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What one frame of <see cref="VirtualKeyboardView.ProcessEvents"/> asks its owner to do.
/// </summary>
/// <param name="Action">The action of the pressed key; <see cref="KeyboardDialogAction.Close"/> ends the dialog.</param>
/// <param name="Popup">A help popup the view asked for, or <see langword="null"/>.</param>
public readonly record struct VirtualKeyboardResult(KeyboardDialogAction Action, DialogPopup? Popup = null);

/// <summary>
/// Whether the text typed into the numpad can be turned into a number, a port of the checks of
/// <c>openVirtualNumpad()</c> (<c>ui_keyboard.cpp:1426-1460</c>).
/// </summary>
public enum VirtualNumpadParse
{
    /// <summary>The text is a number inside the range.</summary>
    Valid,

    /// <summary>The text is empty or is a lone sign, <c>std::invalid_argument</c> of the reference.</summary>
    Invalid,

    /// <summary>The text is a number outside the range, or too large to be one.</summary>
    OutOfRange,
}

/// <summary>
/// The drawable virtual keyboard: the window with its text field, the letter keys and the okay
/// button, a port of <c>processVirtualKeyboardEvent()</c> (<c>ui_keyboard.cpp:1125-1250</c>)
/// together with the two loops that drive it, <c>openVirtualKeyboard()</c> and
/// <c>openVirtualNumpad()</c> (<c>ui_keyboard.cpp:1252-1480</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Deviation: no blocking loop.</b> The reference owns a <c>while ( le.HandleEvents() )</c> loop:
/// it swaps the layout, switches the language and only returns when the player presses OKAY. The
/// port has no local event loop, so <see cref="ProcessEvents"/> handles one frame and applies the
/// layout and language actions itself; the caller loops until it reads
/// <see cref="KeyboardDialogAction.Close"/>, exactly like it drives <see cref="MessageBox"/>.
/// </para>
/// <para>
/// <b>Deviation: no <c>ImageRestorer</c>.</b> The reference rebuilds the window in place and
/// restores the pixels it overwrote. The port redraws the whole frame from a clean buffer every
/// time, so <see cref="Draw"/> is allowed to be called once per frame and nothing is restored.
/// </para>
/// <para>
/// <b>Deviation: hidden keys.</b> The reference keeps every key in the layout and hides the ones it
/// does not want. The port does the same through <see cref="ButtonBase.Hide"/>, so the row length
/// (and therefore the centring) is identical to the reference's.
/// </para>
/// <para>
/// <b>Deviation: no <see cref="LanguageSwitcher"/>.</b> The reference wraps the layout generation in a
/// <c>LanguageSwitcher</c> so that the internal <c>_()</c> msgids come out in the keyboard language.
/// The port takes its labels resolved in advance (<see cref="VirtualKeyboardText"/>), which the
/// caller builds for the wanted language.
/// </para>
/// </remarks>
public sealed class VirtualKeyboardView
{
    /// <summary>Distance of the okay button from the bottom of the window, the <c>35</c> of <c>ui_keyboard.cpp:1207</c>.</summary>
    public const int OkayButtonOffsetFromBottom = 35;

    private readonly VirtualKeyboardArtwork _artwork;
    private readonly VirtualKeyboardText _labels;
    private readonly VirtualKeyboardRenderer _renderer;
    private readonly ButtonSprite _okayButton;
    private readonly bool _isEvilInterface;
    private readonly SupportedLanguage _gameLanguage;
    private readonly List<List<VirtualKeyboardButton>> _rows = [];

    private KeyboardLayoutType _layoutType;
    private SupportedLanguage _language;
    private bool _isLayoutValid;
    private bool _isTextAreaDrag;
    private int _textDragPositionY;
    private bool _textAreaWasDragged;

    private VirtualKeyboardView(
        VirtualKeyboardArtwork artwork,
        VirtualKeyboardText labels,
        VirtualKeyboardRenderer renderer,
        KeyboardLayoutType layoutType,
        SupportedLanguage gameLanguage,
        SupportedLanguage language,
        bool isEvilInterface)
    {
        _artwork = artwork;
        _labels = labels;
        _renderer = renderer;
        _okayButton = new ButtonSprite(artwork.OkayReleased, artwork.OkayPressed);
        _layoutType = layoutType;
        _gameLanguage = gameLanguage;
        _language = language;
        _isEvilInterface = isEvilInterface;
    }

    /// <summary>
    /// The language the keyboard of <paramref name="gameLanguage"/> starts with, the head of
    /// <c>openVirtualKeyboard()</c> (<c>ui_keyboard.cpp:1264-1270</c>).
    /// </summary>
    /// <remarks>
    /// 参考把玩家上次选的语言记在文件级静态变量里，再次打开键盘时沿用；游戏语言变了就不沿用。
    /// 端口照搬这个状态机，状态放在 <see cref="LastSelectedLanguage"/>。
    /// </remarks>
    /// <param name="gameLanguage">The language of the game interface.</param>
    /// <returns>The letter language to start with.</returns>
    public static SupportedLanguage GetStartLanguage(SupportedLanguage gameLanguage) =>
        gameLanguage == LastSelectedLanguage ? LastSelectedLanguage : SupportedLanguage.English;

    /// <summary>
    /// The letter language the player chose last time, the reference's file-static
    /// <c>lastSelectedLanguage</c> (<c>ui_keyboard.cpp:1259</c>).
    /// </summary>
    public static SupportedLanguage LastSelectedLanguage { get; set; } = SupportedLanguage.English;

    /// <summary>
    /// Builds the keyboard, the reference's <c>processVirtualKeyboardEvent()</c> with its first
    /// layout (<c>ui_keyboard.cpp:1125-1211</c>).
    /// </summary>
    /// <param name="artwork">Window frame, key plate, input frame and okay button.</param>
    /// <param name="screen">Size of the screen the keyboard is centred on.</param>
    /// <param name="text">The text to edit.</param>
    /// <param name="lengthLimit">Maximum number of characters; <c>0</c> means no limit.</param>
    /// <param name="isMultiLineText">Whether the text wraps and has its own enter key.</param>
    /// <param name="layoutType">The character set shown first.</param>
    /// <param name="gameLanguage">
    /// The language of the game interface; it is what the language key switches to and back from.
    /// </param>
    /// <param name="language">The language of the letters shown first.</param>
    /// <param name="isEvilInterface">Whether the evil artwork is used.</param>
    /// <param name="labels">The resolved labels; <see cref="VirtualKeyboardText.Default"/> when omitted.</param>
    /// <returns>The keyboard, or <see langword="null"/> when an asset is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthLimit"/> is negative.</exception>
    public static VirtualKeyboardView? TryCreate(
        VirtualKeyboardArtwork? artwork,
        Size screen,
        string text,
        int lengthLimit,
        bool isMultiLineText,
        KeyboardLayoutType layoutType,
        SupportedLanguage gameLanguage,
        SupportedLanguage language,
        bool isEvilInterface,
        VirtualKeyboardText? labels = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(lengthLimit);

        if (artwork is null)
        {
            return null;
        }

        // "A string longer than 64KB is extremely impossible": the reference limits an unlimited
        // input to uint16_t::max characters, the port counts UTF-16 characters.
        int limit = lengthLimit == 0 ? ushort.MaxValue : lengthLimit;

        VirtualKeyboardRenderer renderer = new(screen, artwork, text, limit, isMultiLineText, isEvilInterface);

        return new VirtualKeyboardView(artwork, labels ?? VirtualKeyboardText.Default, renderer, layoutType,
            gameLanguage, language, isEvilInterface);
    }

    /// <summary>
    /// Builds the numeric keyboard, the two <c>openVirtualNumpad()</c> heads of
    /// <c>ui_keyboard.cpp:1418-1428</c> and <c>ui_keyboard.cpp:1483-1493</c>.
    /// </summary>
    /// <param name="artwork">Window frame, key plate, input frame and okay button.</param>
    /// <param name="screen">Size of the screen the keyboard is centred on.</param>
    /// <param name="value">The number shown first.</param>
    /// <param name="minValue">Smallest value the caller accepts; a negative one adds the sign key.</param>
    /// <param name="maxValue">Largest value the caller accepts; unused by the layout.</param>
    /// <param name="isEvilInterface">Whether the evil artwork is used.</param>
    /// <returns>The keyboard, or <see langword="null"/> when an asset is missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="artwork"/> is <see langword="null"/>.</exception>
    public static VirtualKeyboardView? TryCreateNumpad(VirtualKeyboardArtwork? artwork, Size screen, int value,
        int minValue, int maxValue, bool isEvilInterface)
    {
        return TryCreate(artwork, screen, value.ToString(CultureInfo.InvariantCulture), NumpadLengthLimit,
            false, minValue < 0 ? KeyboardLayoutType.SignedNumeric : KeyboardLayoutType.UnsignedNumeric,
            SupportedLanguage.English, SupportedLanguage.English, isEvilInterface);
    }

    /// <summary>
    /// Turns the text of the numpad into a number, the checks of <c>openVirtualNumpad()</c>
    /// (<c>ui_keyboard.cpp:1426-1460</c>).
    /// </summary>
    /// <param name="text">Text the player typed.</param>
    /// <param name="minValue">Smallest value the caller accepts.</param>
    /// <param name="maxValue">Largest value the caller accepts.</param>
    /// <param name="value">The number, zero when the text is not one.</param>
    /// <returns>Whether the text was accepted, unparsable or out of range.</returns>
    /// <remarks>
    /// 差异：<c>std::stoi</c> 会忽略数字后面的垃圾字符（<c>"12abc"</c> 得到 12），<c>int.Parse</c> 直接判为非法。
    /// 键盘只能打出数字和正负号，这条差异走不到。
    /// </remarks>
    public static VirtualNumpadParse ReadNumpad(string text, int minValue, int maxValue, out int value)
    {
        ArgumentNullException.ThrowIfNull(text);

        value = 0;

        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed))
        {
            // An empty string or a lone minus sign is an invalid number; an overflow is out of range.
            return text.Length == 0 || text is "-" or "+" ? VirtualNumpadParse.Invalid : VirtualNumpadParse.OutOfRange;
        }

        if (parsed < minValue || parsed > maxValue)
        {
            return VirtualNumpadParse.OutOfRange;
        }

        value = parsed;
        return VirtualNumpadParse.Valid;
    }

    /// <summary>
    /// Length limit of the numpad, the <c>10</c> of <c>ui_keyboard.cpp:1421</c>: a sign and ten digits
    /// hold <c>INT32_MIN</c>.
    /// </summary>
    public const int NumpadLengthLimit = 10;

    /// <summary>The window, the text field and the caret.</summary>
    public VirtualKeyboardRenderer Renderer => _renderer;

    /// <summary>The resolved labels of the special keys.</summary>
    public VirtualKeyboardText Labels => _labels;

    /// <summary>The character set shown, <c>layoutType</c> of the reference.</summary>
    public KeyboardLayoutType LayoutType
    {
        get => _layoutType;
        set
        {
            if (_layoutType == value)
            {
                return;
            }

            _layoutType = value;
            _isLayoutValid = false;
        }
    }

    /// <summary>The language of the letters shown, <c>language</c> of the reference.</summary>
    public SupportedLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            _isLayoutValid = false;
        }
    }

    /// <summary>
    /// Whether the language key is shown, the reference's <c>isExtraLanguageSupported</c>
    /// (<c>ui_keyboard.cpp:1254</c>): the game language has a second letter set.
    /// </summary>
    public bool IsExtraLanguageSupported => VirtualKeyboardLayout.IsLanguageSwitchingSupported(_gameLanguage);

    /// <summary>The text being edited, <see cref="VirtualKeyboardRenderer.Text"/>.</summary>
    public string Text => _renderer.Text;

    /// <summary>Area of the window interior; the layout is built on the first read.</summary>
    public Rect WindowArea
    {
        get
        {
            EnsureLayout();
            return _renderer.WindowArea;
        }
    }

    /// <summary>Area of the text input frame, <c>getTextRoi()</c> of the reference.</summary>
    public Rect TextArea => _renderer.TextInputArea;

    /// <summary>Area of the okay button, <c>okayButton.area()</c> of the reference.</summary>
    public Rect OkayArea
    {
        get
        {
            EnsureLayout();
            return _okayButton.Area;
        }
    }

    /// <summary>
    /// The rows of keys, the bottom row included; the layout is built on the first read and rebuilt
    /// when <see cref="LayoutType"/> or <see cref="Language"/> changes.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<VirtualKeyboardButton>> Rows
    {
        get
        {
            EnsureLayout();
            return _rows;
        }
    }

    /// <summary>
    /// Draws the window, the text, every key and the okay button.
    /// </summary>
    /// <param name="output">Frame buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        EnsureLayout();

        _renderer.Draw(output);

        foreach (List<VirtualKeyboardButton> row in _rows)
        {
            foreach (VirtualKeyboardButton button in row)
            {
                button.Draw(output);
            }
        }

        _okayButton.DrawShadow(output);
        _okayButton.Draw(output);
    }

    /// <summary>
    /// Handles one frame of input, the body of the reference's event loop
    /// (<c>ui_keyboard.cpp:1214-1249</c>).
    /// </summary>
    /// <param name="events">Input state of the frame.</param>
    /// <param name="closeRequested">
    /// Whether the hot key which closes a window was pressed, <c>Game::HotKeyCloseWindow()</c> of the
    /// reference; it does what a click on the okay button does.
    /// </param>
    /// <returns>
    /// The action of the pressed key, <see cref="KeyboardDialogAction.DoNothing"/> for a frame without
    /// one, and a help popup when the player right clicked a key.
    /// </returns>
    /// <remarks>
    /// 布局与语言切换在这里就地完成（参考把它们放在外层的 <c>openVirtualKeyboard()</c> 里）。按下确定键时参考会把
    /// 事件状态整个 <c>reset()</c> 掉，端口没有可以复位的外部状态，只返回
    /// <see cref="KeyboardDialogAction.Close"/>。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="events"/> is <see langword="null"/>.</exception>
    public VirtualKeyboardResult ProcessEvents(LocalEvent events, bool closeRequested = false)
    {
        ArgumentNullException.ThrowIfNull(events);

        EnsureLayout();

        _okayButton.UpdatePressState(events);

        if (events.MouseClickLeft(OkayArea) || closeRequested)
        {
            return new VirtualKeyboardResult(KeyboardDialogAction.Close);
        }

        (KeyboardDialogAction action, DialogPopup? popup) = HandleKeyAndButtonEvents(events);
        if (action != KeyboardDialogAction.DoNothing)
        {
            ApplyAction(action);

            return new VirtualKeyboardResult(action, popup);
        }

        UpdateButtonStates(events);

        Rect textArea = TextArea;

        if (_renderer.IsMultiLineText)
        {
            if (events.IsMouseWheelUpInArea(textArea))
            {
                _renderer.SetCursorPosition(KeyboardCursorMove.PreviousLine);
            }
            else if (events.IsMouseWheelDownInArea(textArea))
            {
                _renderer.SetCursorPosition(KeyboardCursorMove.NextLine);
            }
        }

        if (_renderer.IsMultiLineText && events.IsMouseLeftButtonPressedInArea(textArea))
        {
            if (!_isTextAreaDrag)
            {
                _textDragPositionY = events.LeftButtonPressPosition.Y;
                _isTextAreaDrag = true;
            }

            if (events.HasMouseMoved)
            {
                int currentY = events.CursorPosition.Y;
                int dragOffset = currentY - _textDragPositionY;

                // The reference compares against the height of one text line, not of the whole frame.
                int lineHeight = VirtualKeyboardLayout.InputAreaSize.Height - (2 * VirtualKeyboardLayout.InputAreaBorders);

                if (Math.Abs(dragOffset) >= lineHeight)
                {
                    _renderer.SetCursorPosition(
                        dragOffset > 0 ? KeyboardCursorMove.PreviousLine : KeyboardCursorMove.NextLine);

                    _textDragPositionY = currentY;
                    _textAreaWasDragged = true;
                    events.RegisterDrag();
                }
            }
        }

        if (events.IsMouseLeftButtonReleased)
        {
            _isTextAreaDrag = false;

            if (_textAreaWasDragged)
            {
                // The release belongs to the drag, not to a click on the text field.
                events.MouseClickLeft();
                _textAreaWasDragged = false;
            }
        }

        if (events.MouseClickLeft(textArea))
        {
            _renderer.SetCursorPosition(events.LeftButtonPressPosition);
        }
        else if (popup is null && events.IsMouseRightButtonPressedInArea(OkayArea))
        {
            popup = new DialogPopup(_labels.Okay, _labels.OkayDescription);
        }

        _renderer.Update();

        return new VirtualKeyboardResult(KeyboardDialogAction.DoNothing, popup);
    }

    private (KeyboardDialogAction Action, DialogPopup? Popup) HandleKeyAndButtonEvents(LocalEvent events)
    {
        Key key = GetHardwareKey(events);
        DialogPopup? popup = null;

        foreach (List<VirtualKeyboardButton> row in _rows)
        {
            foreach (VirtualKeyboardButton button in row)
            {
                if (!button.Sprite.IsVisible)
                {
                    continue;
                }

                if (events.MouseClickLeft(button.Sprite.Area) || (key != Key.None && button.Key == key))
                {
                    return (button.Action?.Invoke(_renderer) ?? KeyboardDialogAction.DoNothing, popup);
                }

                if (events.IsMouseRightButtonPressedInArea(button.Sprite.Area))
                {
                    popup = new DialogPopup(button.PopupTitle ?? button.Text, button.PopupBody ?? string.Empty);
                }

                if (events.MouseLongPressLeft(button.Sprite.Area))
                {
                    KeyboardDialogAction action = button.Action?.Invoke(_renderer) ?? KeyboardDialogAction.DoNothing;
                    if (action == KeyboardDialogAction.DoNothing)
                    {
                        // Only for the event of entering a character: the key may be held and repeat.
                        events.ResetLongPress();
                    }

                    return (action, popup);
                }
            }
        }

        return (KeyboardDialogAction.DoNothing, popup);
    }

    private void UpdateButtonStates(LocalEvent events)
    {
        foreach (List<VirtualKeyboardButton> row in _rows)
        {
            foreach (VirtualKeyboardButton button in row)
            {
                bool isHeld = events.IsMouseLeftButtonPressedAndHeldInArea(button.Sprite.Area);
                button.SetPressed(button.IsInvertedRenderingLogic ? !isHeld : isHeld);
            }
        }
    }

    private void ApplyAction(KeyboardDialogAction action)
    {
        switch (action)
        {
        case KeyboardDialogAction.LowerCase:
            LayoutType = KeyboardLayoutType.LowerCase;
            break;
        case KeyboardDialogAction.UpperCase:
            LayoutType = KeyboardLayoutType.UpperCase;
            break;
        case KeyboardDialogAction.AlphaNumeric:
            LayoutType = KeyboardLayoutType.AlphaNumeric;
            break;
        case KeyboardDialogAction.ChangeLanguage:
            // The reference asserts that the game language has a second letter set; the key is hidden
            // otherwise, so an action that reaches here in spite of it is simply ignored.
            if (!IsExtraLanguageSupported || _gameLanguage == SupportedLanguage.English)
            {
                break;
            }

            Language = _language == SupportedLanguage.English ? _gameLanguage : SupportedLanguage.English;
            LastSelectedLanguage = _language;
            break;
        case KeyboardDialogAction.Close:
            break;
        case KeyboardDialogAction.DoNothing:
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown keyboard action.");
        }
    }

    private void EnsureLayout()
    {
        if (_isLayoutValid)
        {
            return;
        }

        (string[] buttonLetters, string[] returnLetters) =
            VirtualKeyboardLayout.GetCharacterLayout(_layoutType, _language);

        if (buttonLetters.Length == 0 || returnLetters.Length == 0)
        {
            // The reference asserts on a language without a table; the port falls back to English so
            // that the keyboard is still usable.
            (buttonLetters, returnLetters) =
                VirtualKeyboardLayout.GetCharacterLayout(_layoutType, SupportedLanguage.English);
        }

        bool isNumericLayout = _layoutType is KeyboardLayoutType.SignedNumeric or KeyboardLayoutType.UnsignedNumeric;
        int windowWidth = isNumericLayout
            ? VirtualKeyboardLayout.NumpadWindowWidth
            : VirtualKeyboardLayout.DefaultWindowWidth;

        int windowHeight = VirtualKeyboardLayout.DefaultWindowHeight + _renderer.InputAreaHeightIncrease +
                           ((buttonLetters.Length - VirtualKeyboardLayout.DefaultLetterRows) *
                            (VirtualKeyboardLayout.DefaultButtonHeight + (2 * VirtualKeyboardLayout.ButtonOffset)));

        _renderer.Resize(new Size(windowWidth, windowHeight));

        Size buttonSize = _layoutType == KeyboardLayoutType.AlphaNumeric
            ? VirtualKeyboardLayout.GetDefaultButtonSize(SupportedLanguage.English)
            : VirtualKeyboardLayout.GetDefaultButtonSize(_language);

        _rows.Clear();

        for (int rowId = 0; rowId < buttonLetters.Length; ++rowId)
        {
            string labels = buttonLetters[rowId];
            string insertedLetters = returnLetters[rowId];

            // The two tables of a language always have the same shape; the reference asserts it.
            int count = Math.Min(labels.Length, insertedLetters.Length);

            List<VirtualKeyboardButton> row = new(count);
            for (int i = 0; i < count; ++i)
            {
                char label = labels[i];
                char inserted = insertedLetters[i];

                row.Add(new VirtualKeyboardButton(label.ToString(), GetHardwareKeyForCharacter(label),
                    buttonSize, _isEvilInterface, _artwork.ButtonBackground, renderer =>
                    {
                        renderer.InsertCharacter(inserted);
                        return KeyboardDialogAction.DoNothing;
                    }));
            }

            _rows.Add(row);
        }

        AddExtraStandardButtons();
        Place();

        _isLayoutValid = true;
    }

    // The bottom row of every layout, addExtraStandardButtons() of ui_keyboard.cpp:689-873.
    private void AddExtraStandardButtons()
    {
        List<VirtualKeyboardButton> lastRow = [];
        _rows.Add(lastRow);

        Size spaceButtonSize = (_renderer.IsMultiLineText && IsExtraLanguageSupported)
            ? VirtualKeyboardLayout.MultiLineSpacebarButtonSize
            : VirtualKeyboardLayout.SpacebarButtonSize;

        switch (_layoutType)
        {
        case KeyboardLayoutType.LowerCase:
            AddButton(lastRow, "|", VirtualKeyboardLayout.DefaultSpecialButtonSize,
                _ => KeyboardDialogAction.UpperCase, popupTitle: _labels.Uppercase,
                popupBody: _labels.UppercaseDescription);

            AddButton(lastRow, _labels.DigitsKey, VirtualKeyboardLayout.DefaultSpecialButtonSize,
                _ => KeyboardDialogAction.AlphaNumeric, popupTitle: _labels.KeyboardType,
                popupBody: _labels.KeyboardTypeDescription);

            AddButton(lastRow, _labels.SpaceKey, spaceButtonSize, renderer =>
            {
                renderer.InsertCharacter(' ');
                return KeyboardDialogAction.DoNothing;
            }, key: Key.Space);

            if (_renderer.IsMultiLineText)
            {
                AddButton(lastRow, _labels.EnterKey, VirtualKeyboardLayout.EnterButtonSize, renderer =>
                {
                    renderer.InsertCharacter('\n');
                    return KeyboardDialogAction.DoNothing;
                }, key: Key.Enter);
            }

            AddLanguageButton(lastRow);

            AddBackspaceButton(lastRow);
            break;

        case KeyboardLayoutType.UpperCase:
            AddButton(lastRow, "|", VirtualKeyboardLayout.DefaultSpecialButtonSize,
                _ => KeyboardDialogAction.LowerCase, popupTitle: _labels.Lowercase,
                popupBody: _labels.LowercaseDescription, isInverted: true);

            AddButton(lastRow, _labels.DigitsKey, VirtualKeyboardLayout.DefaultSpecialButtonSize,
                _ => KeyboardDialogAction.AlphaNumeric, popupTitle: _labels.KeyboardType,
                popupBody: _labels.KeyboardTypeDescription);

            AddButton(lastRow, _labels.SpaceKey, spaceButtonSize, renderer =>
            {
                renderer.InsertCharacter(' ');
                return KeyboardDialogAction.DoNothing;
            }, key: Key.Space);

            if (_renderer.IsMultiLineText)
            {
                AddButton(lastRow, _labels.EnterKey, VirtualKeyboardLayout.EnterButtonSize, renderer =>
                {
                    renderer.InsertCharacter('\n');
                    return KeyboardDialogAction.DoNothing;
                }, key: Key.Enter);
            }

            AddLanguageButton(lastRow);

            AddBackspaceButton(lastRow);
            break;

        case KeyboardLayoutType.AlphaNumeric:
            AddButton(lastRow, "|", VirtualKeyboardLayout.DefaultSpecialButtonSize,
                _ => KeyboardDialogAction.DoNothing, isHidden: true);

            AddButton(lastRow, _labels.LettersKey, VirtualKeyboardLayout.DefaultSpecialButtonSize,
                _ => KeyboardDialogAction.LowerCase, popupTitle: _labels.KeyboardType,
                popupBody: _labels.KeyboardTypeDescription);

            AddButton(lastRow, _labels.SpaceKey, spaceButtonSize, renderer =>
            {
                renderer.InsertCharacter(' ');
                return KeyboardDialogAction.DoNothing;
            }, key: Key.Space);

            if (_renderer.IsMultiLineText)
            {
                AddButton(lastRow, _labels.EnterKey, VirtualKeyboardLayout.EnterButtonSize, renderer =>
                {
                    renderer.InsertCharacter('\n');
                    return KeyboardDialogAction.DoNothing;
                }, key: Key.Enter);
            }

            AddLanguageButton(lastRow);

            AddBackspaceButton(lastRow);
            break;

        case KeyboardLayoutType.SignedNumeric:
            AddButton(lastRow, "|", VirtualKeyboardLayout.DefaultSpecialButtonSize,
                _ => KeyboardDialogAction.DoNothing, isHidden: true);

            AddButton(lastRow, "-", GetDigitButtonSize(), renderer =>
            {
                renderer.SwapSign();
                return KeyboardDialogAction.DoNothing;
            }, popupTitle: _labels.SwapSign, popupBody: _labels.SwapSignDescription, key: Key.Minus);

            AddDigitButton(lastRow, '0');

            AddButton(lastRow, "\x7F", GetDigitButtonSize(), _ => KeyboardDialogAction.DoNothing,
                isHidden: true);

            AddBackspaceButton(lastRow);
            break;

        case KeyboardLayoutType.UnsignedNumeric:
            AddButton(lastRow, "|", VirtualKeyboardLayout.DefaultSpecialButtonSize,
                _ => KeyboardDialogAction.DoNothing, isHidden: true);

            AddButton(lastRow, "-", GetDigitButtonSize(), _ => KeyboardDialogAction.DoNothing,
                isHidden: true);

            AddDigitButton(lastRow, '0');

            AddButton(lastRow, "\x7F", GetDigitButtonSize(), _ => KeyboardDialogAction.DoNothing,
                isHidden: true);

            AddBackspaceButton(lastRow);
            break;

        default:
            throw new ArgumentOutOfRangeException(nameof(_layoutType), _layoutType, "Unknown keyboard layout.");
        }
    }

    private Size GetDigitButtonSize() => VirtualKeyboardLayout.GetDefaultButtonSize(SupportedLanguage.English);

    private void AddDigitButton(List<VirtualKeyboardButton> row, char digit)
    {
        AddButton(row, digit.ToString(), GetDigitButtonSize(), renderer =>
        {
            renderer.InsertCharacter(digit);
            return KeyboardDialogAction.DoNothing;
        }, key: GetHardwareKeyForCharacter(digit));
    }

    private void AddBackspaceButton(List<VirtualKeyboardButton> row)
    {
        AddButton(row, "~", VirtualKeyboardLayout.DefaultSpecialButtonSize, renderer =>
        {
            renderer.RemoveCharacter();
            return KeyboardDialogAction.DoNothing;
        }, popupTitle: _labels.Backspace, popupBody: _labels.BackspaceDescription, key: Key.Backspace);
    }

    private void AddLanguageButton(List<VirtualKeyboardButton> row)
    {
        if (!IsExtraLanguageSupported && _renderer.IsMultiLineText)
        {
            return;
        }

        AddButton(row, "\x7F", VirtualKeyboardLayout.DefaultSpecialButtonSize,
            _ => KeyboardDialogAction.ChangeLanguage, popupTitle: _labels.ChangeLanguage,
            popupBody: _labels.ChangeLanguageDescription, isHidden: !IsExtraLanguageSupported);
    }

    private void AddButton(List<VirtualKeyboardButton> row, string label, Size size,
        Func<VirtualKeyboardRenderer, KeyboardDialogAction> action, string? popupTitle = null,
        string? popupBody = null, Key key = Key.None, bool isHidden = false, bool isInverted = false)
    {
        VirtualKeyboardButton button = new(label, key, size, _isEvilInterface, _artwork.ButtonBackground, action)
        {
            PopupTitle = popupTitle,
            PopupBody = popupBody,
            IsInvertedRenderingLogic = isInverted,
        };

        if (isHidden)
        {
            button.Sprite.Hide();
        }

        if (isInverted)
        {
            // The key which draws the other letter case looks the same as the keys that are not held.
            button.SetPressed(true);
        }

        row.Add(button);
    }

    // Lays the keys out, getButtonsRoi() and renderButtons() of ui_keyboard.cpp:905-992.
    private void Place()
    {
        Rect window = _renderer.WindowArea;

        Point buttonsOffset = new(
            window.X + VirtualKeyboardLayout.OffsetFromWindowBorders.X,
            window.Y + VirtualKeyboardLayout.OffsetFromWindowBorders.Y + _renderer.InputAreaHeightIncrease);

        int y = buttonsOffset.Y;

        foreach (List<VirtualKeyboardButton> row in _rows)
        {
            int rowLength = 0;
            foreach (VirtualKeyboardButton button in row)
            {
                rowLength += button.Sprite.ReleasedArea.Width;
            }

            rowLength += (row.Count - 1) * VirtualKeyboardLayout.ButtonOffset;

            int x = buttonsOffset.X +
                    ((window.Width - (2 * VirtualKeyboardLayout.OffsetFromWindowBorders.X) - rowLength) / 2);

            foreach (VirtualKeyboardButton button in row)
            {
                button.SetPosition(x, y);
                x += button.Sprite.ReleasedArea.Width + VirtualKeyboardLayout.ButtonOffset;
            }

            y += VirtualKeyboardLayout.DefaultButtonHeight + (2 * VirtualKeyboardLayout.ButtonOffset);
        }

        _okayButton.SetPosition(
            window.X + ((window.Width - _artwork.OkayReleased.Width) / 2),
            window.Y + window.Height - OkayButtonOffsetFromBottom);
    }

    // getKeyForCharacter() of ui_keyboard.cpp:633-665: only the numeric layouts react to the hardware keyboard.
    private Key GetHardwareKeyForCharacter(char character)
    {
        if (_layoutType is not (KeyboardLayoutType.SignedNumeric or KeyboardLayoutType.UnsignedNumeric))
        {
            return Key.None;
        }

        return character switch
        {
            '0' => Key.Digit0,
            '1' => Key.Digit1,
            '2' => Key.Digit2,
            '3' => Key.Digit3,
            '4' => Key.Digit4,
            '5' => Key.Digit5,
            '6' => Key.Digit6,
            '7' => Key.Digit7,
            '8' => Key.Digit8,
            '9' => Key.Digit9,
            _ => Key.None,
        };
    }

    // The numpad keys of the hardware keyboard, the conversion of ui_keyboard.cpp:1003-1031.
    private static Key GetHardwareKey(LocalEvent events)
    {
        if (!events.IsAnyKeyPressed)
        {
            return Key.None;
        }

        return events.PressedKeyValue switch
        {
            Key.KeyPadMinus => Key.Minus,
            Key.KeyPad0 => Key.Digit0,
            Key.KeyPad1 => Key.Digit1,
            Key.KeyPad2 => Key.Digit2,
            Key.KeyPad3 => Key.Digit3,
            Key.KeyPad4 => Key.Digit4,
            Key.KeyPad5 => Key.Digit5,
            Key.KeyPad6 => Key.Digit6,
            Key.KeyPad7 => Key.Digit7,
            Key.KeyPad8 => Key.Digit8,
            Key.KeyPad9 => Key.Digit9,
            Key key => key,
        };
    }
}
