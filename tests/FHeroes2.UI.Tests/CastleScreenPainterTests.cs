using FHeroes2.Assets;
using FHeroes2.Assets.Icn;
using FHeroes2.Assets.Maps;
using FHeroes2.Core.Geometry;
using FHeroes2.Core.Imaging;
using FHeroes2.Game.Castle;
using FHeroes2.UI.Dialogs;

using GameAssets = FHeroes2.Game.Assets;

namespace FHeroes2.UI.Tests;

/// <summary>
/// Tests of the town screen's background and building pass, <c>castle_building.cpp</c>.
/// </summary>
/// <remarks>
/// 需要 AGG 数据的几条在缺数据时直接返回；映射表与「没有档案时不动画面」的几条不依赖资产，任何环境都会跑。
/// </remarks>
[Collection(GlobalArchiveCollection.Name)]
public sealed class CastleScreenPainterTests
{
    [Theory]
    [InlineData(MapRace.Knight, IcnType.Townbkg0)]
    [InlineData(MapRace.Barbarian, IcnType.Townbkg1)]
    [InlineData(MapRace.Sorceress, IcnType.Townbkg2)]
    [InlineData(MapRace.Warlock, IcnType.Townbkg3)]
    [InlineData(MapRace.Wizard, IcnType.Townbkg4)]
    [InlineData(MapRace.Necromancer, IcnType.Townbkg5)]
    public void EveryRaceHasItsOwnBackground(MapRace race, IcnType expected)
    {
        Assert.Equal(expected, CastleScreenPainter.GetBackground(race));
    }

    [Fact]
    public void ARaceWithoutABackgroundIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CastleScreenPainter.GetBackground(MapRace.None));
    }

    [Theory]
    [InlineData(MapRace.Knight, IcnType.Twnkext0)]
    [InlineData(MapRace.Barbarian, IcnType.Twnbext0)]
    [InlineData(MapRace.Sorceress, IcnType.Twnsext0)]
    [InlineData(MapRace.Warlock, IcnType.Twnwext0)]
    [InlineData(MapRace.Wizard, IcnType.WizardCastleBay)]
    [InlineData(MapRace.Necromancer, IcnType.Twnnext0)]
    public void EverySeafaringRaceHasItsOwnBay(MapRace race, IcnType expected)
    {
        Assert.Equal(expected, CastleScreenPainter.GetBay(race));
    }

    [Fact]
    public void ARaceWithoutAShoreHasNoBay()
    {
        Assert.Null(CastleScreenPainter.GetBay(MapRace.None));
    }

    [Fact]
    public void DrawWithoutArchivesLeavesTheTargetAlone()
    {
        GameAssets.Current = null;

        PixelBuffer target = new(640, 480);
        target.Fill(9);

        Castle castle = new(0, MapRace.Knight);

        CastleScreenPainter.Draw(target, castle, new Point(0, 0), 0, new FadeBuilding());

        Assert.Equal(9, target[0, 0]);
        Assert.Equal(9, target[639, 479]);
    }

    [Fact]
    public void DrawPaintsTheBackgroundOfTheRace()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            Sprite? background = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Townbkg0), 0);
            Assert.NotNull(background);

            PixelBuffer target = new(640, 480);
            Castle castle = new(0, MapRace.Knight);

            CastleScreenPainter.Draw(target, castle, new Point(0, 0), 0, new FadeBuilding());

            // 背景帧的 (12, 12) 不可能是透明色，否则整片天空都是洞。
            Assert.NotEqual(0, background.Image[12, 12]);
            Assert.Equal(background.Image[12, 12], target[12, 12]);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void DrawFollowsTheOrigin()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            Sprite? background = GameAssets.GetImage(IcnCatalog.GetFileName(IcnType.Townbkg0), 0);
            Assert.NotNull(background);

            PixelBuffer target = new(640, 480);
            Castle castle = new(0, MapRace.Knight);

            CastleScreenPainter.Draw(target, castle, new Point(0, 20), 0, new FadeBuilding());

            // 背景整体下移 20 像素后，源图 (12, 12) 落到屏幕 (12, 32)。
            Assert.Equal(background.Image[12, 12], target[12, 32]);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void DrawBuildingClipsToTheRenderArea()
    {
        using AssetBundle? assets = OpenArchives();
        if (assets is null)
        {
            return;
        }

        GameAssets.Current = assets;

        try
        {
            IcnType icn = Castle.GetICNBuilding(BuildingType.Well, MapRace.Knight);
            Sprite? frame = GameAssets.GetImage(IcnCatalog.GetFileName(icn), 0);
            Assert.NotNull(frame);

            if (!TryFindOpaque(frame, out int sx, out int sy))
            {
                return;
            }

            Point at = new(frame.OffsetX, frame.OffsetY);
            int screenY = at.Y + sy;

            // 只画到 200 像素高的带子里，井的非透明像素应当被整块切掉。
            if (screenY < 200)
            {
                return;
            }

            PixelBuffer clipped = new(640, 480);
            CastleScreenPainter.DrawBuilding(clipped, icn, 0, at, new Rect(0, 0, 640, 200));

            Assert.Equal(0, clipped[at.X + sx, at.Y + sy]);
        }
        finally
        {
            GameAssets.Current = null;
        }
    }

    [Fact]
    public void DrawBuildingRejectsNegativeFrames()
    {
        PixelBuffer target = new(64, 48);

        CastleScreenPainter.DrawBuilding(target, IcnType.Townbkg0, -1, new Point(0, 0), new Rect(0, 0, 64, 48));

        Assert.Equal(0, target[0, 0]);
    }

    [Fact]
    public void DrawBuildingSkipsAFullyTransparentAlpha()
    {
        PixelBuffer target = new(64, 48);

        CastleScreenPainter.DrawBuilding(target, IcnType.Townbkg0, 0, new Point(0, 0), new Rect(0, 0, 64, 48), alpha: 0);

        Assert.Equal(0, target[0, 0]);
    }

    private static AssetBundle? OpenArchives() =>
        AssetLocator.Resolve() is { } directory ? AssetBundle.Open(directory) : null;

    private static bool TryFindOpaque(Sprite sprite, out int x, out int y)
    {
        x = 0;
        y = 0;

        for (y = 0; y < sprite.Image.Height; ++y)
        {
            for (x = 0; x < sprite.Image.Width; ++x)
            {
                if (sprite.Image[x, y] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>Serializes the tests that swap <see cref="GameAssets.Current"/>.</summary>
/// <remarks>
/// <para>
/// <c>Assets.Current</c> 是进程级全局，字体的字形表还以它为键缓存（<c>FontGlyphTable</c>），所以一个测试把它切到
/// <c>null</c> 时，并行运行的任何绘制类测试拿到的字体都会变，连「同一个对话框画两次像素相同」这种断言都会偶发失败
/// （<c>FHeroes2Sharp-d8y</c>）。
/// </para>
/// <para>
/// 规则：**只要测试会写 <c>Assets.Current</c>，或者会绘制文字/图标因而读到它，就必须挂
/// <c>[Collection(GlobalArchiveCollection.Name)]</c>**。纯逻辑测试不必挂。
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalArchiveCollection
{
    /// <summary>Name of the collection.</summary>
    public const string Name = "FHeroes2.UI global archive";
}
