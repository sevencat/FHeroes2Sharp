using System.Text;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// A single-font text run, the reference's <c>fheroes2::Text</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_text.h:190</c> class Text（构造 197/204、<c>width</c> 218、
/// <c>height</c> 219、<c>width( maxWidth )</c> 221、<c>height( maxWidth )</c> 222、<c>rows</c> 224、
/// <c>area</c> 226、<c>drawInRoi</c> 228/229、<c>set</c> 236、<c>fitToOneRow</c> 250、
/// <c>keepLineTrailingSpaces</c> 260）；实现 <c>src/fheroes2/gui/ui_text.cpp:351</c>（width）、
/// <c>:360</c>（height）、<c>:366</c>（width 带 maxWidth）、<c>:409</c>（height 带 maxWidth）、
/// <c>:424</c>（rows）、<c>:437</c>（area）、<c>:449</c>/<c>:462</c>（drawInRoi）、
/// <c>:495</c>（fitToOneRow）、<c>:562</c>（_getTextLineInfos）。
/// </para>
/// <para>
/// 差异：参考把文本存成 <c>std::string</c> 字节串，这里同样以 UTF-8 字节为真源，
/// <see cref="Content"/> 再解回字符串；<c>fitToOneRow</c> 按字节截断，与参考一致。
/// 参考每个方法开头的 <c>getLanguageSwitcher()</c> 没有搬（本地化子系统 <c>ui_language.*</c> 未移植）。
/// </para>
/// <para>未搬：<c>fitToArea</c>（<c>:524</c>）。</para>
/// </remarks>
public class Text : TextBase
{
    private byte[] _bytes = [];
    private FontType _fontType = new();
    private bool _keepLineTrailingSpaces;

    /// <summary>Creates an empty text with the default font.</summary>
    /// <remarks>参考：<c>ui_text.h:197</c> Text()。</remarks>
    public Text()
    {
    }

    /// <summary>Creates a text with the given font.</summary>
    /// <param name="text">The text, UTF-8 like the reference.</param>
    /// <param name="fontType">Glyph size and colour.</param>
    /// <remarks>参考：<c>ui_text.h:204</c> Text( std::string, const FontType &amp; )。</remarks>
    public Text(string text, in FontType fontType) => Set(text, fontType);

    /// <inheritdoc />
    public override bool IsEmpty => _bytes.Length == 0;

    /// <inheritdoc />
    /// <remarks>参考：<c>ui_text.h:254</c> <c>text()</c>；名字改了，见 <see cref="TextBase"/> 的说明。</remarks>
    public override string Content => Encoding.UTF8.GetString(_bytes);

    /// <summary>Replaces the text and the font.</summary>
    /// <param name="text">The new text.</param>
    /// <param name="fontType">Glyph size and colour.</param>
    /// <remarks>参考：<c>ui_text.h:236</c> set。</remarks>
    public void Set(string text, in FontType fontType)
    {
        _bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        _fontType = fontType;
    }

    /// <summary>Keeps the trailing spaces of every line.</summary>
    /// <remarks>参考：<c>ui_text.h:260</c> keepLineTrailingSpaces。</remarks>
    public void KeepLineTrailingSpaces() => _keepLineTrailingSpaces = true;

    /// <summary>The raw UTF-8 bytes, the reference's <c>_text</c>.</summary>
    /// <remarks>
    /// 参考：<c>ui_text.h:266</c> 的 <c>std::string _text</c>。参考靠 <c>friend class MultiFontText</c> 让
    /// <c>MultiFontText</c> 直接读私有成员，C# 没有友元，改成 <c>internal</c> 属性。
    /// </remarks>
    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>The font, the reference's <c>_fontType</c>.</summary>
    internal FontType Font => _fontType;

    /// <summary>Replaces the raw bytes without touching the font, the reference's <c>_text = ...</c>.</summary>
    /// <param name="bytes">The new UTF-8 bytes.</param>
    /// <remarks>
    /// 参考的 <c>TextInput::set</c> 直接给 <c>_text</c> 赋值、不动 <c>_fontType</c>；<c>Text.Set</c> 会连字体一起换，
    /// 所以单独开一个同程序集可见的入口。
    /// </remarks>
    internal void SetBytes(ReadOnlySpan<byte> bytes) => _bytes = bytes.ToArray();

    /// <summary>Copies the text, like the reference's implicit copy constructor.</summary>
    /// <returns>An independent copy.</returns>
    /// <remarks>
    /// 参考的 <c>MultiFontText::add( Text text )</c> 按值接收再 move 进 <c>std::vector</c>，容器里是自己的一份；
    /// C# 的类是引用语义，所以显式拷贝才能保持一致。
    /// </remarks>
    internal Text Clone()
    {
        Text copy = new()
        {
            _bytes = (byte[])_bytes.Clone(),
            _fontType = _fontType,
            _keepLineTrailingSpaces = _keepLineTrailingSpaces,
        };

        return copy;
    }

    /// <summary>Appends the truncation symbol to the text.</summary>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:549</c> 与 <c>:558</c> 的 <c>_text += truncationSymbol</c>。</remarks>
    private void AppendTruncationSymbol() => _bytes = [.. _bytes, .. TextAlgorithms.TruncationSymbol];

    /// <summary>Cuts the text down to <paramref name="maxCharacterCount"/> bytes and appends the truncation symbol.</summary>
    /// <param name="maxCharacterCount">Bytes to keep.</param>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1294</c> 里 <c>MultiFontText::fitToOneRow</c> 对 <c>_text</c> 的两步操作。</remarks>
    internal void TruncateTo(int maxCharacterCount)
    {
        int keep = Math.Clamp(maxCharacterCount, 0, _bytes.Length);

        byte[] truncated = new byte[keep + TextAlgorithms.TruncationSymbol.Length];
        Array.Copy(_bytes, truncated, keep);
        TextAlgorithms.TruncationSymbol.CopyTo(truncated.AsSpan(keep));

        _bytes = truncated;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:351</c> Text::width。</remarks>
    public override int Width()
    {
        if (_bytes.Length == 0)
        {
            return 0;
        }

        return TextAlgorithms.GetLineWidth(_bytes, new FontCharHandler(_fontType), _keepLineTrailingSpaces);
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:360</c> Text::height。</remarks>
    public override int Height() => FontCharHandler.GetFontHeight(_fontType.Size);

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:366</c> Text::width( maxWidth )。</remarks>
    public override int Width(int maxWidth)
    {
        if (_bytes.Length == 0)
        {
            return 0;
        }

        int fontHeight = Height();

        List<TextLineInfo> lineInfos = [];
        GetTextLineInfos(lineInfos, maxWidth, fontHeight, false);

        if (lineInfos.Count == 1)
        {
            // This is a single line text.
            return lineInfos[0].LineWidth;
        }

        if (!IsUniformedVerticalAlignment)
        {
            // Multi-line text: use the width of the widest line.
            int widest = 0;
            foreach (TextLineInfo info in lineInfos)
            {
                widest = Math.Max(widest, info.LineWidth);
            }

            return widest;
        }

        // Multi-line text: find the smallest width that still needs the same number of lines.
        int startWidth = TextAlgorithms.GetMaxWordWidth(_bytes, _fontType);
        int endWidth = maxWidth;

        while (startWidth + 1 < endWidth)
        {
            int currentWidth = (endWidth + startWidth) / 2;

            List<TextLineInfo> tempLineInfos = [];
            GetTextLineInfos(tempLineInfos, currentWidth, fontHeight, false);

            if (tempLineInfos.Count > lineInfos.Count)
            {
                startWidth = currentWidth;
                continue;
            }

            endWidth = currentWidth;
        }

        return endWidth;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:409</c> Text::height( maxWidth )。</remarks>
    public override int Height(int maxWidth)
    {
        if (_bytes.Length == 0)
        {
            return 0;
        }

        int fontHeight = Height();

        List<TextLineInfo> lineInfos = [];
        GetTextLineInfos(lineInfos, maxWidth, fontHeight, false);

        return lineInfos[^1].OffsetY + fontHeight;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:424</c> Text::rows。</remarks>
    public override int Rows(int maxWidth)
    {
        if (_bytes.Length == 0)
        {
            return 0;
        }

        List<TextLineInfo> lineInfos = [];
        GetTextLineInfos(lineInfos, maxWidth, Height(), false);

        return lineInfos.Count;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:437</c> Text::area。</remarks>
    public override Rect Area()
    {
        if (_bytes.Length == 0)
        {
            return new Rect(0, 0, 0, 0);
        }

        return TextAlgorithms.GetTextLineArea(_bytes, new FontCharHandler(_fontType));
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:449</c> Text::drawInRoi。</remarks>
    public override void DrawInRoi(int x, int y, PixelBuffer output, in Rect imageRoi)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.Width == 0 || _bytes.Length == 0)
        {
            return;
        }

        TextAlgorithms.RenderSingleLine(_bytes, x, y, output, imageRoi, new FontCharHandler(_fontType));
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:462</c> Text::drawInRoi( x, y, maxWidth, output, imageRoi )。每行居中。</remarks>
    public override void DrawInRoi(int x, int y, int maxWidth, PixelBuffer output, in Rect imageRoi)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.Width == 0 || _bytes.Length == 0)
        {
            return;
        }

        if (maxWidth <= 0)
        {
            DrawInRoi(x, y, output, imageRoi);
            return;
        }

        List<TextLineInfo> lineInfos = [];
        GetTextLineInfos(lineInfos, maxWidth, Height(), false);

        FontCharHandler charHandler = new(_fontType);
        int index = 0;

        foreach (TextLineInfo info in lineInfos)
        {
            if (info.CharacterCount > 0)
            {
                // Center every line of a multi-line text.
                int offsetX = info.OffsetX + ((maxWidth - info.LineWidth) / 2);
                int count = Math.Min(info.CharacterCount, _bytes.Length - index);

                TextAlgorithms.RenderSingleLine(_bytes.AsSpan(index, count), x + offsetX, y + info.OffsetY, output, imageRoi, charHandler);
            }

            index += info.CharacterCount;
        }
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:495</c> Text::fitToOneRow。</remarks>
    public override void FitToOneRow(int maxWidth)
    {
        if (maxWidth <= 0 || _bytes.Length == 0)
        {
            return;
        }

        FontCharHandler charHandler = new(_fontType);

        int originalTextWidth = TextAlgorithms.GetLineWidth(_bytes, charHandler, _keepLineTrailingSpaces);
        if (originalTextWidth <= maxWidth)
        {
            return;
        }

        int maxCharacterCount = TextAlgorithms.GetMaxCharacterCount(_bytes, charHandler,
            maxWidth - TextAlgorithms.GetTruncationSymbolWidth(_fontType));

        TruncateTo(maxCharacterCount);
    }

    /// <summary>Cuts the text so that it fits into a <paramref name="maxWidth"/> by <paramref name="maxHeight"/> area.</summary>
    /// <param name="maxWidth">Maximum line width in pixels; <c>&lt; 1</c> does nothing.</param>
    /// <param name="maxHeight">Maximum height in pixels; <c>&lt; 1</c> does nothing.</param>
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_text.cpp:524</c> Text::fitToArea。参考在最后这个修剪循环里没有下界，
    /// 文本短到只剩截断符号时 <c>pop_back</c> 会越界；这里用「长度还够砍一次」作界。
    /// </remarks>
    public void FitToArea(int maxWidth, int maxHeight)
    {
        if (maxWidth <= 0 || maxHeight <= 0)
        {
            return;
        }

        if (_bytes.Length == 0)
        {
            // Nothing needs to be done.
            return;
        }

        if (Height(maxWidth) <= maxHeight)
        {
            // Nothing we need to do as the text fits to the area.
            return;
        }

        while (_bytes.Length > 0 && Height(maxWidth) > maxHeight)
        {
            _bytes = _bytes[..^1];
        }

        // We need to add truncation symbol.
        AppendTruncationSymbol();

        int symbolLength = TextAlgorithms.TruncationSymbol.Length;

        while (Height(maxWidth) > maxHeight && _bytes.Length > symbolLength + 1)
        {
            // Remove the truncation symbol and one more character before it.
            _bytes = _bytes[..^(symbolLength + 1)];

            AppendTruncationSymbol();
        }
    }

    /// <summary>
    /// Lays the text out into lines of at most <paramref name="maxWidth"/> pixels.
    /// </summary>
    /// <param name="textLineInfos">List the lines are appended to.</param>
    /// <param name="maxWidth">Maximum line width; <c>&lt; 1</c> keeps the text on one line.</param>
    /// <param name="rowHeight">Height of a line.</param>
    /// <param name="keepTextTrailingSpaces">Whether the trailing spaces of the last line count.</param>
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_text.cpp:562</c> Text::_getTextLineInfos。参考把 <c>MultiFontText</c> 声明为
    /// 友元才能调到这个私有函数，C# 没有友元，同一个程序集内用 <c>internal</c>。
    /// </remarks>
    internal void GetTextLineInfos(List<TextLineInfo> textLineInfos, int maxWidth, int rowHeight, bool keepTextTrailingSpaces)
    {
        int firstLineOffsetX = textLineInfos.Count == 0 ? 0 : textLineInfos[^1].LineWidth;
        int lineWidth = firstLineOffsetX;
        int offsetY = textLineInfos.Count == 0 ? 0 : textLineInfos[^1].OffsetY;

        FontCharHandler charHandler = new(_fontType);

        if (maxWidth < 1)
        {
            // The text stays on a single line.
            lineWidth += TextAlgorithms.GetLineWidth(_bytes, charHandler, _keepLineTrailingSpaces || keepTextTrailingSpaces);
            textLineInfos.Add(new TextLineInfo(firstLineOffsetX, offsetY, lineWidth, _bytes.Length));
            return;
        }

        int offsetX = firstLineOffsetX;
        int lineCharCount = 0;
        int lastWordCharCount = 0;
        int spaceCharCount = 0;
        int spaceCharWidth = charHandler.SpaceCharWidth;

        int index = 0;

        while (index < _bytes.Length)
        {
            if (FontCharHandler.IsLineSeparator(_bytes[index]))
            {
                if (!_keepLineTrailingSpaces)
                {
                    lineWidth -= spaceCharCount * spaceCharWidth;
                }

                textLineInfos.Add(new TextLineInfo(offsetX, offsetY, lineWidth, lineCharCount + 1));

                spaceCharCount = 0;
                offsetX = 0;
                offsetY += rowHeight;
                lineCharCount = 0;
                lastWordCharCount = 0;
                lineWidth = 0;

                ++index;
            }
            else
            {
                int charWidth = charHandler.GetWidth(_bytes[index]);

                if (lineWidth != 0 && lineWidth + charWidth > maxWidth)
                {
                    // The current character does not fit into the line any more.
                    if (!_keepLineTrailingSpaces && FontCharHandler.IsSpaceChar(_bytes[index]))
                    {
                        // A space ends the line and belongs to it.
                        ++lineCharCount;
                        ++index;
                    }
                    else if (lineCharCount == lastWordCharCount)
                    {
                        // This is the only word on the line: break at a hyphen instead of mid-word.
                        int hyphenIndex = index - lineCharCount;

                        for (; hyphenIndex != index; ++hyphenIndex)
                        {
                            if (_bytes[hyphenIndex] == FontCharHandler.HyphenChar)
                            {
                                break;
                            }
                        }

                        if (hyphenIndex != index)
                        {
                            // Everything after the hyphen moves to the next line.
                            int postHyphenCharCount = (index - hyphenIndex) - 1;

                            if (postHyphenCharCount > 0)
                            {
                                lineCharCount -= postHyphenCharCount;
                                lineWidth -= TextAlgorithms.GetLineWidth(_bytes.AsSpan(index - postHyphenCharCount, postHyphenCharCount), charHandler, true);

                                index = hyphenIndex + 1;
                            }
                        }
                        else if (firstLineOffsetX > 0 && (textLineInfos.Count == 0 || textLineInfos[^1].OffsetY == offsetY))
                        {
                            // The word was not the first on the line, so it can move to the next one.
                            index -= lastWordCharCount;

                            lineCharCount = 0;
                            lineWidth = firstLineOffsetX;
                        }
                    }
                    else if (lastWordCharCount > 0)
                    {
                        // Move the last word to the next line.
                        index -= lastWordCharCount;

                        lineCharCount -= lastWordCharCount;
                        lineWidth -= TextAlgorithms.GetLineWidth(_bytes.AsSpan(index, lastWordCharCount), charHandler, true);
                    }

                    if (!_keepLineTrailingSpaces)
                    {
                        lineWidth -= spaceCharCount * spaceCharWidth;
                    }

                    textLineInfos.Add(new TextLineInfo(offsetX, offsetY, lineWidth, lineCharCount));

                    spaceCharCount = 0;
                    offsetX = 0;
                    offsetY += rowHeight;
                    lineCharCount = 0;
                    lastWordCharCount = 0;
                    lineWidth = 0;
                }
                else
                {
                    if (FontCharHandler.IsSpaceChar(_bytes[index]))
                    {
                        lastWordCharCount = 0;
                        ++spaceCharCount;
                    }
                    else
                    {
                        ++lastWordCharCount;
                        spaceCharCount = 0;
                    }

                    ++index;
                    ++lineCharCount;
                    lineWidth += charWidth;
                }
            }
        }

        if (!_keepLineTrailingSpaces && !keepTextTrailingSpaces)
        {
            lineWidth -= spaceCharCount * spaceCharWidth;
        }

        textLineInfos.Add(new TextLineInfo(offsetX, offsetY, lineWidth, lineCharCount));
    }
}
