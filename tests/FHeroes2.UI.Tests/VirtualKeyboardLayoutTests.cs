using FHeroes2.Core.Geometry;
using FHeroes2.Game;
using FHeroes2.UI.Dialogs;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the character tables and the key sizes of the virtual keyboard, a port of
/// <c>getCharacterLayout()</c> and friends (<c>ui_keyboard.cpp:435-623</c>).
/// </summary>
public sealed class VirtualKeyboardLayoutTests
{
    [Fact]
    public void TheThreeLetterLayoutsOfEnglishKeepTheRowsOfTheReference()
    {
        (string[] buttonLetters, string[] returnLetters) =
            VirtualKeyboardLayout.GetCharacterLayout(KeyboardLayoutType.LowerCase, SupportedLanguage.English);

        Assert.Equal(3, buttonLetters.Length);
        Assert.Equal("QWERTYUIOP", buttonLetters[0]);
        Assert.Equal("ASDFGHJKL", buttonLetters[1]);
        Assert.Equal("ZXCVBNM", buttonLetters[2]);

        // The lower case layout prints capital letters but types small ones.
        Assert.Equal("qwertyuiop", returnLetters[0]);
        Assert.Equal("asdfghjkl", returnLetters[1]);
        Assert.Equal("zxcvbnm", returnLetters[2]);

        (string[] upperButtons, string[] upperReturns) =
            VirtualKeyboardLayout.GetCharacterLayout(KeyboardLayoutType.UpperCase, SupportedLanguage.English);

        Assert.Equal(upperButtons, upperReturns);
        Assert.Equal(buttonLetters, upperButtons);
    }

    [Fact]
    public void TheAlphaNumericLayoutHasDigitsPunctuationAndTheUnsignedLayoutHasDigitsOnly()
    {
        (string[] alphaNumeric, _) =
            VirtualKeyboardLayout.GetCharacterLayout(KeyboardLayoutType.AlphaNumeric, SupportedLanguage.English);

        Assert.Equal(3, alphaNumeric.Length);
        Assert.Equal("1234567890", alphaNumeric[0]);
        Assert.Equal("-:;()_+=", alphaNumeric[1]);
        Assert.Equal("[].,!'?", alphaNumeric[2]);

        (string[] signed, _) =
            VirtualKeyboardLayout.GetCharacterLayout(KeyboardLayoutType.SignedNumeric, SupportedLanguage.English);
        (string[] unsigned, _) =
            VirtualKeyboardLayout.GetCharacterLayout(KeyboardLayoutType.UnsignedNumeric, SupportedLanguage.English);

        Assert.Equal(3, signed.Length);
        Assert.Equal(signed, unsigned);
        Assert.Equal("123", signed[0]);
        Assert.Equal("456", signed[1]);
        Assert.Equal("789", signed[2]);
    }

    [Theory]
    [InlineData(SupportedLanguage.French, 12, 24)]
    [InlineData(SupportedLanguage.Hungarian, 13, 21)]
    [InlineData(SupportedLanguage.Polish, 10, 30)]
    [InlineData(SupportedLanguage.English, 10, 30)]
    public void TheKeySizeFollowsTheLongestRowOfTheLanguage(SupportedLanguage language, int longestRow,
        int expectedWidth)
    {
        string[] capital = VirtualKeyboardLayout.GetCapitalCharacterLayout(language);

        Assert.Equal(longestRow, capital.Max(row => row.Length));

        // A wider row means narrower keys so that the keyboard never gets wider than 640 pixels.
        Assert.Equal(new Size(expectedWidth, VirtualKeyboardLayout.DefaultButtonHeight),
            VirtualKeyboardLayout.GetDefaultButtonSize(language));
    }

    [Fact]
    public void OnlyLanguagesWithASecondLetterSetOfferTheLanguageKey()
    {
        Assert.False(VirtualKeyboardLayout.IsLanguageSwitchingSupported(SupportedLanguage.English));
        Assert.False(VirtualKeyboardLayout.IsLanguageSwitchingSupported(SupportedLanguage.Greek));
        Assert.True(VirtualKeyboardLayout.IsLanguageSwitchingSupported(SupportedLanguage.Russian));
        Assert.True(VirtualKeyboardLayout.IsLanguageSwitchingSupported(SupportedLanguage.French));
    }

    [Fact]
    public void TheLettersAreDecodedFromTheCodePageOfTheLanguage()
    {
        // The Russian table is stored in CP1251, so the first byte 0xC9 has to come out as a capital I.
        string[] russian = VirtualKeyboardLayout.GetCapitalCharacterLayout(SupportedLanguage.Russian);

        Assert.Equal("ЙЦУКЕНГШЩЗХЪ", russian[0]);
        Assert.Equal(12, russian[0].Length);
    }

    [Fact]
    public void AnUnknownLayoutTypeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VirtualKeyboardLayout.GetCharacterLayout((KeyboardLayoutType)99, SupportedLanguage.English));
    }
}
