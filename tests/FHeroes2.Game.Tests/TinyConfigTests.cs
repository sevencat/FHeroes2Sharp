using FHeroes2.Core.Geometry;
using FHeroes2.Game.System;

namespace FHeroes2.Game.Tests;

/// <summary>
/// Covers the parser of the configuration files, <c>TinyConfig</c> (<c>engine/tinyconfig.cpp</c>).
/// </summary>
/// <remarks>
/// 只用 <see cref="TinyConfig.Parse"/>，不碰磁盘：解析规则本身与文件位置无关，写文件的测试在
/// <see cref="SettingsTests"/> 里做一次就够。
/// </remarks>
public sealed class TinyConfigTests
{
    [Fact]
    public void KeysAreLowercasedTrimmedAndCollapsed()
    {
        TinyConfig config = Build("  Sound   Volume  = 6\nlang =ru\n");

        Assert.True(config.Exists("sound volume"));
        Assert.True(config.Exists("SOUND VOLUME"));
        Assert.Equal("6", config.StrParams("sound volume"));
        Assert.Equal("ru", config.StrParams(" lang "));
    }

    [Fact]
    public void CommentsBlankLinesAndLinesWithoutSeparatorAreSkipped()
    {
        TinyConfig config = Build("# sound volume = 9\n\n   \nno separator here\nmusic = original\n");

        Assert.False(config.Exists("sound volume"));
        Assert.False(config.Exists("no separator here"));
        Assert.Equal("original", config.StrParams("music"));
    }

    [Fact]
    public void TheFirstOfTwoDuplicateKeysWins()
    {
        TinyConfig config = Build("music = original\nmusic = expansion\n");

        Assert.Equal("original", config.StrParams("music"));
    }

    [Fact]
    public void OnlyTheSeparatorSplitsTheLine()
    {
        TinyConfig config = Build("lang = de=DE\n");

        Assert.Equal("de=DE", config.StrParams("lang"));
    }

    [Fact]
    public void AValueThatIsNotANumberIsZero()
    {
        TinyConfig config = Build("a = 12\nb = 12abc\nc = -7\nd =\n");

        Assert.Equal(12, config.IntParams("a"));
        Assert.Equal(0, config.IntParams("b"));
        Assert.Equal(-7, config.IntParams("c"));
        Assert.Equal(0, config.IntParams("d"));
        Assert.Equal(0, config.IntParams("missing"));
    }

    [Fact]
    public void APointNeedsBracketsAndTwoNumbers()
    {
        TinyConfig config = Build(
            "a = [ 5, 6 ]\nb = [-1,-2]\nc = 5, 6\nd = [ 5, 6, 7 ]\n");

        Assert.Equal(new Point(5, 6), config.PointParams("a", new Point(0, 0)));
        Assert.Equal(new Point(-1, -2), config.PointParams("b", new Point(0, 0)));
        Assert.Equal(new Point(0, 0), config.PointParams("c", new Point(0, 0)));
        Assert.Equal(new Point(0, 0), config.PointParams("d", new Point(0, 0)));
        Assert.Equal(new Point(3, 4), config.PointParams("missing", new Point(3, 4)));
    }

    [Fact]
    public void TheNewResolutionFormatCarriesBothSizes()
    {
        TinyConfig config = Build("videomode = 640x480 : 1280x960\n");

        Assert.Equal(new ResolutionInfo(640, 480, 1280, 960),
            config.ResolutionParams("videomode", new ResolutionInfo(1, 2, 3, 4)));
    }

    [Fact]
    public void TheLegacyResolutionFormatMultipliesTheGameSize()
    {
        TinyConfig config = Build("videomode = 640x480x2\n");

        Assert.Equal(new ResolutionInfo(640, 480, 1280, 960),
            config.ResolutionParams("videomode", new ResolutionInfo(1, 2, 3, 4)));
    }

    [Fact]
    public void ABrokenResolutionFallsBack()
    {
        ResolutionInfo fallback = new(1, 2, 3, 4);

        TinyConfig config = Build("a = 640x480x0\nb = 2147483647x480x2\nc = 640 x 480\nd = garbage\n");

        Assert.Equal(fallback, config.ResolutionParams("a", fallback));
        Assert.Equal(fallback, config.ResolutionParams("b", fallback));
        Assert.Equal(fallback, config.ResolutionParams("c", fallback));
        Assert.Equal(fallback, config.ResolutionParams("d", fallback));
        Assert.Equal(fallback, config.ResolutionParams("missing", fallback));
    }

    [Fact]
    public void AMissingFileReadsAsFalseAndKeepsTheParserEmpty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fh2-cfg-{Guid.NewGuid():N}.cfg");
        TinyConfig config = new();

        Assert.False(config.LoadFile(path));
        Assert.False(config.Exists("music"));
    }

    private static TinyConfig Build(string content)
    {
        TinyConfig config = new();

        config.Parse(content);

        return config;
    }
}
