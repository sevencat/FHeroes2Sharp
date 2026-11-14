using System.Text;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// A single-line or multi-line editable text field, the reference's <c>fheroes2::TextInput</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_text.h:278</c> class TextInput（构造 282、<c>set</c> 293、<c>width</c> 302、
/// <c>drawInRoi</c> 306、<c>fitToOneRow</c> 310、<c>getCursorPosition</c> 312、<c>cursorArea</c> 321、
/// <c>getCursorPositionInAdjacentLine</c> 326、私有成员 330–344）；实现
/// <c>src/fheroes2/gui/ui_text.cpp:702</c>（width）、<c>:732</c>（drawInRoi）、<c>:766</c>
/// （getCursorPositionInAdjacentLine）、<c>:826</c>（fitToOneRow）、<c>:931</c>
/// （_getMultiLineTextInputCursorPosition）、<c>:990</c>（_getTextInputCursorPosition）、
/// <c>:1022</c>（_updateCursorAreaInText）。
/// </para>
/// <para>
/// 差异：参考构造函数的最后一个参数是 <c>std::optional&lt;SupportedLanguage&gt;</c>，本地化子系统
/// <c>ui_language.*</c> 还没移植，这里省掉该参数（与 <see cref="TextBase"/> 里没搬 <c>_language</c> 一致）。
/// 参考靠 <c>using Text::width</c> / <c>using Text::drawInRoi</c> 把被隐藏的重载重新暴露出来，C# 的重载
/// 本来就按签名区分，不需要这一步。
/// </para>
/// <para>
/// 参考的 <c>Rect</c> 成员可以就地自增（<c>_cursorArea.x += ...</c>），C# 的 <see cref="Rect"/> 是只读记录
/// 结构体，所以这里用局部坐标算完再一次性赋值。
/// </para>
/// </remarks>
public sealed class TextInput : Text
{
    /// <summary><c>cursorChar</c>: the glyph that marks the caret.</summary>
    private const byte CursorChar = (byte)'|';

    /// <summary>The caret position kept clear of the truncated text edges.</summary>
    private const int CursorToTruncationDistance = 4;

    private Rect _cursorArea;
    private int _cursorPositionInText;
    private int _visibleTextBeginPos;
    private int _visibleTextLength;

    // The (<1) value of `_maxTextWidth` makes the code render the text in one line without limiting its width.
    private int _maxTextWidth;

    // When `false` the text that exceeds the `_maxTextWidth` is moved to the next line, otherwise it is truncated.
    private bool _isMultiLine;

    /// <summary>Creates an empty input field.</summary>
    /// <param name="fontType">Glyph size and colour; an input field has a single font.</param>
    /// <param name="maxTextWidth">Width limit in pixels; <c>&lt; 1</c> means no limit.</param>
    /// <param name="isMultiLine">Whether the text wraps instead of being truncated.</param>
    /// <remarks>参考：<c>ui_text.h:282</c> TextInput( fontType, maxTextWidth, isMultiLine, language )。构造完就调 <c>_updateCursorAreaInText</c>。</remarks>
    public TextInput(in FontType fontType, int maxTextWidth, bool isMultiLine)
        : base(string.Empty, fontType)
    {
        _maxTextWidth = maxTextWidth;
        _isMultiLine = isMultiLine;

        KeepLineTrailingSpaces();

        UpdateCursorAreaInText();
    }

    /// <summary>Replaces the text and puts the caret at <paramref name="cursorPosition"/>.</summary>
    /// <param name="text">The new text.</param>
    /// <param name="cursorPosition">Caret offset in bytes, counted from the start of the text.</param>
    /// <remarks>参考：<c>ui_text.h:293</c> <c>set( std::string, cursorPosition )</c>。参考不校验光标位置的上界。</remarks>
    public void Set(string text, int cursorPosition)
    {
        SetBytes(Encoding.UTF8.GetBytes(text ?? string.Empty));
        _cursorPositionInText = cursorPosition;
        _visibleTextLength = Bytes.Length;

        UpdateCursorAreaInText();
    }

    /// <summary>The area covered by the caret glyph, relative to the text origin.</summary>
    /// <remarks>参考：<c>ui_text.h:321</c> <c>cursorArea()</c>。</remarks>
    public Rect CursorArea => _cursorArea;

    /// <summary>Caret offset in bytes, counted from the start of the text.</summary>
    public int CursorPositionInText => _cursorPositionInText;

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:702</c> TextInput::width。</remarks>
    public override int Width()
    {
        if (Bytes.Length == 0)
        {
            return 0;
        }

        if (_isMultiLine && _maxTextWidth > 0)
        {
            // This is a multi-line text.
            return Width(_maxTextWidth);
        }

        FontCharHandler charHandler = new(Font);

        int textWidth = TextAlgorithms.GetLineWidth(
            Bytes.Slice(_visibleTextBeginPos, _visibleTextLength),
            charHandler,
            true);

        bool isTextTruncatedAtBegin = _visibleTextBeginPos != 0;
        bool isTextTruncatedAtEnd = _visibleTextLength + _visibleTextBeginPos < Bytes.Length;

        if (isTextTruncatedAtBegin && isTextTruncatedAtEnd)
        {
            return textWidth + (2 * TextAlgorithms.GetTruncationSymbolWidth(Font));
        }

        if (isTextTruncatedAtBegin || isTextTruncatedAtEnd)
        {
            return textWidth + TextAlgorithms.GetTruncationSymbolWidth(Font);
        }

        return textWidth;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:732</c> TextInput::drawInRoi。</remarks>
    public override void DrawInRoi(int x, int y, PixelBuffer output, in Rect imageRoi)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.Width == 0 || Bytes.Length == 0 || _visibleTextLength == 0)
        {
            // No use to render something on an empty image or if something is empty.
            return;
        }

        if (_isMultiLine && _maxTextWidth > 0)
        {
            DrawInRoi(x, y, _maxTextWidth, output, imageRoi);
            return;
        }

        FontCharHandler charHandler = new(Font);

        int offsetX = x;

        if (_visibleTextBeginPos != 0)
        {
            // Insert truncation symbol at the beginning.
            offsetX = TextAlgorithms.RenderSingleLine(TextAlgorithms.TruncationSymbol, x, y, output, imageRoi, charHandler);
        }

        offsetX = TextAlgorithms.RenderSingleLine(
            Bytes.Slice(_visibleTextBeginPos, _visibleTextLength),
            offsetX,
            y,
            output,
            imageRoi,
            charHandler);

        // Insert truncation symbol at the end if required.
        if (_visibleTextLength + _visibleTextBeginPos < Bytes.Length)
        {
            TextAlgorithms.RenderSingleLine(TextAlgorithms.TruncationSymbol, offsetX, y, output, imageRoi, charHandler);
        }
    }

    /// <summary>Moves the caret to the closest position on the line above or below.</summary>
    /// <param name="currentPos">Current caret offset in bytes.</param>
    /// <param name="maxWidth">Maximum line width the text is wrapped to.</param>
    /// <param name="moveUp"><see langword="true"/> to go up, <see langword="false"/> to go down.</param>
    /// <returns>The new caret offset in bytes.</returns>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:766</c> TextInput::getCursorPositionInAdjacentLine。</remarks>
    public int GetCursorPositionInAdjacentLine(int currentPos, int maxWidth, bool moveUp)
    {
        List<TextLineInfo> tempLineInfos = [];
        GetTextLineInfos(tempLineInfos, maxWidth, Height(), true);

        if (tempLineInfos.Count == 0)
        {
            return currentPos;
        }

        int currentLineNumber = 0;
        int numberOfCharacters = 0;

        while (numberOfCharacters + tempLineInfos[currentLineNumber].CharacterCount <= currentPos
               && currentLineNumber < tempLineInfos.Count - 1)
        {
            numberOfCharacters += tempLineInfos[currentLineNumber].CharacterCount;
            ++currentLineNumber;
        }

        int targetLineNumber;

        if (moveUp)
        {
            if (currentLineNumber == 0)
            {
                return currentPos;
            }

            targetLineNumber = currentLineNumber - 1;
        }
        else
        {
            if (currentLineNumber == tempLineInfos.Count - 1)
            {
                return currentPos;
            }

            targetLineNumber = currentLineNumber + 1;
        }

        FontCharHandler charHandler = new(Font);

        int currentLineStartPos = 0;
        for (int i = 0; i < currentLineNumber; ++i)
        {
            currentLineStartPos += tempLineInfos[i].CharacterCount;
        }

        int targetLineStartPos = 0;
        for (int i = 0; i < targetLineNumber; ++i)
        {
            targetLineStartPos += tempLineInfos[i].CharacterCount;
        }

        // TODO: update those line once we support different alignment in multi-line text.
        int currentXPos = ((maxWidth - tempLineInfos[currentLineNumber].LineWidth) / 2)
                          + charHandler.GetWidth(Bytes.Slice(currentLineStartPos, currentPos - currentLineStartPos));
        int targetLineXOffset = (maxWidth - tempLineInfos[targetLineNumber].LineWidth) / 2;

        int bestPos = targetLineStartPos;
        int bestDistance = Math.Abs(currentXPos - targetLineXOffset);

        int targetXPos = targetLineXOffset;

        for (int i = 0; i < tempLineInfos[targetLineNumber].CharacterCount + 1; ++i)
        {
            int textPos = targetLineStartPos + i;
            int distance = Math.Abs(currentXPos - targetXPos);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPos = textPos;
            }

            if (textPos < Bytes.Length)
            {
                targetXPos += charHandler.GetWidth(Bytes[textPos]);
            }
        }

        return bestPos;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_text.cpp:826</c> TextInput::fitToOneRow。与 <see cref="Text.FitToOneRow"/>
    /// 不同，这里不截断文本本身，只更新可见窗口，好让用户在光标附近继续编辑。
    /// </remarks>
    public override void FitToOneRow(int maxWidth)
    {
        if (maxWidth <= 0 || Bytes.Length == 0)
        {
            _visibleTextBeginPos = 0;
            _visibleTextLength = 0;

            return;
        }

        FontCharHandler charHandler = new(Font);
        ReadOnlySpan<byte> textData = Bytes;
        _visibleTextLength = Bytes.Length;

        int originalTextWidth = TextAlgorithms.GetLineWidth(textData, charHandler, true);

        if (originalTextWidth < maxWidth)
        {
            // There is no need to fit the text. Reset text fitting values.
            _visibleTextBeginPos = 0;

            return;
        }

        // First we update the left text truncation position by updating the visible text beginning position.
        // If the cursor is to the left of or close to the visible text begin position, we update this position.
        // And we don't allow the begin position to be negative.
        _visibleTextBeginPos = _cursorPositionInText > CursorToTruncationDistance
                                   ? Math.Min(_visibleTextBeginPos, _cursorPositionInText - CursorToTruncationDistance)
                                   : 0;

        _visibleTextLength -= _visibleTextBeginPos;

        int currentWidth = TextAlgorithms.GetLineWidth(textData[_visibleTextBeginPos..], charHandler, true);
        int truncationSymbolWidth = TextAlgorithms.GetTruncationSymbolWidth(Font);
        int truncatedMaxWidth = maxWidth - truncationSymbolWidth;
        int twiceTruncatedMaxWidth = truncatedMaxWidth - truncationSymbolWidth;

        // If the text should be truncated from the right side.
        if (currentWidth > truncatedMaxWidth)
        {
            _visibleTextLength = TextAlgorithms.GetMaxCharacterCount(
                textData[_visibleTextBeginPos..],
                charHandler,
                _visibleTextBeginPos != 0 ? twiceTruncatedMaxWidth : truncatedMaxWidth);

            currentWidth = TextAlgorithms.GetLineWidth(textData.Slice(_visibleTextBeginPos, _visibleTextLength), charHandler, true);
        }

        int originalTextSize = Bytes.Length;
        int textEndPos = _visibleTextBeginPos + _visibleTextLength;

        // If the cursor is to the right of or close to the visible text end position, we update this position to
        // show the cursor and text around it.
        while (textEndPos < _cursorPositionInText + CursorToTruncationDistance && textEndPos < originalTextSize)
        {
            currentWidth += charHandler.GetWidth(textData[textEndPos]);
            ++textEndPos;
            ++_visibleTextLength;

            // 参考这里是 `while ( currentWidth > limit )`，没有下界；当 maxWidth 小到两个截断符号都放不下时
            // `twiceTruncatedMaxWidth` 为负，循环会把 `_visibleTextBeginPos` 推过文本末尾（参考是未定义行为）。
            // 这里以「起点不能越过终点」为界，退化输入下最多只剩截断符号。
            while (currentWidth > (textEndPos != originalTextSize ? twiceTruncatedMaxWidth : truncatedMaxWidth)
                   && _visibleTextBeginPos < textEndPos)
            {
                // Remove characters from the begin.
                currentWidth -= charHandler.GetWidth(textData[_visibleTextBeginPos]);
                ++_visibleTextBeginPos;
                --_visibleTextLength;
            }

            while (textEndPos != originalTextSize)
            {
                // Some characters from the text end might also fit the width.
                int charWidth = charHandler.GetWidth(textData[textEndPos]);

                if (currentWidth + charWidth > twiceTruncatedMaxWidth)
                {
                    break;
                }

                currentWidth += charHandler.GetWidth(textData[textEndPos]);
                ++textEndPos;
                ++_visibleTextLength;
            }
        }

        while (_visibleTextBeginPos != 0)
        {
            // Some characters from the text begin might also fit the width.
            int prevCharWidth = charHandler.GetWidth(textData[_visibleTextBeginPos - 1]);

            if (currentWidth + prevCharWidth > (textEndPos != originalTextSize ? twiceTruncatedMaxWidth : truncatedMaxWidth))
            {
                break;
            }

            --_visibleTextBeginPos;
            ++_visibleTextLength;
            currentWidth += prevCharWidth;
        }

        if (textEndPos != originalTextSize
            && textEndPos + CursorToTruncationDistance > originalTextSize
            && TextAlgorithms.GetLineWidth(textData[textEndPos..], charHandler, true) <= truncatedMaxWidth - currentWidth)
        {
            // The end of the text fits the given width. Update text length to remove truncation at the end.
            _visibleTextLength = originalTextSize - _visibleTextBeginPos;
        }

        if (_visibleTextBeginPos > 0
            && _visibleTextBeginPos < CursorToTruncationDistance
            && TextAlgorithms.GetLineWidth(textData[.._visibleTextBeginPos], charHandler, true) <= truncatedMaxWidth - currentWidth)
        {
            // The beginning of the text fits the given width. Update the begin position to remove truncation at the beginning.
            _visibleTextBeginPos = 0;
            _visibleTextLength = TextAlgorithms.GetMaxCharacterCount(textData, charHandler, truncatedMaxWidth);
        }
    }

    /// <summary>Maps a point of the input field to a caret offset.</summary>
    /// <param name="pos">Point in the frame buffer.</param>
    /// <param name="roi">Area the text is drawn in.</param>
    /// <param name="isCenterAligned">Whether a single line is centred inside <paramref name="roi"/>.</param>
    /// <returns>Caret offset in bytes.</returns>
    /// <remarks>参考：<c>ui_text.h:312</c> <c>getCursorPosition</c>。</remarks>
    public int GetCursorPosition(in Point pos, in Rect roi, bool isCenterAligned)
    {
        if (_isMultiLine)
        {
            return GetMultiLineCursorPosition(pos, roi);
        }

        return GetSingleLineCursorPosition(pos.X, roi, isCenterAligned);
    }

    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:931</c> TextInput::_getMultiLineTextInputCursorPosition。</remarks>
    private int GetMultiLineCursorPosition(in Point cursorOffset, in Rect roi)
    {
        if (Bytes.Length == 0 || roi.Width < 1 || roi.Height < 1)
        {
            // The text is empty.
            return 0;
        }

        int fontHeight = FontCharHandler.GetFontHeight(Font.Size);
        int pointerLine = (cursorOffset.Y - roi.Y) / fontHeight;

        if (pointerLine < 0)
        {
            // Pointer is upper than the first text line.
            return 0;
        }

        List<TextLineInfo> lineInfos = [];
        GetTextLineInfos(lineInfos, _maxTextWidth, fontHeight, true);

        if (pointerLine >= lineInfos.Count)
        {
            // Pointer is lower than the last text line.
            return Bytes.Length - 1;
        }

        int cursorPosition = 0;

        for (int i = 0; i < pointerLine; ++i)
        {
            cursorPosition += lineInfos[i].CharacterCount;
        }

        int positionOffsetX = 0;
        int maxOffsetX = cursorOffset.X - roi.X - ((_maxTextWidth - lineInfos[pointerLine].LineWidth) / 2);

        if (maxOffsetX <= 0)
        {
            // Pointer is to the left of the text line.
            return cursorPosition;
        }

        if (maxOffsetX > lineInfos[pointerLine].LineWidth)
        {
            // Pointer is to the right of the text line.
            cursorPosition += lineInfos[pointerLine].CharacterCount;

            return cursorPosition;
        }

        FontCharHandler charHandler = new(Font);
        int textSize = Bytes.Length;

        for (int i = cursorPosition; i < textSize; ++i)
        {
            int charWidth = charHandler.GetWidth(Bytes[i]);

            if (positionOffsetX + (charWidth / 2) >= maxOffsetX)
            {
                return i;
            }

            positionOffsetX += charWidth;
        }

        return textSize - 1;
    }

    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:990</c> TextInput::_getTextInputCursorPosition。</remarks>
    private int GetSingleLineCursorPosition(int cursorOffsetX, in Rect roi, bool isCenterAligned)
    {
        if (Bytes.Length == 0)
        {
            return 0;
        }

        int textStartOffsetX = roi.X
                               + (isCenterAligned ? (roi.Width - Width()) / 2 : 0)
                               + (_visibleTextBeginPos == 0 ? 0 : TextAlgorithms.GetTruncationSymbolWidth(Font));

        if (cursorOffsetX <= textStartOffsetX)
        {
            // The text is empty or mouse cursor position is to the left of input field.
            return _visibleTextBeginPos;
        }

        int maxOffset = cursorOffsetX - textStartOffsetX;
        ReadOnlySpan<byte> visibleText = Bytes.Slice(_visibleTextBeginPos, _visibleTextLength);
        int textSize = visibleText.Length;
        int positionOffset = 0;

        FontCharHandler charHandler = new(Font);

        for (int i = 0; i < textSize; ++i)
        {
            int currentCharWidth = charHandler.GetWidth(visibleText[i]);

            if (positionOffset + (currentCharWidth / 2) >= maxOffset)
            {
                return i + _visibleTextBeginPos;
            }

            positionOffset += currentCharWidth;
        }

        return textSize + _visibleTextBeginPos;
    }

    /// <summary>Recomputes the caret area, fitting the text to one row first when in single-line mode.</summary>
    private void UpdateCursorAreaInText()
    {
        if (!_isMultiLine && _maxTextWidth > 0)
        {
            FitToOneRow(_maxTextWidth);
        }

        FontCharHandler charHandler = new(Font);

        Sprite charSprite = charHandler.GetSprite(CursorChar);

        int cursorWidth = charSprite.Width;
        int cursorHeight = charSprite.Height;
        int cursorX = charSprite.OffsetX - (cursorWidth / 2);

        // Move cursor symbol one pixel up by deducting 1 from y.
        int cursorY = charSprite.OffsetY - 1;

        if (Bytes.Length == 0)
        {
            if (_isMultiLine && _maxTextWidth > 0)
            {
                cursorX += _maxTextWidth / 2;
            }

            _cursorArea = new Rect(cursorX, cursorY, cursorWidth, cursorHeight);

            return;
        }

        int textLineBegin = _visibleTextBeginPos;

        if (_isMultiLine && _maxTextWidth > 0)
        {
            // This is a multi-line text.
            int textHeight = Height();

            List<TextLineInfo> lineInfos = [];
            GetTextLineInfos(lineInfos, _maxTextWidth, textHeight, true);

            if (_cursorPositionInText == Bytes.Length)
            {
                // The cursor is at the end of the text.
                cursorY += (lineInfos.Count - 1) * textHeight;
                cursorX += (_maxTextWidth + lineInfos[^1].LineWidth) / 2;

                _cursorArea = new Rect(cursorX, cursorY, cursorWidth, cursorHeight);

                return;
            }

            textLineBegin = 0;

            for (int i = 0; i < lineInfos.Count; ++i)
            {
                if (_cursorPositionInText < textLineBegin + lineInfos[i].CharacterCount)
                {
                    cursorX += (_maxTextWidth - lineInfos[i].LineWidth) / 2;
                    cursorY += i * textHeight;
                    break;
                }

                textLineBegin += lineInfos[i].CharacterCount;
            }
        }
        else if (_visibleTextBeginPos != 0)
        {
            // The visible text is truncated at the begin.
            cursorX += TextAlgorithms.GetTruncationSymbolWidth(Font);
        }

        if (_cursorPositionInText > textLineBegin)
        {
            cursorX += TextAlgorithms.GetLineWidth(
                Bytes.Slice(textLineBegin, _cursorPositionInText - textLineBegin),
                charHandler,
                true);
        }

        _cursorArea = new Rect(cursorX, cursorY, cursorWidth, cursorHeight);
    }
}
