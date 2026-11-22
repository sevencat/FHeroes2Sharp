using FHeroes2.Core.Imaging;
using FHeroes2.Game.Gui;
using Xunit;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Tests of the generated button alphabets, <c>generateButtonAlphabet</c> plus the five
/// <c>generateCPxxxxGoodButtonFont</c> functions (<c>ui_font.cpp:5604-6870</c>).
/// </summary>
/// <remarks>
/// 与 <see cref="FontAlphabetTests"/> 一样，这里只钉骨架：哪些语言会长出 256 帧、非 ASCII 码位上真的有字、
/// 不断行空格的宽度，以及四个面都拿到同一套字母。
/// </remarks>
public sealed class ButtonFontTests
{
    [Fact]
    public void EnglishButtonsStayAsciiOnly()
    {
        ButtonFont.Faces faces = ButtonFont.Create(SupportedLanguage.English);

        Assert.Equal(ButtonFont.AsciiCharSetSize, faces.WhiteReleased.Length);
        Assert.Equal(ButtonFont.AsciiCharSetSize, faces.WhitePressed.Length);
        Assert.Equal(ButtonFont.AsciiCharSetSize, faces.GrayReleased.Length);
        Assert.Equal(ButtonFont.AsciiCharSetSize, faces.GrayPressed.Length);
    }

    [Fact]
    public void RussianButtonsCarryTheCyrillicLetters()
    {
        ButtonFont.Faces faces = ButtonFont.Create(SupportedLanguage.Russian);

        // 参考把 128 帧插到 256 帧（ui_font.cpp:5607），补完字母再派生四个面。
        Assert.Equal(FontAlphabet.CodePageSize, faces.WhiteReleased.Length);
        Assert.Equal(FontAlphabet.CodePageSize, faces.WhitePressed.Length);
        Assert.Equal(FontAlphabet.CodePageSize, faces.GrayReleased.Length);
        Assert.Equal(FontAlphabet.CodePageSize, faces.GrayPressed.Length);

        // CP1251 0xC0 ('А') 是照着白纸画的，不是 ASCII 'A' 的副本：宽 10 + 2 * 2 像素（ui_font.cpp:5896）。
        Sprite cyrillicA = Assert.IsType<Sprite>(faces.WhiteReleased[0xC0]);
        Assert.Equal(10 + (2 * 2), cyrillicA.Width);
        Assert.NotSame(faces.WhiteReleased[0x41], cyrillicA);

        // 不断行空格是 8 × 1 的横条（ui_font.cpp:5620 的 bigFontSpaceWidth）。
        Sprite noBreakSpace = Assert.IsType<Sprite>(faces.WhiteReleased[0xA0]);
        Assert.Equal(8, noBreakSpace.Width);
        Assert.Equal(1, noBreakSpace.Height);
    }

    [Fact]
    public void TurkishButtonsGetTheDotlessLetters()
    {
        ButtonFont.Faces faces = ButtonFont.Create(SupportedLanguage.Turkish);

        // CP1254 只补了带变音符的少数几个字母，0xDD ('İ') 和 0xFD ('ı') 都在里面。
        Assert.Equal(FontAlphabet.CodePageSize, faces.WhiteReleased.Length);
        Assert.NotNull(faces.WhiteReleased[0xDD]);
        Assert.NotNull(faces.WhiteReleased[0xFD]);
        Assert.True(faces.WhiteReleased[0xDD]!.Width > 1);
    }

    [Fact]
    public void LanguagesWithoutBonusLettersKeepTheAsciiSize()
    {
        // 越南语的码页是 CP1258，参考的按钮字体没有对应补充（只有 1250-1254 五个），表停在 128 帧。
        Assert.Equal(ButtonFont.AsciiCharSetSize, ButtonFont.Create(SupportedLanguage.Vietnamese).WhiteReleased.Length);
    }

    [Fact]
    public void EveryFaceGetsTheSameLetters()
    {
        ButtonFont.Faces faces = ButtonFont.Create(SupportedLanguage.Russian);

        for (int code = FontAlphabet.FirstCharacter; code < FontAlphabet.CodePageSize; ++code)
        {
            Assert.NotNull(faces.WhiteReleased[code]);
            Assert.NotNull(faces.WhitePressed[code]);
            Assert.NotNull(faces.GrayReleased[code]);
            Assert.NotNull(faces.GrayPressed[code]);
        }
    }
}
