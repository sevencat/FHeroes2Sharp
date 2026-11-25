using FHeroes2.Assets.Fonts;
using FHeroes2.Assets.Icn;
using FHeroes2.Core.Diagnostics;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;
using FHeroes2.UI.Input;
using FHeroes2.UI.Widgets;
using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The drawable, clickable <c>Dialog::inputString()</c> window (<c>src/fheroes2/dialog/dialog_selectcount.cpp:227-460</c>).
/// </summary>
/// <remarks>
/// <para>
/// The window is a <see cref="FrameBox"/> with the title and the question wrapped to 244 pixels, the input area
/// below them, and three buttons: OKAY at the bottom left, CANCEL at the bottom right and the virtual keyboard
/// button centred above them. Where every piece lands is <see cref="InputStringLayout"/>'s job.
/// </para>
/// <para>
/// 参考的文本控件 <see cref="TextInputField"/> 自带光标 <c>MovableSprite</c>，靠 <c>eventProcessing()</c> 翻它的
/// 可见性；端口每帧整屏重画，若照用就会每帧把闪烁计时重置，光标永远亮着。所以这里把控件的光标贴图一直按在隐藏
/// 状态，闪烁由 <see cref="Update"/> 自己翻一个布尔量、由 <see cref="Draw"/> 自己贴同一个字形。
/// </para>
/// <para>
/// 虚拟键盘按钮照参考画出来并参与按下态；端口还没有虚拟键盘的窗口（<see cref="VirtualKeyboardView"/> 已移植但
/// 没有场景），点击时经 <see cref="VirtualKeyboardRequested"/> 通知调用方，没有人订阅就什么也不做。
/// </para>
/// </remarks>
public sealed class InputStringView
{
    /// <summary>Frame of the OKAY button in SYSTEM.ICN / SYSTEME.ICN, the released look.</summary>
    public const int OkayReleasedFrame = 1;

    /// <summary>Frame of the OKAY button in SYSTEM.ICN / SYSTEME.ICN, the pressed look.</summary>
    public const int OkayPressedFrame = 2;

    /// <summary>Frame of the CANCEL button in SYSTEM.ICN / SYSTEME.ICN, the released look.</summary>
    public const int CancelReleasedFrame = 3;

    /// <summary>Frame of the CANCEL button in SYSTEM.ICN / SYSTEME.ICN, the pressed look.</summary>
    public const int CancelPressedFrame = 4;

    private readonly InputStringDialog _dialog;
    private readonly FrameBox _frame;
    private readonly Sprite? _inputArtwork;
    private readonly TextInputField _field;
    private readonly Sprite _caret;
    private readonly ButtonSprite _okay;
    private readonly ButtonSprite _cancel;
    private readonly ButtonSprite? _virtualKeyboard;
    private readonly IGlyphSource _titleFont;
    private readonly IGlyphSource _bodyFont;
    private readonly Rect _inputArea;
    private readonly Rect _textArea;
    private readonly Rect _clickArea;

    private string _text;
    private int _cursor;
    private Point? _cursorPoint;
    private bool _leftDown;
    private bool _caretVisible = true;

    private InputStringView(
        InputStringDialog dialog,
        FrameBox frame,
        Sprite? inputArtwork,
        TextInputField field,
        Sprite caret,
        ButtonSprite okay,
        ButtonSprite cancel,
        ButtonSprite? virtualKeyboard,
        IGlyphSource titleFont,
        IGlyphSource bodyFont,
        Rect inputArea,
        Rect textArea,
        int titleHeight)
    {
        _dialog = dialog;
        _frame = frame;
        _inputArtwork = inputArtwork;
        _field = field;
        _caret = caret;
        _okay = okay;
        _cancel = cancel;
        _virtualKeyboard = virtualKeyboard;
        _titleFont = titleFont;
        _bodyFont = bodyFont;
        _inputArea = inputArea;
        _textArea = textArea;
        _clickArea = InputStringLayout.ClickArea(textArea);
        _text = dialog.InitialText;
        _cursor = _text.Length;

        // 控件自己的光标贴图永远不画，闪烁见类注释。
        _field.Cursor.Hide();
        _field.Set(_text, _cursor);

        UpdateOkayState();

        _ = titleHeight;
    }

    /// <summary>Raised when the player presses the virtual keyboard button.</summary>
    public event Action? VirtualKeyboardRequested;

    /// <summary>The model this view answers into.</summary>
    public InputStringDialog Dialog => _dialog;

    /// <summary>Text area of the frame, <see cref="FrameBox.Area"/>.</summary>
    public Rect Area => _frame.Area;

    /// <summary>Whether OKAY or CANCEL has closed the window.</summary>
    public bool IsClosed { get; private set; }

    /// <summary>Whether the window was closed by OKAY rather than by CANCEL.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>The text as typed so far.</summary>
    public string Text => _text;

    /// <summary>Caret offset in the text, counted from its start.</summary>
    public int CursorPosition => _cursor;

    /// <summary>The area the text itself is drawn in.</summary>
    public Rect TextArea => _textArea;

    /// <summary>Area a click moves the caret into, <c>textEditClickArea</c>.</summary>
    public Rect ClickArea => _clickArea;

    /// <summary>Area of the OKAY button.</summary>
    public Rect OkayArea => _okay.Area;

    /// <summary>Area of the CANCEL button.</summary>
    public Rect CancelArea => _cancel.Area;

    /// <summary>Area of the virtual keyboard button, or <see cref="Rect.Empty"/> when its artwork is missing.</summary>
    public Rect VirtualKeyboardArea => _virtualKeyboard?.Area ?? Rect.Empty;

    /// <summary>Whether OKAY is clickable, i.e. whether the text is not empty.</summary>
    public bool IsOkayEnabled => _okay.IsEnabled;

    /// <summary>
    /// Builds the window, <c>Dialog::inputString()</c> up to its loop.
    /// </summary>
    /// <param name="dialog">The model with the texts and the limits.</param>
    /// <param name="titleFont">Face of the title, the yellow one of the reference.</param>
    /// <param name="bodyFont">Face of the question, the white one of the reference.</param>
    /// <param name="screen">Screen the frame is centred on; 640x480 when omitted.</param>
    /// <returns>The window, or <see langword="null"/> when an archive is missing frames.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dialog"/> or a font is <see langword="null"/>.</exception>
    public static InputStringView? TryCreate(
        InputStringDialog dialog, IGlyphSource titleFont, IGlyphSource bodyFont, Size? screen = null)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(titleFont);
        ArgumentNullException.ThrowIfNull(bodyFont);

        bool isEvil = Settings.Get().IsEvilInterfaceEnabled;

        // 输入区的图与 FrameBox 的外框恰好反着取（dialog_selectcount.cpp:245）。
        Sprite? inputArtwork = Load(IcnCatalog.GetFileName(isEvil ? IcnType.Buybuild : IcnType.Buybuile), 3);

        // 参考的 UNIFORM_*_OKAY/CANCEL_BUTTON 是引擎生成的 ICN；用原版资源时它就直接取 SYSTEM/SYSTEME 的第
        // 1、2 帧与第 3、4 帧（game_assets.cpp:1320-1324、1338-1342），和设置对话框等处用的那一对是同一张图。
        // 端口没有生成 ICN 这一层，所以按这条等价关系直接从档案里取。
        IcnType buttonFile = isEvil ? IcnType.Systeme : IcnType.System;
        ButtonSprite? okay = CreateArchiveButton(buttonFile, OkayReleasedFrame, OkayPressedFrame);
        ButtonSprite? cancel = CreateArchiveButton(buttonFile, CancelReleasedFrame, CancelPressedFrame);

        // 虚拟键盘键同样是生成的 ICN（game_assets.cpp:5466-5511），按参考的拼法现做；h2d 不在时省掉这个键。
        ButtonSprite? virtualKeyboard = ButtonFactory.MakeVirtualKeyboardButton(isEvil) is { } keyboard
            ? new ButtonSprite(keyboard.Released, keyboard.Pressed, disabled: null, 0, 0)
            : null;

        if (okay is null || cancel is null || (inputArtwork is null && !dialog.IsMultiLine))
        {
            GameLog.Warn(
                "The input window artwork is missing: "
                + $"input={(inputArtwork is null ? "none" : "ok")}, "
                + $"okay={(okay is null ? "none" : "ok")}, "
                + $"cancel={(cancel is null ? "none" : "ok")}, "
                + $"keyboard={(virtualKeyboard is null ? "none" : "ok")}.");

            return null;
        }

        int titleHeight = dialog.Title.Length > 0 ? Measure(titleFont, dialog.Title) + InputStringLayout.Gap : 0;
        int bodyHeight = Measure(bodyFont, dialog.Body);

        int inputWidth = dialog.IsMultiLine ? InputStringLayout.MultiLineWidth : inputArtwork!.Width;
        int inputHeight = dialog.IsMultiLine ? InputStringLayout.MultiLineHeight : inputArtwork!.Height;

        FrameBox frame = FrameBox.Create(
            InputStringLayout.FrameHeight(titleHeight, bodyHeight, inputHeight), showButtons: true, startY: -1, screen);

        Rect inputArea = InputStringLayout.InputArea(frame.Area, inputWidth, inputHeight, titleHeight, bodyHeight);
        Rect textArea = InputStringLayout.TextInputArea(inputArea, dialog.IsMultiLine);

        TextInputField field = new(textArea, dialog.IsMultiLine, isCenterAligned: true);

        okay.SetPosition(frame.Area.X, frame.Area.Y + frame.Area.Height - okay.Released.Height);
        cancel.SetPosition(
            frame.Area.X + frame.Area.Width - cancel.Released.Width,
            frame.Area.Y + frame.Area.Height - cancel.Released.Height);
        if (virtualKeyboard is not null)
        {
            Point position = InputStringLayout.VirtualKeyboardPosition(
                frame.Area, new Size(virtualKeyboard.Released.Width, virtualKeyboard.Released.Height));

            virtualKeyboard.SetPosition(position.X, position.Y);
        }

        return new InputStringView(
            dialog,
            frame,
            inputArtwork,
            field,
            FontCharHandler.GetCursorSprite(FontType.NormalWhite()),
            okay,
            cancel,
            virtualKeyboard,
            titleFont,
            bodyFont,
            inputArea,
            textArea,
            titleHeight);
    }

    /// <summary>Records where the mouse is, so that the buttons can show their pressed look.</summary>
    /// <param name="point">Cursor position in logical pixels, or <see langword="null"/> when it left the window.</param>
    public void SetCursor(Point? point) => _cursorPoint = point;

    /// <summary>Records whether the left mouse button is held, so that the buttons can show their pressed look.</summary>
    /// <param name="down">Whether the left button is now held.</param>
    public void SetLeftButton(bool down) => _leftDown = down;

    /// <summary>
    /// Answers a click at <paramref name="point"/>: the button it landed on, or a caret move, or nothing.
    /// </summary>
    /// <param name="point">Where the left button was pressed, in logical pixels.</param>
    /// <returns>
    /// <see langword="true"/> when the window closed or the caret moved, i.e. when it has to be redrawn.
    /// </returns>
    public bool ClickLeft(Point point)
    {
        if (IsClosed)
        {
            return false;
        }

        if (_okay.Area.Contains(point))
        {
            if (!_okay.IsEnabled)
            {
                return false;
            }

            Confirm();

            return true;
        }

        if (_cancel.Area.Contains(point))
        {
            Cancel();

            return true;
        }

        if (_virtualKeyboard is { } keyboard && keyboard.Area.Contains(point))
        {
            VirtualKeyboardRequested?.Invoke();

            return false;
        }

        if (_clickArea.Contains(point))
        {
            int position = _field.GetCursorInTextPosition(point);

            if (position == _cursor)
            {
                return false;
            }

            _cursor = position;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Answers a hot key that cannot be typed into the field, the <c>KEY_ENTER</c> / <c>KEY_ESCAPE</c> checks of
    /// <c>dialog_selectcount.cpp:336-343</c>.
    /// </summary>
    /// <param name="hotKey">The hot key.</param>
    /// <returns><see langword="true"/> when the window closed.</returns>
    public bool ProcessHotKey(ButtonHotKey hotKey)
    {
        if (IsClosed)
        {
            return false;
        }

        switch (hotKey)
        {
            case ButtonHotKey.Okay when !_dialog.IsMultiLine:
            case ButtonHotKey.Cancel:

                // 参考里单行的回车就是「确认」，而 ESC 一律是「取消」；OKAY 不可用时回车什么也不做。
                if (hotKey == ButtonHotKey.Cancel)
                {
                    Cancel();
                }
                else if (_okay.IsEnabled)
                {
                    Confirm();
                }

                return IsClosed;

            default:
                return false;
        }
    }

    /// <summary>
    /// Types a key into the field, <c>InsertKeySym()</c> guarded by <c>charLimit</c> and by the height of a
    /// multi-line text (<c>dialog_selectcount.cpp:368-409</c>).
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifier">Modifiers held with it.</param>
    /// <returns><see langword="true"/> when the text or the caret changed.</returns>
    public bool PressKey(Key key, KeyModifier modifier)
    {
        if (IsClosed || key is Key.Enter or Key.Escape or Key.Tab)
        {
            return false;
        }

        bool isEditing = key is Key.Backspace or Key.Delete or Key.Up or Key.Down or Key.Left or Key.Right;

        if (!isEditing && _dialog.CharacterLimit > 0 && _text.Length >= _dialog.CharacterLimit)
        {
            return false;
        }

        System.Text.StringBuilder text = new(_text);
        int position = KeyText.InsertKeySym(text, _cursor, key, modifier);
        string candidate = text.ToString();

        if (candidate == _text && position == _cursor)
        {
            return false;
        }

        if (_dialog.IsMultiLine && _field.Height(candidate) > _textArea.Height)
        {
            return false;
        }

        _text = candidate;
        _cursor = position;

        UpdateOkayState();

        return true;
    }

    /// <summary>Advances the caret blink, the <c>eventProcessing()</c> of the reference's loop.</summary>
    /// <returns><see langword="true"/> when the caret changed visibility.</returns>
    public bool Update()
    {
        if (IsClosed || !GameDelays.ValidateAnimationDelay(DelayType.CursorBlinkDelay))
        {
            return false;
        }

        _caretVisible = !_caretVisible;

        return true;
    }

    /// <summary>Draws the frame, the texts, the input area, the caret and the buttons.</summary>
    /// <param name="target">Indexed frame buffer of the scene.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer target)
    {
        ArgumentNullException.ThrowIfNull(target);

        _frame.Draw(target);

        int titleHeight = _dialog.Title.Length > 0 ? Measure(_titleFont, _dialog.Title) + InputStringLayout.Gap : 0;

        if (_dialog.Title.Length > 0)
        {
            GlyphText.DrawMultiline(
                _titleFont, target, InputStringLayout.TitlePosition(_frame.Area), _dialog.Title, FrameBox.AreaWidth,
                center: true);
        }

        if (_dialog.Body.Length > 0)
        {
            GlyphText.DrawMultiline(
                _bodyFont, target, InputStringLayout.BodyPosition(_frame.Area, titleHeight), _dialog.Body,
                FrameBox.AreaWidth, center: true);
        }

        if (_inputArtwork is { } artwork)
        {
            ImageOps.BlitSprite(target, _inputArea.Position, artwork);
        }
        else
        {
            StandardWindow.ApplyTextBackgroundShading(target, _inputArea);
        }

        _field.Draw(_text, _cursor, target);

        if (_caretVisible)
        {
            Rect caretArea = _field.CursorArea;

            if (!caretArea.IsEmpty)
            {
                ImageOps.BlitSpriteClipped(target, caretArea.Position, _caret, _caret.Image.Bounds, target.Bounds);
            }
        }

        // 参考的对话框每帧只重画状态变了的那一块（display 自己留着上一帧的像素），端口每帧整屏重绘，
        // 所以先按鼠标位置摆好按下/松开的样子，再无条件画一遍，不能用只画「状态变化」的 DrawOnState()。
        ApplyHeldState(_okay);
        ApplyHeldState(_cancel);

        _okay.Draw(target);
        _cancel.Draw(target);

        if (_virtualKeyboard is not null)
        {
            ApplyHeldState(_virtualKeyboard);
            _virtualKeyboard.Draw(target);
        }
    }

    /// <summary>Confirms the window with the typed text, the <c>KEY_ENTER</c> / OKAY branch of the reference.</summary>
    public void Confirm()
    {
        if (IsClosed)
        {
            return;
        }

        _dialog.Choose(_text);
        IsConfirmed = true;
        IsClosed = true;
    }

    /// <summary>Cancels the window, the <c>KEY_ESCAPE</c> / CANCEL branch of the reference.</summary>
    public void Cancel()
    {
        if (IsClosed)
        {
            return;
        }

        _dialog.Choose(null);
        IsConfirmed = false;
        IsClosed = true;
    }

    /// <summary>Height <paramref name="text"/> takes when wrapped to the frame's text area.</summary>
    private static int Measure(IGlyphSource font, string text) =>
        text.Length == 0 ? 0 : GlyphText.WrapLines(font, text, FrameBox.AreaWidth).Count * font.LineHeight;

    private static Sprite? Load(string fileName, int frame) =>
        GameAssets.GetImageCount(fileName) > frame ? GameAssets.GetImage(fileName, frame) : null;

    private static ButtonSprite? CreateArchiveButton(IcnType icnType, int releasedFrame, int pressedFrame)
    {
        string fileName = IcnCatalog.GetFileName(icnType);
        Sprite? released = Load(fileName, releasedFrame);
        Sprite? pressed = Load(fileName, pressedFrame);

        return released is null || pressed is null
            ? null
            : new ButtonSprite(released, pressed, disabled: null, 0, 0);
    }

    private bool IsHeld(in Rect area) => _leftDown && _cursorPoint is { } point && area.Contains(point);

    /// <summary>Puts the button into the look the mouse position and the left button ask for, <c>drawOnState()</c>.</summary>
    /// <param name="button">Button to update.</param>
    private void ApplyHeldState(ButtonSprite button)
    {
        if (IsHeld(button.Area))
        {
            button.Press();
        }
        else
        {
            button.Release();
        }
    }

    private void UpdateOkayState()
    {
        if (_text.Length == 0)
        {
            _okay.Disable();
        }
        else
        {
            _okay.Enable();
        }
    }
}
