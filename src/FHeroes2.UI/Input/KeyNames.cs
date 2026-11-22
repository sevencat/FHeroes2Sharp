namespace FHeroes2.UI.Input;

/// <summary>
/// The printable name of every <see cref="Key"/>, the port of <c>fheroes2::KeySymGetName()</c>
/// (<c>src/engine/localevent.cpp:1167-1170</c>, which forwards to the SDL back end).
/// </summary>
/// <remarks>
/// <para>
/// The names are the ones SDL reports for the same keys, upper cased where the reference upper cases them: the hot
/// key configuration file (<c>fheroes2.key</c>) is written with <c>StringUpper( KeySymGetName( key ) )</c> and read
/// back the same way, so <c>ENTER</c>, <c>LEFT SHIFT</c> and <c>KEYPAD 4</c> are the spellings that have to survive a
/// save and a load. The port has no SDL dependent name table of its own, so this static table is the single place
/// that turns a <see cref="Key"/> into text and back.
/// </para>
/// <para>
/// The table is indexed by the enumeration value: <see cref="Key.LastKey"/> doubles as its length, exactly like the
/// reference's <c>LAST_KEY</c>. Do not reorder the enumeration without regenerating this table.
/// </para>
/// </remarks>
public static class KeyNames
{
    /// <summary>The name of a key, the reference's <c>KeySymGetName()</c>.</summary>
    /// <param name="key">The key to name.</param>
    /// <returns>An upper-case name, e.g. <c>"RETURN"</c>; <c>"UNKNOWN"</c> for <see cref="Key.None"/>.</returns>
    public static string Get(Key key)
    {
        int index = (int)key;
        return index > 0 && index < Names.Length ? Names[index] : Unknown;
    }

    /// <summary>Parses a name written by <see cref="Get"/>, ignoring case.</summary>
    /// <param name="name">The text to parse, e.g. <c>"Left Shift"</c>.</param>
    /// <param name="key">The key the name stands for.</param>
    /// <returns><see langword="true"/> when the name is known.</returns>
    public static bool TryParse(string? name, out Key key)
    {
        key = Key.None;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string trimmed = name.Trim();
        if (ByName.TryGetValue(trimmed, out key))
        {
            return true;
        }

        // 参考写文件时用 StringUpper()，读回来也只认大写；这里放宽到忽略大小写，配置手改过一半也没关系。
        return false;
    }

    private const string Unknown = "UNKNOWN";

    private static readonly string[] Names = Build();

    private static readonly Dictionary<string, Key> ByName = BuildLookup();

    private static string[] Build()
    {
        string[] names = new string[(int)Key.LastKey];
        names[(int)Key.None] = Unknown;

        names[(int)Key.Backspace] = "BACKSPACE";
        names[(int)Key.Enter] = "RETURN";
        names[(int)Key.Escape] = "ESCAPE";
        names[(int)Key.Space] = "SPACE";
        names[(int)Key.Exclaim] = "!";
        names[(int)Key.DoubleQuote] = "\"";
        names[(int)Key.Hash] = "#";
        names[(int)Key.Dollar] = "$";
        names[(int)Key.Ampersand] = "&";
        names[(int)Key.Quote] = "'";
        names[(int)Key.Backquote] = "`";
        names[(int)Key.LeftParenthesis] = "(";
        names[(int)Key.RightParenthesis] = ")";
        names[(int)Key.Asterisk] = "*";
        names[(int)Key.Plus] = "+";
        names[(int)Key.Comma] = ",";
        names[(int)Key.Minus] = "-";
        names[(int)Key.Period] = ".";
        names[(int)Key.Slash] = "/";
        names[(int)Key.Colon] = ":";
        names[(int)Key.Semicolon] = ";";
        names[(int)Key.Less] = "<";
        names[(int)Key.Equals] = "=";
        names[(int)Key.Greater] = ">";
        names[(int)Key.Question] = "?";
        names[(int)Key.At] = "@";
        names[(int)Key.LeftBracket] = "[";
        names[(int)Key.Backslash] = "\\";
        names[(int)Key.RightBracket] = "]";
        names[(int)Key.Caret] = "^";
        names[(int)Key.Underscore] = "_";
        names[(int)Key.LeftAlt] = "LEFT ALT";
        names[(int)Key.RightAlt] = "RIGHT ALT";
        names[(int)Key.LeftControl] = "LEFT CTRL";
        names[(int)Key.RightControl] = "RIGHT CTRL";
        names[(int)Key.LeftShift] = "LEFT SHIFT";
        names[(int)Key.RightShift] = "RIGHT SHIFT";
        names[(int)Key.Tab] = "TAB";
        names[(int)Key.Delete] = "DELETE";
        names[(int)Key.PageUp] = "PAGE UP";
        names[(int)Key.PageDown] = "PAGE DOWN";
        names[(int)Key.F1] = "F1";
        names[(int)Key.F2] = "F2";
        names[(int)Key.F3] = "F3";
        names[(int)Key.F4] = "F4";
        names[(int)Key.F5] = "F5";
        names[(int)Key.F6] = "F6";
        names[(int)Key.F7] = "F7";
        names[(int)Key.F8] = "F8";
        names[(int)Key.F9] = "F9";
        names[(int)Key.F10] = "F10";
        names[(int)Key.F11] = "F11";
        names[(int)Key.F12] = "F12";
        names[(int)Key.Left] = "LEFT";
        names[(int)Key.Right] = "RIGHT";
        names[(int)Key.Up] = "UP";
        names[(int)Key.Down] = "DOWN";

        for (int digit = 0; digit <= 9; ++digit)
        {
            names[(int)Key.Digit0 + digit] = digit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        for (int letter = 0; letter < 26; ++letter)
        {
            names[(int)Key.A + letter] = ((char)('A' + letter)).ToString();
        }

        names[(int)Key.Print] = "PRINT SCREEN";

        for (int digit = 0; digit <= 9; ++digit)
        {
            names[(int)Key.KeyPad0 + digit] = "KEYPAD " + digit.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        names[(int)Key.KeyPadPeriod] = "KEYPAD .";
        names[(int)Key.KeyPadDivide] = "KEYPAD /";
        names[(int)Key.KeyPadMultiply] = "KEYPAD *";
        names[(int)Key.KeyPadMinus] = "KEYPAD -";
        names[(int)Key.KeyPadPlus] = "KEYPAD +";
        names[(int)Key.KeyPadEnter] = "KEYPAD ENTER";
        names[(int)Key.KeyPadEquals] = "KEYPAD =";
        names[(int)Key.Home] = "HOME";
        names[(int)Key.End] = "END";
        names[(int)Key.MouseButtonBackward] = "MOUSE BACKWARD";
        names[(int)Key.MouseButtonForward] = "MOUSE FORWARD";

        return names;
    }

    private static Dictionary<string, Key> BuildLookup()
    {
        Dictionary<string, Key> lookup = new(Names.Length, StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < Names.Length; ++index)
        {
            // 同一个名字只保留第一个键：KEY_0 与 KEY_KP_0 的名字不同，所以不会真的撞车，但重复名字按先出现的算。
            lookup.TryAdd(Names[index], (Key)index);
        }

        return lookup;
    }
}
