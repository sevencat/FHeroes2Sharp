using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// Several text runs with different fonts laid out as one block, the reference's <c>fheroes2::MultiFontText</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考：<c>src/fheroes2/gui/ui_text.h:347</c> class MultiFontText（<c>add</c> 353、其余接口在
/// <see cref="TextBase"/> 的对应位置）；实现 <c>src/fheroes2/gui/ui_text.cpp:1086</c>（析构）到
/// <c>:1331</c>（<c>_getMultiFontTextLineInfos</c>）。
/// </para>
/// <para>
/// 差异：参考的 <c>add( Text text )</c> 按值接收再 move 进 <c>std::vector&lt;Text&gt;</c>，容器里存的是副本；
/// C# 的类是引用语义，所以 <see cref="Add"/> 里显式 <see cref="Text.Clone"/> 一次，保持「加进去之后外部再改
/// 原对象不影响这里」的行为（<c>fitToOneRow</c> 会就地截断 <c>_texts</c> 里的文本）。参考靠
/// <c>friend class MultiFontText</c> 读 <c>Text</c> 的私有成员，C# 没有友元，改成同程序集的 <c>internal</c>。
/// </para>
/// <para>
/// 差异：<c>width( maxWidth )</c> 与 <c>height( maxWidth )</c> 在参考里对空的行信息直接取 <c>front()</c> /
/// <c>back()</c>（未定义行为），这里返回 0。
/// </para>
/// </remarks>
public sealed class MultiFontText : TextBase
{
    private readonly List<Text> _texts = [];

    /// <summary>Appends a text run; empty runs are ignored.</summary>
    /// <param name="text">The run to copy in.</param>
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1088</c> MultiFontText::add。</remarks>
    public void Add(Text text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.IsEmpty)
        {
            _texts.Add(text.Clone());
        }
    }

    /// <inheritdoc />
    public override bool IsEmpty => _texts.Count == 0;

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1095</c> MultiFontText::width。</remarks>
    public override int Width()
    {
        int totalWidth = 0;

        foreach (Text text in _texts)
        {
            totalWidth += text.Width();
        }

        return totalWidth;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1105</c> MultiFontText::height。取最大的那一行高，不累加。</remarks>
    public override int Height()
    {
        int maxHeight = 0;

        foreach (Text text in _texts)
        {
            maxHeight = Math.Max(maxHeight, text.Height());
        }

        return maxHeight;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1116</c> MultiFontText::width( maxWidth )。取最宽的一行。</remarks>
    public override int Width(int maxWidth)
    {
        int maxFontHeight = Height();

        List<TextLineInfo> lineInfos = [];
        GetMultiFontTextLineInfos(lineInfos, maxWidth, maxFontHeight);

        if (lineInfos.Count == 0)
        {
            return 0;
        }

        int maxRowWidth = lineInfos[0].LineWidth;

        foreach (TextLineInfo lineInfo in lineInfos)
        {
            maxRowWidth = Math.Max(maxRowWidth, lineInfo.LineWidth);
        }

        return maxRowWidth;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1131</c> MultiFontText::height( maxWidth )。最后一行起点加行高。</remarks>
    public override int Height(int maxWidth)
    {
        int maxFontHeight = Height();

        List<TextLineInfo> lineInfos = [];
        GetMultiFontTextLineInfos(lineInfos, maxWidth, maxFontHeight);

        if (lineInfos.Count == 0)
        {
            return 0;
        }

        return lineInfos[^1].OffsetY + maxFontHeight;
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1141</c> MultiFontText::rows。</remarks>
    public override int Rows(int maxWidth)
    {
        if (_texts.Count == 0)
        {
            return 0;
        }

        int maxFontHeight = Height();

        List<TextLineInfo> lineInfos = [];
        GetMultiFontTextLineInfos(lineInfos, maxWidth, maxFontHeight);

        return lineInfos.Count;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_text.cpp:1159</c> MultiFontText::area。逐段累加，遇到画得更高的字形就把
    /// 顶边上移并重新取高度，与参考同样的写法（含参考里 <c>area.width += textArea.x + textArea.width</c>
    /// 这一步）。
    /// </remarks>
    public override Rect Area()
    {
        Rect area = default;
        bool isFirstText = true;

        foreach (Text text in _texts)
        {
            if (isFirstText)
            {
                isFirstText = false;
                area = text.Area();
                continue;
            }

            Rect textArea = text.Area();

            if (textArea.Y < area.Y)
            {
                // This character sprite is drawn higher than all previous - update `height` and `y`.
                area = area with
                {
                    Height = area.Height + (area.Y - textArea.Y),
                    Y = textArea.Y,
                };
                area = area with { Height = Math.Max(area.Height, textArea.Height) };
            }
            else
            {
                area = area with { Height = Math.Max(area.Height, textArea.Y - area.Y + textArea.Height) };
            }

            area = area with { Width = area.Width + textArea.X + textArea.Width };
        }

        return area;
    }

    /// <inheritdoc />
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_text.cpp:1189</c> MultiFontText::drawInRoi。逐段横着接下去，每段按
    /// <c>( 最高行高 - 本段行高 ) / 2</c> 竖直居中。
    /// </remarks>
    public override void DrawInRoi(int x, int y, PixelBuffer output, in Rect imageRoi)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.Width == 0 || _texts.Count == 0)
        {
            return;
        }

        int maxFontHeight = Height();

        int offsetX = x;

        foreach (Text text in _texts)
        {
            int fontHeight = FontCharHandler.GetFontHeight(text.Font.Size);
            FontCharHandler charHandler = new(text.Font);

            offsetX = TextAlgorithms.RenderSingleLine(text.Bytes, offsetX, y + ((maxFontHeight - fontHeight) / 2), output,
                imageRoi, charHandler);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_text.cpp:1209</c> MultiFontText::drawInRoi（带 <c>maxWidth</c>）。一行里可能
    /// 混着不同字体，所以先把每行的最终宽度收出来（同一行的多个 <c>TextLineInfo</c> 只留最后一个宽度），再按段
    /// 逐行走 <c>lineInfos</c>。
    /// </remarks>
    public override void DrawInRoi(int x, int y, int maxWidth, PixelBuffer output, in Rect imageRoi)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.Width == 0 || _texts.Count == 0)
        {
            return;
        }

        int maxFontHeight = Height();

        List<TextLineInfo> lineInfos = [];
        GetMultiFontTextLineInfos(lineInfos, maxWidth, maxFontHeight);

        if (lineInfos.Count == 0)
        {
            return;
        }

        // One line can contain text with a different font. Calculate the width of each line.
        List<int> lineWidths = [];
        int offsetY = 0;

        foreach (TextLineInfo info in lineInfos)
        {
            if (lineWidths.Count == 0 || offsetY != info.OffsetY)
            {
                lineWidths.Add(info.LineWidth);
                offsetY = info.OffsetY;
            }
            else
            {
                lineWidths[^1] = info.LineWidth;
            }
        }

        int widthIndex = 0;
        int infoIndex = 0;

        foreach (Text singleText in _texts)
        {
            ReadOnlySpan<byte> data = singleText.Bytes;
            int dataIndex = 0;

            FontCharHandler charHandler = new(singleText.Font);

            while (dataIndex < data.Length)
            {
                if (infoIndex >= lineInfos.Count)
                {
                    // The reference would run past the end here; the line list always covers the text.
                    return;
                }

                TextLineInfo info = lineInfos[infoIndex];

                if (info.CharacterCount > 0)
                {
                    int offsetX = x + (maxWidth > 0 ? (maxWidth - lineWidths[widthIndex]) / 2 : 0);
                    int count = Math.Min(info.CharacterCount, data.Length - dataIndex);

                    TextAlgorithms.RenderSingleLine(data.Slice(dataIndex, count), offsetX + info.OffsetX, y + info.OffsetY,
                        output, imageRoi, charHandler);
                }

                dataIndex += info.CharacterCount;

                ++widthIndex;
                ++infoIndex;
            }

            // The next run continues on the last line this one ended on.
            --widthIndex;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_text.cpp:1269</c> MultiFontText::fitToOneRow。逐段扣可用宽度，第一段放不下就
    /// 用 <c>...</c> 收尾并把后面的段整段丢掉。
    /// </remarks>
    public override void FitToOneRow(int maxWidth)
    {
        int widthLeft = maxWidth;

        for (int i = 0; i < _texts.Count; ++i)
        {
            FontCharHandler charHandler = new(_texts[i].Font);

            int originalTextWidth = TextAlgorithms.GetLineWidth(_texts[i].Bytes, charHandler, true);

            if ((i + 1 == _texts.Count) && originalTextWidth <= widthLeft)
            {
                // This is the last text and all texts fit the given width.
                break;
            }

            // This is not the last text and we need to keep space for the possible truncation symbol.
            int correctedWidthLeft = widthLeft - TextAlgorithms.GetTruncationSymbolWidth(_texts[i].Font);

            if (originalTextWidth > correctedWidthLeft)
            {
                // The text does not fit the given width.
                int maxCharacterCount = TextAlgorithms.GetMaxCharacterCount(_texts[i].Bytes, charHandler, correctedWidthLeft);

                // Remove the characters that do not fit the given width.
                _texts[i].TruncateTo(maxCharacterCount);

                // Remove other texts that do not fit the given width.
                _texts.RemoveRange(i + 1, _texts.Count - (i + 1));

                break;
            }

            // The text is not longer than the provided maximum width. Go to the next text.
            widthLeft -= originalTextWidth;
        }
    }

    /// <inheritdoc />
    /// <remarks>参考：<c>src/fheroes2/gui/ui_text.cpp:1309</c> MultiFontText::text。各段首尾相接。</remarks>
    public override string Content
    {
        get
        {
            System.Text.StringBuilder output = new();

            foreach (Text singleText in _texts)
            {
                output.Append(singleText.Content);
            }

            return output.ToString();
        }
    }

    /// <summary>Lays out every run into the shared line list.</summary>
    /// <param name="textLineInfos">List the lines are appended to.</param>
    /// <param name="maxWidth">Maximum line width; <c>&lt; 1</c> keeps the text on one line.</param>
    /// <param name="rowHeight">Height of a line.</param>
    /// <remarks>
    /// 参考：<c>src/fheroes2/gui/ui_text.cpp:1320</c> MultiFontText::_getMultiFontTextLineInfos。除最后一段外都要
    /// 保留行尾空格，否则两段之间的空格会被吃掉。
    /// </remarks>
    private void GetMultiFontTextLineInfos(List<TextLineInfo> textLineInfos, int maxWidth, int rowHeight)
    {
        int textsCount = _texts.Count;

        for (int i = 0; i < textsCount; ++i)
        {
            bool isNotLastTextEntry = i != textsCount - 1;

            _texts[i].GetTextLineInfos(textLineInfos, maxWidth, rowHeight, isNotLastTextEntry);
        }
    }
}
