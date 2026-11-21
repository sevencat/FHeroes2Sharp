namespace FHeroes2.Game.Gui;

/// <summary>
/// Turns a list of <see cref="LocalizedString"/> runs into one drawable text, a port of the two
/// <c>fheroes2::getLocalizedText()</c> overloads (<c>src/fheroes2/gui/ui_tool.cpp:913-948</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考的用法：一段界面文字里有玩家输入的名字（地图名、英雄名、城镇名），名字可能和界面语言不同，
/// 于是先由 <c>getLocalizedStrings()</c> 把模板切成「界面语言的前半段 / 带语言标签的名字 / 界面语言的后半段」，
/// 再交给这里拼成文本。空列表返回空指针，一段返回单个 <see cref="Text"/>，多段返回一条
/// <see cref="MultiFontText"/>——三种情况的返回类型是参考的约定，调用点据此判断要不要居中排版。
/// </para>
/// <para>
/// 偏差：参考的 <c>Text( text, font, language )</c> 会把语言标签带进文本，由字体层挑对应语言的字形；
/// 端口没有字体回退（本地化子系统未移植，见 <see cref="LocalizedString"/> 的说明），所以
/// <see cref="LocalizedString.Language"/> 在这里只被忽略，每段都用调用方给的字体。
/// </para>
/// <para>
/// 放 <c>Game/Gui</c> 而不放 <c>UI/Widgets</c>：它只依赖 <see cref="Text"/>、<see cref="MultiFontText"/>
/// 与 <see cref="LocalizedString"/>，全在 Game 层，Game 层的存档/事件文案将来也要用，放 UI 会反向依赖。
/// </para>
/// </remarks>
public static class LocalizedText
{
    /// <summary>
    /// Builds one text out of runs that all use <paramref name="font"/>,
    /// <c>getLocalizedText( std::vector&lt;LocalizedString&gt;, const FontType )</c>.
    /// </summary>
    /// <param name="texts">The runs, in drawing order.</param>
    /// <param name="font">Font of every run.</param>
    /// <returns>
    /// <see langword="null"/> for an empty list, a <see cref="Text"/> for a single run and a
    /// <see cref="MultiFontText"/> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> is <see langword="null"/>.</exception>
    /// <remarks>参考：<c>ui_tool.cpp:913-929</c>。</remarks>
    public static TextBase? Create(IReadOnlyList<LocalizedString> texts, in FontType font)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return null;
        }

        if (texts.Count == 1)
        {
            return new Text(texts[0].Text, font);
        }

        MultiFontText result = new();
        foreach (LocalizedString text in texts)
        {
            result.Add(new Text(text.Text, font));
        }

        return result;
    }

    /// <summary>
    /// Builds one text out of runs that may each use their own font,
    /// <c>getLocalizedText( std::vector&lt;std::pair&lt;LocalizedString, FontType&gt;&gt; )</c>.
    /// </summary>
    /// <param name="texts">The runs and their fonts, in drawing order.</param>
    /// <returns>
    /// <see langword="null"/> for an empty list, a <see cref="Text"/> for a single run and a
    /// <see cref="MultiFontText"/> otherwise.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="texts"/> is <see langword="null"/>.</exception>
    /// <remarks>参考：<c>ui_tool.cpp:931-948</c>。</remarks>
    public static TextBase? Create(IReadOnlyList<LocalizedTextRun> texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return null;
        }

        if (texts.Count == 1)
        {
            LocalizedTextRun run = texts[0];
            return new Text(run.Text.Text, run.Font);
        }

        MultiFontText result = new();
        foreach (LocalizedTextRun run in texts)
        {
            result.Add(new Text(run.Text.Text, run.Font));
        }

        return result;
    }
}

/// <summary>
/// One run of <see cref="LocalizedText.Create(IReadOnlyList{LocalizedTextRun})"/>: a text and the font
/// it is drawn with, the reference's <c>std::pair&lt;LocalizedString, FontType&gt;</c>.
/// </summary>
/// <param name="Text">The text itself.</param>
/// <param name="Font">Font of that run.</param>
public readonly record struct LocalizedTextRun(LocalizedString Text, FontType Font);
