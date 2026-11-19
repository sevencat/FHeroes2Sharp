using System.Text;
using FHeroes2.UI.Input;

namespace FHeroes2.UI.Tests;

/// <summary>
/// <see cref="KeyText"/>: the character side of the keyboard of <c>src/engine/localevent.cpp:76-320</c>
/// and <c>InsertKeySym</c> (<c>localevent.cpp:1172-1210</c>).
/// </summary>
public sealed class KeyTextTests
{
    [Fact]
    public void Letters_FollowShiftAndCaps()
    {
        Assert.Equal('a', KeyText.GetCharacterFromPressedKey(Key.A, KeyModifier.None));
        Assert.Equal('a', KeyText.GetCharacterFromPressedKey(Key.A, KeyModifier.Num));
        Assert.Equal('A', KeyText.GetCharacterFromPressedKey(Key.A, KeyModifier.Shift));
        Assert.Equal('A', KeyText.GetCharacterFromPressedKey(Key.A, KeyModifier.Caps));

        // Shift and Caps Lock cancel each other, the reference's first two lines.
        Assert.Equal('a', KeyText.GetCharacterFromPressedKey(Key.A, KeyModifier.Shift | KeyModifier.Caps));

        Assert.Equal('z', KeyText.GetCharacterFromPressedKey(Key.Z, KeyModifier.None));
        Assert.Equal('Z', KeyText.GetCharacterFromPressedKey(Key.Z, KeyModifier.Shift));
    }

    [Fact]
    public void Digits_AreShiftedSymbols()
    {
        Assert.Equal('1', KeyText.GetCharacterFromPressedKey(Key.Digit1, KeyModifier.None));
        Assert.Equal('!', KeyText.GetCharacterFromPressedKey(Key.Digit1, KeyModifier.Shift));
        Assert.Equal('0', KeyText.GetCharacterFromPressedKey(Key.Digit0, KeyModifier.None));
        Assert.Equal(')', KeyText.GetCharacterFromPressedKey(Key.Digit0, KeyModifier.Shift));
        Assert.Equal('7', KeyText.GetCharacterFromPressedKey(Key.Digit7, KeyModifier.Caps));
        Assert.Equal('&', KeyText.GetCharacterFromPressedKey(Key.Digit7, KeyModifier.Shift));
    }

    [Fact]
    public void Punctuation_IsShiftedByShiftOnly()
    {
        Assert.Equal('-', KeyText.GetCharacterFromPressedKey(Key.Minus, KeyModifier.None));
        Assert.Equal('_', KeyText.GetCharacterFromPressedKey(Key.Minus, KeyModifier.Shift));
        Assert.Equal('=', KeyText.GetCharacterFromPressedKey(Key.Equals, KeyModifier.None));
        Assert.Equal('+', KeyText.GetCharacterFromPressedKey(Key.Equals, KeyModifier.Shift));
        Assert.Equal('\\', KeyText.GetCharacterFromPressedKey(Key.Backslash, KeyModifier.None));
        Assert.Equal('|', KeyText.GetCharacterFromPressedKey(Key.Backslash, KeyModifier.Shift));
        Assert.Equal('[', KeyText.GetCharacterFromPressedKey(Key.LeftBracket, KeyModifier.None));
        Assert.Equal('{', KeyText.GetCharacterFromPressedKey(Key.LeftBracket, KeyModifier.Shift));
        Assert.Equal(';', KeyText.GetCharacterFromPressedKey(Key.Semicolon, KeyModifier.None));
        Assert.Equal(':', KeyText.GetCharacterFromPressedKey(Key.Semicolon, KeyModifier.Shift));
        Assert.Equal('\'', KeyText.GetCharacterFromPressedKey(Key.Quote, KeyModifier.None));
        Assert.Equal('"', KeyText.GetCharacterFromPressedKey(Key.Quote, KeyModifier.Shift));
        Assert.Equal(',', KeyText.GetCharacterFromPressedKey(Key.Comma, KeyModifier.None));
        Assert.Equal('<', KeyText.GetCharacterFromPressedKey(Key.Comma, KeyModifier.Shift));
        Assert.Equal('.', KeyText.GetCharacterFromPressedKey(Key.Period, KeyModifier.None));
        Assert.Equal('>', KeyText.GetCharacterFromPressedKey(Key.Period, KeyModifier.Shift));
        Assert.Equal('/', KeyText.GetCharacterFromPressedKey(Key.Slash, KeyModifier.None));
        Assert.Equal('?', KeyText.GetCharacterFromPressedKey(Key.Slash, KeyModifier.Shift));

        // Caps Lock does not touch the punctuation.
        Assert.Equal('-', KeyText.GetCharacterFromPressedKey(Key.Minus, KeyModifier.Caps));
    }

    [Fact]
    public void Keypad_NeedsNumLock()
    {
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.KeyPad5, KeyModifier.None));
        Assert.Equal('5', KeyText.GetCharacterFromPressedKey(Key.KeyPad5, KeyModifier.Num));
        Assert.Equal('.', KeyText.GetCharacterFromPressedKey(Key.KeyPadPeriod, KeyModifier.Num));
        Assert.Equal('/', KeyText.GetCharacterFromPressedKey(Key.KeyPadDivide, KeyModifier.Num));
        Assert.Equal('*', KeyText.GetCharacterFromPressedKey(Key.KeyPadMultiply, KeyModifier.Num));
        Assert.Equal('-', KeyText.GetCharacterFromPressedKey(Key.KeyPadMinus, KeyModifier.Num));
        Assert.Equal('+', KeyText.GetCharacterFromPressedKey(Key.KeyPadPlus, KeyModifier.Num));

        // Shift does not change a keypad digit.
        Assert.Equal('5', KeyText.GetCharacterFromPressedKey(Key.KeyPad5, KeyModifier.Num | KeyModifier.Shift));
    }

    [Fact]
    public void AlreadyShiftedKeys_IgnoreTheModifier()
    {
        Assert.Equal('!', KeyText.GetCharacterFromPressedKey(Key.Exclaim, KeyModifier.None));
        Assert.Equal('!', KeyText.GetCharacterFromPressedKey(Key.Exclaim, KeyModifier.Shift));
        Assert.Equal('#', KeyText.GetCharacterFromPressedKey(Key.Hash, KeyModifier.None));
        Assert.Equal('@', KeyText.GetCharacterFromPressedKey(Key.At, KeyModifier.None));
        Assert.Equal('_', KeyText.GetCharacterFromPressedKey(Key.Underscore, KeyModifier.None));
        Assert.Equal(' ', KeyText.GetCharacterFromPressedKey(Key.Space, KeyModifier.Shift));
    }

    [Fact]
    public void KeysWithoutACharacter_ReturnNul()
    {
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.None, KeyModifier.None));
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.Enter, KeyModifier.None));
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.Escape, KeyModifier.None));
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.Backspace, KeyModifier.None));
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.F1, KeyModifier.None));
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.Left, KeyModifier.None));
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.KeyPadEnter, KeyModifier.Num));
        Assert.Equal('\0', KeyText.GetCharacterFromPressedKey(Key.LastKey, KeyModifier.None));
    }

    [Fact]
    public void InsertKeySym_AppendsAtTheEnd()
    {
        StringBuilder text = new("ab");

        int position = KeyText.InsertKeySym(text, 2, Key.C, KeyModifier.None);

        Assert.Equal("abc", text.ToString());
        Assert.Equal(3, position);
    }

    [Fact]
    public void InsertKeySym_InsertsAtTheCursor()
    {
        StringBuilder text = new("ac");

        int position = KeyText.InsertKeySym(text, 1, Key.B, KeyModifier.None);

        Assert.Equal("abc", text.ToString());
        Assert.Equal(2, position);
    }

    [Fact]
    public void InsertKeySym_BackspaceDeletesBeforeTheCursor()
    {
        StringBuilder text = new("abc");

        int position = KeyText.InsertKeySym(text, 2, Key.Backspace, KeyModifier.None);

        Assert.Equal("ac", text.ToString());
        Assert.Equal(1, position);
    }

    [Fact]
    public void InsertKeySym_BackspaceAtTheEndDeletesTheLastCharacter()
    {
        // The reference has a separate branch for "pos >= size": it shrinks the string instead of
        // erasing at a stale index. The port clamps the cursor into the text first, so a cursor one
        // past the end behaves like one sitting on the last character.
        StringBuilder text = new("abc");

        int position = KeyText.InsertKeySym(text, 99, Key.Backspace, KeyModifier.None);

        Assert.Equal("ab", text.ToString());
        Assert.Equal(2, position);
    }

    [Fact]
    public void InsertKeySym_BackspaceOnAnEmptyTextDoesNothing()
    {
        StringBuilder text = new();

        int position = KeyText.InsertKeySym(text, 0, Key.Backspace, KeyModifier.None);

        Assert.Equal("", text.ToString());
        Assert.Equal(0, position);
    }

    [Fact]
    public void InsertKeySym_DeleteRemovesUnderTheCursor()
    {
        StringBuilder text = new("abc");

        int position = KeyText.InsertKeySym(text, 1, Key.Delete, KeyModifier.None);

        Assert.Equal("ac", text.ToString());
        Assert.Equal(1, position);

        // Nothing to the right of the cursor: no change.
        position = KeyText.InsertKeySym(text, 2, Key.Delete, KeyModifier.None);

        Assert.Equal("ac", text.ToString());
        Assert.Equal(2, position);
    }

    [Fact]
    public void InsertKeySym_ArrowsHomeAndEndMoveTheCursorOnly()
    {
        StringBuilder text = new("abc");

        Assert.Equal(2, KeyText.InsertKeySym(text, 3, Key.Left, KeyModifier.None));
        Assert.Equal(0, KeyText.InsertKeySym(text, 0, Key.Left, KeyModifier.None));
        Assert.Equal(1, KeyText.InsertKeySym(text, 0, Key.Right, KeyModifier.None));
        Assert.Equal(3, KeyText.InsertKeySym(text, 3, Key.Right, KeyModifier.None));
        Assert.Equal(0, KeyText.InsertKeySym(text, 2, Key.Home, KeyModifier.None));
        Assert.Equal(3, KeyText.InsertKeySym(text, 1, Key.End, KeyModifier.None));

        Assert.Equal("abc", text.ToString());
    }

    [Fact]
    public void InsertKeySym_PrintableKeysRespectTheModifier()
    {
        StringBuilder text = new();

        int position = KeyText.InsertKeySym(text, 0, Key.Digit1, KeyModifier.Shift);
        position = KeyText.InsertKeySym(text, position, Key.A, KeyModifier.None);
        position = KeyText.InsertKeySym(text, position, Key.A, KeyModifier.Shift);

        Assert.Equal("!aA", text.ToString());
        Assert.Equal(3, position);
    }

    [Fact]
    public void InsertKeySym_IgnoresKeysWithoutACharacter()
    {
        StringBuilder text = new("ab");

        int position = KeyText.InsertKeySym(text, 1, Key.F5, KeyModifier.None);

        Assert.Equal("ab", text.ToString());
        Assert.Equal(1, position);
    }

    [Fact]
    public void InsertKeySym_ClampsAStaleCursor()
    {
        StringBuilder text = new("ab");

        Assert.Equal(2, KeyText.InsertKeySym(text, -5, Key.End, KeyModifier.None));
        Assert.Equal("ab", text.ToString());

        Assert.Equal(0, KeyText.InsertKeySym(text, 50, Key.Home, KeyModifier.None));
    }
}
