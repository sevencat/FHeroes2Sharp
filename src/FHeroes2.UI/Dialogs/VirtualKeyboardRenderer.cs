using System.Text;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.UI.Widgets;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The window, the text being edited and the caret of the virtual keyboard, a port of
/// <c>KeyboardRenderer</c> (<c>ui_keyboard.cpp:192-330</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的 <c>resize()</c> 是「改窗口尺寸 + 立刻把窗口和输入框画进 <c>Display</c>」，因为屏幕上的其他部分要
/// 靠 <c>ImageRestorer</c> 恢复。端口每帧从干净缓冲重画，<see cref="Resize"/> 只负责摆好窗口与输入框，
/// 真正的绘制在 <see cref="Draw"/>。
/// </para>
/// <para>
/// 窗口位置照抄参考：宽度居中，高度以 <see cref="VirtualKeyboardLayout.DefaultWindowHeight"/> 为基准上下居中，
/// 多出来的行数全部往上长，所以键盘变高时确定键的位置不动。
/// </para>
/// <para>
/// 光标位置：参考的 <c>_cursorPosition</c> 是 <c>std::string</c> 的字节下标，端口的文本是 UTF-16，
/// 这里存的是字符下标，调 <see cref="TextInputField"/> 时再换算成 UTF-8 字节下标。插入的都是
/// 单个 BMP 字符，两种下标只有在非 ASCII 文本上才差一个倍数。
/// </para>
/// <para>
/// 差异：<c>lengthLimit</c> 参考比的是字节数，端口比的是字符数；键盘每次只插入一个字符，
/// 对纯 ASCII 输入两者等价，对西里尔字母则端口允许输入两倍的字符。
/// </para>
/// </remarks>
public sealed class VirtualKeyboardRenderer
{
    private readonly VirtualKeyboardArtwork _artwork;
    private readonly bool _isMultiLineText;
    private readonly bool _isEvilInterface;
    private readonly int _lengthLimit;
    private readonly Size _screen;

    private StandardWindow? _window;
    private TextInputField? _textField;

    private Rect _textInputArea =
        new(0, 0, VirtualKeyboardLayout.InputAreaSize.Width, VirtualKeyboardLayout.InputAreaSize.Height);

    private int _cursorPosition;

    /// <summary>Creates the renderer of an empty text.</summary>
    /// <param name="screen">Size of the screen the keyboard is centred on.</param>
    /// <param name="artwork">Window frame, key plate, input frame and okay button.</param>
    /// <param name="text">The text to edit.</param>
    /// <param name="lengthLimit">Maximum number of characters; zero disables the input.</param>
    /// <param name="isMultiLineText">Whether the text wraps and has its own enter key.</param>
    /// <param name="isEvilInterface">Whether the evil artwork is selected.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="artwork"/> or <paramref name="text"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthLimit"/> is negative.</exception>
    public VirtualKeyboardRenderer(
        Size screen,
        VirtualKeyboardArtwork artwork,
        string text,
        int lengthLimit,
        bool isMultiLineText,
        bool isEvilInterface)
    {
        ArgumentNullException.ThrowIfNull(artwork);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(lengthLimit);

        _screen = screen;
        _artwork = artwork;
        _isMultiLineText = isMultiLineText;
        _isEvilInterface = isEvilInterface;
        _lengthLimit = lengthLimit;
        Text = text;
        _cursorPosition = text.Length;
    }

    /// <summary>The text being edited, <c>_text</c> of the reference.</summary>
    public string Text { get; private set; }

    /// <summary>Caret offset in UTF-16 characters, <c>_cursorPosition</c> of the reference (bytes there).</summary>
    public int CursorPosition => _cursorPosition;

    /// <summary>Whether the text wraps, <c>_isMultiLineText</c>.</summary>
    public bool IsMultiLineText => _isMultiLineText;

    /// <summary>Whether the evil artwork is selected, <c>_isEvilInterface</c>.</summary>
    public bool IsEvilInterface => _isEvilInterface;

    /// <summary>Area of the window interior, <c>getRoi()</c>; empty before the first <see cref="Resize"/>.</summary>
    public Rect WindowArea => _window?.ActiveArea ?? Rect.Empty;

    /// <summary>Area of the text input frame, <c>getTextInputArea()</c>.</summary>
    public Rect TextInputArea => _textInputArea;

    /// <summary>
    /// How much taller the window is than the default one because of the multi line input,
    /// <c>getInputAreaHeightIncrease()</c> (<c>ui_keyboard.cpp:186-190</c>).
    /// </summary>
    /// <remarks>按键行从这个高度以下开始，所以窗口长高后按键整体下移。</remarks>
    public int InputAreaHeightIncrease => _isMultiLineText
        ? VirtualKeyboardLayout.MultiLineInputAreaHeight - VirtualKeyboardLayout.InputAreaSize.Height
        : 0;

    /// <summary>
    /// Lays the window out for a new interior size, <c>KeyboardRenderer::resize()</c>
    /// (<c>ui_keyboard.cpp:238-283</c>).
    /// </summary>
    /// <param name="size">Interior size of the window.</param>
    /// <returns><see langword="true"/> when the window was rebuilt, i.e. the size actually changed.</returns>
    public bool Resize(Size size)
    {
        if (_window is not null && size.Width == _window.ActiveArea.Width && size.Height == _window.ActiveArea.Height)
        {
            return false;
        }

        Size currentInputAreaSize = _isMultiLineText
            ? VirtualKeyboardLayout.MultiLineInputAreaSize
            : VirtualKeyboardLayout.InputAreaSize;

        Point defaultOffset = new(
            (_screen.Width - size.Width) / 2,
            (_screen.Height - VirtualKeyboardLayout.DefaultWindowHeight) / 2);

        Point offset = new(
            defaultOffset.X,
            defaultOffset.Y - (size.Height - VirtualKeyboardLayout.DefaultWindowHeight));

        _window = StandardWindow.Create(new Rect(offset.X, offset.Y, size.Width, size.Height));

        Rect windowRoi = _window.ActiveArea;
        _textInputArea = new Rect(
            windowRoi.X + ((windowRoi.Width - currentInputAreaSize.Width) / 2),
            windowRoi.Y + VirtualKeyboardLayout.InputAreaOffsetFromWindowTop,
            currentInputAreaSize.Width,
            currentInputAreaSize.Height);

        _textField = new TextInputField(
            new Rect(
                _textInputArea.X + VirtualKeyboardLayout.InputAreaBorders,
                _textInputArea.Y + VirtualKeyboardLayout.InputAreaBorders,
                currentInputAreaSize.Width - (2 * VirtualKeyboardLayout.InputAreaBorders),
                currentInputAreaSize.Height - (2 * VirtualKeyboardLayout.InputAreaBorders)),
            _isMultiLineText,
            false);

        return true;
    }

    /// <summary>Advances the caret blink, <c>_textUI-&gt;eventProcessing()</c> of the reference.</summary>
    public void Update() => _textField?.EventProcessing();

    /// <summary>
    /// Draws the window, the frame of the input and the text with its caret.
    /// </summary>
    /// <param name="output">Frame buffer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void Draw(PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (_window is null)
        {
            return;
        }

        _window.Draw(output);

        if (_isMultiLineText)
        {
            StandardWindow.ApplyTextBackgroundShading(output, _textInputArea);
        }
        else
        {
            // A single line input uses the request background of the original interface.
            ImageOps.Blit(output, _textInputArea.Position, _artwork.RequestBackground.Image,
                new Rect(40, 286, _textInputArea.Width, _textInputArea.Height));

            if (_isEvilInterface)
            {
                ImageOps.ApplyPalette(output, _textInputArea, PaletteTable.Get(PaletteId.GoodToEvilInterface));
            }
        }

        _textField?.Draw(Text, ToByteOffset(Text, _cursorPosition), output);
    }

    /// <summary>Inserts one character at the caret, <c>insertCharacter()</c> (<c>ui_keyboard.cpp:293-307</c>).</summary>
    /// <param name="character">The character to insert.</param>
    public void InsertCharacter(char character)
    {
        if (Text.Length >= _lengthLimit)
        {
            return;
        }

        Text = Text.Insert(_cursorPosition, character.ToString());
        ++_cursorPosition;
    }

    /// <summary>Removes the character before the caret, <c>removeCharacter()</c> (<c>ui_keyboard.cpp:309-320</c>).</summary>
    public void RemoveCharacter()
    {
        if (Text.Length == 0 || _cursorPosition == 0)
        {
            return;
        }

        Text = _cursorPosition >= Text.Length
            ? Text[..^1]
            : Text.Remove(_cursorPosition - 1, 1);

        --_cursorPosition;
    }

    /// <summary>
    /// Adds or removes the minus sign in front of the text, <c>swapSign()</c>
    /// (<c>ui_keyboard.cpp:322-335</c>).
    /// </summary>
    public void SwapSign()
    {
        if (Text.Length == 0)
        {
            return;
        }

        if (Text[0] == '-')
        {
            Text = Text[1..];
            if (_cursorPosition > 0)
            {
                --_cursorPosition;
            }
        }
        else
        {
            Text = "-" + Text;
            ++_cursorPosition;
        }
    }

    /// <summary>
    /// Moves the caret to the place the user clicked, <c>setCursorPosition( const Point &amp; )</c>
    /// (<c>ui_keyboard.cpp:337-350</c>).
    /// </summary>
    /// <param name="position">Mouse position.</param>
    /// <returns><see langword="true"/> when the caret moved.</returns>
    public bool SetCursorPosition(in Point position)
    {
        if (_textField is null || !_textField.OverallArea.Contains(position))
        {
            return false;
        }

        int cursorPosition = ToCharIndex(Text, _textField.GetCursorInTextPosition(position));
        if (cursorPosition == _cursorPosition)
        {
            return false;
        }

        _cursorPosition = cursorPosition;
        return true;
    }

    /// <summary>
    /// Moves the caret by a key press, <c>setCursorPosition( CursorPosition )</c>
    /// (<c>ui_keyboard.cpp:352-390</c>).
    /// </summary>
    /// <param name="position">Where to move the caret.</param>
    /// <returns><see langword="true"/> when the caret moved.</returns>
    public bool SetCursorPosition(KeyboardCursorMove position)
    {
        int cursorPosition;

        switch (position)
        {
        case KeyboardCursorMove.PreviousChar:
            cursorPosition = Math.Max(_cursorPosition - 1, 0);
            break;
        case KeyboardCursorMove.NextChar:
            cursorPosition = Math.Min(_cursorPosition + 1, Text.Length);
            break;
        case KeyboardCursorMove.PreviousLine:
        case KeyboardCursorMove.NextLine:
        {
            if (!_isMultiLineText || _textField is null)
            {
                return false;
            }

            bool moveUp = position == KeyboardCursorMove.PreviousLine;
            int adjacent = _textField.GetCursorPositionInAdjacentLine(ToByteOffset(Text, _cursorPosition), moveUp);
            cursorPosition = ToCharIndex(Text, adjacent);
            break;
        }
        case KeyboardCursorMove.BeginningOfText:
            cursorPosition = 0;
            break;
        case KeyboardCursorMove.EndOfText:
            cursorPosition = Text.Length;
            break;
        default:
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        cursorPosition = Math.Clamp(cursorPosition, 0, Text.Length);
        if (cursorPosition == _cursorPosition)
        {
            return false;
        }

        _cursorPosition = cursorPosition;
        return true;
    }

    /// <summary>UTF-8 byte offset of a caret offset given in characters.</summary>
    internal static int ToByteOffset(string text, int charIndex) =>
        charIndex <= 0 ? 0 : Encoding.UTF8.GetByteCount(text.AsSpan(0, Math.Min(charIndex, text.Length)));

    /// <summary>Caret offset in characters of a UTF-8 byte offset.</summary>
    internal static int ToCharIndex(string text, int byteOffset)
    {
        if (byteOffset <= 0)
        {
            return 0;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        int length = Math.Min(byteOffset, bytes.Length);

        return Encoding.UTF8.GetCharCount(bytes, 0, length);
    }
}
