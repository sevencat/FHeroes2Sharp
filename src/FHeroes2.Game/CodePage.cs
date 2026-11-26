namespace FHeroes2.Game;

/// <summary>
/// The character set a language needs, the reference's <c>enum class CodePage</c>
/// (<c>game_language.h:56-68</c>).
/// </summary>
/// <remarks>
/// 字体按代码页生成（<c>ui_font.cpp</c>），语言相同代码页的可以复用同一套字形，所以参考在
/// <c>getSupportedLanguages()</c> 里按代码页分组以避免反复重建字体资源。端口在
/// <see cref="Gui.FontAlphabet"/> / <see cref="Gui.ButtonFont"/> 里生成各代码页的字形，由
/// <see cref="Gui.FontGlyphTable"/> 按语言挑一份；这里保留枚举本身，供语言元数据与字形生成共用。
/// </remarks>
public enum CodePage : byte
{
    /// <summary>No code page, the reference's default value.</summary>
    None,

    /// <summary>Plain ASCII, what the English texts need.</summary>
    Ascii,

    /// <summary>Central European, the reference's <c>CP1250</c>.</summary>
    Cp1250,

    /// <summary>Cyrillic, the reference's <c>CP1251</c>.</summary>
    Cp1251,

    /// <summary>Western European, the reference's <c>CP1252</c>.</summary>
    Cp1252,

    /// <summary>Greek, the reference's <c>CP1253</c>.</summary>
    Cp1253,

    /// <summary>Turkish, the reference's <c>CP1254</c>.</summary>
    Cp1254,

    /// <summary>Vietnamese, the reference's <c>CP1258</c>.</summary>
    Cp1258,

    /// <summary>Romanian, the reference's <c>ISO8859_16</c>.</summary>
    Iso8859_16,

    /// <summary>Esperanto, the reference's <c>ISO8859_3</c>.</summary>
    Iso8859_3,
}
