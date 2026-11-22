using FHeroes2.UI.Input;

namespace FHeroes2.App.Input;

/// <summary>
/// Translates the SDL3 key codes into the keys of the game, the table the reference keeps inside
/// <c>EventEngine::handleEvents</c> (<c>localevent.cpp:1450-1660</c>).
/// </summary>
/// <remarks>
/// <para>
/// SDL gives a key code, not a character: the value of a letter is its lower case ASCII code and the
/// value of a special key is <c>SDL_SCANCODE_TO_KEYCODE( scancode )</c>, i.e. the scancode with
/// <see cref="ScanCodeMask"/> set. The game's <see cref="Key"/> enumeration follows the reference's
/// <c>KEY_*</c> order instead, so the codes cannot be cast and this table is the only bridge.
/// </para>
/// <para>
/// The reference collapses both Enter keys: <c>KEY_KP_ENTER</c> and <c>KEY_ENTER</c> reach
/// <see cref="LocalEvent"/> as <see cref="Key.Enter"/>, exactly like SDL gives <c>'\r'</c> for both.
/// </para>
/// </remarks>
internal static class SdlKeyMap
{
    /// <summary><c>SDLK_SCANCODE_MASK</c>: set on every key code that is not a printable character.</summary>
    private const int ScanCodeMask = 1 << 30;

    // SDL3 key codes of the special keys (SDL_keycode.h). The scancode numbers are the ones of
    // SDL_scancode.h | SDLK_SCANCODE_MASK.
    private const int F1 = ScanCodeMask | 0x3A;
    private const int F12 = ScanCodeMask | 0x45;
    private const int PrintScreen = ScanCodeMask | 0x46;
    private const int Home = ScanCodeMask | 0x4A;
    private const int PageUp = ScanCodeMask | 0x4B;
    private const int End = ScanCodeMask | 0x4D;
    private const int PageDown = ScanCodeMask | 0x4E;
    private const int ArrowRight = ScanCodeMask | 0x4F;
    private const int ArrowLeft = ScanCodeMask | 0x50;
    private const int ArrowDown = ScanCodeMask | 0x51;
    private const int ArrowUp = ScanCodeMask | 0x52;
    private const int KeyPadDivide = ScanCodeMask | 0x54;
    private const int KeyPadMultiply = ScanCodeMask | 0x55;
    private const int KeyPadMinus = ScanCodeMask | 0x56;
    private const int KeyPadPlus = ScanCodeMask | 0x57;
    private const int KeyPadEnter = ScanCodeMask | 0x58;
    private const int KeyPadOne = ScanCodeMask | 0x59;
    private const int KeyPadNine = ScanCodeMask | 0x61;
    private const int KeyPadZero = ScanCodeMask | 0x62;
    private const int KeyPadPeriod = ScanCodeMask | 0x63;
    private const int KeyPadEquals = ScanCodeMask | 0x67;
    private const int LeftControl = ScanCodeMask | 0xE0;
    private const int LeftShift = ScanCodeMask | 0xE1;
    private const int LeftAlt = ScanCodeMask | 0xE2;
    private const int RightControl = ScanCodeMask | 0xE4;
    private const int RightShift = ScanCodeMask | 0xE5;
    private const int RightAlt = ScanCodeMask | 0xE6;

    // SDL_Keymod bits (SDL_keycode.h). The port only needs the four the game reacts to; Caps Lock and
    // Num Lock are informational and are mapped too, because KeyModifier carries them.
    private const int ModLeftShift = 0x0001;
    private const int ModRightShift = 0x0002;
    private const int ModLeftControl = 0x0040;
    private const int ModRightControl = 0x0080;
    private const int ModLeftAlt = 0x0100;
    private const int ModRightAlt = 0x0200;
    private const int ModNumLock = 0x1000;
    private const int ModCapsLock = 0x2000;

    /// <summary>The game's key for an SDL key code, or <see cref="Key.None"/> for a key the game ignores.</summary>
    /// <param name="code">SDL key code, <c>SDL_KeyboardEvent::key</c>.</param>
    /// <returns>The key of <see cref="Key"/>.</returns>
    public static Key ToKey(int code) => code switch
    {
        // Letters and digits are their unshifted ASCII values, and both enumerations are contiguous.
        >= 'a' and <= 'z' => (Key)((int)Key.A + (code - 'a')),
        >= 'A' and <= 'Z' => (Key)((int)Key.A + (code - 'A')),
        >= '0' and <= '9' => (Key)((int)Key.Digit0 + (code - '0')),

        '\r' => Key.Enter,
        '\b' => Key.Backspace,
        '\t' => Key.Tab,
        0x1B => Key.Escape,
        ' ' => Key.Space,
        0x7F => Key.Delete,

        '!' => Key.Exclaim,
        '"' => Key.DoubleQuote,
        '#' => Key.Hash,
        '$' => Key.Dollar,
        '&' => Key.Ampersand,
        '\'' => Key.Quote,
        '`' => Key.Backquote,
        '(' => Key.LeftParenthesis,
        ')' => Key.RightParenthesis,
        '*' => Key.Asterisk,
        '+' => Key.Plus,
        ',' => Key.Comma,
        '-' => Key.Minus,
        '.' => Key.Period,
        '/' => Key.Slash,
        ':' => Key.Colon,
        ';' => Key.Semicolon,
        '<' => Key.Less,
        '=' => Key.Equals,
        '>' => Key.Greater,
        '?' => Key.Question,
        '@' => Key.At,
        '[' => Key.LeftBracket,
        '\\' => Key.Backslash,
        ']' => Key.RightBracket,
        '^' => Key.Caret,
        '_' => Key.Underscore,

        >= F1 and <= F12 => (Key)((int)Key.F1 + (code - F1)),
        PrintScreen => Key.Print,
        Home => Key.Home,
        End => Key.End,
        PageUp => Key.PageUp,
        PageDown => Key.PageDown,
        ArrowRight => Key.Right,
        ArrowLeft => Key.Left,
        ArrowDown => Key.Down,
        ArrowUp => Key.Up,

        KeyPadDivide => Key.KeyPadDivide,
        KeyPadMultiply => Key.KeyPadMultiply,
        KeyPadMinus => Key.KeyPadMinus,
        KeyPadPlus => Key.KeyPadPlus,
        KeyPadEnter => Key.Enter,
        >= KeyPadOne and <= KeyPadNine => (Key)((int)Key.KeyPad1 + (code - KeyPadOne)),
        KeyPadZero => Key.KeyPad0,
        KeyPadPeriod => Key.KeyPadPeriod,
        KeyPadEquals => Key.KeyPadEquals,

        LeftControl => Key.LeftControl,
        RightControl => Key.RightControl,
        LeftShift => Key.LeftShift,
        RightShift => Key.RightShift,
        LeftAlt => Key.LeftAlt,
        RightAlt => Key.RightAlt,

        _ => Key.None,
    };

    /// <summary>The modifier bits of an SDL event, <c>SDL_KeyboardEvent::mod</c>.</summary>
    /// <param name="mod">SDL key modifier bits.</param>
    /// <returns>The bits of <see cref="KeyModifier"/>.</returns>
    public static KeyModifier ToModifier(int mod)
    {
        KeyModifier result = KeyModifier.None;

        if ((mod & (ModLeftControl | ModRightControl)) != 0)
        {
            result |= KeyModifier.Ctrl;
        }

        if ((mod & (ModLeftShift | ModRightShift)) != 0)
        {
            result |= KeyModifier.Shift;
        }

        if ((mod & (ModLeftAlt | ModRightAlt)) != 0)
        {
            result |= KeyModifier.Alt;
        }

        if ((mod & ModCapsLock) != 0)
        {
            result |= KeyModifier.Caps;
        }

        if ((mod & ModNumLock) != 0)
        {
            result |= KeyModifier.Num;
        }

        return result;
    }
}
