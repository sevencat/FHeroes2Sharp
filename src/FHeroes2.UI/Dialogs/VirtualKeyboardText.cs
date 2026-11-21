using System.Globalization;

namespace FHeroes2.UI.Dialogs;

/// <summary>
/// The translated strings the virtual keyboard paints: the labels of its special keys and the help
/// texts of the right click popups (<c>ui_keyboard.cpp:696-871</c>, <c>ui_keyboard.cpp:1215-1218</c>).
/// </summary>
/// <remarks>
/// <para>
/// The UI layer never reads a translation catalogue: the caller passes the msgids resolved, exactly
/// like <see cref="ScenarioDialogText"/> and <see cref="SettingsOption"/> do.
/// </para>
/// <para>
/// 默认值是参考里的 msgid <b>去掉上下文前缀之后</b>的样子：参考的 <c>_()</c> 在没有翻译时会把
/// <c>"Keyboard|123"</c> 截成 <c>"123"</c>（<c>translations.cpp:334-339</c> 的 <c>stripContext()</c>），
/// 所以没有目录时端口画出来也必须只有 <c>123</c>。
/// </para>
/// <para>
/// 数字键盘的报错文案也放在这里：参考在 <c>openVirtualNumpad()</c> 里直接弹出
/// <c>showStandardTextMessage()</c>（<c>ui_keyboard.cpp:1442-1475</c>），端口把判断与文案拆开，
/// 由宿主决定怎么显示 <see cref="MessageBox"/>。
/// </para>
/// </remarks>
public sealed record VirtualKeyboardText
{
    /// <summary>Help title of the key which switches to capital letters, <c>_( "Uppercase" )</c>.</summary>
    public required string Uppercase { get; init; }

    /// <summary>Help body of that key, <c>_( "Click to switch to uppercase letters." )</c>.</summary>
    public required string UppercaseDescription { get; init; }

    /// <summary>Help title of the key which switches to small letters, <c>_( "Lowercase" )</c>.</summary>
    public required string Lowercase { get; init; }

    /// <summary>Help body of that key, <c>_( "Click to switch to lowercase letters." )</c>.</summary>
    public required string LowercaseDescription { get; init; }

    /// <summary>Help title of the key which switches the layout, <c>_( "Keyboard type" )</c>.</summary>
    public required string KeyboardType { get; init; }

    /// <summary>Help body of that key, <c>_( "Click to switch between keyboard types." )</c>.</summary>
    public required string KeyboardTypeDescription { get; init; }

    /// <summary>Help title of the language key, <c>_( "Change language" )</c>.</summary>
    public required string ChangeLanguage { get; init; }

    /// <summary>Help body of the language key, <c>_( "Click to switch between languages." )</c>.</summary>
    public required string ChangeLanguageDescription { get; init; }

    /// <summary>Help title of the backspace key, <c>_( "Backspace" )</c>.</summary>
    public required string Backspace { get; init; }

    /// <summary>Help body of the backspace key, <c>_( "Click to remove characters." )</c>.</summary>
    public required string BackspaceDescription { get; init; }

    /// <summary>Help title of the sign key of the numpad, <c>_( "Swap sign" )</c>.</summary>
    public required string SwapSign { get; init; }

    /// <summary>
    /// Help body of the sign key, <c>_( "Click to switch between signed and unsigned numbers." )</c>.
    /// </summary>
    public required string SwapSignDescription { get; init; }

    /// <summary>Help title of the okay button, <c>_( "Okay" )</c>.</summary>
    public required string Okay { get; init; }

    /// <summary>Help body of the okay button, <c>_( "Click to apply changes." )</c>.</summary>
    public required string OkayDescription { get; init; }

    /// <summary>Label of the key which shows digits, <c>_( "Keyboard|123" )</c> without the context.</summary>
    public required string DigitsKey { get; init; }

    /// <summary>Label of the key which shows letters, <c>_( "Keyboard|ABC" )</c> without the context.</summary>
    public required string LettersKey { get; init; }

    /// <summary>Label of the space bar, <c>_( "Keyboard|SPACE" )</c> without the context.</summary>
    public required string SpaceKey { get; init; }

    /// <summary>Label of the enter key, <c>_( "Keyboard|ENTER" )</c> without the context.</summary>
    public required string EnterKey { get; init; }

    /// <summary>Title of the error box of the numpad, <c>_( "Error" )</c>.</summary>
    public required string ErrorTitle { get; init; }

    /// <summary>Error of an unparsable number, <c>_( "The entered value is invalid." )</c>.</summary>
    public required string InvalidValue { get; init; }

    /// <summary>
    /// Error of a number outside the range,
    /// <c>_( "The entered value is out of range.\nIt should be not less than %{minValue} and not more than %{maxValue}." )</c>.
    /// </summary>
    public required string OutOfRange { get; init; }

    /// <summary>The untranslated strings of the reference implementation.</summary>
    public static VirtualKeyboardText Default { get; } = new()
    {
        Uppercase = "Uppercase",
        UppercaseDescription = "Click to switch to uppercase letters.",
        Lowercase = "Lowercase",
        LowercaseDescription = "Click to switch to lowercase letters.",
        KeyboardType = "Keyboard type",
        KeyboardTypeDescription = "Click to switch between keyboard types.",
        ChangeLanguage = "Change language",
        ChangeLanguageDescription = "Click to switch between languages.",
        Backspace = "Backspace",
        BackspaceDescription = "Click to remove characters.",
        SwapSign = "Swap sign",
        SwapSignDescription = "Click to switch between signed and unsigned numbers.",
        Okay = "Okay",
        OkayDescription = "Click to apply changes.",
        DigitsKey = "123",
        LettersKey = "ABC",
        SpaceKey = "SPACE",
        EnterKey = "ENTER",
        ErrorTitle = "Error",
        InvalidValue = "The entered value is invalid.",
        OutOfRange = "The entered value is out of range.\nIt should be not less than %{minValue} and not more than %{maxValue}.",
    };

    /// <summary>
    /// Fills the placeholders of <see cref="OutOfRange"/>, the <c>StringReplace()</c> calls of the
    /// reference (<c>ui_keyboard.cpp:1448-1450</c>).
    /// </summary>
    /// <param name="minValue">Smallest value the dialog accepts.</param>
    /// <param name="maxValue">Largest value the dialog accepts.</param>
    /// <returns>The message with both limits filled in.</returns>
    public string GetOutOfRangeMessage(int minValue, int maxValue) => OutOfRange
        .Replace("%{minValue}", minValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("%{maxValue}", maxValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
