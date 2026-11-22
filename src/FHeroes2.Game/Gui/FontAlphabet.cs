using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;

namespace FHeroes2.Game.Gui;

/// <summary>
/// The letters a code page needs on top of the ASCII ones the archive ships with, the port of
/// <c>generateAlphabet( language, icnVsSprite )</c> and its eight <c>generateCPxxxxAlphabet</c>
/// helpers (<c>ui_font.cpp:186-4947</c>).
/// </summary>
/// <remarks>
/// <para>
/// 参考里每个函数把 <c>ICN::FONT</c> 与 <c>ICN::SMALFONT</c> 两张图分别拿一遍，先 <c>resizeCodePage</c>
/// 把 128 帧扩成 256 帧（按码位索引），再用「拷一段现成字母的像素 + 画几个点 + 补阴影」的方式拼出带
/// 音标的字母。这里保持同样的顺序：工作数组按码位 0–255 索引，生成完再折回端口按
/// 「码位 − 0x20」索引的字形表。
/// </para>
/// <para>
/// 八个函数的语句是纯数据（四千余条 <c>resize</c>/<c>Copy</c>/<c>FillTransform</c>），由
/// <c>tools/GenerateFontAlphabets.ps1</c> 逐句转写；本文件只放它们的公共骨架。
/// </para>
/// </remarks>
internal static partial class FontAlphabet
{
    /// <summary>How many code points a generated alphabet covers, <c>resizeCodePage()</c>'s result.</summary>
    public const int CodePageSize = 256;

    /// <summary>The first character the archive's font table holds, the space.</summary>
    public const int FirstCharacter = 0x20;

    /// <summary>The width of a non-breaking space in the normal font (<c>ui_font.cpp:26</c>).</summary>
    private const int NormalFontSpaceWidth = 6;

    /// <summary>The width of a non-breaking space in the small font (<c>ui_font.cpp:27</c>).</summary>
    private const int SmallFontSpaceWidth = 4;

    /// <summary>
    /// Applies the alphabet <paramref name="language"/> needs to one base table, the equivalent of
    /// <c>generateAlphabet( language, icnVsSprite )</c> (<c>ui_font.cpp:6762</c>).
    /// </summary>
    /// <param name="language">The game language whose text is drawn.</param>
    /// <param name="size">The font size, only the normal and the small size have generated letters.</param>
    /// <param name="table">The glyphs, indexed by character minus <see cref="FirstCharacter"/>.</param>
    /// <returns>The table with the language's letters appended, or <paramref name="table"/> itself.</returns>
    public static Sprite?[] Apply(SupportedLanguage language, FontSize size, Sprite?[] table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (!NeedsGeneratedAlphabet(language))
        {
            return table;
        }

        // French assets with the English language are generated as CP1252 (ui_language.cpp:298).
        CodePage codePage = GameLanguage.GetCodePage(language == SupportedLanguage.English ? SupportedLanguage.French : language);

        if (size is not (FontSize.Normal or FontSize.Small))
        {
            return table;
        }

        Sprite[] font = Expand(table);
        Generate(codePage, font, small: size == FontSize.Small);

        return Compact(font);
    }

    /// <summary>
    /// Runs the half of the reference's generator that fills <paramref name="font"/>.
    /// </summary>
    /// <remarks>
    /// 参考里每个函数在一段作用域里先做 <c>ICN::FONT</c>、再在另一段里做 <c>ICN::SMALFONT</c>；端口的两张表
    /// 是分开构建的，所以两个半段拆成了两个方法。
    /// </remarks>
    /// <param name="codePage">The code page to build.</param>
    /// <param name="font">The code point indexed working table.</param>
    /// <param name="small">Whether to fill the small font half.</param>
    private static void Generate(CodePage codePage, Sprite[] font, bool small)
    {
        Action<Sprite[]>? generator = (codePage, small) switch
        {
            (CodePage.Cp1250, false) => GenerateCp1250Normal,
            (CodePage.Cp1250, true) => GenerateCp1250Small,
            (CodePage.Cp1251, false) => GenerateCp1251Normal,
            (CodePage.Cp1251, true) => GenerateCp1251Small,
            (CodePage.Cp1252, false) => GenerateCp1252Normal,
            (CodePage.Cp1252, true) => GenerateCp1252Small,
            (CodePage.Cp1253, false) => GenerateCp1253Normal,
            (CodePage.Cp1253, true) => GenerateCp1253Small,
            (CodePage.Cp1254, false) => GenerateCp1254Normal,
            (CodePage.Cp1254, true) => GenerateCp1254Small,
            (CodePage.Cp1258, false) => GenerateCp1258Normal,
            (CodePage.Cp1258, true) => GenerateCp1258Small,
            (CodePage.Iso8859_16, false) => GenerateIso8859_16Normal,
            (CodePage.Iso8859_16, true) => GenerateIso8859_16Small,
            (CodePage.Iso8859_3, false) => GenerateIso8859_3Normal,
            (CodePage.Iso8859_3, true) => GenerateIso8859_3Small,
            _ => null,
        };

        generator?.Invoke(font);
    }

    /// <summary>
    /// Whether <paramref name="language"/> needs letters the archive does not have,
    /// <c>Assets::updateAlphabet()</c> (<c>ui_language.cpp:291</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// 参考的判据是「资材本来就是这门语言」就不用生成：英文永远算原生；语言与资材语言相同、且资材不是
    /// 法语/俄语时也算原生（这两套资材的 ASCII 段被换成了本地字符，必须重排后重新生成）。
    /// </para>
    /// <para>
    /// 法语资材配英文语言的组合参考也会生成 CP1252，因为那套资材把几个 ASCII 特殊字符换成了法语字符。
    /// </para>
    /// </remarks>
    private static bool NeedsGeneratedAlphabet(SupportedLanguage language)
    {
        SupportedLanguage resource = ResourceLanguage.Get();

        if (language == SupportedLanguage.English)
        {
            // English text needs nothing unless the resources are the French ones.
            return resource == SupportedLanguage.French;
        }

        return language != resource || resource is SupportedLanguage.French or SupportedLanguage.Russian;
    }

    /// <summary>
    /// Widens a glyph table to <see cref="CodePageSize"/> code points, the equivalent of
    /// <c>resizeCodePage( icnVsSprite )</c> (<c>ui_font.cpp:30</c>).
    /// </summary>
    /// <remarks>
    /// The ASCII sprites are shared, not cloned: no generated alphabet ever writes into a code point below
    /// <c>0x80</c>, they only read them as the sources of the accented letters.
    /// </remarks>
    private static Sprite[] Expand(Sprite?[] table)
    {
        Sprite[] result = new Sprite[CodePageSize];

        for (int character = 0; character < CodePageSize; ++character)
        {
            int index = character - FirstCharacter;

            result[character] = index >= 0 && index < table.Length && table[index] is { } glyph
                ? glyph
                : new Sprite(0, 0);
        }

        return result;
    }

    /// <summary>Folds a code point indexed working table back into the port's frame indexed glyph table.</summary>
    private static Sprite?[] Compact(Sprite[] table)
    {
        Sprite?[] result = new Sprite?[CodePageSize - FirstCharacter];

        for (int character = FirstCharacter; character < CodePageSize; ++character)
        {
            Sprite glyph = table[character];

            // An untouched slot stays the 0x0 sprite the reference's resizeCodePage left there; the handler
            // draws nothing for it, which is the same as the reference's empty frame.
            result[character - FirstCharacter] = glyph.Image.IsEmpty ? null : glyph;
        }

        return result;
    }

    /// <summary>Moves a letter to its anchor, the port's stand-in for <c>Image::setPosition()</c>.</summary>
    /// <remarks>The layers are shared: the generated letters keep the very pixels built before the move.</remarks>
    private static Sprite At(Sprite sprite, int offsetX, int offsetY) =>
        new(sprite.Image, offsetX, offsetY, sprite.Transform);

    /// <summary><c>updateNormalFontLetterShadow()</c> (<c>ui_font.cpp:154</c>).</summary>
    private static void UpdateNormalFontLetterShadow(Sprite letter) =>
        ImageOps.UpdateShadow(letter, new Point(-1, 2), 2, false);

    /// <summary><c>updateSmallFontLetterShadow()</c> (<c>ui_font.cpp:162</c>).</summary>
    /// <remarks>
    /// The small font has a one pixel high shadow that is skipped on the very first pixel of the letter, so
    /// the two sizes keep their different weights.
    /// </remarks>
    private static void UpdateSmallFontLetterShadow(Sprite letter) =>
        ImageOps.UpdateShadow(letter, new Point(-1, 1), 2, true);

    /// <summary><c>removeShadows()</c> (<c>ui_font.cpp:170</c>): turns the shadow back into plain pixels.</summary>
    private static void RemoveShadows(Sprite letter)
    {
        if (letter.Transform is not { } transform)
        {
            return;
        }

        for (int index = 0; index < transform.Length; ++index)
        {
            if (transform[index] != 0)
            {
                transform[index] = 1;
            }
        }
    }
}
