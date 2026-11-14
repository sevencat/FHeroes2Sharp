using System.Text;
using FHeroes2.Assets.Maps;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="MapTextCodec"/>, the decoder of the name, description and condition fields of
/// an MP2 header. The header carries no encoding marker, so these tests pin the guesses it makes.
/// </summary>
public sealed class MapTextCodecTests
{
    private static readonly byte[] GbkMineWar =
        [0xBF, 0xF3, 0xC9, 0xBD, 0xD6, 0xAE, 0xD5, 0xBD]; // 矿山之战

    [Fact]
    public void Decode_StopsAtTheTerminatorAndTrimsThePadding()
    {
        byte[] field = [.. Encoding.ASCII.GetBytes("Test Map   "), 0x00, 0x41];

        Assert.Equal("Test Map", MapTextCodec.Decode(field));
    }

    [Fact]
    public void Decode_ReturnsAnEmptyStringForAnEmptyField()
    {
        Assert.Equal(string.Empty, MapTextCodec.Decode([]));
        Assert.Equal(string.Empty, MapTextCodec.Decode([0x00, 0x00]));
    }

    [Fact]
    public void Decode_KeepsAsciiAsItIs()
    {
        Assert.Equal("Slugfes", MapTextCodec.Decode("Slugfes"u8));
    }

    [Fact]
    public void Decode_ReadsTheChineseMapPacks()
    {
        if (!MapTextCodec.SupportsGbk)
        {
            return;
        }

        Assert.Equal("矿山之战", MapTextCodec.Decode(GbkMineWar));
        Assert.Equal("矿山之战", MapTextCodec.Decode([.. GbkMineWar, 0x00, 0x00]));
    }

    [Fact]
    public void Decode_KeepsASingleAccentedWordLatin1()
    {
        // 'Château' in CP1252 is C h E2 t e a u: the pair E2 74 also parses as one CJK glyph, which
        // is exactly why a GBK guess needs more than one CJK character to win.
        byte[] field = [0x43, 0x68, 0xE2, 0x74, 0x65, 0x61, 0x75];

        Assert.Equal("Château", MapTextCodec.Decode(field));
    }

    [Fact]
    public void Decode_ReadsUtf8Maps()
    {
        Assert.Equal("矿山之战", MapTextCodec.Decode(Encoding.UTF8.GetBytes("矿山之战")));
    }

    [Fact]
    public void CountCjkCharacters_IgnoresTheLatinText()
    {
        Assert.Equal(4, MapTextCodec.CountCjkCharacters("矿山之战"));
        Assert.Equal(0, MapTextCodec.CountCjkCharacters("Alteris"));
        Assert.Equal(1, MapTextCodec.CountCjkCharacters("Map 山"));
    }
}
