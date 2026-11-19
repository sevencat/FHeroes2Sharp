using System.Text;

namespace FHeroes2.UI.Input;

/// <summary>
/// Turns a key of a keyboard layout into the character it stands for, the reference's
/// <c>getCharacterFromPressedKey</c> and <c>InsertKeySym</c> (<c>src/engine/localevent.cpp:76-320</c>).
/// </summary>
/// <remarks>
/// <para>
/// The reference keeps both functions in the anonymous namespace of <c>localevent.cpp</c>, so their
/// only caller is <c>InsertKeySym</c>. The port exposes them as well: they are pure functions of the
/// key and the modifier bits, and the text input of a dialog can be tested without a window.
/// </para>
/// <para>
/// <see cref="GetCharacterFromPressedKey"/> returns <c>'\0'</c> where the reference returns
/// <c>0</c> - the value that makes the caller skip the key.
/// </para>
/// </remarks>
public static class KeyText
{
    /// <summary>
    /// The character <paramref name="key"/> produces with <paramref name="modifier"/> held, or
    /// <c>'\0'</c> when it produces none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference: <c>localevent.cpp:76-320</c>. The first two lines are the reference's own rule for
    /// the case that Shift and Caps Lock are both on: the two cancel each other, so holding Shift
    /// with Caps Lock on types the lower case letter.
    /// </para>
    /// <para>
    /// The keypad digits and operators only produce a character while Num Lock is on, which is what
    /// the <c>if ( KEY_MODIFIER_NUM &amp; mod )</c> branches say. The arrow keys, the F keys,
    /// Enter, Escape and the keypad Enter/Equals produce nothing at all in the reference, so they
    /// produce nothing here either.
    /// </para>
    /// </remarks>
    /// <param name="key">Key that was pressed.</param>
    /// <param name="modifier">Modifier bits that were held.</param>
    /// <returns>The character, or <c>'\0'</c>.</returns>
    public static char GetCharacterFromPressedKey(Key key, KeyModifier modifier)
    {
        if ((modifier & KeyModifier.Shift) != 0 && (modifier & KeyModifier.Caps) != 0)
        {
            modifier &= ~(KeyModifier.Shift | KeyModifier.Caps);
        }

        bool shift = (modifier & KeyModifier.Shift) != 0;
        bool num = (modifier & KeyModifier.Num) != 0;
        bool upper = (modifier & (KeyModifier.Shift | KeyModifier.Caps)) != 0;

        switch (key)
        {
            case Key.Digit1:
                return shift ? '!' : '1';
            case Key.Digit2:
                return shift ? '@' : '2';
            case Key.Digit3:
                return shift ? '#' : '3';
            case Key.Digit4:
                return shift ? '$' : '4';
            case Key.Digit5:
                return shift ? '%' : '5';
            case Key.Digit6:
                return shift ? '^' : '6';
            case Key.Digit7:
                return shift ? '&' : '7';
            case Key.Digit8:
                return shift ? '*' : '8';
            case Key.Digit9:
                return shift ? '(' : '9';
            case Key.Digit0:
                return shift ? ')' : '0';

            case Key.KeyPad0:
                return num ? '0' : '\0';
            case Key.KeyPad1:
                return num ? '1' : '\0';
            case Key.KeyPad2:
                return num ? '2' : '\0';
            case Key.KeyPad3:
                return num ? '3' : '\0';
            case Key.KeyPad4:
                return num ? '4' : '\0';
            case Key.KeyPad5:
                return num ? '5' : '\0';
            case Key.KeyPad6:
                return num ? '6' : '\0';
            case Key.KeyPad7:
                return num ? '7' : '\0';
            case Key.KeyPad8:
                return num ? '8' : '\0';
            case Key.KeyPad9:
                return num ? '9' : '\0';
            case Key.KeyPadPeriod:
                return num ? '.' : '\0';
            case Key.KeyPadDivide:
                return num ? '/' : '\0';
            case Key.KeyPadMultiply:
                return num ? '*' : '\0';
            case Key.KeyPadMinus:
                return num ? '-' : '\0';
            case Key.KeyPadPlus:
                return num ? '+' : '\0';

            case Key.Minus:
                return shift ? '_' : '-';
            case Key.Equals:
                return shift ? '+' : '=';
            case Key.Backslash:
                return shift ? '|' : '\\';
            case Key.LeftBracket:
                return shift ? '{' : '[';
            case Key.RightBracket:
                return shift ? '}' : ']';
            case Key.Semicolon:
                return shift ? ':' : ';';
            case Key.Quote:
                return shift ? '"' : '\'';
            case Key.Comma:
                return shift ? '<' : ',';
            case Key.Period:
                return shift ? '>' : '.';
            case Key.Slash:
                return shift ? '?' : '/';

            // The keys that already are a shifted character: their modifier does not matter.
            case Key.Exclaim:
                return '!';
            case Key.At:
                return '@';
            case Key.Hash:
                return '#';
            case Key.Dollar:
                return '$';
            case Key.Ampersand:
                return '&';
            case Key.Asterisk:
                return '*';
            case Key.LeftParenthesis:
                return '(';
            case Key.RightParenthesis:
                return ')';
            case Key.DoubleQuote:
                return '"';
            case Key.Plus:
                return '+';
            case Key.Colon:
                return ':';
            case Key.Less:
                return '<';
            case Key.Greater:
                return '>';
            case Key.Question:
                return '?';
            case Key.Caret:
                return '^';
            case Key.Underscore:
                return '_';
            case Key.Space:
                return ' ';

            // The reference has one case per letter (localevent.cpp:230-281) with the same
            // ( Shift | Caps ) & mod test; the 26 cases are folded into one here because Key.A ..
            // Key.Z are contiguous in the enumeration above.
            case >= Key.A and <= Key.Z:
                return upper ? (char)('A' + (key - Key.A)) : (char)('a' + (key - Key.A));

            default:
                return '\0';
        }
    }

    /// <summary>
    /// Applies a key to <paramref name="text"/> at <paramref name="position"/> and returns the
    /// cursor position afterwards, the reference's <c>InsertKeySym</c>
    /// (<c>localevent.cpp:1172-1210</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Backspace deletes the character before the cursor - or the last one when the cursor sits at or
    /// past the end, which is the reference's <c>pos &gt;= res.size()</c> branch - Delete removes the
    /// one under it, the arrows, Home and End only move the cursor, and every other key inserts the
    /// character <see cref="GetCharacterFromPressedKey"/> gives it.
    /// </para>
    /// <para>
    /// The reference takes <c>position</c> as a <c>size_t</c> and trusts the caller to keep it within
    /// the string; the port clamps it into <c>[0, text.Length]</c> so that a stale cursor from a
    /// dialog cannot throw out of a text box.
    /// </para>
    /// </remarks>
    /// <param name="text">Text that is edited in place.</param>
    /// <param name="position">Cursor position, in characters.</param>
    /// <param name="key">Key that was pressed.</param>
    /// <param name="modifier">Modifier bits that were held.</param>
    /// <returns>The cursor position after the key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static int InsertKeySym(StringBuilder text, int position, Key key, KeyModifier modifier)
    {
        ArgumentNullException.ThrowIfNull(text);

        position = Math.Clamp(position, 0, text.Length);

        switch (key)
        {
            case Key.Backspace:
                if (text.Length > 0 && position > 0)
                {
                    if (position >= text.Length)
                    {
                        text.Length -= 1;
                    }
                    else
                    {
                        text.Remove(position - 1, 1);
                    }

                    --position;
                }

                break;

            case Key.Delete:
                if (position < text.Length)
                {
                    text.Remove(position, 1);
                }

                break;

            case Key.Left:
                if (position > 0)
                {
                    --position;
                }

                break;

            case Key.Right:
                if (position < text.Length)
                {
                    ++position;
                }

                break;

            case Key.Home:
                position = 0;
                break;

            case Key.End:
                position = text.Length;
                break;

            default:
                char character = GetCharacterFromPressedKey(key, modifier);

                if (character != '\0')
                {
                    text.Insert(position, character);
                    ++position;
                }

                break;
        }

        return position;
    }
}
