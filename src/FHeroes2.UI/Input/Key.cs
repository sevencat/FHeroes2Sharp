namespace FHeroes2.UI.Input;

/// <summary>
/// The keys the game reacts to, the reference's <c>fheroes2::Key</c>
/// (<c>src/engine/localevent.h:40-170</c>).
/// </summary>
/// <remarks>
/// <para>
/// The order is the reference's order and has to stay that way: <see cref="LastKey"/> doubles as the
/// count of the enumeration, and hot key tables are indexed by value. The names drop the
/// <c>KEY_</c> prefix of the reference (<c>KEY_BACKSPACE</c> is <see cref="Backspace"/>,
/// <c>KEY_KP_0</c> is <see cref="KeyPad0"/>, <c>KEY_0</c> is <see cref="Digit0"/>), the values do
/// not change.
/// </para>
/// <para>
/// <see cref="Enter"/> stands for both Enter keys of a keyboard, exactly like the reference explains
/// next to <c>KEY_ENTER</c>: the SDL layer collapses the keypad Enter into it before the event
/// reaches <see cref="LocalEvent"/>.
/// </para>
/// </remarks>
public enum Key
{
    /// <summary>No key, the reference's <c>NONE</c>.</summary>
    None = 0,

    Backspace,
    Enter,
    Escape,
    Space,

    // Punctuation as a key, not as a character: KEY_EXCLAIM is the key that produces '!' on a US
    // layout. The character it produces with a modifier is decided by KeyText.
    Exclaim,
    DoubleQuote,
    Hash,
    Dollar,
    Ampersand,
    Quote,
    Backquote,
    LeftParenthesis,
    RightParenthesis,
    Asterisk,
    Plus,
    Comma,
    Minus,
    Period,
    Slash,
    Colon,
    Semicolon,
    Less,
    Equals,
    Greater,
    Question,
    At,
    LeftBracket,
    Backslash,
    RightBracket,
    Caret,
    Underscore,

    LeftAlt,
    RightAlt,
    LeftControl,
    RightControl,
    LeftShift,
    RightShift,

    Tab,
    Delete,
    PageUp,
    PageDown,

    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,

    Left,
    Right,
    Up,
    Down,

    Digit0,
    Digit1,
    Digit2,
    Digit3,
    Digit4,
    Digit5,
    Digit6,
    Digit7,
    Digit8,
    Digit9,

    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,

    Print,

    KeyPad0,
    KeyPad1,
    KeyPad2,
    KeyPad3,
    KeyPad4,
    KeyPad5,
    KeyPad6,
    KeyPad7,
    KeyPad8,
    KeyPad9,
    KeyPadPeriod,
    KeyPadDivide,
    KeyPadMultiply,
    KeyPadMinus,
    KeyPadPlus,
    KeyPadEnter,
    KeyPadEquals,

    Home,
    End,

    /// <summary>The backward mouse button, the reference's <c>KEY_MOUSE_BUTTON_BACKWARD</c>.</summary>
    MouseButtonBackward,

    /// <summary>The forward mouse button, the reference's <c>KEY_MOUSE_BUTTON_FORWARD</c>.</summary>
    MouseButtonForward,

    /// <summary>One past the last key, the reference's <c>LAST_KEY</c>.</summary>
    LastKey,
}

/// <summary>
/// Modifier bits of a key event, the reference's <c>fheroes2::KeyModifier</c>
/// (<c>src/engine/localevent.h:173-183</c>).
/// </summary>
[Flags]
public enum KeyModifier
{
    None = 0,

    Ctrl = 1 << 0,
    Shift = 1 << 1,
    Alt = 1 << 2,
    Caps = 1 << 3,
    Num = 1 << 4,
}
