using System.Text;
using FHeroes2.Core.Geometry;
using FHeroes2.Game;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// What a key of the virtual keyboard does when it is pressed, the reference's
/// <c>enum class DialogAction</c> (<c>ui_keyboard.cpp:106-114</c>).
/// </summary>
public enum KeyboardDialogAction
{
    /// <summary>Nothing, the dialog keeps the current layout.</summary>
    DoNothing,

    /// <summary>Show capital letters, <c>DialogAction::UpperCase</c>.</summary>
    UpperCase,

    /// <summary>Show small letters, <c>DialogAction::LowerCase</c>.</summary>
    LowerCase,

    /// <summary>Show digits and punctuation, <c>DialogAction::AlphaNumeric</c>.</summary>
    AlphaNumeric,

    /// <summary>Switch the language of the letters, <c>DialogAction::ChangeLanguage</c>.</summary>
    ChangeLanguage,

    /// <summary>Close the dialog, <c>DialogAction::Close</c>.</summary>
    Close,
}

/// <summary>
/// The character set the keyboard currently shows, the reference's <c>enum class LayoutType</c>
/// (<c>ui_keyboard.cpp:116-123</c>).
/// </summary>
public enum KeyboardLayoutType
{
    /// <summary>Letters, the buttons show capital letters but insert small ones.</summary>
    LowerCase,

    /// <summary>Capital letters.</summary>
    UpperCase,

    /// <summary>Digits and punctuation.</summary>
    AlphaNumeric,

    /// <summary>Digits of a keypad, with a sign button.</summary>
    SignedNumeric,

    /// <summary>Digits of a keypad, without a sign button.</summary>
    UnsignedNumeric,
}

/// <summary>
/// Where the text cursor moves to, the reference's <c>enum class CursorPosition</c>
/// (<c>ui_keyboard.cpp:125-133</c>).
/// </summary>
public enum KeyboardCursorMove
{
    /// <summary>One character to the left.</summary>
    PreviousChar,

    /// <summary>One character to the right.</summary>
    NextChar,

    /// <summary>One line up, multi line text only.</summary>
    PreviousLine,

    /// <summary>One line down, multi line text only.</summary>
    NextLine,

    /// <summary>Before the first character.</summary>
    BeginningOfText,

    /// <summary>After the last character.</summary>
    EndOfText,
}

/// <summary>
/// The letter tables and the metrics of the virtual keyboard, the anonymous namespace of
/// <c>src/fheroes2/gui/ui_keyboard.cpp</c>.
/// </summary>
/// <remarks>
/// <para>
/// 参考把每个语言的按键写成 <c>std::string</c> 字面量，里面的高位字节是该语言代码页（<c>getCodePage()</c>）
/// 的字节，不是 UTF-8。例如俄语大写行是 <c>"\xC9\xD6\xD3\xCA\xC5\xCD\xC3\xD8\xD9\xC7\xD5\xDA"</c>，
/// 按 CP1251 解出来就是「ЙЦУКЕНГШЩЗХЪ」。端口沿用同一份字节表，在需要时按代码页解码成
/// <see cref="string"/>，这样源码可以逐字节对照参考，运行时拿到的是正确的 Unicode 字符。
/// </para>
/// <para>
/// 差异：参考遇到未知语言在 <c>getCapitalCharacterLayout()</c> 里 <c>assert( 0 )</c>，端口返回空表。
/// 意大利语、荷兰语、世界语、罗马尼亚语、土耳其语、越南语在参考里没有字母表（走不到虚拟键盘），
/// 端口对这几种语言同样返回空表。
/// </para>
/// </remarks>
public static class VirtualKeyboardLayout
{
    /// <summary>The gap between two buttons, <c>buttonOffset</c> (<c>ui_keyboard.cpp:55</c>).</summary>
    public const int ButtonOffset = 8;

    /// <summary>The height of every keyboard button, <c>defaultButtonHeight</c> (<c>ui_keyboard.cpp:56</c>).</summary>
    public const int DefaultButtonHeight = 25;

    /// <summary>Width of the alphabet keyboard window, <c>defaultWindowWidth</c> (<c>ui_keyboard.cpp:61</c>).</summary>
    public const int DefaultWindowWidth = 520;

    /// <summary>Width of the numeric keyboard window, <c>numpadWindowWidth</c> (<c>ui_keyboard.cpp:62</c>).</summary>
    public const int NumpadWindowWidth = 320;

    /// <summary>Height of the window before it grows for extra letter rows, <c>defaultWindowHeight</c>.</summary>
    public const int DefaultWindowHeight = 250;

    /// <summary>How many letter rows fit the default window height, <c>defaultLetterRows</c>.</summary>
    public const int DefaultLetterRows = 3;

    /// <summary>The gap of the buttons from the window borders, <c>offsetFromWindowBorders</c>.</summary>
    public static readonly Point OffsetFromWindowBorders = new(25, 50);

    /// <summary>How far the shadow of a button is offset, <c>buttonShadowOffset</c> (<c>ui_keyboard.cpp:65</c>).</summary>
    public static readonly Point ButtonShadowOffset = new(-5, 5);

    /// <summary>Size of the text input frame in the window, <c>inputAreaSize</c> (<c>ui_keyboard.cpp:67</c>).</summary>
    public static readonly Size InputAreaSize = new(268, 21);

    /// <summary>Border of the text input frame, <c>inputAreaBorders</c> (<c>ui_keyboard.cpp:68</c>).</summary>
    public const int InputAreaBorders = 2;

    /// <summary>How many text lines a multi line input shows, <c>multiLineInputAreaRows</c>.</summary>
    public const int MultiLineInputAreaRows = 5;

    /// <summary>Height of the multi line text input frame, <c>multiLineInputAreaHeight</c> (<c>ui_keyboard.cpp:70</c>).</summary>
    public const int MultiLineInputAreaHeight =
        (InputAreaBorders * 2) + (MultiLineInputAreaRows * (21 - (2 * InputAreaBorders))) + 1;

    /// <summary>Size of the multi line text input frame, <c>multiLineInputAreaSize</c> (<c>ui_keyboard.cpp:72</c>).</summary>
    public static readonly Size MultiLineInputAreaSize = new(220, MultiLineInputAreaHeight);

    /// <summary>Distance of the text input frame from the window top, <c>inputAreaOffsetFromWindowTop</c>.</summary>
    public const int InputAreaOffsetFromWindowTop = 20;

    /// <summary>Size of a key with a symbol instead of a letter, <c>defaultSpecialButtonSize</c>.</summary>
    public static readonly Size DefaultSpecialButtonSize = new(54, DefaultButtonHeight);

    /// <summary>Size of the space bar, <c>spacebarButtonSize</c> (<c>ui_keyboard.cpp:58</c>).</summary>
    public static readonly Size SpacebarButtonSize = new(175, DefaultButtonHeight);

    /// <summary>Size of the enter key of a multi line keyboard, <c>enterButtonSize</c> (<c>ui_keyboard.cpp:59</c>).</summary>
    public static readonly Size EnterButtonSize = new(72, DefaultButtonHeight);

    /// <summary>Size of the space bar next to an enter key, <c>multiLineSpacebarButtonSize</c>.</summary>
    public static readonly Size MultiLineSpacebarButtonSize = new(110, DefaultButtonHeight);

    private static readonly byte[][] AlphaNumericKeys =
    [
        Ascii("1234567890"),
        Ascii("-:;()_+="),
        Ascii("[].,!'?"),
    ];

    private static readonly byte[][] NumericKeys =
    [
        Ascii("123"),
        Ascii("456"),
        Ascii("789"),
    ];

    // The tables below are copied byte for byte from ui_keyboard.cpp:471-515 (capital)
    // and ui_keyboard.cpp:517-561 (small). Only the languages the reference has a layout for
    // are listed; the rest get an empty table.

    private static readonly byte[][] BelarusianCapital =
    [
        [0xC9, 0xD6, 0xD3, 0xCA, 0xC5, 0xCD, 0xC3, 0xD8, 0xA1, 0xC7, 0xD5, 0x92],
        [0xD4, 0xDB, 0xC2, 0xC0, 0xCF, 0xD0, 0xCE, 0xCB, 0xC4, 0xC6, 0xDD],
        [0xDF, 0xD7, 0xD1, 0xCC, 0xB2, 0xD2, 0xDC, 0xC1, 0xDE, 0xA8],
    ];

    private static readonly byte[][] BelarusianLowerCase =
    [
        [0xE9, 0xF6, 0xF3, 0xEA, 0xE5, 0xED, 0xE3, 0xF8, 0xA2, 0xE7, 0xF5, 0x92],
        [0xF4, 0xFB, 0xE2, 0xE0, 0xEF, 0xF0, 0xEE, 0xEB, 0xE4, 0xE6, 0xFD],
        [0xFF, 0xF7, 0xF1, 0xEC, 0xB3, 0xF2, 0xFC, 0xE1, 0xFE, 0xB8],
    ];

    private static readonly byte[][] BulgarianCapital =
    [
        [0xDB, 0xD3, 0xC5, 0xC8, 0xD8, 0xD9, 0xCA, 0xD1, 0xC7, 0xD6],
        [0xDC, 0xDF, 0xC0, 0xCE, 0xC6, 0xC3, 0xD2, 0xCD, 0xC2, 0xCC, 0xD7],
        [0xDE, 0xC9, 0xDA, 0xDD, 0xD4, 0xD5, 0xCF, 0xD0, 0xCB, 0xC1],
    ];

    private static readonly byte[][] BulgarianLowerCase =
    [
        [0xFB, 0xF3, 0xE5, 0xE8, 0xF8, 0xF9, 0xEA, 0xF1, 0xE7, 0xF6],
        [0xFC, 0xFF, 0xE0, 0xEE, 0xE6, 0xE3, 0xF2, 0xED, 0xE2, 0xEC, 0xF7],
        [0xFE, 0xE9, 0xFA, 0xFD, 0xF4, 0xF5, 0xEF, 0xF0, 0xEB, 0xE1],
    ];

    private static readonly byte[][] CzechCapital =
    [
        [0xCC, 0x8A, 0xC8, 0xD8, 0x8E, 0xDD, 0xC1, 0xCD, 0xC9],
        [.. Ascii("QWERTZUIOP"), 0xDA],
        [.. Ascii("ASDFGHJKL"), 0xD9],
        Ascii("YXCVBNM"),
    ];

    private static readonly byte[][] CzechLowerCase =
    [
        [0xEC, 0x9A, 0xE8, 0xF8, 0xBE, 0xFD, 0xE1, 0xED, 0xE9],
        [.. Ascii("qwertzuiop"), 0xFA],
        [.. Ascii("asdfghjkl"), 0xF9],
        Ascii("yxcvbnm"),
    ];

    private static readonly byte[][] DanishCapital =
    [
        [.. Ascii("QWERTYUIOP"), 0xC5],
        [.. Ascii("ASDFGHJKL"), 0xC6, 0xD8],
        Ascii("ZXCVBNM"),
    ];

    private static readonly byte[][] DanishLowerCase =
    [
        [.. Ascii("qwertyuiop"), 0xE5],
        [.. Ascii("asdfghjkl"), 0xE6, 0xF8],
        Ascii("zxcvbnm"),
    ];

    private static readonly byte[][] EnglishCapital =
    [
        Ascii("QWERTYUIOP"),
        Ascii("ASDFGHJKL"),
        Ascii("ZXCVBNM"),
    ];

    private static readonly byte[][] EnglishLowerCase =
    [
        Ascii("qwertyuiop"),
        Ascii("asdfghjkl"),
        Ascii("zxcvbnm"),
    ];

    private static readonly byte[][] FrenchCapital =
    [
        [0xC9, 0xC8, 0xC7, 0xC0, 0xC2, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF, 0xDB, 0xDC],
        [.. Ascii("AZERTYUIOP"), 0xD4],
        [.. Ascii("QSDFGHJKLM"), 0xD9],
        Ascii("WXCVBN"),
    ];

    private static readonly byte[][] FrenchLowerCase =
    [
        [0xE9, 0xE8, 0xE7, 0xE0, 0xE2, 0xEA, 0xEB, 0xED, 0xEE, 0xEF, 0xFB, 0xFC],
        [.. Ascii("azertyuiop"), 0xF4],
        [.. Ascii("qsdfghjklm"), 0xF9],
        Ascii("wxcvbn"),
    ];

    private static readonly byte[][] GermanCapital =
    [
        [.. Ascii("QWERTZUIOP"), 0xDC],
        [.. Ascii("ASDFGHJKL"), 0xD6, 0xC4],
        [.. Ascii("YXCVBNM"), 0xDF],
    ];

    private static readonly byte[][] GermanLowerCase =
    [
        [.. Ascii("qwertzuiop"), 0xFC],
        [.. Ascii("asdfghjkl"), 0xF6, 0xE4],
        [.. Ascii("yxcvbnm"), 0xDF],
    ];

    private static readonly byte[][] GreekCapital =
    [
        [0xF2, 0xC5, 0xC1, 0xD4, 0xD5, 0xC8, 0xC9, 0xCF, 0xD0],
        [0xC1, 0xD3, 0xC4, 0xD6, 0xC3, 0xC7, 0xCE, 0xCA, 0xCB],
        [0xC6, 0xD7, 0xD8, 0xD9, 0xC2, 0xCD, 0xCC],
    ];

    private static readonly byte[][] GreekLowerCase =
    [
        [0xF2, 0xE5, 0xF1, 0xF4, 0xF5, 0xE8, 0xE9, 0xEF, 0xF0],
        [0xE1, 0xF3, 0xE4, 0xF6, 0xE3, 0xE7, 0xEE, 0xEA, 0xEB],
        [0xE6, 0xF7, 0xF8, 0xF9, 0xE2, 0xED, 0xEC],
    ];

    private static readonly byte[][] HungarianCapital =
    [
        [.. Ascii("QWERTZUIOP"), 0xD5, 0xDA, 0xDB],
        [.. Ascii("ASDFGHJKL"), 0xC9, 0xC1],
        [.. Ascii("YXCVBNM"), 0xD6, 0xDC, 0xD3],
    ];

    private static readonly byte[][] HungarianLowerCase =
    [
        [.. Ascii("qwertzuiop"), 0xF5, 0xFA, 0xFB],
        [.. Ascii("asdfghjkl"), 0xE9, 0xE1],
        [.. Ascii("yxcvbnm"), 0xF6, 0xFC, 0xF3],
    ];

    private static readonly byte[][] NorwegianCapital =
    [
        [.. Ascii("QWERTYUIOP"), 0xC5],
        [.. Ascii("ASDFGHJKL"), 0xD8, 0xC6],
        Ascii("ZXCVBNM"),
    ];

    private static readonly byte[][] NorwegianLowerCase =
    [
        [.. Ascii("qwertyuiop"), 0xE5],
        [.. Ascii("asdfghjkl"), 0xF8, 0xE6],
        Ascii("zxcvbnm"),
    ];

    private static readonly byte[][] PolishCapital =
    [
        [0x8C, 0x8F, 0xA3, 0xA5, 0xAF, 0xC6, 0xCA, 0xD1, 0xD3],
        Ascii("QWERTYUIOP"),
        Ascii("ASDFGHJKL"),
        Ascii("ZXCVBNM"),
    ];

    private static readonly byte[][] PolishLowerCase =
    [
        [0x9C, 0x9F, 0xB3, 0xB9, 0xBF, 0xE6, 0xEA, 0xF1, 0xF3],
        Ascii("qwertyuiop"),
        Ascii("asdfghjkl"),
        Ascii("zxcvbnm"),
    ];

    private static readonly byte[][] PortugueseCapital =
    [
        Ascii("QWERTYUIOP"),
        [.. Ascii("ASDFGHJKL"), 0xC7],
        Ascii("ZXCVBNM"),
    ];

    private static readonly byte[][] PortugueseLowerCase =
    [
        Ascii("qwertyuiop"),
        [.. Ascii("asdfghjkl"), 0xE7],
        Ascii("zxcvbnm"),
    ];

    private static readonly byte[][] RussianCapital =
    [
        [0xC9, 0xD6, 0xD3, 0xCA, 0xC5, 0xCD, 0xC3, 0xD8, 0xD9, 0xC7, 0xD5, 0xDA],
        [0xD4, 0xDB, 0xC2, 0xC0, 0xCF, 0xD0, 0xCE, 0xCB, 0xC4, 0xC6, 0xDD],
        [0xDF, 0xD7, 0xD1, 0xCC, 0xC8, 0xD2, 0xDC, 0xC1, 0xDE, 0xA8],
    ];

    private static readonly byte[][] RussianLowerCase =
    [
        [0xE9, 0xF6, 0xF3, 0xEA, 0xE5, 0xED, 0xE3, 0xF8, 0xF9, 0xE7, 0xF5, 0xFA],
        [0xF4, 0xFB, 0xE2, 0xE0, 0xEF, 0xF0, 0xEE, 0xEB, 0xE4, 0xE6, 0xFD],
        [0xFF, 0xF7, 0xF1, 0xEC, 0xE8, 0xF2, 0xFC, 0xE1, 0xFE, 0xB8],
    ];

    private static readonly byte[][] SlovakCapital =
    [
        [0xCF, 0xBC, 0x8A, 0xC8, 0x8D, 0x8E, 0xDD, 0xC1, 0xCD, 0xC9, 0xD3],
        [.. Ascii("QWERTZUIOP"), 0xDA],
        [.. Ascii("ASDFGHJKL"), 0xD4, 0xD2],
        [0xC4, .. Ascii("YXCVBNM"), 0xC5, 0xC0],
    ];

    private static readonly byte[][] SlovakLowerCase =
    [
        [0xEF, 0xBE, 0x9A, 0xE8, 0x9D, 0x9E, 0xFD, 0xE1, 0xED, 0xE9, 0xF3],
        [.. Ascii("qwertzuiop"), 0xFA],
        [.. Ascii("asdfghjkl"), 0xF4, 0xF2],
        [0xE4, .. Ascii("yxcvbnm"), 0xE5, 0xE0],
    ];

    private static readonly byte[][] SpanishCapital =
    [
        Ascii("QWERTYUIOP"),
        [.. Ascii("ASDFGHJKL"), 0xD1, 0xC7],
        Ascii("ZXCVBNM"),
    ];

    private static readonly byte[][] SpanishLowerCase =
    [
        Ascii("qwertyuiop"),
        [.. Ascii("asdfghjkl"), 0xF1, 0xE7],
        Ascii("zxcvbnm"),
    ];

    private static readonly byte[][] SwedishCapital =
    [
        [.. Ascii("QWERTYUIOP"), 0xC5],
        [.. Ascii("ASDFGHJKL"), 0xD6, 0xC4],
        Ascii("ZXCVBNM"),
    ];

    private static readonly byte[][] SwedishLowerCase =
    [
        [.. Ascii("qwertyuiop"), 0xE5],
        [.. Ascii("asdfghjkl"), 0xF6, 0xE4],
        Ascii("zxcvbnm"),
    ];

    private static readonly byte[][] UkrainianCapital =
    [
        [0xC9, 0xD6, 0xD3, 0xCA, 0xC5, 0xCD, 0xC3, 0xD8, 0xD9, 0xC7, 0xD5, 0xAF],
        [0xD4, 0xB2, 0xC2, 0xC0, 0xCF, 0xD0, 0xCE, 0xCB, 0xC4, 0xC6, 0xAA],
        [0xDF, 0xD7, 0xD1, 0xCC, 0xC8, 0xD2, 0xDC, 0xC1, 0xDE, 0xA5],
    ];

    private static readonly byte[][] UkrainianLowerCase =
    [
        [0xE9, 0xF6, 0xF3, 0xEA, 0xE5, 0xED, 0xE3, 0xF8, 0xF9, 0xE7, 0xF5, 0xBF],
        [0xF4, 0xB3, 0xE2, 0xE0, 0xEF, 0xF0, 0xEE, 0xEB, 0xE4, 0xE6, 0xBA],
        [0xFF, 0xF7, 0xF1, 0xEC, 0xE8, 0xF2, 0xFC, 0xE1, 0xFE, 0xB4],
    ];

    /// <summary>
    /// Whether the current language offers a second letter set, the reference's
    /// <c>isSupportedForLanguageSwitching()</c> (<c>ui_keyboard.cpp:77-104</c>).
    /// </summary>
    /// <param name="language">The language of the game interface.</param>
    /// <returns><see langword="true"/> for a language which is not the default one and has letters.</returns>
    /// <remarks>英语是默认语言，不算「额外语言」；希腊语有字母表但参考没把它列进来，端口照搬。</remarks>
    public static bool IsLanguageSwitchingSupported(SupportedLanguage language) => language switch
    {
        SupportedLanguage.Belarusian
            or SupportedLanguage.Bulgarian
            or SupportedLanguage.Czech
            or SupportedLanguage.Danish
            or SupportedLanguage.French
            or SupportedLanguage.German
            or SupportedLanguage.Hungarian
            or SupportedLanguage.Norwegian
            or SupportedLanguage.Polish
            or SupportedLanguage.Portuguese
            or SupportedLanguage.Russian
            or SupportedLanguage.Slovak
            or SupportedLanguage.Spanish
            or SupportedLanguage.Swedish
            or SupportedLanguage.Ukrainian
            => true,
        _ => false,
    };

    /// <summary>
    /// The two letter sets of a layout, the reference's <c>getCharacterLayout()</c>
    /// (<c>ui_keyboard.cpp:563-589</c>).
    /// </summary>
    /// <param name="layoutType">The layout to build.</param>
    /// <param name="language">The language of the letters.</param>
    /// <returns>
    /// The letters written on the buttons and the letters the buttons insert; the two arrays have
    /// the same number of rows.
    /// </returns>
    public static (string[] ButtonLetters, string[] ReturnLetters) GetCharacterLayout(KeyboardLayoutType layoutType, SupportedLanguage language)
    {
        switch (layoutType)
        {
            case KeyboardLayoutType.LowerCase:
                return (GetCapitalCharacterLayout(language), GetNonCapitalCharacterLayout(language));
            case KeyboardLayoutType.UpperCase:
            {
                string[] capital = GetCapitalCharacterLayout(language);
                return (capital, capital);
            }
            case KeyboardLayoutType.AlphaNumeric:
            {
                string[] alphaNumeric = GetAlphaNumericCharacterLayout(language);
                return (alphaNumeric, alphaNumeric);
            }
            case KeyboardLayoutType.SignedNumeric:
            case KeyboardLayoutType.UnsignedNumeric:
            {
                string[] numeric = GetNumericCharacterLayout();
                return (numeric, numeric);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(layoutType));
        }
    }

    /// <summary>Digits and punctuation, <c>getAlphaNumericCharacterLayout()</c> (<c>ui_keyboard.cpp:435-464</c>).</summary>
    /// <param name="language">The language of the game interface.</param>
    /// <returns>Three rows of keys; empty for a language the reference has no table for.</returns>
    public static string[] GetAlphaNumericCharacterLayout(SupportedLanguage language)
    {
        switch (language)
        {
            case SupportedLanguage.Belarusian:
            case SupportedLanguage.Bulgarian:
            case SupportedLanguage.Czech:
            case SupportedLanguage.Danish:
            case SupportedLanguage.English:
            case SupportedLanguage.French:
            case SupportedLanguage.German:
            case SupportedLanguage.Greek:
            case SupportedLanguage.Hungarian:
            case SupportedLanguage.Norwegian:
            case SupportedLanguage.Polish:
            case SupportedLanguage.Portuguese:
            case SupportedLanguage.Russian:
            case SupportedLanguage.Slovak:
            case SupportedLanguage.Spanish:
            case SupportedLanguage.Swedish:
            case SupportedLanguage.Ukrainian:
                return Decode(language, AlphaNumericKeys);
            default:
                return [];
        }
    }

    /// <summary>The digits of a keypad, <c>getNumericCharacterLayout()</c> (<c>ui_keyboard.cpp:466-469</c>).</summary>
    /// <returns>Three rows of three digits.</returns>
    public static string[] GetNumericCharacterLayout() => Decode(SupportedLanguage.English, NumericKeys);

    /// <summary>Capital letters, <c>getCapitalCharacterLayout()</c> (<c>ui_keyboard.cpp:471-515</c>).</summary>
    /// <param name="language">The language of the letters.</param>
    /// <returns>The rows of capital letters; empty for a language without a layout.</returns>
    public static string[] GetCapitalCharacterLayout(SupportedLanguage language) => language switch
    {
        SupportedLanguage.Belarusian => Decode(language, BelarusianCapital),
        SupportedLanguage.Bulgarian => Decode(language, BulgarianCapital),
        SupportedLanguage.Czech => Decode(language, CzechCapital),
        SupportedLanguage.Danish => Decode(language, DanishCapital),
        SupportedLanguage.English => Decode(language, EnglishCapital),
        SupportedLanguage.French => Decode(language, FrenchCapital),
        SupportedLanguage.German => Decode(language, GermanCapital),
        SupportedLanguage.Greek => Decode(language, GreekCapital),
        SupportedLanguage.Hungarian => Decode(language, HungarianCapital),
        SupportedLanguage.Norwegian => Decode(language, NorwegianCapital),
        SupportedLanguage.Polish => Decode(language, PolishCapital),
        SupportedLanguage.Portuguese => Decode(language, PortugueseCapital),
        SupportedLanguage.Russian => Decode(language, RussianCapital),
        SupportedLanguage.Slovak => Decode(language, SlovakCapital),
        SupportedLanguage.Spanish => Decode(language, SpanishCapital),
        SupportedLanguage.Swedish => Decode(language, SwedishCapital),
        SupportedLanguage.Ukrainian => Decode(language, UkrainianCapital),
        _ => [],
    };

    /// <summary>Small letters, <c>getNonCapitalCharacterLayout()</c> (<c>ui_keyboard.cpp:517-561</c>).</summary>
    /// <param name="language">The language of the letters.</param>
    /// <returns>The rows of small letters; empty for a language without a layout.</returns>
    public static string[] GetNonCapitalCharacterLayout(SupportedLanguage language) => language switch
    {
        SupportedLanguage.Belarusian => Decode(language, BelarusianLowerCase),
        SupportedLanguage.Bulgarian => Decode(language, BulgarianLowerCase),
        SupportedLanguage.Czech => Decode(language, CzechLowerCase),
        SupportedLanguage.Danish => Decode(language, DanishLowerCase),
        SupportedLanguage.English => Decode(language, EnglishLowerCase),
        SupportedLanguage.French => Decode(language, FrenchLowerCase),
        SupportedLanguage.German => Decode(language, GermanLowerCase),
        SupportedLanguage.Greek => Decode(language, GreekLowerCase),
        SupportedLanguage.Hungarian => Decode(language, HungarianLowerCase),
        SupportedLanguage.Norwegian => Decode(language, NorwegianLowerCase),
        SupportedLanguage.Polish => Decode(language, PolishLowerCase),
        SupportedLanguage.Portuguese => Decode(language, PortugueseLowerCase),
        SupportedLanguage.Russian => Decode(language, RussianLowerCase),
        SupportedLanguage.Slovak => Decode(language, SlovakLowerCase),
        SupportedLanguage.Spanish => Decode(language, SpanishLowerCase),
        SupportedLanguage.Swedish => Decode(language, SwedishLowerCase),
        SupportedLanguage.Ukrainian => Decode(language, UkrainianLowerCase),
        _ => [],
    };

    /// <summary>
    /// Size of a letter key in a language, the reference's <c>getDefaultButtonSize()</c>
    /// (<c>ui_keyboard.cpp:591-623</c>).
    /// </summary>
    /// <param name="language">The language of the letters.</param>
    /// <returns>The size of one key.</returns>
    /// <remarks>
    /// 不同语言的每行字母数不同，窗口不能超过 640 像素宽，所以按键宽度按语言调整。
    /// 差异：参考遇到没列出的语言会 <c>assert( 0 )</c> 并返回 0×0，端口退回英文的 30×25。
    /// </remarks>
    public static Size GetDefaultButtonSize(SupportedLanguage language) => language switch
    {
        SupportedLanguage.Czech
            or SupportedLanguage.English
            or SupportedLanguage.Polish
            => new Size(30, DefaultButtonHeight),
        SupportedLanguage.Belarusian
            or SupportedLanguage.Bulgarian
            or SupportedLanguage.Danish
            or SupportedLanguage.German
            or SupportedLanguage.Greek
            or SupportedLanguage.Norwegian
            or SupportedLanguage.Portuguese
            or SupportedLanguage.Russian
            or SupportedLanguage.French
            or SupportedLanguage.Slovak
            or SupportedLanguage.Spanish
            or SupportedLanguage.Swedish
            or SupportedLanguage.Ukrainian
            => new Size(24, DefaultButtonHeight),
        SupportedLanguage.Hungarian => new Size(21, DefaultButtonHeight),
        _ => new Size(30, DefaultButtonHeight),
    };

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static string[] Decode(SupportedLanguage language, byte[][] rows)
    {
        Encoding encoding = GetEncoding(GameLanguage.GetCodePage(language));

        string[] result = new string[rows.Length];
        for (int i = 0; i < rows.Length; ++i)
        {
            result[i] = encoding.GetString(rows[i]);
        }

        return result;
    }

    private static Encoding GetEncoding(CodePage codePage)
    {
        // The reference's text is a byte string in the code page of the language; the port keeps
        // Unicode strings, so the tables are decoded once per use. Registering the provider twice
        // is harmless.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return codePage switch
        {
            CodePage.Ascii => Encoding.ASCII,
            CodePage.Cp1250 => Encoding.GetEncoding(1250),
            CodePage.Cp1251 => Encoding.GetEncoding(1251),
            CodePage.Cp1252 => Encoding.GetEncoding(1252),
            CodePage.Cp1253 => Encoding.GetEncoding(1253),
            CodePage.Cp1254 => Encoding.GetEncoding(1254),
            CodePage.Cp1258 => Encoding.GetEncoding(1258),
            CodePage.Iso8859_16 => Encoding.GetEncoding("iso-8859-16"),
            CodePage.Iso8859_3 => Encoding.GetEncoding("iso-8859-3"),
            _ => throw new ArgumentOutOfRangeException(nameof(codePage)),
        };
    }
}
