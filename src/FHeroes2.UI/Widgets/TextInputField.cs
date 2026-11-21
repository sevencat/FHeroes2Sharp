using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using FHeroes2.Game.System;

namespace FHeroes2.UI.Widgets;

/// <summary>
/// An editable text box with a blinking caret, the port of <c>fheroes2::TextInputField</c>
/// (<c>src/fheroes2/gui/ui_tool.h:147-200</c>, <c>ui_tool.cpp:208-281</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考把要画的那块画面交给 <c>ImageRestorer</c> 记住、把光标做成自己上屏的 <c>MovableSprite</c>；端口每帧从干净缓冲
/// 重画整屏，于是这两件事都改成「往里画 + 报告脏矩形」：光标由 <see cref="Draw"/> 按可见性决定画不画，光标字形直接
/// 贴进缓冲。使用顺序因此是每帧 <see cref="EventProcessing"/>（管闪烁）→ <see cref="Draw"/>（画字与光标），
/// 参考里则是 <c>eventProcessing()</c> 让 <c>MovableSprite</c> 自己隐藏/显示。
/// </para>
/// <para>
/// 多行滚动的算法照抄：光标跑到可视区上沿之上就把整块文字上移，跑到下沿之下就按整行数往下挪，这样滚动总是停在行边界。
/// </para>
/// <para>
/// 差异一：参考的构造函数收一个 <c>Image &amp; output</c> 存起来（给 <c>ImageRestorer</c> 用），端口把它挪到
/// <see cref="Draw"/> 的最后一个参数——与 <see cref="MovableText"/> 等控件的约定一致。
/// </para>
/// <para>
/// 差异二：参考构造函数的 <c>std::optional&lt;SupportedLanguage&gt;</c> 参数没搬，本地化子系统 <c>ui_language.*</c>
/// 未移植（与 <see cref="TextInput"/> 同一处理）。
/// </para>
/// <para>
/// 差异三：<see cref="Height"/> 不是复制一份 <see cref="TextInput"/> 再量，而是用同样的字体与宽度新造一个临时的——
/// 参考的拷贝构造只是把这几个构造参数带过去，结果相同。
/// </para>
/// </remarks>
public sealed class TextInputField
{
    private readonly TextInput _text;
    private readonly MovableSprite _cursor;
    private readonly Rect _textInputArea;
    private readonly bool _isMultiLineText;
    private readonly bool _isSingleLineTextCenterAligned;

    private int _verticalTextOffset;

    /// <summary>Creates an empty input field.</summary>
    /// <param name="textArea">Area the text is drawn into, <c>textArea</c> of the reference.</param>
    /// <param name="isMultiLine">Whether the text wraps instead of being truncated, <c>isMultiLine</c>.</param>
    /// <param name="isCenterAligned">Whether a single line text is centred in <paramref name="textArea"/>.</param>
    /// <remarks>
    /// 参考：<c>ui_tool.cpp:208</c>。参考把背景比文字区外扩 1 像素、上沿再扩 2 像素，给光标与变音符号留位置，
    /// 端口的 <see cref="OverallArea"/> 保留同样的外扩。
    /// </remarks>
    public TextInputField(in Rect textArea, bool isMultiLine, bool isCenterAligned)
    {
        _text = new TextInput(FontType.NormalWhite(), textArea.Width, isMultiLine);
        _cursor = new MovableSprite(FontCharHandler.GetCursorSprite(FontType.NormalWhite()));
        _textInputArea = textArea;
        _isMultiLineText = isMultiLine;
        _isSingleLineTextCenterAligned = !isMultiLine && isCenterAligned;
    }

    /// <summary>The text model itself, the reference's <c>_text</c>.</summary>
    public TextInput Text => _text;

    /// <summary>The caret sprite, the reference's <c>_cursor</c>.</summary>
    /// <remarks>参考：<c>ui_tool.h:194</c>。可见性就是闪烁状态，<see cref="EventProcessing"/> 翻它。</remarks>
    public MovableSprite Cursor => _cursor;

    /// <summary>Area the text is drawn into, the reference's <c>_textInputArea</c>.</summary>
    public Rect TextInputArea => _textInputArea;

    /// <summary>Whether the text wraps, the reference's <c>_isMultiLineText</c>.</summary>
    public bool IsMultiLineText => _isMultiLineText;

    /// <summary>Whether a single line text is centred, the reference's <c>_isSingleLineTextCenterAligned</c>.</summary>
    public bool IsSingleLineTextCenterAligned => _isSingleLineTextCenterAligned;

    /// <summary>How far the multi-line text is scrolled up, the reference's <c>_verticalTextOffset</c>.</summary>
    public int VerticalTextOffset => _verticalTextOffset;

    /// <summary>The area the caret occupies on the target, <c>getCursorArea()</c> (<c>ui_tool.h:157</c>).</summary>
    public Rect CursorArea => _cursor.Sprite is { } sprite
        ? new Rect(_cursor.X, _cursor.Y, sprite.Width, sprite.Height)
        : Rect.Empty;

    /// <summary>
    /// The area the field may paint on, <c>getOverallArea()</c> (<c>ui_tool.h:162</c>): the text area enlarged by one
    /// pixel at both sides and by two pixels at the top.
    /// </summary>
    public Rect OverallArea =>
        new(_textInputArea.X - 1, _textInputArea.Y - 2, _textInputArea.Width + 2, _textInputArea.Height + 2);

    /// <summary>
    /// Advances the caret blink, <c>TextInputField::eventProcessing()</c> (<c>ui_tool.cpp:222-236</c>).
    /// </summary>
    /// <returns>Whether the caret changed visibility, i.e. whether the field has to be redrawn.</returns>
    public bool EventProcessing()
    {
        if (!GameDelays.ValidateAnimationDelay(DelayType.CursorBlinkDelay))
        {
            return false;
        }

        if (_cursor.IsHidden)
        {
            _cursor.Show();
        }
        else
        {
            _cursor.Hide();
        }

        return true;
    }

    /// <summary>
    /// Draws the text and, when it is in its visible blink phase, the caret,
    /// <c>TextInputField::draw()</c> (<c>ui_tool.cpp:238-281</c>).
    /// </summary>
    /// <param name="newText">The text to show.</param>
    /// <param name="cursorPositionInText">Caret offset in bytes, counted from the start of the text.</param>
    /// <param name="output">Frame buffer.</param>
    /// <returns>The area that was painted, ready to be marked dirty.</returns>
    /// <remarks>
    /// 参考开头先 <c>_cursor.hide()</c>、结尾无条件 <c>_cursor.show()</c> 并重置闪烁计时；端口把「无条件显示」改成
    /// 「进来时可见才画、才重置」，否则在整屏重画的模型里闪烁永远关不掉。闪烁关掉的那一帧仍会重画文字，
    /// 只是不画光标、不动计时。
    /// </remarks>
    public Rect Draw(string newText, int cursorPositionInText, PixelBuffer output)
    {
        ArgumentNullException.ThrowIfNull(output);

        bool isCaretVisible = !_cursor.IsHidden;

        _cursor.Hide();

        _text.Set(newText, cursorPositionInText);

        if (_isMultiLineText)
        {
            Rect caretArea = _text.CursorArea;
            int visibleTextHeight = _textInputArea.Height - 2;

            if (caretArea.Y < _verticalTextOffset)
            {
                _verticalTextOffset = caretArea.Y;
            }
            else
            {
                int caretBottom = caretArea.Y + caretArea.Height;
                int visibleBottom = _verticalTextOffset + visibleTextHeight;

                if (caretBottom > visibleBottom)
                {
                    int lineHeight = _text.Height();
                    if (lineHeight > 0)
                    {
                        int overflow = caretBottom - visibleBottom;

                        // Only whole lines are scrolled off, exactly as in the reference.
                        _verticalTextOffset += ((overflow + lineHeight - 1) / lineHeight) * lineHeight;
                    }
                }
            }
        }
        else
        {
            _verticalTextOffset = 0;
        }

        // Multi-line text is always centred automatically.
        int offsetX = _isSingleLineTextCenterAligned
            ? _textInputArea.X + ((_textInputArea.Width - _text.Width()) / 2)
            : _textInputArea.X;
        int offsetY = _textInputArea.Y + 2 - _verticalTextOffset;

        Rect textRoi = _isMultiLineText ? _textInputArea : OverallArea;

        _text.DrawInRoi(offsetX, offsetY, output, textRoi);

        Rect cursorArea = _text.CursorArea;
        _cursor.SetPosition(cursorArea.X + offsetX, cursorArea.Y + offsetY);

        if (isCaretVisible && _cursor.Sprite is { } caret)
        {
            ImageOps.BlitSpriteClipped(output, new Point(_cursor.X, _cursor.Y), caret, caret.Image.Bounds, output.Bounds);

            _cursor.Show();

            GameDelays.AnimateResetDelay(DelayType.CursorBlinkDelay);
        }

        return textRoi.Intersect(output.Bounds);
    }

    /// <summary>Replaces the text without drawing it, <c>set()</c> (<c>ui_tool.h:169</c>).</summary>
    /// <param name="text">The new text.</param>
    /// <param name="cursorPosition">Caret offset in bytes.</param>
    public void Set(string text, int cursorPosition) => _text.Set(text, cursorPosition);

    /// <summary>
    /// Caret offset for a point on the target, <c>getCursorInTextPosition()</c> (<c>ui_tool.h:174</c>).
    /// </summary>
    /// <param name="pos">A point on the target, usually the mouse position.</param>
    /// <returns>Caret offset in bytes, counted from the start of the text.</returns>
    public int GetCursorInTextPosition(in Point pos) =>
        _text.GetCursorPosition(new Point(pos.X, pos.Y + _verticalTextOffset), _textInputArea, _isSingleLineTextCenterAligned);

    /// <summary>
    /// Caret offset after moving one line up or down, <c>getCursorPositionInAdjacentLine()</c>
    /// (<c>ui_tool.h:179</c>).
    /// </summary>
    /// <param name="currentPos">Current caret offset in bytes.</param>
    /// <param name="moveUp">Whether to move up rather than down.</param>
    /// <returns>The new caret offset in bytes.</returns>
    public int GetCursorPositionInAdjacentLine(int currentPos, bool moveUp) =>
        _text.GetCursorPositionInAdjacentLine(currentPos, _textInputArea.Width, moveUp);

    /// <summary>
    /// Height <paramref name="text"/> would take in this field, <c>height( const std::string &amp; )</c>
    /// (<c>ui_tool.h:184</c>). The field's own text is left alone.
    /// </summary>
    /// <param name="text">The text to measure.</param>
    /// <returns>Height in pixels.</returns>
    public int Height(string text)
    {
        TextInput measure = new(FontType.NormalWhite(), _textInputArea.Width, _isMultiLineText);
        measure.Set(text ?? string.Empty, 0);

        return measure.Height(_textInputArea.Width);
    }
}
