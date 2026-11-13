using FHeroes2.Assets.Icn;

namespace FHeroes2.Assets.Tests;

/// <summary>
/// Covers <see cref="AssetBundle.TryReadIcn"/> against the real archives. The tests return early
/// when the game data is unavailable (see docs/MISC.md).
/// </summary>
public sealed class AssetBundleTests
{
    [Fact]
    public void TryReadIcn_ReturnsNullForAMissingEntry()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        using AssetBundle bundle = AssetBundle.Open(dataDirectory);

        Assert.Null(bundle.TryReadIcn("THERE.IS.NO.SUCH.ICN"));
    }

    [Fact]
    public void TryReadIcn_DecodesTheMainMenuArtwork()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        using AssetBundle bundle = AssetBundle.Open(dataDirectory);

        // HEROES.ICN is the 640x480 main menu background: one screen sized sprite. The Price of
        // Loyalty archive ships its own copy, and the bundle prefers it, exactly like AGG::GetICN.
        IcnSpriteFile? background = bundle.TryReadIcn("HEROES.ICN");

        Assert.NotNull(background);
        Assert.Equal(1, background.Count);
        Assert.Equal(new Core.Geometry.Size(640, 480), background.Sprites[0].Image.Size);
        Assert.Equal(0, background.Sprites[0].OffsetX);
        Assert.Equal(0, background.Sprites[0].OffsetY);

        // BTNSHNGL.ICN holds four frames (default, highlighted, pressed, unused) per sign, and the
        // five main menu buttons start at 1, 5, 9, 13 and 17.
        IcnSpriteFile? signs = bundle.TryReadIcn("BTNSHNGL.ICN");

        Assert.NotNull(signs);
        Assert.Equal(20, signs.Count);
        Assert.All(signs.Sprites, sprite => Assert.True(sprite.HasTransformLayer));

        // Every sign is drawn where the archive says it is; the offsets are the menu layout.
        Assert.Equal(new Core.Geometry.Rect(482, 176, 81, 113), AreaOf(signs, 1));
        Assert.Equal(new Core.Geometry.Rect(195, 180, 80, 77), AreaOf(signs, 5));
        Assert.Equal(new Core.Geometry.Rect(408, 107, 80, 76), AreaOf(signs, 9));
        Assert.Equal(new Core.Geometry.Rect(303, 137, 76, 43), AreaOf(signs, 13));
        Assert.Equal(new Core.Geometry.Rect(0, 425, 86, 44), AreaOf(signs, 17));
    }

    [Fact]
    public void TryReadIcn_MatchesParseOfTheSameEntry()
    {
        string? dataDirectory = GameData.ResolveDataDirectory();
        if (dataDirectory is null)
        {
            return;
        }

        using AssetBundle bundle = AssetBundle.Open(dataDirectory);

        byte[]? raw = bundle.TryRead("SMALFONT.ICN");
        if (raw is null)
        {
            return;
        }

        IcnSpriteFile fromHelper = bundle.TryReadIcn("SMALFONT.ICN")!;
        IcnSpriteFile fromParser = IcnSpriteFile.Parse(raw);

        Assert.Equal(fromParser.Count, fromHelper.Count);
        Assert.Equal(fromParser.DataBlockSize, fromHelper.DataBlockSize);

        for (int index = 0; index < fromParser.Count; ++index)
        {
            Assert.Equal(fromParser.Sprites[index].Image.AsSpan(), fromHelper.Sprites[index].Image.AsSpan());
            Assert.Equal(fromParser.Sprites[index].OffsetX, fromHelper.Sprites[index].OffsetX);
            Assert.Equal(fromParser.Sprites[index].OffsetY, fromHelper.Sprites[index].OffsetY);
        }
    }

    private static Core.Geometry.Rect AreaOf(IcnSpriteFile file, int index)
    {
        Core.Imaging.Sprite sprite = file.Sprites[index];
        return new Core.Geometry.Rect(sprite.OffsetX, sprite.OffsetY, sprite.Width, sprite.Height);
    }
}
